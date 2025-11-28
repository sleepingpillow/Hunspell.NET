// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

using System.Text;

namespace Hunspell;

/// <summary>
/// Manages the hash table for dictionary words.
/// Provides efficient word lookup and storage.
/// </summary>
internal sealed class HashManager : IDisposable
{
    // Support multiple dictionary entries for the same surface word (homonyms)
    // by storing a list of WordEntry per surface form. This allows us to
    // determine whether a word is "forbidden" only when all homonym entries
    // carry the FORBIDDENWORD flag (matching upstream Hunspell behavior).
    private readonly Dictionary<string, List<WordEntry>> _words = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _runtimeWords = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public HashManager(string dictionaryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionaryPath);
        LoadDictionary(dictionaryPath);
    }

    private void LoadDictionary(string dictionaryPath)
    {
        if (!File.Exists(dictionaryPath))
        {
            throw new FileNotFoundException($"Dictionary file not found: {dictionaryPath}");
        }

        using var stream = File.OpenRead(dictionaryPath);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        // First line typically contains the word count
        var firstLine = reader.ReadLine();
        if (firstLine is null)
        {
            return;
        }

        // If first line is a number, it's the word count (optional)
        if (!int.TryParse(firstLine.Trim(), out _))
        {
            // Not a count, treat as a word
            ProcessDictionaryLine(firstLine);
        }

        // Read remaining lines
        while (reader.ReadLine() is { } line)
        {
            ProcessDictionaryLine(line);
        }
    }

    private void ProcessDictionaryLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            return;
        }

        var parts = line.Split('/', 2, StringSplitOptions.TrimEntries);
        var word = parts[0];
        var flags = parts.Length > 1 ? parts[1] : string.Empty;

        if (!string.IsNullOrEmpty(word))
        {
            if (!_words.TryGetValue(word, out var list))
            {
                list = new List<WordEntry>();
                _words[word] = list;
            }
            list.Add(new WordEntry(word, flags));
        }
    }

    public bool Lookup(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Runtime words are stored case-insensitively and should match directly
        if (_runtimeWords.Contains(word)) return true;

        // _words uses a case-insensitive key comparison. Use TryGetValue to
        // retrieve all variants for the surface form and then decide whether
        // a match should be allowed based on exact-case or permissive rules.
        if (!_words.TryGetValue(word, out var entries)) return false;

        // If an exact-case variant exists, accept it immediately
        if (entries.Any(e => string.Equals(e.Word, word, StringComparison.Ordinal)))
        {
            return true;
        }

        // Otherwise, apply a refined case-handling rule set:
        // - If the dictionary has a lower-case variant, allow case-insensitive matches.
        // - If the dictionary has an all-upper variant (acronym), accept only ALL-UPPER words.
        // - If the dictionary has mixed-case variants (e.g., OpenOffice.org), accept
        //   only exact-case or the ALL-UPPER variant (OpenOffice.org -> OPENOFFICE.ORG).
        bool hasAllLower = entries.Any(e => e.Word == e.Word.ToLowerInvariant());
        if (hasAllLower) return true;

        bool hasAllUpper = entries.Any(e => e.Word == e.Word.ToUpperInvariant());
        bool hasMixed = entries.Any(e => !(e.Word == e.Word.ToUpperInvariant() || e.Word == e.Word.ToLowerInvariant()));

        // If dictionary contains an all-upper entry, accept only all-upper input
        if (hasAllUpper && string.Equals(word, word.ToUpperInvariant(), StringComparison.Ordinal))
        {
            return true;
        }

        // If dictionary contains a mixed-case entry, accept its all-upper variant
        // (e.g., OPENOFFICE.ORG for OpenOffice.org) but not arbitrary case changes.
        if (hasMixed && entries.Any(e => string.Equals(e.Word.ToUpperInvariant(), word, StringComparison.Ordinal)))
        {
            return true;
        }

        return false;
    }

    public bool Add(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _runtimeWords.Add(word);
    }

    public bool Remove(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _runtimeWords.Remove(word);
    }

    public IEnumerable<string> GetAllWords()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _words.Keys.Concat(_runtimeWords);
    }

    /// <summary>
    /// Get the flags associated with a word.
    /// </summary>
    /// <param name="word">The word to look up</param>
    /// <returns>The flags string if found, null otherwise</returns>
    public string? GetWordFlags(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_words.TryGetValue(word, out var entries))
        {
            // Return the union of all flags across homonym entries. This keeps
            // the existing callers working (they typically call .Contains on
            // the returned flags string), while allowing higher-level code to
            // inspect individual variants when necessary.
            var set = new HashSet<char>();
            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.Flags)) continue;
                foreach (var ch in e.Flags)
                {
                    set.Add(ch);
                }
            }
            return string.Concat(set);
        }

        // Runtime words have no flags
        return _runtimeWords.Contains(word) ? string.Empty : null;
    }

    /// <summary>
    /// Return all flag-variants for a given surface word as they were read from
    /// the dictionary file. If the word is not present, an empty sequence is
    /// returned.
    /// </summary>
    public IEnumerable<string> GetWordFlagVariants(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_words.TryGetValue(word, out var entries))
        {
            // If an exact-case variant exists (e.g. 'Kg') prefer those entries
            // so per-case flags (like 'Kg/X') are respected. Otherwise fall back
            // to all variants for the case-insensitive match.
            var exact = entries.Where(e => string.Equals(e.Word, word, StringComparison.Ordinal)).ToList();
            var source = exact.Count > 0 ? exact : entries;
            foreach (var e in source)
            {
                yield return e.Flags ?? string.Empty;
            }
            yield break;
        }

        // runtime words have zero flags
        if (_runtimeWords.Contains(word)) yield return string.Empty;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _words.Clear();
            _runtimeWords.Clear();
            _disposed = true;
        }
    }

    private record WordEntry(string Word, string Flags);
}
