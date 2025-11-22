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
    private readonly Dictionary<string, WordEntry> _words = new(StringComparer.OrdinalIgnoreCase);
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
            _words[word] = new WordEntry(word, flags);
        }
    }

    public bool Lookup(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _words.ContainsKey(word) || _runtimeWords.Contains(word);
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
