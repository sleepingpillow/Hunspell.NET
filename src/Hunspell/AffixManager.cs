// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

using System.Text;

namespace Hunspell;

/// <summary>
/// Manages affix rules and generates word forms.
/// </summary>
internal sealed class AffixManager : IDisposable
{
    private readonly HashManager _hashManager;
    private readonly Dictionary<string, string> _options = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AffixRule> _prefixes = new();
    private readonly List<AffixRule> _suffixes = new();
    private bool _disposed;

    public string Encoding => _options.TryGetValue("SET", out var encoding) ? encoding : "UTF-8";

    public AffixManager(string affixPath, HashManager hashManager)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(affixPath);
        _hashManager = hashManager ?? throw new ArgumentNullException(nameof(hashManager));
        
        LoadAffix(affixPath);
    }

    private void LoadAffix(string affixPath)
    {
        if (!File.Exists(affixPath))
        {
            throw new FileNotFoundException($"Affix file not found: {affixPath}");
        }

        using var stream = File.OpenRead(affixPath);
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        
        while (reader.ReadLine() is { } line)
        {
            ProcessAffixLine(line);
        }
    }

    private void ProcessAffixLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
        {
            return;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return;
        }

        var command = parts[0].ToUpperInvariant();

        switch (command)
        {
            case "SET":
                if (parts.Length > 1)
                {
                    _options["SET"] = parts[1];
                }
                break;

            case "TRY":
                if (parts.Length > 1)
                {
                    _options["TRY"] = parts[1];
                }
                break;

            case "PFX":
                ParseAffixRule(parts, isPrefix: true);
                break;

            case "SFX":
                ParseAffixRule(parts, isPrefix: false);
                break;

            case "KEY":
            case "REP":
            case "MAP":
            case "WORDCHARS":
                // Store for potential future use
                if (parts.Length > 1)
                {
                    _options[command] = string.Join(" ", parts[1..]);
                }
                break;
        }
    }

    private void ParseAffixRule(string[] parts, bool isPrefix)
    {
        // PFX/SFX flag cross_product count
        // PFX/SFX flag stripping prefix [condition [morphological_fields...]]
        
        if (parts.Length < 4)
        {
            return;
        }

        var flag = parts[1];
        var stripping = parts[2] == "0" ? string.Empty : parts[2];
        var affix = parts[3];
        var condition = parts.Length > 4 ? parts[4] : ".";

        var rule = new AffixRule(flag, stripping, affix, condition, isPrefix);
        
        if (isPrefix)
        {
            _prefixes.Add(rule);
        }
        else
        {
            _suffixes.Add(rule);
        }
    }

    public void GenerateSuggestions(string word, List<string> suggestions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Try simple character substitutions
        GenerateSubstitutionSuggestions(word, suggestions);

        // Try character insertions
        GenerateInsertionSuggestions(word, suggestions);

        // Try character deletions
        GenerateDeletionSuggestions(word, suggestions);

        // Try character swaps
        GenerateSwapSuggestions(word, suggestions);

        // Limit suggestions
        if (suggestions.Count > 10)
        {
            suggestions.RemoveRange(10, suggestions.Count - 10);
        }
    }

    private void GenerateSubstitutionSuggestions(string word, List<string> suggestions)
    {
        var tryChars = _options.TryGetValue("TRY", out var chars) ? chars : "abcdefghijklmnopqrstuvwxyz";

        for (int i = 0; i < word.Length; i++)
        {
            foreach (var c in tryChars)
            {
                if (c == word[i])
                {
                    continue;
                }

                var suggestion = string.Create(word.Length, (word, i, c), static (span, state) =>
                {
                    state.word.AsSpan().CopyTo(span);
                    span[state.i] = state.c;
                });

                if (_hashManager.Lookup(suggestion) && !suggestions.Contains(suggestion))
                {
                    suggestions.Add(suggestion);
                }
            }
        }
    }

    private void GenerateInsertionSuggestions(string word, List<string> suggestions)
    {
        var tryChars = _options.TryGetValue("TRY", out var chars) ? chars : "abcdefghijklmnopqrstuvwxyz";

        for (int i = 0; i <= word.Length; i++)
        {
            foreach (var c in tryChars)
            {
                var suggestion = word.Insert(i, c.ToString());

                if (_hashManager.Lookup(suggestion) && !suggestions.Contains(suggestion))
                {
                    suggestions.Add(suggestion);
                }
            }
        }
    }

    private void GenerateDeletionSuggestions(string word, List<string> suggestions)
    {
        for (int i = 0; i < word.Length; i++)
        {
            var suggestion = word.Remove(i, 1);

            if (_hashManager.Lookup(suggestion) && !suggestions.Contains(suggestion))
            {
                suggestions.Add(suggestion);
            }
        }
    }

    private void GenerateSwapSuggestions(string word, List<string> suggestions)
    {
        for (int i = 0; i < word.Length - 1; i++)
        {
            var suggestion = string.Create(word.Length, (word, i), static (span, state) =>
            {
                state.word.AsSpan().CopyTo(span);
                (span[state.i], span[state.i + 1]) = (span[state.i + 1], span[state.i]);
            });

            if (_hashManager.Lookup(suggestion) && !suggestions.Contains(suggestion))
            {
                suggestions.Add(suggestion);
            }
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _options.Clear();
            _prefixes.Clear();
            _suffixes.Clear();
            _disposed = true;
        }
    }

    private record AffixRule(string Flag, string Stripping, string Affix, string Condition, bool IsPrefix);
}
