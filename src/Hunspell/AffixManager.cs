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

    // Compound word flags
    private string? _compoundFlag;
    private string? _compoundBegin;
    private string? _compoundMiddle;
    private string? _compoundEnd;
    private string? _compoundRoot;
    private string? _compoundPermitFlag;
    private string? _compoundForbidFlag;
    private string? _onlyInCompound;

    // Compound word options
    private int _compoundMin = 3;
    private int _compoundWordMax = 0; // 0 means unlimited
    private bool _compoundMoreSuffixes = false;

    // Compound checking options
    private bool _checkCompoundDup = false;
    private bool _checkCompoundCase = false;
    private bool _checkCompoundTriple = false;
    private bool _simplifiedTriple = false;
    private bool _checkCompoundRep = false;

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

            // Compound word flags
            case "COMPOUNDFLAG":
                if (parts.Length > 1)
                {
                    _compoundFlag = parts[1];
                }
                break;

            case "COMPOUNDBEGIN":
                if (parts.Length > 1)
                {
                    _compoundBegin = parts[1];
                }
                break;

            case "COMPOUNDMIDDLE":
                if (parts.Length > 1)
                {
                    _compoundMiddle = parts[1];
                }
                break;

            case "COMPOUNDLAST":
            case "COMPOUNDEND":
                if (parts.Length > 1)
                {
                    _compoundEnd = parts[1];
                }
                break;

            case "COMPOUNDROOT":
                if (parts.Length > 1)
                {
                    _compoundRoot = parts[1];
                }
                break;

            case "COMPOUNDPERMITFLAG":
                if (parts.Length > 1)
                {
                    _compoundPermitFlag = parts[1];
                }
                break;

            case "COMPOUNDFORBIDFLAG":
                if (parts.Length > 1)
                {
                    _compoundForbidFlag = parts[1];
                }
                break;

            case "ONLYINCOMPOUND":
                if (parts.Length > 1)
                {
                    _onlyInCompound = parts[1];
                }
                break;

            // Compound word options
            case "COMPOUNDMIN":
                if (parts.Length > 1 && int.TryParse(parts[1], out var minValue))
                {
                    _compoundMin = minValue;
                }
                break;

            case "COMPOUNDWORDMAX":
                if (parts.Length > 1 && int.TryParse(parts[1], out var maxValue))
                {
                    _compoundWordMax = maxValue;
                }
                break;

            case "COMPOUNDMORESUFFIXES":
                _compoundMoreSuffixes = true;
                break;

            // Compound checking options
            case "CHECKCOMPOUNDDUP":
                _checkCompoundDup = true;
                break;

            case "CHECKCOMPOUNDCASE":
                _checkCompoundCase = true;
                break;

            case "CHECKCOMPOUNDTRIPLE":
                _checkCompoundTriple = true;
                break;

            case "SIMPLIFIEDTRIPLE":
                _simplifiedTriple = true;
                break;

            case "CHECKCOMPOUNDREP":
                _checkCompoundRep = true;
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

    /// <summary>
    /// Check if a word is a valid compound word.
    /// </summary>
    public bool CheckCompound(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // If no compound flags are defined, no compounds are allowed
        if (_compoundFlag is null && _compoundBegin is null)
        {
            return false;
        }

        // Try to split the word into valid compound parts
        return CheckCompoundRecursive(word, 0, 0, null);
    }

    /// <summary>
    /// Recursively check if a word can be split into valid compound parts.
    /// </summary>
    private bool CheckCompoundRecursive(string word, int wordCount, int position, string? previousPart)
    {
        // If we've consumed the entire word, we have a valid compound
        if (position >= word.Length)
        {
            // Check if we're within the maximum word count limit
            if (_compoundWordMax > 0 && wordCount > _compoundWordMax)
            {
                return false;
            }
            return wordCount >= 2; // Must have at least 2 parts to be a compound
        }

        // Check if adding another word would exceed the maximum
        if (_compoundWordMax > 0 && wordCount + 1 > _compoundWordMax)
        {
            return false;
        }

        // Try different split positions
        for (int i = position + _compoundMin; i <= word.Length; i++)
        {
            var part = word.Substring(position, i - position);

            // Check if this part is valid for its position in the compound
            if (!IsValidCompoundPart(part, wordCount, position, i, word))
            {
                continue;
            }

            // Check compound-specific rules
            if (!CheckCompoundRules(word, position, i, previousPart, part))
            {
                continue;
            }

            // Try to continue building the compound
            if (CheckCompoundRecursive(word, wordCount + 1, i, part))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a word part is valid for its position in the compound.
    /// </summary>
    private bool IsValidCompoundPart(string part, int wordCount, int startPos, int endPos, string fullWord)
    {
        // Must meet minimum length
        if (part.Length < _compoundMin)
        {
            return false;
        }

        // Get the word's flags from the dictionary
        var flags = _hashManager.GetWordFlags(part);
        if (flags is null)
        {
            return false; // Word not in dictionary
        }

        // Check position-specific flags
        if (wordCount == 0)
        {
            // First word in compound
            if (_compoundBegin is not null && !flags.Contains(_compoundBegin) &&
                _compoundFlag is not null && !flags.Contains(_compoundFlag))
            {
                return false;
            }
        }
        else if (endPos < fullWord.Length)
        {
            // Middle word in compound
            if (_compoundMiddle is not null && !flags.Contains(_compoundMiddle) &&
                _compoundFlag is not null && !flags.Contains(_compoundFlag))
            {
                return false;
            }
        }
        else
        {
            // Last word in compound
            if (_compoundEnd is not null && !flags.Contains(_compoundEnd) &&
                _compoundFlag is not null && !flags.Contains(_compoundFlag))
            {
                return false;
            }
        }

        // Check COMPOUNDFORBIDFLAG - forbid this word in compounds
        if (_compoundForbidFlag is not null && flags.Contains(_compoundForbidFlag))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Check compound-specific rules (dup, case, triple, etc.)
    /// </summary>
    private bool CheckCompoundRules(string word, int prevEnd, int currentEnd, string? previousPart, string currentPart)
    {
        if (prevEnd == 0)
        {
            return true; // No previous part to check against
        }

        // Check CHECKCOMPOUNDDUP - forbid duplicated words
        if (_checkCompoundDup && previousPart is not null && 
            previousPart.Equals(currentPart, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check CHECKCOMPOUNDCASE - forbid uppercase letters at boundaries
        if (_checkCompoundCase)
        {
            if (prevEnd > 0 && prevEnd < word.Length)
            {
                var lastChar = word[prevEnd - 1];
                var firstChar = word[prevEnd];
                
                // Forbid lowercase followed by uppercase at boundary
                if (char.IsLower(lastChar) && char.IsUpper(firstChar))
                {
                    return false;
                }
            }
        }

        // Check CHECKCOMPOUNDTRIPLE - forbid triple repeating letters
        if (_checkCompoundTriple && prevEnd >= 1 && currentEnd > prevEnd + 1)
        {
            // Check if we have three consecutive identical letters at the boundary
            if (prevEnd >= 2)
            {
                var char1 = word[prevEnd - 2];
                var char2 = word[prevEnd - 1];
                var char3 = word[prevEnd];
                
                if (char1 == char2 && char2 == char3)
                {
                    // Check for simplified triple exception
                    if (!_simplifiedTriple)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a word has the ONLYINCOMPOUND flag.
    /// </summary>
    public bool IsOnlyInCompound(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_onlyInCompound is null)
        {
            return false;
        }

        var flags = _hashManager.GetWordFlags(word);
        return flags is not null && flags.Contains(_onlyInCompound);
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
