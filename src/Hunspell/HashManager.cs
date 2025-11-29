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
    // Index of dictionary 'ph:' morphological fields -> surfaces (e.g., "forbiddenroot" -> ["forbidden root"]) 
    private readonly Dictionary<string, List<string>> _phIndex = new(StringComparer.OrdinalIgnoreCase);
    // ph replacement rules (from -> to) produced from 'ph:' fields in dictionary
    // - plain ph:token -> mapping token -> surface
    // - ph:from->to -> mapping from -> to
    // - ph:pattern* -> mapping pattern_minus_lastchar -> surface_minus_lastchar
    private readonly List<(string from, string to)> _phReplacementRules = new();
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

            // Dictionary lines can contain additional morphological fields
            // separated by tabs or spaces (e.g. `foo ph:bar`) and surface forms
            // themselves may include spaces (multi-word entries). The canonical
            // format is: <surface>[/flags][\t<morph-fields>...].
            //
            // Robust parsing strategy:
            // - Split into tokens on whitespace.
            // - Treat all leading tokens up to the first token that contains ':'
            //   (a morphological key like "ph:") as part of the surface + flags
            //   token(s). This preserves multi-word surfaces that precede morph
            //   fields.
            // - Join those leading tokens back into a single string and then
            //   split it on '/' to extract surface and flag string (if any).

            var trimmed = line.Trim();
            // Split on whitespace to find morphological fields (tokens like ph:..)
            // Split on whitespace. Use the null-forgiving operator because passing
            // a literal null here is the canonical way to request splitting on
            // whitespace; avoid CS8600 by telling the compiler we know this is OK.
            var tokens = trimmed.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return;

            int morphIndex = Array.FindIndex(tokens, t => t.Contains(':'));
            string mainToken;
            if (morphIndex <= 0)
            {
                // No morph fields or the first token itself contains ':', keep whole line
                mainToken = tokens[0];
                // If multiple tokens and none contains ':', assume whole first token is the entry
                // (this covers the common case like "word/FLAGS")
            }
            else
            {
                // Join all tokens up to (but excluding) the first morphological token
                mainToken = string.Join(' ', tokens.Take(morphIndex));
            }

            var parts = mainToken.Split('/', 2, StringSplitOptions.TrimEntries);
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
            // If there are morphological tokens after the main token, extract
            // any ph: entries and index them so higher-level logic (e.g., the
            // affix/compound checker) can consult them.
            if (tokens.Length > 1 && Array.FindIndex(tokens, t => t.Contains(':')) is var idx && idx >= 0)
            {
                for (int j = idx; j < tokens.Length; j++)
                {
                    var tok = tokens[j];
                    if (tok.StartsWith("ph:", StringComparison.OrdinalIgnoreCase))
                    {
                        var key = tok.Substring(3);
                        if (string.IsNullOrEmpty(key)) continue;
                        if (!_phIndex.TryGetValue(key, out var surfList))
                        {
                            surfList = new List<string>();
                            _phIndex[key] = surfList;
                        }
                        surfList.Add(word);
                        // Parse 'ph:' token into replacement rules
                        try
                        {
                            // 1) Replacement operator 'from->to'
                            if (key.Contains("->"))
                            {
                                var partsRt = key.Split(new[] { "->" }, StringSplitOptions.None);
                                if (partsRt.Length == 2 && !string.IsNullOrEmpty(partsRt[0]) && !string.IsNullOrEmpty(partsRt[1]))
                                {
                                    _phReplacementRules.Add((partsRt[0], partsRt[1]));
                                }
                            }
                            // 2) Wildcard form 'pattern*' -> strip last char of pattern and surface
                            else if (key.EndsWith("*", StringComparison.Ordinal))
                            {
                                var p = key.TrimEnd('*');
                                if (p.Length >= 1 && word.Length >= 1)
                                {
                                    var from = p.Length > 1 ? p.Substring(0, p.Length - 1) : p;
                                    var to = word.Length > 1 ? word.Substring(0, word.Length - 1) : word;
                                    if (!string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to)) _phReplacementRules.Add((from, to));
                                }
                            }
                            // 3) Plain token: map token -> surface
                            else
                            {
                                _phReplacementRules.Add((key, word));
                            }
                        }
                        catch
                        {
                            // Be defensive for odd tokens; ignore malformed ph: tokens
                        }
                    }
                }
            }
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
    /// Check whether a string is present as a 'ph:' target in the dictionary.
    /// Returns true if any dictionary entry had a 'ph:VALUE' token where VALUE
    /// matches the given key (case-insensitive).
    /// </summary>
    public bool HasPhTarget(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _phIndex.ContainsKey(key);
    }

    /// <summary>
    /// Check whether any plain 'ph:' target (no '*' or '->' markers) appears
    /// as a contiguous substring of the supplied word. This is used by the
    /// compound checker to forbid compounds that contain ph-mapped collapsed
    /// forms anywhere within them (upstream behaviour exercised by tests).
    /// </summary>
    public bool HasPhTargetSubstring(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(word)) return false;
        foreach (var key in _phIndex.Keys)
        {
            if (string.IsNullOrEmpty(key)) continue;
            // ignore special pattern keys for this check
            if (key.Contains('*') || key.Contains("->")) continue;
            if (word.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    /// <summary>
    /// Apply all stored ph: replacement rules (single-occurrence) to the supplied
    /// misspelled word and yield candidate surface forms. This mirrors upstream
    /// behavior where ph: entries are used as phonetic-like substitutions to
    /// generate suggestions.
    /// </summary>
    public IEnumerable<string> GetPhReplacementCandidates(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(word)) yield break;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (from, to) in _phReplacementRules)
        {
            if (string.IsNullOrEmpty(from)) continue;
            int idx = 0;
            while ((idx = word.IndexOf(from, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var cand = word.Substring(0, idx) + to + word.Substring(idx + from.Length);
                if (seen.Add(cand)) yield return cand;
                idx++; // next possible occurrence
            }
        }
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
