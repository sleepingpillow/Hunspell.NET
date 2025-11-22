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
    
    // Compound syllable options (COMPOUNDSYLLABLE)
    private int _compoundSyllableMax = 0; // 0 means no syllable limit
    private string _compoundSyllableVowels = string.Empty;

    // Compound checking options
    private bool _checkCompoundDup = false;
    private bool _checkCompoundCase = false;
    private bool _checkCompoundTriple = false;
    private bool _simplifiedTriple = false;
    private bool _checkCompoundRep = false;

    // Compound rules (COMPOUNDRULE)
    private readonly List<string> _compoundRules = new();

    // Compound pattern checking (CHECKCOMPOUNDPATTERN)
    private readonly List<CompoundPattern> _compoundPatterns = new();

    // Break points (BREAK)
    private readonly List<string> _breakPoints = new();

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

            case "COMPOUNDSYLLABLE":
                if (parts.Length > 2)
                {
                    if (int.TryParse(parts[1], out var syllableMax))
                    {
                        _compoundSyllableMax = syllableMax;
                        _compoundSyllableVowels = parts[2];
                    }
                }
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

            case "COMPOUNDRULE":
                if (parts.Length > 1)
                {
                    // First COMPOUNDRULE line contains the count, subsequent lines contain patterns
                    if (int.TryParse(parts[1], out _))
                    {
                        // This is the count line, ignore it (we'll just collect patterns)
                    }
                    else
                    {
                        // This is a pattern line
                        _compoundRules.Add(parts[1]);
                    }
                }
                break;

            case "CHECKCOMPOUNDPATTERN":
                if (parts.Length > 1)
                {
                    // First line contains the count, subsequent lines contain patterns
                    if (int.TryParse(parts[1], out _))
                    {
                        // This is the count line, ignore it
                    }
                    else if (parts.Length >= 3)
                    {
                        // Parse pattern: endchars[/flag] beginchars[/flag] [replacement]
                        var (endChars, endFlag) = ParseFlaggedPart(parts[1]);
                        var (beginChars, beginFlag) = ParseFlaggedPart(parts[2]);
                        var replacement = parts.Length > 3 ? parts[3] : null;
                        
                        _compoundPatterns.Add(new CompoundPattern(endChars, endFlag, beginChars, beginFlag, replacement));
                    }
                }
                break;

            case "BREAK":
                if (parts.Length > 1)
                {
                    // First line contains the count, subsequent lines contain break strings
                    if (int.TryParse(parts[1], out _))
                    {
                        // This is the count line, ignore it
                    }
                    else
                    {
                        // This is a break string
                        _breakPoints.Add(parts[1]);
                    }
                }
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

    /// <summary>
    /// Parse a part that may contain a flag (e.g., "chars/flag" or just "chars").
    /// </summary>
    private (string chars, string? flag) ParseFlaggedPart(string part)
    {
        var slashIndex = part.IndexOf('/');
        if (slashIndex > 0)
        {
            return (part.Substring(0, slashIndex), part.Substring(slashIndex + 1));
        }
        return (part, null);
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

        // If COMPOUNDRULE is defined, use it for compound checking
        if (_compoundRules.Count > 0)
        {
            return CheckCompoundWithRules(word);
        }

        // Otherwise, use flag-based compound checking
        // If no compound flags are defined, no compounds are allowed
        if (_compoundFlag is null && _compoundBegin is null)
        {
            return false;
        }

        // Try to split the word into valid compound parts
        return CheckCompoundRecursive(word, 0, 0, null, 0);
    }

    /// <summary>
    /// Check if a word matches any COMPOUNDRULE pattern.
    /// </summary>
    private bool CheckCompoundWithRules(string word)
    {
        // Try each compound rule
        foreach (var rule in _compoundRules)
        {
            if (MatchesCompoundRule(word, rule, 0, 0, new List<string>()))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Recursively match a word against a COMPOUNDRULE pattern.
    /// </summary>
    /// <param name="word">The word to check</param>
    /// <param name="pattern">The COMPOUNDRULE pattern</param>
    /// <param name="wordPos">Current position in the word</param>
    /// <param name="patternPos">Current position in the pattern</param>
    /// <param name="matchedParts">List of matched word parts (for debugging)</param>
    private bool MatchesCompoundRule(string word, string pattern, int wordPos, int patternPos, List<string> matchedParts)
    {
        // If we've consumed the entire word and pattern, we have a match
        if (wordPos >= word.Length && patternPos >= pattern.Length)
        {
            return matchedParts.Count >= 2; // Must have at least 2 parts
        }

        // If we've consumed the word but not the pattern, check if remaining pattern is optional
        if (wordPos >= word.Length)
        {
            return IsPatternOptional(pattern, patternPos);
        }

        // If we've consumed the pattern but not the word, no match
        if (patternPos >= pattern.Length)
        {
            return false;
        }

        // Parse the current pattern element
        var (flag, quantifier, nextPatternPos) = ParsePatternElement(pattern, patternPos);

        // Handle quantifiers
        if (quantifier == '*')
        {
            // Zero or more: try matching zero times first, then one or more
            if (MatchesCompoundRule(word, pattern, wordPos, nextPatternPos, new List<string>(matchedParts)))
            {
                return true;
            }
            // Try matching one or more times
            return TryMatchFlagMultipleTimes(word, pattern, wordPos, patternPos, nextPatternPos, flag, matchedParts, allowZero: false);
        }
        else if (quantifier == '?')
        {
            // Zero or one: try matching zero times first, then one time
            if (MatchesCompoundRule(word, pattern, wordPos, nextPatternPos, new List<string>(matchedParts)))
            {
                return true;
            }
            // Try matching once
            return TryMatchFlagOnce(word, pattern, wordPos, nextPatternPos, flag, matchedParts);
        }
        else
        {
            // Exactly once
            return TryMatchFlagOnce(word, pattern, wordPos, nextPatternPos, flag, matchedParts);
        }
    }

    /// <summary>
    /// Try to match a flag one time at the current word position.
    /// </summary>
    private bool TryMatchFlagOnce(string word, string pattern, int wordPos, int nextPatternPos, string flag, List<string> matchedParts)
    {
        // Try different word lengths starting from COMPOUNDMIN
        for (int len = _compoundMin; len <= word.Length - wordPos; len++)
        {
            var part = word.Substring(wordPos, len);
            var flags = _hashManager.GetWordFlags(part);

            if (flags is not null && flags.Contains(flag))
            {
                var newMatchedParts = new List<string>(matchedParts) { part };
                if (MatchesCompoundRule(word, pattern, wordPos + len, nextPatternPos, newMatchedParts))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Try to match a flag multiple times (for * quantifier).
    /// </summary>
    private bool TryMatchFlagMultipleTimes(string word, string pattern, int wordPos, int patternPos, int nextPatternPos, string flag, List<string> matchedParts, bool allowZero)
    {
        // Try matching one time and continue with the same pattern element
        for (int len = _compoundMin; len <= word.Length - wordPos; len++)
        {
            var part = word.Substring(wordPos, len);
            var flags = _hashManager.GetWordFlags(part);

            if (flags is not null && flags.Contains(flag))
            {
                var newMatchedParts = new List<string>(matchedParts) { part };
                // Continue trying to match more of the same flag (stay at patternPos)
                if (MatchesCompoundRule(word, pattern, wordPos + len, patternPos, newMatchedParts))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Parse a pattern element (flag with optional quantifier).
    /// </summary>
    /// <returns>Tuple of (flag, quantifier, nextPosition)</returns>
    private (string flag, char quantifier, int nextPos) ParsePatternElement(string pattern, int pos)
    {
        if (pos >= pattern.Length)
        {
            return (string.Empty, '\0', pos);
        }

        // Check for parentheses (group)
        if (pattern[pos] == '(')
        {
            int endParen = pattern.IndexOf(')', pos);
            if (endParen > pos)
            {
                var group = pattern.Substring(pos + 1, endParen - pos - 1);
                int nextPos = endParen + 1;
                char quantifier = '\0';
                
                if (nextPos < pattern.Length && (pattern[nextPos] == '*' || pattern[nextPos] == '?'))
                {
                    quantifier = pattern[nextPos];
                    nextPos++;
                }
                
                return (group, quantifier, nextPos);
            }
        }

        // Single flag character
        string flag = pattern[pos].ToString();
        int next = pos + 1;
        char quant = '\0';

        if (next < pattern.Length && (pattern[next] == '*' || pattern[next] == '?'))
        {
            quant = pattern[next];
            next++;
        }

        return (flag, quant, next);
    }

    /// <summary>
    /// Check if the remaining pattern is all optional (all have * or ? quantifiers).
    /// </summary>
    private bool IsPatternOptional(string pattern, int pos)
    {
        while (pos < pattern.Length)
        {
            var (_, quantifier, nextPos) = ParsePatternElement(pattern, pos);
            if (quantifier != '*' && quantifier != '?')
            {
                return false;
            }
            pos = nextPos;
        }
        return true;
    }

    /// <summary>
    /// Recursively check if a word can be split into valid compound parts.
    /// </summary>
    private bool CheckCompoundRecursive(string word, int wordCount, int position, string? previousPart, int syllableCount)
    {
        // If we've consumed the entire word, we have a valid compound
        if (position >= word.Length)
        {
            // Check if we're within the maximum word count limit
            // Exception: if syllable limit is set and we're within it, allow more words
            if (_compoundWordMax > 0 && wordCount > _compoundWordMax)
            {
                // Check if syllable exception applies
                if (_compoundSyllableMax > 0 && syllableCount <= _compoundSyllableMax)
                {
                    // Syllable limit allows this compound despite word count
                }
                else
                {
                    return false;
                }
            }
            return wordCount >= 2; // Must have at least 2 parts to be a compound
        }

        // Check if adding another word would exceed the maximum
        if (_compoundWordMax > 0 && wordCount + 1 > _compoundWordMax)
        {
            // Check if syllable exception could still apply
            if (_compoundSyllableMax == 0)
            {
                return false; // No syllable exception available
            }
            // Continue - might still be valid if syllable count is low enough
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

            // Count syllables in this part
            int partSyllables = CountSyllables(part);

            // Try to continue building the compound
            if (CheckCompoundRecursive(word, wordCount + 1, i, part, syllableCount + partSyllables))
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

        // Check CHECKCOMPOUNDPATTERN - forbid specific patterns at boundaries
        if (_compoundPatterns.Count > 0 && previousPart is not null)
        {
            foreach (var pattern in _compoundPatterns)
            {
                if (CheckCompoundPatternMatch(previousPart, currentPart, pattern))
                {
                    return false; // Pattern matched, forbid this compound
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a compound boundary matches a forbidden pattern.
    /// </summary>
    private bool CheckCompoundPatternMatch(string prevPart, string currentPart, CompoundPattern pattern)
    {
        // Check if the previous part ends with the pattern's end chars
        if (!prevPart.EndsWith(pattern.EndChars, StringComparison.Ordinal))
        {
            return false;
        }

        // Check if the current part begins with the pattern's begin chars
        if (!currentPart.StartsWith(pattern.BeginChars, StringComparison.Ordinal))
        {
            return false;
        }

        // If flags are specified, check them
        if (pattern.EndFlag is not null)
        {
            var prevFlags = _hashManager.GetWordFlags(prevPart);
            if (prevFlags is null || !prevFlags.Contains(pattern.EndFlag))
            {
                return false;
            }
        }

        if (pattern.BeginFlag is not null)
        {
            var currentFlags = _hashManager.GetWordFlags(currentPart);
            if (currentFlags is null || !currentFlags.Contains(pattern.BeginFlag))
            {
                return false;
            }
        }

        // Pattern matched - this boundary is forbidden
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

    /// <summary>
    /// Count syllables in a word based on vowel characters.
    /// </summary>
    private int CountSyllables(string word)
    {
        if (string.IsNullOrEmpty(_compoundSyllableVowels))
        {
            return 0;
        }

        int count = 0;
        foreach (char c in word)
        {
            if (_compoundSyllableVowels.Contains(c))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Check if a word can be broken at break points with all parts being valid.
    /// </summary>
    public bool CheckBreak(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // If no break points defined, no break checking
        if (_breakPoints.Count == 0)
        {
            return false;
        }

        // Try to break the word at each break point
        return CheckBreakRecursive(word);
    }

    /// <summary>
    /// Recursively check if a word can be broken into valid parts.
    /// </summary>
    private bool CheckBreakRecursive(string word)
    {
        // Empty word is not valid
        if (string.IsNullOrEmpty(word))
        {
            return false;
        }

        // Check if the whole word is valid
        if (_hashManager.Lookup(word) && !IsOnlyInCompound(word))
        {
            return true;
        }

        // Try breaking at each break point
        foreach (var breakPoint in _breakPoints)
        {
            int index = word.IndexOf(breakPoint);
            if (index > 0 && index < word.Length - breakPoint.Length)
            {
                // Break the word into parts
                var before = word.Substring(0, index);
                var after = word.Substring(index + breakPoint.Length);

                // Both parts must be valid (recursively)
                if (CheckBreakRecursive(before) && CheckBreakRecursive(after))
                {
                    return true;
                }
            }
        }

        return false;
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
    
    /// <summary>
    /// Represents a CHECKCOMPOUNDPATTERN rule.
    /// </summary>
    private record CompoundPattern(string EndChars, string? EndFlag, string BeginChars, string? BeginFlag, string? Replacement);
}
