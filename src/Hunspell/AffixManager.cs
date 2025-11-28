// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

using System.Text;
using System.Text.RegularExpressions;
                // If the part can be produced by affix rules (e.g., suffix application),
                // allow it, but only when the underlying base that produced the affixed
                // form would itself be a valid compound part in this position. This
                // prevents allowing derived forms like "foosuf" to serve as the left
                // hand of a compound where the derived form lacks the required compound
                // flag.

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

    // Word attribute flags
    private string? _noSuggestFlag;
    private string? _forbiddenWordFlag;
    private string? _needAffixFlag;
    private string? _forceUCaseFlag;

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

    // Suggestion options
    private int _maxCompoundSuggestions = 0; // 0 means unlimited
    private int _maxDiff = 0; // 0 means unlimited
    private bool _onlyMaxDiff = false;
    private bool _noSplitSuggestions = false;
    private bool _fullStrip = false;

    // Compound rules (COMPOUNDRULE)
    private readonly List<string> _compoundRules = new();

    // Compound pattern checking (CHECKCOMPOUNDPATTERN)
    private readonly List<CompoundPattern> _compoundPatterns = new();

    // Break points (BREAK)
    private readonly List<string> _breakPoints = new();

    // REP table (replacement patterns)
    private readonly List<(string from, string to)> _repTable = new();

    // Common suffixes for COMPOUNDMORESUFFIXES simplified implementation
    private static readonly string[] CommonSuffixes = { "s", "es", "ed", "ing", "er", "est", "ly", "ness", "ment", "tion" };

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

        string? line;
        while (reader.ReadLine() is { } rline)
        {
            // handle directives that put their argument on the following line
            // e.g. "COMPOUNDRULE" followed by pattern on next line, or
            // "ONLYINCOMPOUND" followed by the flag on the next line
            line = rline.TrimEnd();
            if (string.Equals(line.Trim(), "COMPOUNDRULE", StringComparison.OrdinalIgnoreCase))
            {
                // read next non-empty non-comment line as the pattern
                while (reader.ReadLine() is { } nextLine)
                {
                    var t = nextLine.Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith('#')) continue;
                    // add the pattern straight into the list
                    _compoundRules.Add(t);
                    break;
                }
                continue;
            }

            if (string.Equals(line.Trim(), "ONLYINCOMPOUND", StringComparison.OrdinalIgnoreCase))
            {
                // read next non-empty non-comment line for the flag(s)
                while (reader.ReadLine() is { } nextLine)
                {
                    var t = nextLine.Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith('#')) continue;
                    // take the first token on the next line as the only-in-compound flag
                    var parts = t.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length > 0)
                    {
                        _onlyInCompound = parts[0];
                    }
                    break;
                }
                continue;
            }

            // Several directives occasionally appear with their value on the
            // following line (many upstream test files use this style). Support
            // multi-line arguments for common directives so ProcessAffixLine()
            // can handle a single logical line consistently.
            // Support multi-line arguments for common compound flags here so later
            // parsing logic (ProcessAffixLine) isn't required to handle that style.
            var trimmed = line.Trim();
            if (string.Equals(trimmed, "COMPOUNDFLAG", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "COMPOUNDBEGIN", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "COMPOUNDMIDDLE", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "COMPOUNDFORBIDFLAG", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "COMPOUNDPERMITFLAG", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "COMPOUNDEND", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(trimmed, "COMPOUNDLAST", StringComparison.OrdinalIgnoreCase))
            {
                while (reader.ReadLine() is { } nextLine)
                {
                    var t = nextLine.Trim();
                    if (string.IsNullOrEmpty(t) || t.StartsWith('#')) continue;

                    // Append the argument token to the current logical line so the
                    // existing ProcessAffixLine() handling can pick it up cleanly.
                    line = line + " " + t;
                    break;
                }
                // Feed this modified line through ProcessAffixLine below
            }
            // Handle multi-line PFX/SFX rule formats that use a header line followed
            // by one or two continuation lines. Common layout from upstream tests:
            //   SFX S Y 1        <- header (count)
            //   SFX S 0          <- continuation with stripping
            //   suf .            <- rule body on its own line
            // Normalize these into a single logical line such as
            //   SFX S 0 suf .
            var trimmedCmd = line.TrimStart();
            if (trimmedCmd.StartsWith("PFX ", StringComparison.OrdinalIgnoreCase) ||
                trimmedCmd.StartsWith("SFX ", StringComparison.OrdinalIgnoreCase))
            {
                var hdrParts = trimmedCmd.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                // If this is a header line (e.g., "SFX X Y 1") then the next lines
                // contain the actual rule(s). Read the next non-empty, non-comment
                // line and try to assemble a full rule line.
                if (hdrParts.Length >= 4 && int.TryParse(hdrParts[3], out _))
                {
                    string? nextNonEmpty = null;
                    while (reader.ReadLine() is { } extra)
                    {
                        var t = extra.Trim();
                        if (string.IsNullOrEmpty(t) || t.StartsWith('#')) continue;
                        nextNonEmpty = t;
                        break;
                    }

                    if (!string.IsNullOrEmpty(nextNonEmpty))
                    {
                        // If the continuation line starts with PFX/SFX it may itself
                        // be a partial "PFX S 0" that needs the following line to form
                        // a full rule, otherwise it may already be a full rule.
                        var contTrim = nextNonEmpty.TrimStart();
                        if (contTrim.StartsWith("PFX ", StringComparison.OrdinalIgnoreCase) || contTrim.StartsWith("SFX ", StringComparison.OrdinalIgnoreCase))
                        {
                            var contParts = contTrim.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                            if (contParts.Length >= 4 && !int.TryParse(contParts[3], out _))
                            {
                                // continuation is already a full rule line
                                line = nextNonEmpty;
                            }
                            else
                            {
                                // continuation seems partial (e.g., "SFX S 0"); read one
                                // more non-empty line and concatenate as the rule body.
                                string? ruleBody = null;
                                while (reader.ReadLine() is { } extra2)
                                {
                                    var t2 = extra2.Trim();
                                    if (string.IsNullOrEmpty(t2) || t2.StartsWith('#')) continue;
                                    ruleBody = t2;
                                    break;
                                }

                                if (!string.IsNullOrEmpty(ruleBody))
                                {
                                    line = nextNonEmpty + " " + ruleBody;
                                }
                                else
                                {
                                    // fallback - use the continuation line as-is
                                    line = nextNonEmpty;
                                }
                            }
                        }
                        else
                        {
                            // nextNonEmpty doesn't start with PFX/SFX: assemble a rule line
                            // using the header's flag and assumed stripping if not present.
                            var flag = hdrParts.Length > 1 ? hdrParts[1] : string.Empty;
                            var strip = hdrParts.Length > 2 ? hdrParts[2] : "0";
                            line = (hdrParts[0] + " " + flag + " " + strip + " " + nextNonEmpty).TrimEnd();
                        }
                    }
                }
            }
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
            case "MAP":
            case "WORDCHARS":
                // Store for potential future use
                if (parts.Length > 1)
                {
                    _options[command] = string.Join(" ", parts[1..]);
                }
                break;

            case "REP":
                if (parts.Length > 1)
                {
                    // First line contains the count, subsequent lines contain patterns
                    if (int.TryParse(parts[1], out _))
                    {
                        // This is the count line, ignore it
                    }
                    else if (parts.Length >= 3)
                    {
                        // This is a replacement pattern: REP from to
                        _repTable.Add((parts[1], parts[2]));
                    }
                }
                break;

            // Word attribute flags
            case "NOSUGGEST":
                if (parts.Length > 1)
                {
                    _noSuggestFlag = parts[1];
                }
                break;

            case "FORBIDDENWORD":
                if (parts.Length > 1)
                {
                    _forbiddenWordFlag = parts[1];
                }
                break;

            case "NEEDAFFIX":
                if (parts.Length > 1)
                {
                    _needAffixFlag = parts[1];
                }
                break;

            case "FORCEUCASE":
                if (parts.Length > 1)
                {
                    _forceUCaseFlag = parts[1];
                }
                break;

            // Suggestion options
            case "MAXCPDSUGS":
                if (parts.Length > 1 && int.TryParse(parts[1], out var maxCpdSugs))
                {
                    _maxCompoundSuggestions = maxCpdSugs;
                }
                break;

            case "MAXDIFF":
                if (parts.Length > 1 && int.TryParse(parts[1], out var maxDiff))
                {
                    _maxDiff = maxDiff;
                }
                break;

            case "ONLYMAXDIFF":
                _onlyMaxDiff = true;
                break;

            case "NOSPLITSUGS":
                _noSplitSuggestions = true;
                break;

            case "FULLSTRIP":
                _fullStrip = true;
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

        // Some affix files use a header line like "SFX A Y 1" which indicates
        // the flag and a following count. If the 4th token is an integer this is
        // a header and not an actual rule line; skip it.
        if (int.TryParse(parts[3], out _))
        {
            return;
        }

        var flag = parts[1];
        var stripping = parts[2] == "0" ? string.Empty : parts[2];
        // The affix token may carry an appended /flag (e.g., "s/Y").
        // Extract only the affix text portion before any slash.
        var affixField = parts[3];
        var affixParts = affixField.Split('/', StringSplitOptions.None);
        var affix = affixParts[0];
        var appendedFlag = affixParts.Length > 1 ? affixParts[1].Trim() : null;
        var condition = parts.Length > 4 ? parts[4] : ".";

        var rule = new AffixRule(flag, stripping, affix, condition, isPrefix, appendedFlag);

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
            bool isValid = CheckCompoundWithRules(word);
            if (isValid && _checkCompoundRep)
            {
                // Check if this compound matches a dictionary word via REP replacements
                if (CheckCompoundRep(word))
                {
                    return false; // Forbid compound if it matches via REP
                }
            }
            return isValid;
        }

        // Otherwise, use flag-based compound checking
        // If no compound flags are defined, no compounds are allowed
        if (_compoundFlag is null && _compoundBegin is null)
        {
            return false;
        }

        // Try to split the word into valid compound parts
        bool result = CheckCompoundRecursive(word, 0, 0, null, 0, false);

        if (result && _checkCompoundRep)
        {
            // Check if this compound matches a dictionary word via REP replacements
            if (CheckCompoundRep(word))
            {
                return false; // Forbid compound if it matches via REP
            }
        }

        return result;
    }

    /// <summary>
    /// Check if a compound word would match a dictionary word via REP replacements.
    /// </summary>
    private bool CheckCompoundRep(string word)
    {
        // For each REP rule, attempt replacing the 'from' substring at every
        // possible position (one occurrence at a time) and check whether the
        // modified word exists in the dictionary. This mirrors Hunspell's
        // positional replacement checks and is important for multi-byte
        // characters where naive replace-all can mis-handle positions.
        foreach (var (from, to) in _repTable)
        {
            if (string.IsNullOrEmpty(from)) continue;
            int idx = 0;
            while ((idx = word.IndexOf(from, idx, StringComparison.Ordinal)) >= 0)
            {
                var modified = word.Substring(0, idx) + to + word.Substring(idx + from.Length);
                if (_hashManager.Lookup(modified))
                {
                    return true;
                }
                idx++; // continue searching after this position
            }
        }
        return false;
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
            // Must have at least 2 parts
            if (matchedParts.Count < 2) return false;

            // Post-validate ordinals: if the last part looks like an ordinal suffix
            // (st, nd, rd, th) and the preceding parts are numeric, ensure the
            // ordinal suffix is valid for the numeric value (e.g. 10001th is invalid).
            var lastRaw = matchedParts[^1];
            var last = lastRaw.ToLowerInvariant();

            // detect if the terminal part contains an ordinal suffix either as whole part
            // or as trailing letters following digits (e.g. "1th"). Extract trailing letters.
            int firstNonDigit = 0;
            while (firstNonDigit < lastRaw.Length && char.IsDigit(lastRaw[firstNonDigit])) firstNonDigit++;
            var trailingLetters = firstNonDigit < lastRaw.Length ? lastRaw.Substring(firstNonDigit).ToLowerInvariant() : string.Empty;
            if (!string.IsNullOrEmpty(trailingLetters) &&
                (trailingLetters.StartsWith("st") || trailingLetters.StartsWith("nd") || trailingLetters.StartsWith("rd") || trailingLetters.StartsWith("th")) ||
                (string.IsNullOrEmpty(trailingLetters) && (last.StartsWith("st") || last.StartsWith("nd") || last.StartsWith("rd") || last.StartsWith("th"))))
            {
                // Collect preceding consecutive numeric parts, but also include any
                // leading digit sequence from the final part (e.g. '1' from '1th')
                var digitsReversed = new List<string>();
                // If final part starts with digits, include that prefix
                if (firstNonDigit > 0)
                {
                    digitsReversed.Add(lastRaw.Substring(0, firstNonDigit));
                }

                for (int i = matchedParts.Count - 2; i >= 0; i--)
                {
                    var part = matchedParts[i];
                    if (part.All(c => char.IsDigit(c)))
                    {
                        digitsReversed.Add(part);
                    }
                    else
                    {
                        break;
                    }
                }

                if (digitsReversed.Count > 0)
                {
                    digitsReversed.Reverse();
                    var numStr = string.Concat(digitsReversed);
                    // Extract only digits (defensive)
                    var digitOnly = new string(numStr.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(digitOnly) && int.TryParse(digitOnly.Length <= 2 ? digitOnly : digitOnly[^2..], out var lastTwo))
                    {
                        // ordinal validation check (no debug output)
                        // derive rule: if lastTwo is 11..13 then suffix should be 'th'
                        bool isElevenToThirteen = lastTwo >= 11 && lastTwo <= 13;
                        int lastDigit = digitOnly.Length > 0 ? (digitOnly[^1] - '0') : 0;

                        var suffixToCheck = !string.IsNullOrEmpty(trailingLetters) ? trailingLetters : last;
                        if (suffixToCheck.StartsWith("th"))
                        {
                            // valid when number ends with 11..13 OR ends with 0 or 4..9
                            if (!isElevenToThirteen && (lastDigit == 1 || lastDigit == 2 || lastDigit == 3))
                                return false; // e.g., 10001th invalid
                        }
                        else if (suffixToCheck.StartsWith("st"))
                        {
                            // st valid for lastDigit ==1 unless lastTwo == 11
                            if (lastDigit != 1 || isElevenToThirteen)
                                return false;
                        }
                        else if (suffixToCheck.StartsWith("nd"))
                        {
                            if (lastDigit != 2 || isElevenToThirteen)
                                return false;
                        }
                        else if (suffixToCheck.StartsWith("rd"))
                        {
                            if (lastDigit != 3 || isElevenToThirteen)
                                return false;
                        }
                    }
                }
            }

            // final acceptance
            return true;
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

            // If pattern element is a single-character digit token (e.g., '1'..'7')
            // treat it as a special token classification and match accordingly.
            if (flag.Length == 1 && char.IsDigit(flag[0]))
            {
                if (ComponentMatchesDigitClass(part, flag[0]))
                {
                    var newMatchedParts = new List<string>(matchedParts) { part };
                    if (MatchesCompoundRule(word, pattern, wordPos + len, nextPatternPos, newMatchedParts))
                    {
                        return true;
                    }
                }

                // Try the next length
                continue;
            }

            // If we have flags for the current part, and any character in the pattern element
            // matches one of those flags, try advancing the pattern to nextPatternPos.
            if (flags is not null)
            {
                if (flag.Any(ch => flags.Contains(ch)))
                {
                    var newMatchedParts = new List<string>(matchedParts) { part };
                    if (MatchesCompoundRule(word, pattern, wordPos + len, nextPatternPos, newMatchedParts))
                    {
                        return true;
                    }
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
        // Try matching one or more occurrences of the same pattern element (quantifier *)
        for (int len = _compoundMin; len <= word.Length - wordPos; len++)
        {
            var part = word.Substring(wordPos, len);
            var flags = _hashManager.GetWordFlags(part);

            // Digit-token handling (1..7)
            if (flag.Length == 1 && char.IsDigit(flag[0]))
            {
                if (ComponentMatchesDigitClass(part, flag[0]))
                {
                    var newMatchedParts = new List<string>(matchedParts) { part };
                    // Stay at the same pattern position to allow additional repeats
                    if (MatchesCompoundRule(word, pattern, wordPos + len, patternPos, newMatchedParts))
                    {
                        return true;
                    }
                }

                // try next length
                continue;
            }

            // For flag groups / characters, if any char in the pattern element exists in the part's flags
            // then we can advance while keeping patternPos for additional repeats
            if (flags is not null && flag.Any(ch => flags.Contains(ch)))
            {
                var newMatchedParts = new List<string>(matchedParts) { part };
                if (MatchesCompoundRule(word, pattern, wordPos + len, patternPos, newMatchedParts))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool ComponentMatchesDigitClass(string component, char digitToken)
    {
        // '1' -> digits-only
        if (digitToken == '1')
        {
            return component.All(c => char.IsDigit(c));
        }

        // '3' -> mixed digits/letters or digit groups with punctuation
        if (digitToken == '3')
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(component, "^\\d+[\\-:\\.]\\d+$")) return true;
            bool hasLetter = component.Any(c => char.IsLetter(c));
            bool hasDigit = component.Any(c => char.IsDigit(c));
            return hasLetter && hasDigit;
        }

        // For spelled-number / ordinal detection reuse helpers similar to the muncher
        if (digitToken == '2' || digitToken == '4' || digitToken == '5' || digitToken == '7' || digitToken == '6')
        {
            var s = component.Replace("-", "").Replace(" ", "").ToLowerInvariant();
            if (digitToken == '6')
            {
                // heuristic: suffix-based numeric-like classes (e.g. år, års, åring, tals)
                var suffixes = new[] { "år", "års", "åring", "tals", "tal" };
                return suffixes.Any(suf => s.EndsWith(suf));
            }

            if (TryParseSpelledNumber(s, out var value, out var isOrdinal))
            {
                if (digitToken == '2')
                {
                    // spelled-number class (units/tens/teens/hundreds up to 100)
                    return value >= 1 && value <= 100; // treat 0..100 as class 2
                }
                if (digitToken == '4') return value >= 100 && value % 100 == 0; // multiples of 100
                if (digitToken == '5') return value >= 1000; // >= 1000
                if (digitToken == '7') return isOrdinal; // ordinal
            }

            // fallback spelled-number detection for class 2
            if (digitToken == '2')
            {
                return IsSpelledNumber(s);
            }

            return false;
        }

        return false;
    }

    // Copy adapted helpers from muncher for spelled-number parsing
    private bool TryParseSpelledNumber(string input, out int value, out bool isOrdinal)
    {
        value = 0;
        isOrdinal = false;
        if (string.IsNullOrEmpty(input)) return false;

        var s = input.Replace("-", "").Replace(" ", "");
        s = s.ToLowerInvariant();

        var ordSuffixMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
        {
            {"första", "en"}, {"först", "en"}, {"andra", "två"}, {"tredje", "tre"}, {"fjärde", "fyra"}, {"femte", "fem"}, {"sjätte", "sex"}, {"sjunde", "sju"}, {"åttonde", "åtta"}, {"nionde", "nio"}, {"tionde", "tio"},
            {"hundrade", "hundra"}, {"tusende", "tusen"}
        };

        foreach (var kv in ordSuffixMap.OrderByDescending(e => e.Key.Length))
        {
            var suf = kv.Key.ToLowerInvariant();
            if (s.EndsWith(suf, StringComparison.InvariantCultureIgnoreCase))
            {
                isOrdinal = true;
                s = s.Substring(0, s.Length - suf.Length) + kv.Value;
                break;
            }
        }

        var units = new Dictionary<string,int> {
            {"noll",0},{"en",1},{"ett",1},{"två",2},{"tva",2},{"tre",3},{"fyra",4},{"fem",5},{"sex",6},{"sju",7},{"åtta",8},{"atta",8},{"nio",9}
        };
        var teens = new Dictionary<string,int> {
            {"tio",10},{"elva",11},{"tolv",12},{"treton",13},{"fjorton",14},{"femton",15},{"sexton",16},{"sjutton",17},{"arton",18},{"nitton",19}
        };
        var tens = new Dictionary<string,int> {
            {"tjugo",20},{"trettio",30},{"fyrtio",40},{"femtio",50},{"sextio",60},{"sjuttio",70},{"åttio",80},{"attio",80},{"nittio",90}
        };

        var idx = 0;
        int total = 0;
        int current = 0;
        bool matched = false;

        while (idx < s.Length)
        {
            matched = false;

            if (s.Substring(idx).StartsWith("miljarder")) { if (current==0) current=1; total += current * 1000000000; current = 0; idx += "miljarder".Length; matched = true; continue; }
            if (s.Substring(idx).StartsWith("miljard")) { if (current==0) current=1; total += current * 1000000000; current = 0; idx += "miljard".Length; matched = true; continue; }
            if (s.Substring(idx).StartsWith("miljoner")) { if (current==0) current=1; total += current * 1000000; current = 0; idx += "miljoner".Length; matched = true; continue; }
            if (s.Substring(idx).StartsWith("miljon")) { if (current==0) current=1; total += current * 1000000; current = 0; idx += "miljon".Length; matched = true; continue; }
            if (s.Substring(idx).StartsWith("tusentals")) { if (current==0) current=1; total += current * 1000; current = 0; idx += "tusentals".Length; matched = true; continue; }
            if (s.Substring(idx).StartsWith("tusen")) { if (current==0) current=1; total += current * 1000; current = 0; idx += "tusen".Length; matched = true; continue; }

            if (s.Substring(idx).StartsWith("hundra")) { if (current==0) current=1; current *= 100; idx += "hundra".Length; matched = true; continue; }

            // tens
            foreach (var t in tens.OrderByDescending(x=>x.Key.Length))
            {
                if (s.Substring(idx).StartsWith(t.Key)) { current += t.Value; idx += t.Key.Length; matched = true; break; }
            }
            if (matched) continue;

            // teens
            foreach (var t in teens.OrderByDescending(x=>x.Key.Length))
            {
                if (s.Substring(idx).StartsWith(t.Key)) { current += t.Value; idx += t.Key.Length; matched = true; break; }
            }
            if (matched) continue;

            // units
            foreach (var u in units.OrderByDescending(x=>x.Key.Length))
            {
                if (s.Substring(idx).StartsWith(u.Key)) { current += u.Value; idx += u.Key.Length; matched = true; break; }
            }
            if (!matched) break;
        }

        if (!matched && current==0 && total==0) return false;

        total += current;

        value = total;
        return true;
    }

    private bool IsSpelledNumber(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        var tokens = new[] { "noll", "en", "ett", "två", "tre", "fyra", "fem", "sex", "sju", "åtta", "nio",
            "tio", "elva", "tolv", "treton", "fjorton", "femton", "sexton", "sjutton", "arton", "nitton", "tjugo",
            "trettio", "fyrtio", "femtio", "sextio", "sjuttio", "åttio", "nittio", "hundra", "tusen", "miljon", "miljoner", "miljard", "miljarder", "och" };

        int idx = 0; bool matched = false;
        while (idx < s.Length)
        {
            matched = false;
            foreach (var t in tokens.OrderByDescending(t => t.Length))
            {
                if (s.Substring(idx).StartsWith(t)) { idx += t.Length; matched = true; break; }
            }
            if (!matched) break;
        }

        return matched && idx >= s.Length;
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
    private bool CheckCompoundRecursive(string word, int wordCount, int position, string? previousPart, int syllableCount, bool requiresForceUCase)
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
            var ok = wordCount >= 2; // Must have at least 2 parts to be a compound
            if (ok)
            {
                // matched
            }
            // If FORCEUCASE was required by any component then the final word
            // should have an upper-case initial letter; otherwise reject.
            if (ok && requiresForceUCase)
            {
                // DEBUG: log compound acceptance decision for force-u case
                // Final compound acceptance decision: ensure final-case constraints are met
                if (string.IsNullOrEmpty(word) || !char.IsUpper(word[0])) return false;
            }
            return ok;
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
            if (!IsValidCompoundPart(part, wordCount, position, i, word, out var partRequiresForce))
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
            // If this part is itself a valid two-word compound, it contributes
            // two components to the overall word count (so enforce COMPOUNDWORDMAX
            // correctly). Otherwise it contributes a single component.
            var contribution = 1;
            if (IsCompoundMadeOfTwoWords(part, out var aInnerForce, out var bInnerForce))
            {
                contribution = 2;
                // Only propagate a/b inner force when those inner components are at
                // the edges of the full word where FORCEUCASE actually applies.
                // aInnerForce applies to the first subcomponent of 'part' (absolute
                // position = position). bInnerForce applies to the second subcomponent
                // of 'part' (absolute position ends at i). FORCEUCASE should be
                // propagated only when that subcomponent is at the beginning or the
                // end of the ENTIRE word respectively.
                if ((aInnerForce && position == 0) || (bInnerForce && i == word.Length))
                {
                    partRequiresForce = true;
                }
            }
            // Propagate whether any component required FORCEUCASE
            if (CheckCompoundRecursive(word, wordCount + contribution, i, part, syllableCount + partSyllables, requiresForceUCase || partRequiresForce))
            {
                // Accepting decomposition at split {i}
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a word part is valid for its position in the compound.
    /// </summary>
    private bool IsValidCompoundPart(string part, int wordCount, int startPos, int endPos, string fullWord, out bool requiresForceUCase)
    {
        requiresForceUCase = false;
        // Must meet minimum length
        if (part.Length < _compoundMin)
        {
            return false;
        }

        // Get the word's flags from the dictionary
        var flags = _hashManager.GetWordFlags(part);
        // Debug traces removed
        // Allow basic separator handling (hyphens) by attempting trimmed lookups if direct lookup fails
        var lookUpPart = part;
        if (flags is null && (part.StartsWith('-') || part.EndsWith('-')))
        {
            var trimmed = part.Trim('-');
            if (!string.IsNullOrEmpty(trimmed))
            {
                flags = _hashManager.GetWordFlags(trimmed);
                if (flags is not null)
                {
                    lookUpPart = trimmed; // use trimmed variant for further flag checks
                }
            }
        }
            if (flags is null)
        {
            // If COMPOUNDMORESUFFIXES is enabled, try to check if this could be
            // a word with affixes applied. This is a simplified check.
            if (_compoundMoreSuffixes && IsValidCompoundPartWithAffixes(part, wordCount, endPos, fullWord))
            {
                return true;
            }
            // If the part can be produced by affix rules, check the underlying
            // base that yields this derived form. We only allow the derived form
            // if the underlying base would be a valid compound-part in this
            // position — and also enforce simple positional constraints for
            // prefix/suffix placements (e.g., suffix-derived pieces shouldn't
            // appear in non-final positions).
            if (TryFindAffixBase(part, allowBaseOnlyInCompound: true, out var affixBase, out var matchKind, out var appendedFlag))
            {
                // If the base itself is a small two-word compound, accept it
                if (affixBase is not null && IsCompoundMadeOfTwoWords(affixBase, out var aInnerAffix, out var bInnerAffix))
                {
                    if ((aInnerAffix && wordCount == 0) || (bInnerAffix && endPos == fullWord.Length)) requiresForceUCase = true;
                    return true;
                }

                // If the original Text 'part' is not a direct dictionary lookup it is
                // an affix-derived piece. Derivation containing a suffix should not
                // be used in non-final positions, and derivation containing a
                // prefix should not be used in non-initial positions.
                var derived = !_hashManager.Lookup(part);
                bool hasPrefix = matchKind == AffixMatchKind.PrefixOnly || matchKind == AffixMatchKind.PrefixThenSuffix || matchKind == AffixMatchKind.SuffixThenPrefix;
                bool hasSuffix = matchKind == AffixMatchKind.SuffixOnly || matchKind == AffixMatchKind.PrefixThenSuffix || matchKind == AffixMatchKind.SuffixThenPrefix;

                if (derived)
                {
                    // If the affix rules that produced this derived piece appended a
                    // COMPOUNDPERMITFLAG then allow positional exceptions. Otherwise
                    // enforce the default: suffix-derived pieces may not appear in
                    // non-final positions and prefix-derived pieces may not appear
                    // in non-initial positions.
                    var permittedByAffix = false;
                    if (!string.IsNullOrEmpty(appendedFlag) && !string.IsNullOrEmpty(_compoundPermitFlag))
                    {
                        // If any appended flag is present in the COMPOUNDPERMITFLAG set
                        permittedByAffix = appendedFlag.Any(ch => _compoundPermitFlag.Contains(ch));
                    }

                    if (endPos < fullWord.Length && hasSuffix && !permittedByAffix)
                    {
                        // suffix-based derivations are not allowed when this part is
                        // not the last element of the compound
                        return false;
                    }
                    if (wordCount > 0 && hasPrefix && !permittedByAffix)
                    {
                        // prefix-based derivations are not permitted for non-initial
                        // components
                        return false;
                    }
                }

                // Validate flags on the underlying baseCandidate. When an affix
                // produced the derived form, append any 'appendedFlag' characters
                // from the affix rule and treat those as if they were present
                // on the derived form; this affects COMPOUNDFLAG/COMPOUNDPERMITFLAG
                // and COMPOUNDFORBIDFLAG semantics (upstream Hunspell appends
                // these flags to derived word forms).
                if (affixBase is null) return false;
                var baseFlags = _hashManager.GetWordFlags(affixBase);
                var combinedBaseFlags = (baseFlags ?? string.Empty) + (appendedFlag ?? string.Empty);
                if (combinedBaseFlags is not null)
                {
                    // mark if derived variant(s) require FORCEUCASE — only apply when
                    // the derived piece occupies an edge position of the full word
                    // (first or last); middle pieces shouldn't force capitalization
                    if (!string.IsNullOrEmpty(_forceUCaseFlag) && combinedBaseFlags.Contains(_forceUCaseFlag) && (wordCount == 0 || endPos == fullWord.Length))
                    {
                        requiresForceUCase = true;
                    }
                    if (wordCount == 0)
                    {
                        if (_compoundBegin is not null)
                        {
                            if (combinedBaseFlags.Contains(_compoundBegin) || (_compoundFlag is not null && combinedBaseFlags.Contains(_compoundFlag)) || (_onlyInCompound is not null && combinedBaseFlags.Contains(_onlyInCompound))) return true;
                        }
                        else if (_compoundFlag is not null)
                        {
                            if ((_compoundFlag is not null && combinedBaseFlags.Contains(_compoundFlag)) || (_onlyInCompound is not null && combinedBaseFlags.Contains(_onlyInCompound))) return true;
                        }
                    }
                    else if (endPos < fullWord.Length)
                    {
                        if (_compoundMiddle is not null)
                        {
                            if (combinedBaseFlags.Contains(_compoundMiddle) || (_compoundFlag is not null && combinedBaseFlags.Contains(_compoundFlag)) || (_onlyInCompound is not null && combinedBaseFlags.Contains(_onlyInCompound))) return true;
                        }
                        else if (_compoundFlag is not null)
                        {
                            if ((_compoundFlag is not null && combinedBaseFlags.Contains(_compoundFlag)) || (_onlyInCompound is not null && combinedBaseFlags.Contains(_onlyInCompound))) return true;
                        }
                    }
                    else
                    {
                        if (_compoundEnd is not null)
                        {
                            if (combinedBaseFlags.Contains(_compoundEnd) || (_compoundFlag is not null && combinedBaseFlags.Contains(_compoundFlag)) || (_onlyInCompound is not null && combinedBaseFlags.Contains(_onlyInCompound))) return true;
                        }
                        else if (_compoundFlag is not null)
                        {
                            if ((_compoundFlag is not null && combinedBaseFlags.Contains(_compoundFlag)) || (_onlyInCompound is not null && combinedBaseFlags.Contains(_onlyInCompound))) return true;
                        }
                    }
                }

                    // Check if the derived form (including appended flags) is marked
                    // as forbidden to appear inside compounds.
                    if (!string.IsNullOrEmpty(_compoundForbidFlag) && combinedBaseFlags.Contains(_compoundForbidFlag))
                    {
                        return false;
                    }

                    // Not permitted as a compound part
                return false;
            }

            // If the part can be formed as a valid two-word compound, allow it
            if (IsCompoundMadeOfTwoWords(part, out var aInnerPart, out var bInnerPart))
            {
                if ((aInnerPart && wordCount == 0) || (bInnerPart && endPos == fullWord.Length)) requiresForceUCase = true;
                return true;
            }

            return false; // Word not in dictionary
        }

        // Check position-specific flags
        // When a surface-form has multiple homonym variants in the dictionary
        // (e.g., foo/S and foo/YX) we must accept the part when at least one
        // variant meets the position requirements and is not forbidden. A
        // variant that contains COMPOUNDFORBIDFLAG or FORBIDDENWORD must not be
        // used for compounds.
        var variants = _hashManager.GetWordFlagVariants(lookUpPart).ToList();
        if (!string.IsNullOrEmpty(_forceUCaseFlag) && (wordCount == 0 || endPos == fullWord.Length) && variants.Any(v => !string.IsNullOrEmpty(v) && v.Contains(_forceUCaseFlag)))
        {
            requiresForceUCase = true;
            // Part requires FORCEUCASE based on variant flags
        }
        if (wordCount == 0)
        {
            // First word in compound: accept if there exists a variant that has
            // the position-specific flag (compound-begin) or the generic
            // compound flag, and that variant is not forbidden or marked
            // COMPOUNDFORBIDFLAG.
            if (_compoundBegin is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) &&
                                       (v.Contains(_compoundBegin) || (_compoundFlag is not null && v.Contains(_compoundFlag)) || (_onlyInCompound is not null && v.Contains(_onlyInCompound))) &&
                                       !(!string.IsNullOrEmpty(_compoundForbidFlag) && v.Contains(_compoundForbidFlag)) &&
                                       !(!string.IsNullOrEmpty(_forbiddenWordFlag) && v.Contains(_forbiddenWordFlag))))
                {
                    return false;
                }
            }
            else if (_compoundFlag is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) && (v.Contains(_compoundFlag) || (_onlyInCompound is not null && v.Contains(_onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && v.Contains(_compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && v.Contains(_forbiddenWordFlag)))) return false;
            }
        }
        else if (endPos < fullWord.Length)
        {
            // Middle word in compound: require either the compound-middle flag (if defined)
            // or the generic compound flag (if defined).
            if (_compoundMiddle is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) && (v.Contains(_compoundMiddle) || (_compoundFlag is not null && v.Contains(_compoundFlag)) || (_onlyInCompound is not null && v.Contains(_onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && v.Contains(_compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && v.Contains(_forbiddenWordFlag))))
                {
                    return false;
                }
            }
            else if (_compoundFlag is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) && (v.Contains(_compoundFlag) || (_onlyInCompound is not null && v.Contains(_onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && v.Contains(_compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && v.Contains(_forbiddenWordFlag)))) return false;
            }
        }
        else
        {
            // Last word in compound: require either the compound-end flag (if defined)
            // or the generic compound flag (if defined).
            if (_compoundEnd is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) && (v.Contains(_compoundEnd) || (_compoundFlag is not null && v.Contains(_compoundFlag)) || (_onlyInCompound is not null && v.Contains(_onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && v.Contains(_compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && v.Contains(_forbiddenWordFlag))))
                {
                    return false;
                }
            }
            else if (_compoundFlag is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) && (v.Contains(_compoundFlag) || (_onlyInCompound is not null && v.Contains(_onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && v.Contains(_compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && v.Contains(_forbiddenWordFlag)))) return false;
            }
        }

        // Additional rejection: if every variant of the surface form carries
        // COMPOUNDFORBIDFLAG or FORBIDDENWORD then it can't be used inside
        // compounds. If at least one suitable variant remains, it's acceptable.
        if (!string.IsNullOrEmpty(_compoundForbidFlag) || !string.IsNullOrEmpty(_forbiddenWordFlag))
        {
            // if every variant is either compound-forbidden or fully forbidden then
            // reject the part for compound use.
            if (variants.Count > 0 && variants.All(v => (!string.IsNullOrEmpty(_compoundForbidFlag) && v.Contains(_compoundForbidFlag)) || (!string.IsNullOrEmpty(_forbiddenWordFlag) && v.Contains(_forbiddenWordFlag))))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a compound part could be valid with affixes applied (COMPOUNDMORESUFFIXES).
    /// This is a simplified implementation that checks for common suffix patterns.
    /// </summary>
    /// <remarks>
    /// COMPOUNDMORESUFFIXES allows twofold suffixes within compounds. Full implementation
    /// requires deep integration with affix stripping/application logic. This simplified
    /// version provides basic support by attempting to strip common suffixes and check
    /// the resulting base forms.
    /// </remarks>
    private bool IsValidCompoundPartWithAffixes(string part, int wordCount, int endPos, string fullWord)
    {
        // Try stripping common English suffixes as a basic implementation
        // In a full implementation, this would use the actual affix rules

        foreach (var suffix in CommonSuffixes)
        {
            if (part.Length >= suffix.Length + _compoundMin && part.EndsWith(suffix))
            {
                var basePart = part[..^suffix.Length];
                var baseFlags = _hashManager.GetWordFlags(basePart);

                if (baseFlags is not null)
                {
                    // Check if the base word has appropriate compound flags
                    bool hasCompoundFlag = false;

                    if (wordCount == 0)
                    {
                        hasCompoundFlag = (_compoundBegin is not null && baseFlags.Contains(_compoundBegin)) ||
                                        (_compoundFlag is not null && baseFlags.Contains(_compoundFlag));
                    }
                    else if (endPos < fullWord.Length)
                    {
                        hasCompoundFlag = (_compoundMiddle is not null && baseFlags.Contains(_compoundMiddle)) ||
                                        (_compoundFlag is not null && baseFlags.Contains(_compoundFlag));
                    }
                    else
                    {
                        hasCompoundFlag = (_compoundEnd is not null && baseFlags.Contains(_compoundEnd)) ||
                                        (_compoundFlag is not null && baseFlags.Contains(_compoundFlag));
                    }

                    if (hasCompoundFlag)
                    {
                        // Check COMPOUNDFORBIDFLAG
                        if (_compoundForbidFlag is not null && baseFlags.Contains(_compoundForbidFlag))
                        {
                            continue;
                        }
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check compound-specific rules (dup, case, triple, etc.)
    /// </summary>
    private bool CheckCompoundRules(string word, int prevEnd, int currentEnd, string? previousPart, string currentPart)
    {
        // check boundary rules for the boundary between prevEnd and currentEnd
        if (prevEnd == 0)
        {
            return true; // No previous part to check against
        }

        // Check CHECKCOMPOUNDDUP - forbid duplicated words or duplicated atomic
        // components across boundaries. This also looks into simple two-word
        // components (e.g., "foofoo" -> "foo" + "foo") to detect duplicates
        // across the boundary when nested components would otherwise hide them.
        if (_checkCompoundDup && previousPart is not null)
        {
            if (previousPart.Equals(currentPart, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // If the current part is itself a simple two-word compound and its
            // first atomic subpart equals the previous part, upstream Hunspell
            // allows the sequence (e.g., "foo" + "fooBar" is allowed). We do
            // not reject this case here.

            // If the previous part is a simple two-word compound and its last
            // atomic subpart equals the current part, that's also a duplicate
            // across the boundary (e.g., prev="fooBar", current="Bar").
            var previousSplit = FindTwoWordSplit(previousPart);
            if (previousSplit is not null)
            {
                if (previousSplit.Value.second.Equals(currentPart, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        // Check CHECKCOMPOUNDCASE - forbid uppercase letters at boundaries
        if (_checkCompoundCase)
        {
            if (prevEnd > 0 && prevEnd < word.Length)
            {
                var lastChar = word[prevEnd - 1];
                var firstChar = word[prevEnd];

                // Only apply case checks for adjacent letters. If either side is not a letter
                // (e.g., a hyphen), then the check is skipped and the compound may be allowed.
                if (char.IsLetter(lastChar) && char.IsLetter(firstChar))
                {
                    // DEBUG TRACE - can be removed once behavior verified
                    // Console debug removed — rely on unit tests for validation
                        // CHECKCOMPOUNDCASE: forbid uppercase initial letter of the next
                        // component at the boundary (e.g. fooBar is invalid). Upstream
                        // Hunspell allows an uppercase earlier component followed by
                        // lowercase (e.g. BAZfoo).
                        if (char.IsUpper(lastChar) || char.IsUpper(firstChar))
                        {
                            // boundary case rejected
                            return false;
                        }
                }
            }
        }

        // Check CHECKCOMPOUNDTRIPLE - forbid triple repeating letters
        if (_checkCompoundTriple && prevEnd >= 1 && currentEnd > prevEnd + 1)
        {
            // Check if we have three consecutive identical letters overlapping the boundary
            // We'll look for any run of three identical chars that overlaps the boundary
            int start = Math.Max(0, prevEnd - 2);
            int end = Math.Min(word.Length - 3, prevEnd);
            for (int i = start; i <= end; i++)
            {
                if (word[i] == word[i + 1] && word[i + 1] == word[i + 2])
                {
                    // Ensure the triple sequence overlaps the boundary (i..i+2 includes prevEnd-1..prevEnd etc.)
                    if (i <= prevEnd && i + 2 >= prevEnd)
                    {
                        if (!_simplifiedTriple)
                        {
                            return false;
                        }
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

        // Use per-entry flag variants to correctly detect multi-character flags
        // (e.g. 'cc'). GetWordFlags() returns deduplicated characters which can
        // hide repeated characters and lead to incorrect matches.
        var variants = _hashManager.GetWordFlagVariants(word).ToList();
        if (variants.Count == 0) return false;
        return variants.Any(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound));
    }

    /// <summary>
    /// Return true if the given dictionary word requires an affix to be valid
    /// (i.e. marked by NEEDAFFIX). This mirrors the NEEDAFFIX directive's intent.
    /// </summary>
    public bool RequiresAffix(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_needAffixFlag is null) return false;

        var flags = _hashManager.GetWordFlags(word);
        return flags is not null && flags.Contains(_needAffixFlag);
    }

    /// <summary>
    /// Return true if the given word is marked as forbidden (FORBIDDENWORD) either
    /// directly in the dictionary or via affix-derived forms that inherit/apply
    /// appended flags from affix rules.
    /// </summary>
    public bool IsForbiddenWord(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_forbiddenWordFlag is null) return false;

        // direct lookup — gather all homonym flag variants. If any variant
        // doesn't include the forbidden flag then the surface form is allowed.
        var variants = _hashManager.GetWordFlagVariants(word).ToList();
        if (variants.Count > 0)
        {
            if (variants.Any(v => string.IsNullOrEmpty(v) || !v.Contains(_forbiddenWordFlag)))
            {
                return false;
            }
            return true; // all variants contain the forbidden flag
        }

        // If it's affix-derived, try to locate the base and see whether the
        // base (including appended affix flags) indicates forbidden.
        if (TryFindAffixBase(word, allowBaseOnlyInCompound: false, out var baseCandidate, out _, out var appended))
        {
            if (baseCandidate is not null)
            {
                var baseVariants = _hashManager.GetWordFlagVariants(baseCandidate).ToList();
                if (baseVariants.Count == 0) return false;
                // For derived forms treat each homonym variant separately; only
                // when every variant (after adding appended flags) contains the
                // forbidden flag is the derived form forbidden.
                return baseVariants.All(v => (v + (appended ?? string.Empty)).Contains(_forbiddenWordFlag));
            }
        }

        return false;
    }

    /// <summary>
    /// Try to validate a word by applying simple affix rules (prefix/suffix)
    /// and checking if the resulting base exists in the dictionary. This is a
    /// simplified check sufficient for many tests (e.g., SFX 's' forming plurals).
    /// </summary>
    public bool CheckAffixedWord(string word, bool allowBaseOnlyInCompound = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(word)) return false;

        // Use the helper that returns the base root candidate when a derivation
        // is found; this centralizes the logic for nested affix combinations.
        return TryFindAffixBase(word, allowBaseOnlyInCompound, out _, out _, out _);
    }

    /// <summary>
    /// Try to find a dictionary base candidate that generates the supplied
    /// word via one or two affix operations (suffix/prefix and prefix/suffix).
    /// If a base is found, return true and set baseCandidate to the matched
    /// dictionary root (or the compound base formed by two words).
    /// </summary>
    private enum AffixMatchKind { None, PrefixOnly, SuffixOnly, PrefixThenSuffix, SuffixThenPrefix }

    private bool TryFindAffixBase(string word, bool allowBaseOnlyInCompound, out string? baseCandidate, out AffixMatchKind kind, out string? appendedFlag)
    {
        baseCandidate = null;
        kind = AffixMatchKind.None;
        appendedFlag = null;

        // Helper to join appended flags safely
        static string ConcatFlags(string? a, string? b)
            => (a ?? string.Empty) + (b ?? string.Empty);

        // 1) Try suffix-first: word = base + suffix
        foreach (var sfx in _suffixes)
        {
            if (string.IsNullOrEmpty(sfx.Affix)) continue;
            if (!word.EndsWith(sfx.Affix, StringComparison.Ordinal)) continue;

            var base1 = word.Substring(0, word.Length - sfx.Affix.Length);

            // apply stripping
            if (!string.IsNullOrEmpty(sfx.Stripping) && sfx.Stripping != "0")
            {
                if (base1.EndsWith(sfx.Stripping, StringComparison.Ordinal))
                {
                    base1 = base1.Substring(0, base1.Length - sfx.Stripping.Length);
                }
                else
                {
                    continue;
                }
            }

            // condition check
            if (!string.IsNullOrEmpty(sfx.Condition) && sfx.Condition != ".")
            {
                try
                {
                    if (!Regex.IsMatch(base1, sfx.Condition + "$", RegexOptions.CultureInvariant)) continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            // If base1 is a dictionary word and allowed
            if (_hashManager.Lookup(base1))
            {
                var baseVariants = _hashManager.GetWordFlagVariants(base1).ToList();
                if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                {
                    // base only allowed in compounds and caller disallows it
                    // try other possibilities
                }
                else
                {
                    baseCandidate = base1;
                    kind = AffixMatchKind.SuffixOnly;
                    appendedFlag = sfx.AppendedFlag;
                    return true;
                }
            }

            // If base1 can be created by combining two dictionary words, accept it
            if (IsCompoundMadeOfTwoWords(base1, out _, out _))
            {
                baseCandidate = base1;
                kind = AffixMatchKind.SuffixOnly;
                appendedFlag = sfx.AppendedFlag;
                return true;
            }

            // Try stripping a prefix from base1: base1 = prefix + root
            foreach (var pfx in _prefixes)
            {
                if (string.IsNullOrEmpty(pfx.Affix)) continue;
                if (!base1.StartsWith(pfx.Affix, StringComparison.Ordinal)) continue;

                var base2 = base1.Substring(pfx.Affix.Length);

                if (!string.IsNullOrEmpty(pfx.Stripping) && pfx.Stripping != "0")
                {
                    if (base2.StartsWith(pfx.Stripping, StringComparison.Ordinal))
                    {
                        base2 = base2.Substring(pfx.Stripping.Length);
                    }
                    else
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(pfx.Condition) && pfx.Condition != ".")
                {
                    try
                    {
                        if (!Regex.IsMatch(base2, "^" + pfx.Condition, RegexOptions.CultureInvariant)) continue;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }

                if (_hashManager.Lookup(base2))
                {
                    var baseVariants = _hashManager.GetWordFlagVariants(base2).ToList();
                    if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                    {
                        // not allowed by caller
                    }
                    else
                    {
                        baseCandidate = base2;
                        kind = AffixMatchKind.SuffixThenPrefix;
                        appendedFlag = ConcatFlags(sfx.AppendedFlag, pfx.AppendedFlag);
                        return true;
                    }
                }

                if (IsCompoundMadeOfTwoWords(base2, out _, out _))
                {
                    baseCandidate = base2;
                    kind = AffixMatchKind.SuffixThenPrefix;
                    appendedFlag = ConcatFlags(sfx.AppendedFlag, pfx.AppendedFlag);
                    return true;
                }
            }
        }

        // 2) Try prefix-first: word = prefix + base
        foreach (var pfx in _prefixes)
        {
            if (string.IsNullOrEmpty(pfx.Affix)) continue;
            if (!word.StartsWith(pfx.Affix, StringComparison.Ordinal)) continue;

            var rem = word.Substring(pfx.Affix.Length);

            if (!string.IsNullOrEmpty(pfx.Stripping) && pfx.Stripping != "0")
            {
                if (rem.StartsWith(pfx.Stripping, StringComparison.Ordinal))
                {
                    rem = rem.Substring(pfx.Stripping.Length);
                }
                else
                {
                    continue;
                }
            }

            if (!string.IsNullOrEmpty(pfx.Condition) && pfx.Condition != ".")
            {
                try
                {
                    if (!Regex.IsMatch(rem, "^" + pfx.Condition, RegexOptions.CultureInvariant)) continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            // direct base after prefix
            if (_hashManager.Lookup(rem))
            {
                var baseVariants = _hashManager.GetWordFlagVariants(rem).ToList();
                if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                {
                    // not allowed by caller, continue
                }
                else
                {
                    baseCandidate = rem;
                    kind = AffixMatchKind.PrefixOnly;
                    appendedFlag = pfx.AppendedFlag;
                    return true;
                }
            }

            if (IsCompoundMadeOfTwoWords(rem, out _, out _))
            {
                baseCandidate = rem;
                kind = AffixMatchKind.PrefixOnly;
                appendedFlag = pfx.AppendedFlag;
                return true;
            }

            // try suffix on remainder
            foreach (var sfx in _suffixes)
            {
                if (string.IsNullOrEmpty(sfx.Affix)) continue;
                if (!rem.EndsWith(sfx.Affix, StringComparison.Ordinal)) continue;

                var base2 = rem.Substring(0, rem.Length - sfx.Affix.Length);

                if (!string.IsNullOrEmpty(sfx.Stripping) && sfx.Stripping != "0")
                {
                    if (base2.EndsWith(sfx.Stripping, StringComparison.Ordinal))
                    {
                        base2 = base2.Substring(0, base2.Length - sfx.Stripping.Length);
                    }
                    else
                    {
                        continue;
                    }
                }

                if (!string.IsNullOrEmpty(sfx.Condition) && sfx.Condition != ".")
                {
                    try
                    {
                        if (!Regex.IsMatch(base2, sfx.Condition + "$", RegexOptions.CultureInvariant)) continue;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }

                if (_hashManager.Lookup(base2))
                {
                    var baseVariants = _hashManager.GetWordFlagVariants(base2).ToList();
                    if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                    {
                        // not allowed
                    }
                    else
                    {
                        baseCandidate = base2;
                        kind = AffixMatchKind.PrefixThenSuffix;
                        appendedFlag = ConcatFlags(pfx.AppendedFlag, sfx.AppendedFlag);
                        return true;
                    }
                }

                if (IsCompoundMadeOfTwoWords(base2, out _, out _))
                {
                    baseCandidate = base2;
                    kind = AffixMatchKind.PrefixThenSuffix;
                    appendedFlag = ConcatFlags(pfx.AppendedFlag, sfx.AppendedFlag);
                    return true;
                }
            }
        }

        return false;
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
    /// Check whether the supplied word can be made by concatenating exactly two
    /// dictionary words that meet the minimum compound length constraints. This
    /// provides a shallow nested-compound check without unbounded recursion.
    /// </summary>
    private bool IsCompoundMadeOfTwoWords(string word, out bool aRequiresForce, out bool bRequiresForce)
    {
        aRequiresForce = false;
        bRequiresForce = false;
        if (string.IsNullOrEmpty(word)) return false;
        if (word.Length < 2 * _compoundMin) return false;

            for (int i = _compoundMin; i <= word.Length - _compoundMin; i++)
        {
            var a = word.Substring(0, i);
            var b = word.Substring(i);

                // both pieces must be present in the dictionary
                if (_hashManager.Lookup(a) && _hashManager.Lookup(b))
                {
                    // Validate that each subcomponent would be valid in the position it
                    // would occupy (first and last) and that the internal boundary
                    // obeys compound rules. This prevents allowing nested splits that
                    // violate COMPOUNDFLAG/position constraints (e.g., a first component
                    // without the required compound flag).
                    if (!IsValidCompoundPart(a, 0, 0, i, word, out var aForce)) continue;
                    if (!IsValidCompoundPart(b, 1, i, word.Length, word, out var bForce)) continue;

                    // validate the internal boundary's compound rules
                    if (CheckCompoundRules(word, i, word.Length, a, b))
                    {
                        // matched and valid
                        aRequiresForce = aForce;
                        bRequiresForce = bForce;
                        return true;
                    }
                }
        }
        aRequiresForce = false;
        bRequiresForce = false;
        return false;
    }

    /// <summary>
    /// Try to split a word into two dictionary words honoring _compoundMin.
    /// Returns (first, second) if a split is found; otherwise null.
    /// </summary>
    private (string first, string second)? FindTwoWordSplit(string word)
    {
        if (string.IsNullOrEmpty(word)) return null;
        if (word.Length < 2 * _compoundMin) return null;

        for (int i = _compoundMin; i <= word.Length - _compoundMin; i++)
        {
            var a = word.Substring(0, i);
            var b = word.Substring(i);

            if (_hashManager.Lookup(a) && _hashManager.Lookup(b))
            {
                return (a, b);
            }
        }
        return null;
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
        foreach (var breakPoint in _breakPoints)
        {
            // Find all occurrences of the break point
            int index = 0;
            while ((index = word.IndexOf(breakPoint, index)) >= 0)
            {
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
                index++; // Move to next position
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

    private record AffixRule(string Flag, string Stripping, string Affix, string Condition, bool IsPrefix, string? AppendedFlag);

    /// <summary>
    /// Represents a CHECKCOMPOUNDPATTERN rule.
    /// </summary>
    private record CompoundPattern(string EndChars, string? EndFlag, string BeginChars, string? BeginFlag, string? Replacement);
}
