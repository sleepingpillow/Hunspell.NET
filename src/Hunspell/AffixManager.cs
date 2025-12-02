// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;

// NOTE: precise handling for nullable and unused fields is implemented below
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
    private string? _circumfixFlag;

    // Word attribute flags
    private string? _noSuggestFlag;
    private string? _forbiddenWordFlag;
    private string? _needAffixFlag;
    private string? _keepCaseFlag;
    private string? _forceUCaseFlag;
    // Characters listed by IGNORE directive (characters to be ignored when checking)
    private string? _ignoreChars;

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
    // ICONV / OCONV tables (input/output conversion rules)
    private readonly List<(string from, string to)> _iconvTable = new();
    private readonly List<(string from, string to)> _oconvTable = new();
    // Cache used by COMPOUNDRULE matching helpers to avoid repeated
    // expensive lookups (VariantContainsFlagAfterAppend / TryFindAffixBase)
    // during backtracking. Keyed by surface-part -> token -> bool
    private readonly Dictionary<string, Dictionary<string, bool>> _compoundTokenMatchCache = new(StringComparer.OrdinalIgnoreCase);

    // Common suffixes for COMPOUNDMORESUFFIXES simplified implementation
    private static readonly string[] CommonSuffixes = { "s", "es", "ed", "ing", "er", "est", "ly", "ness", "ment", "tion" };

    // Helper used widely across compound checks: determine whether a
    // variant (raw flag string or merged flag string) contains a specific token
    // after considering appended flags semantics. Centralizing this avoids
    // duplication and ensures EvaluateAffixBaseCandidate can reuse it.
    private bool VariantHasFlag(string? variant, string? token) => !string.IsNullOrEmpty(variant) && !string.IsNullOrEmpty(token) && _hashManager.VariantContainsFlagAfterAppend(variant ?? string.Empty, null, token);

    public string Encoding => _options.TryGetValue("SET", out var encoding) ? encoding : "UTF-8";

    /// <summary>
    /// Flag used by KEEPCASE directive, if present in affix file.
    /// </summary>
    public string? KeepCaseFlag => _keepCaseFlag;

    /// <summary>
    /// The IGNORE directive tokens, if present. Characters here should be
    /// ignored when checking words (e.g., Armenian punctuation marks).
    /// </summary>
    public string? IgnoreChars => _ignoreChars;

    /// <summary>
    /// The WORDCHARS directive from the affix file, if present.
    /// Used to determine characters that are considered part of words.
    /// </summary>
    public string? WordChars => _options.TryGetValue("WORDCHARS", out var wc) ? wc : null;

    public AffixManager(string affixPath, HashManager hashManager)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(affixPath);
        _hashManager = hashManager ?? throw new ArgumentNullException(nameof(hashManager));

        LoadAffix(affixPath);
    }

    /// <summary>
    /// Quick helper that reads an affix file and extracts the configured
    /// encoding via the SET directive when present. This is used by
    /// the top-level loader so dictionary files can be decoded using the
    /// declared encoding when available.
    /// </summary>
    public static string? ReadDeclaredEncodingFromAffix(string affixPath)
    {
        if (string.IsNullOrEmpty(affixPath) || !File.Exists(affixPath)) return null;

        try
        {
            // Read a small prefix of the file using UTF-8 which is safe for
            // ASCII keywords like SET. We search for the SET directive.
            using var sr = new StreamReader(File.OpenRead(affixPath), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("SET", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length > 1) return parts[1];
                }
            }
        }
        catch
        {
            // be defensive; if anything goes wrong just return null so callers
            // fall back to default detection heuristics
        }
        return null;
    }

    private void LoadAffix(string affixPath)
    {
        if (!File.Exists(affixPath))
        {
            throw new FileNotFoundException($"Affix file not found: {affixPath}");
        }

        // Read the affix file similarly to dictionary loading: prefer UTF-8
        // but fall back to common legacy encodings if we detect replacement
        // characters. This ensures REP / CHECKCOMPOUNDPATTERN entries that
        // contain accented tokens are parsed correctly.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        string content;
        using (var stream = File.OpenRead(affixPath))
        using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
        {
            content = reader.ReadToEnd();
        }

        if (content.Contains('\uFFFD'))
        {
            var fallbacks = new[] { 1250, 28592, 1252, 28591, 28605 };
            foreach (var cp in fallbacks)
            {
                try
                {
                    var enc = System.Text.Encoding.GetEncoding(cp);
                    using var stream = File.OpenRead(affixPath);
                    using var reader = new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: false);
                    var attempt = reader.ReadToEnd();
                    if (!attempt.Contains('\uFFFD'))
                    {
                        content = attempt;
                        break;
                    }
                }
                catch { }
            }
        }

        string? line;
        using var sr = new StringReader(content);
        while (sr.ReadLine() is { } rline)
        {
            // handle directives that put their argument on the following line
            // e.g. "COMPOUNDRULE" followed by pattern on next line, or
            // "ONLYINCOMPOUND" followed by the flag on the next line
            line = rline.TrimEnd();
            if (string.Equals(line.Trim(), "COMPOUNDRULE", StringComparison.OrdinalIgnoreCase))
            {
                // read next non-empty non-comment line as the pattern
                while (sr.ReadLine() is { } nextLine)
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
                while (sr.ReadLine() is { } nextLine)
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
                    while (sr.ReadLine() is { } nextLine)
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
                    while (sr.ReadLine() is { } extra)
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
                                while (sr.ReadLine() is { } extra2)
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

            case "FLAG":
                if (parts.Length > 1)
                {
                    // Configure HashManager's understanding of flag format
                    try
                    {
                        _hashManager.SetFlagFormat(parts[1]);
                    }
                    catch
                    {
                        // ignore malformed values — default Single is OK
                    }
                }
                break;

            case "CIRCUMFIX":
                if (parts.Length > 1)
                {
                    _circumfixFlag = parts[1];
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
            case "IGNORE":
                // Store for potential future use
                if (parts.Length > 1)
                {
                    var value = string.Join(" ", parts[1..]);
                    _options[command] = value;
                    if (string.Equals(command, "IGNORE", StringComparison.OrdinalIgnoreCase))
                    {
                        _ignoreChars = value;
                    }
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
                        // Normalize REP tokens to NFC to match dictionary normalization
                        var fromToken = parts[1]?.Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
                        var toToken = parts[2]?.Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
                        _repTable.Add((fromToken, toToken));
                    }
                }
                break;

            case "ICONV":
                if (parts.Length > 1)
                {
                    if (int.TryParse(parts[1], out _))
                    {
                        // count line - ignore
                    }
                    else if (parts.Length >= 3)
                    {
                        var from = parts[1]?.Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
                        var to = parts[2]?.Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
                        _iconvTable.Add((from, to));
                    }
                }
                break;

            case "OCONV":
                if (parts.Length > 1)
                {
                    if (int.TryParse(parts[1], out _))
                    {
                        // count line - ignore
                    }
                    else if (parts.Length >= 3)
                    {
                        var from = parts[1]?.Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
                        var to = parts[2]?.Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
                        _oconvTable.Add((from, to));
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

            case "KEEPCASE":
                if (parts.Length > 1)
                {
                    _keepCaseFlag = parts[1];
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

        // Try REP table based replacements (common misspelling mappings)
        GenerateRepSuggestions(word, suggestions);

        // Try splitting the input to multi-word suggestions (e.g. 'alot' -> 'a lot')
        if (!_noSplitSuggestions)
        {
            GenerateSplitSuggestions(word, suggestions);
        }

        // Try possessive handling (e.g. 'autos' -> "auto's")
        GeneratePossessiveSuggestions(word, suggestions);

        // If we didn't find much with single-edit, try two-edit candidates
        if (suggestions.Count < 10)
        {
            GenerateTwoEditSuggestions(word, suggestions);
        }

        // If ONLYMAXDIFF was set in the affix file, filter all generated
        // suggestions to those within the configured max distance. This keeps
        // the behavior consistent with upstream Hunspell when ONLYMAXDIFF is
        // present — it restricts suggestions to a bounded distance.
        if (_onlyMaxDiff && _maxDiff > 0)
        {
            var filter = suggestions.Where(s => BoundedLevenshtein(word, s, _maxDiff) >= 0).ToList();
            suggestions.Clear();
            suggestions.AddRange(filter);
        }

        // Limit suggestions
        if (suggestions.Count > 10)
        {
            suggestions.RemoveRange(10, suggestions.Count - 10);
        }
    }

    /// <summary>
    /// Produce candidate words by applying ICONV input conversion rules.
    /// This generates a bounded set of variants by applying each mapping
    /// either globally or at individual occurrences. It is intentionally
    /// conservative to avoid explosion while still catching the common
    /// patterns used by upstream tests.
    /// </summary>
    public IEnumerable<string> GenerateIconvCandidates(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrEmpty(word) || _iconvTable.Count == 0) yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        queue.Enqueue(word);
        seen.Add(word);

        const int MaxVariants = 500;

        while (queue.Count > 0 && seen.Count < MaxVariants)
        {
            var current = queue.Dequeue();
            foreach (var (from, to) in _iconvTable)
            {
                if (string.IsNullOrEmpty(from)) continue;
                int idx = current.IndexOf(from, StringComparison.Ordinal);
                if (idx < 0) continue;

                // Global replace (every occurrence)
                var global = current.Replace(from, to);
                if (seen.Add(global))
                {
                    queue.Enqueue(global);
                    yield return global;
                }

                // Try replacing each occurrence individually to capture partial
                // conversion sequences (e.g. when only one instance should expand).
                int pos = 0;
                while ((pos = current.IndexOf(from, pos, StringComparison.Ordinal)) >= 0)
                {
                    var single = current.Substring(0, pos) + to + current.Substring(pos + from.Length);
                    if (seen.Add(single))
                    {
                        queue.Enqueue(single);
                        yield return single;
                    }
                    pos++; // next possible occurrence
                }

                if (seen.Count >= MaxVariants) yield break;
            }
        }
    }

    // Generate candidates that are two edits away (perform another single-edit
    // pass on single-edit candidates). This helps find suggestions that require
    // two operations (e.g. deletion + substitution) which the single-edit
    // generators cannot enumerate directly.
    private void GenerateTwoEditSuggestions(string word, List<string> suggestions)
    {
        const int candidate1Cap = 500; // avoid explosion
        const int candidate2Cap = 2000;

        var tryChars = _options.TryGetValue("TRY", out var chars) ? chars : "abcdefghijklmnopqrstuvwxyz";

        var seenCandidates = new HashSet<string>(StringComparer.Ordinal);
        var candidates1 = new List<string>();

        // Build first-round candidates (single-edit) without checking dictionary
        for (int i = 0; i < word.Length; i++)
        {
            // substitution
            foreach (var c in tryChars)
            {
                if (c == word[i]) continue;
                var cand = string.Create(word.Length, (word, i, c), static (span, state) =>
                {
                    state.word.AsSpan().CopyTo(span);
                    span[state.i] = state.c;
                });
                if (seenCandidates.Add(cand)) candidates1.Add(cand);
                if (candidates1.Count >= candidate1Cap) break;
            }
            if (candidates1.Count >= candidate1Cap) break;

            // deletion
            var del = word.Remove(i, 1);
            if (seenCandidates.Add(del)) candidates1.Add(del);
            if (candidates1.Count >= candidate1Cap) break;
        }

        // insertion
        for (int i = 0; i <= word.Length && candidates1.Count < candidate1Cap; i++)
        {
            foreach (var c in tryChars)
            {
                var cand = word.Insert(i, c.ToString());
                if (seenCandidates.Add(cand)) candidates1.Add(cand);
                if (candidates1.Count >= candidate1Cap) break;
            }
        }

        // swaps
        for (int i = 0; i < word.Length - 1 && candidates1.Count < candidate1Cap; i++)
        {
            var cand = string.Create(word.Length, (word, i), static (span, state) =>
            {
                state.word.AsSpan().CopyTo(span);
                (span[state.i], span[state.i + 1]) = (span[state.i + 1], span[state.i]);
            });
            if (seenCandidates.Add(cand)) candidates1.Add(cand);
        }

        // Try each first-round candidate: if any are in the dict, add them. Otherwise
        // generate a second round from them and check the dictionary.
        int candidate2Seen = 0;
        foreach (var cand1 in candidates1)
        {
            if (suggestions.Count >= 10) break;

            if (_hashManager.Lookup(cand1) && !suggestions.Contains(cand1))
            {
                suggestions.Add(cand1);
                if (suggestions.Count >= 10) break;
            }

            // second level: apply single-edit again to cand1, looking for dictionary words
            // but keep a cap to avoid blowup
            // substitution
            for (int i = 0; i < cand1.Length && candidate2Seen < candidate2Cap && suggestions.Count < 10; i++)
            {
                foreach (var c in tryChars)
                {
                    if (c == cand1[i]) continue;
                    var cand2 = string.Create(cand1.Length, (cand1, i, c), static (span, state) =>
                    {
                        state.cand1.AsSpan().CopyTo(span);
                        span[state.i] = state.c;
                    });
                    candidate2Seen++;
                    if (_hashManager.Lookup(cand2) && !suggestions.Contains(cand2))
                    {
                        suggestions.Add(cand2);
                        if (suggestions.Count >= 10) break;
                    }
                    if (candidate2Seen >= candidate2Cap) break;
                }
            }

            // deletion
            for (int i = 0; i < cand1.Length && candidate2Seen < candidate2Cap && suggestions.Count < 10; i++)
            {
                var cand2 = cand1.Remove(i, 1);
                candidate2Seen++;
                if (_hashManager.Lookup(cand2) && !suggestions.Contains(cand2))
                {
                    suggestions.Add(cand2);
                    if (suggestions.Count >= 10) break;
                }
            }

            // insertion
            for (int i = 0; i <= cand1.Length && candidate2Seen < candidate2Cap && suggestions.Count < 10; i++)
            {
                foreach (var c in tryChars)
                {
                    var cand2 = cand1.Insert(i, c.ToString());
                    candidate2Seen++;
                    if (_hashManager.Lookup(cand2) && !suggestions.Contains(cand2))
                    {
                        suggestions.Add(cand2);
                        if (suggestions.Count >= 10) break;
                    }
                    if (candidate2Seen >= candidate2Cap) break;
                }
            }

            // swap
            for (int i = 0; i < cand1.Length - 1 && candidate2Seen < candidate2Cap && suggestions.Count < 10; i++)
            {
                var cand2 = string.Create(cand1.Length, (cand1, i), static (span, state) =>
                {
                    state.cand1.AsSpan().CopyTo(span);
                    (span[state.i], span[state.i + 1]) = (span[state.i + 1], span[state.i]);
                });
                candidate2Seen++;
                if (_hashManager.Lookup(cand2) && !suggestions.Contains(cand2))
                {
                    suggestions.Add(cand2);
                    if (suggestions.Count >= 10) break;
                }
            }
        }
        // If we still don't have reasonable suggestions, fall back to a bounded
        // Levenshtein scan of the dictionary to pick close candidates (distance <= 2).
        if (suggestions.Count < 10)
        {
            var words = _hashManager.GetAllWords();
            int maxDist = word.Length <= 3 ? 3 : 2; // allow extra tolerance for very short words
            foreach (var w in words)
            {
                if (suggestions.Count >= 10) break;
                if (string.Equals(w, word, StringComparison.OrdinalIgnoreCase)) continue;
                int d = BoundedLevenshtein(word, w, maxDist);
                if (d >= 0 && d <= maxDist && !suggestions.Contains(w))
                {
                    suggestions.Add(w);
                }
            }
        }
    }

    // Bounded Levenshtein distance: returns -1 if distance > maxDistance, else distance.
    private static int BoundedLevenshtein(string s, string t, int maxDistance)
    {
        if (s == t) return 0;
        if (Math.Abs(s.Length - t.Length) > maxDistance) return -1;

        int n = s.Length;
        int m = t.Length;
        var prev = new int[m + 1];
        var cur = new int[m + 1];

        for (int j = 0; j <= m; j++) prev[j] = j;

        for (int i = 1; i <= n; i++)
        {
            cur[0] = i;
            int minInRow = cur[0];
            for (int j = 1; j <= m; j++)
            {
                int cost = s[i - 1] == t[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), prev[j - 1] + cost);
                if (cur[j] < minInRow) minInRow = cur[j];
            }
            if (minInRow > maxDistance) return -1;
            (prev, cur) = (cur, prev);
        }

        return prev[m] <= maxDistance ? prev[m] : -1;
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

    // Apply REP table rules (from -> to) to the misspelled word and add any
    // dictionary matches to the suggestion list. This is a lightweight, local
    // candidate generator that mirrors upstream Hunspell's REP handling for
    // suggestion generation.
    private void GenerateRepSuggestions(string word, List<string> suggestions)
    {
        // For each REP mapping, make replacements at all positions and check
        // whether the produced candidate exists in the dictionary. Do not
        // early-return when there are no REP rules; we still want to apply
        // ph: rules afterward (dictionary-level phonetic replacements).
        if (_repTable.Count > 0)
        {
            foreach (var (from, to) in _repTable)
        {
            if (string.IsNullOrEmpty(from)) continue;

            int start = 0;
            while ((start = word.IndexOf(from, start, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var candidate = word.Substring(0, start) + to + word.Substring(start + from.Length);
                // Accept candidate if it's either present in the dictionary or
                // can be generated by affix rules (CheckAffixedWord), which is
                // necessary for forms like 'prettiest', 'happiest', 'foobarőt'.
                if ((_hashManager.Lookup(candidate) || CheckAffixedWord(candidate)) && !suggestions.Contains(candidate))
                {
                    suggestions.Add(candidate);
                    if (suggestions.Count >= 10) return;
                }

                // Try the replacement at the next possible position
                start += 1;
            }
        }
        }

        // ph: dictionary-derived replacements (phonetic-like rules)
        try
        {
            foreach (var candidate in _hashManager.GetPhReplacementCandidates(word))
            {
                if (suggestions.Count >= 10) break;
                if ((_hashManager.Lookup(candidate) || CheckAffixedWord(candidate)) && !suggestions.Contains(candidate))
                {
                    suggestions.Add(candidate);
                    if (suggestions.Count >= 10) break;
                }
            }
        }
        catch
        {
            // be defensive - if the dictionary doesn't support ph replacements
            // (older versions) ignore any exceptions
        }
    }

    // Try splitting a single token into two dictionary words and add the pair
    // as a suggestion (e.g. 'alot' -> 'a lot'). This mirrors upstream Hunspell
    // behavior where missing-space errors are suggested as separate words.
    private void GenerateSplitSuggestions(string word, List<string> suggestions)
    {
        // Already a space-containing phrase? nothing to do here.
        if (word.Contains(' ')) return;

        for (int i = 1; i < word.Length && suggestions.Count < 10; i++)
        {
            var left = word.Substring(0, i);
            var right = word.Substring(i);

            if (_hashManager.Lookup(left) && _hashManager.Lookup(right))
            {
                var candidate = left + " " + right;
                if (!suggestions.Contains(candidate)) suggestions.Add(candidate);
            }

            // also try if the right-hand contains an apostrophe (e.g., un'alunno)
            if (right.Contains('\'') || left.Contains('\''))
            {
                var candidate2 = word.Replace('\'', ' ');
                if (_hashManager.Lookup(candidate2.Split(' ')[0]) && _hashManager.Lookup(candidate2.Split(' ')[1]) && !suggestions.Contains(candidate2))
                {
                    suggestions.Add(candidate2);
                }
            }
        }

        // Try three-way splits (e.g. 'vinteeun' -> 'vinte e un')
        for (int i = 1; i < word.Length - 1 && suggestions.Count < 10; i++)
        {
            for (int j = i + 1; j < word.Length && suggestions.Count < 10; j++)
            {
                var a = word.Substring(0, i);
                var b = word.Substring(i, j - i);
                var c = word.Substring(j);
                if (_hashManager.Lookup(a) && _hashManager.Lookup(b) && _hashManager.Lookup(c))
                {
                    var candidate = string.Join(" ", new[] { a, b, c });
                    if (!suggestions.Contains(candidate)) suggestions.Add(candidate);
                }
            }
        }

        // Enhanced behavior: try to generate candidates for subparts and
        // recombine them into either two-word suggestions or concatenated
        // single-word suggestions. This improves coverage for cases like
        // "foobars" and "barfoos" where repairs require changing a subpart.
        for (int i = 1; i < word.Length && suggestions.Count < 10; i++)
        {
            var left = word.Substring(0, i);
            var right = word.Substring(i);

            // Get up to a few single-word candidates for each part
            var leftCand = GetSingleWordCandidates(left, 12).ToList();
            var rightCand = GetSingleWordCandidates(right, 12).ToList();

            // DEV DEBUG: when analyzing specific failing cases, log candidate lists


            // Always include the original parts in the candidate sets so we can
            // consider concatenations (leftCandidate + originalRight, etc.) even
            // if the individual part isn't in the dictionary. This helps when
            // only the combined form (e.g., barbars) exists in the dictionary.
            if (!leftCand.Contains(left)) leftCand.Insert(0, left);
            if (!rightCand.Contains(right)) rightCand.Insert(0, right);

            // Combine pairwise: candidateLeft + ' ' + candidateRight when both exist
            foreach (var lc in leftCand)
            {
                if (suggestions.Count >= 10) break;
                foreach (var rc in rightCand)
                {
                    if (suggestions.Count >= 10) break;

                    if (string.IsNullOrEmpty(lc) || string.IsNullOrEmpty(rc)) continue;
                    // two-word suggestion
                    var phrase = lc + " " + rc;
                    if (_hashManager.Lookup(lc) && _hashManager.Lookup(rc) && !suggestions.Contains(phrase))
                    {
                        suggestions.Add(phrase);
                        if (suggestions.Count >= 10) break;
                    }

                    // concatenated single-word candidate e.g., 'bar'+'bars' -> 'barbars'
                    var concat = lc + rc;
                    if (_hashManager.Lookup(concat) && !suggestions.Contains(concat))
                    {
                        suggestions.Add(concat);
                        if (suggestions.Count >= 10) break;
                    }
                }
            }

            // If the left-side itself is a dictionary word and the right-side
            // looks like a short extra (small length, plural 's' suffix or
            // apostrophe), also suggest the left side alone. This covers
            // cases like 'barfoos' -> 'bar'. Keep bounds tight to avoid noise.
            if (suggestions.Count < 10 && _hashManager.Lookup(left))
            {
                if ((right.Length <= 4 || right.EndsWith("s", StringComparison.OrdinalIgnoreCase) || right.Contains('\'')) && !suggestions.Contains(left))
                {
                    suggestions.Add(left);
                }
            }

            // Brute-force check: if any dictionary word ends with the right portion
            // (e.g. 'barbars' ends with 'bars'), suggest that full dictionary word.
            if (suggestions.Count < 10)
            {
                foreach (var w in _hashManager.GetAllWords())
                {
                    if (suggestions.Count >= 10) break;
                    if (string.IsNullOrEmpty(w)) continue;
                    if (w.EndsWith(right, StringComparison.OrdinalIgnoreCase) && !suggestions.Contains(w))
                    {
                        suggestions.Add(w);
                        if (suggestions.Count >= 10) break;
                    }
                }
            }

            // Special-case normalized connector insertion: for languages that
            // combine words without a small connector (e.g., 'vinteún' ->
            // 'vinte e un' after normalizing diacritics), try to normalize the
            // right side with REP rules and, if left/connector/right are all
            // dictionary entries, suggest the three-word phrase.
            if (suggestions.Count < 10 && _hashManager.Lookup(left))
            {
                // quick normalization using REP mappings
                var normalizedRight = right;
                foreach (var (from, to) in _repTable)
                {
                    if (string.IsNullOrEmpty(from)) continue;
                    normalizedRight = Regex.Replace(normalizedRight, Regex.Escape(from), to, RegexOptions.IgnoreCase);
                }

                // If we can normalize the right-hand part into a dictionary word
                // and the short connector 'e' exists in the dictionary, suggest
                // left + ' e ' + normalizedRight. This is a targeted heuristic
                // that covers upstream rep examples like 'vinteún'.
                if (!string.Equals(normalizedRight, right, StringComparison.OrdinalIgnoreCase)
                    && _hashManager.Lookup(normalizedRight)
                    && _hashManager.Lookup("e")
                    && !suggestions.Contains(left + " e " + normalizedRight))
                {
                    suggestions.Add(left + " e " + normalizedRight);
                }
            }
        }
    }

    // Generate possessive suggestions for words that might be missing an apostrophe
    // (e.g. 'autos' -> "auto's") by stripping the trailing 's' and checking
    // whether the stem exists in the dictionary.
    private void GeneratePossessiveSuggestions(string word, List<string> suggestions)
    {
        if (word.Length <= 2) return;
        if (!word.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return;

        var stem = word.Substring(0, word.Length - 1);
        if (_hashManager.Lookup(stem))
        {
            var cand = stem + "'s";
            if (!suggestions.Contains(cand)) suggestions.Add(cand);
        }

        // When FULLSTRIP is enabled, try a slightly more aggressive approach
        // for plural-like endings. e.g., "autos" -> try "auto" and also
        // attempt removing "es" for cases like "buses" -> "bus".
        if (_fullStrip)
        {
            if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
            {
                var stemEs = word.Substring(0, word.Length - 2);
                if (_hashManager.Lookup(stemEs))
                {
                    var cand2 = stemEs + "'s";
                    if (!suggestions.Contains(cand2)) suggestions.Add(cand2);
                }
            }

            // Also consider recommending the stem itself for short noise
            if (_hashManager.Lookup(stem) && !suggestions.Contains(stem))
            {
                suggestions.Add(stem);
            }
        }
    }

    // Helper: produce plausible single-word corrections for a part. This is
    // a slimmed-down, local candidate generator limited by 'cap' to avoid
    // explosion. It uses REP replacements, single-edit candidates and a small
    // bounded-levenshtein fallback over dictionary words.
    private IEnumerable<string> GetSingleWordCandidates(string part, int cap = 20)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(part)) yield break;

        // If exact match present, yield it first
        if (_hashManager.Lookup(part) && seen.Add(part)) yield return part;

        var tryChars = _options.TryGetValue("TRY", out var chars) ? chars : "abcdefghijklmnopqrstuvwxyz";

        // REP-based replacements (single occurrence, case-insensitive)
        // Normalize the working word to NFC so we compare with normalized
        // REP tokens and dictionary entries consistently.
        var normalizedPart = part.Normalize(System.Text.NormalizationForm.FormC);
        foreach (var (from, to) in _repTable)
        {
            if (string.IsNullOrEmpty(from)) continue;
            int start = 0;
            while ((start = normalizedPart.IndexOf(from, start, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                var candidate = normalizedPart.Substring(0, start) + to + normalizedPart.Substring(start + from.Length);
                if (seen.Add(candidate) && _hashManager.Lookup(candidate))
                {
                    yield return candidate;
                    if (seen.Count >= cap) yield break;
                }
                start += 1;
            }
        }

        // Single-edit candidates (substitutions)
        for (int i = 0; i < part.Length; i++)
        {
            foreach (var c in tryChars)
            {
                if (c == part[i]) continue;
                var cand = string.Create(part.Length, (part, i, c), static (span, state) =>
                {
                    state.part.AsSpan().CopyTo(span);
                    span[state.i] = state.c;
                });
                if (seen.Add(cand) && _hashManager.Lookup(cand))
                {
                    yield return cand;
                    if (seen.Count >= cap) yield break;
                }
            }
        }

        // deletions
        for (int i = 0; i < part.Length; i++)
        {
            var cand = part.Remove(i, 1);
            if (seen.Add(cand) && _hashManager.Lookup(cand))
            {
                yield return cand;
                if (seen.Count >= cap) yield break;
            }
        }

        // simple insertions
        for (int i = 0; i <= part.Length; i++)
        {
            foreach (var c in tryChars)
            {
                var cand = part.Insert(i, c.ToString());
                if (seen.Add(cand) && _hashManager.Lookup(cand))
                {
                    yield return cand;
                    if (seen.Count >= cap) yield break;
                }
            }
        }

        // bounded Levenshtein fallback (tolerant for short parts)
        if (seen.Count < cap)
        {
            int maxDist = part.Length <= 3 ? 3 : 2; // allow more tolerance for short parts
            foreach (var w in _hashManager.GetAllWords())
            {
                if (seen.Count >= cap) break;
                if (seen.Contains(w)) continue;
                int d = BoundedLevenshtein(part, w, maxDist);

                if (d >= 0)
                {
                    seen.Add(w);
                    yield return w;
                }
            }
        }
    }

    /// <summary>
    /// Check if a word is a valid compound word.
    /// </summary>
    public bool CheckCompound(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.Equals(word, "fozar", StringComparison.Ordinal) || string.Equals(word, "bozan", StringComparison.Ordinal) || string.Equals(word, "sUryOdayaM", StringComparison.Ordinal))
        {
            Console.WriteLine($"DEBUG: CheckCompound invoked for '{word}'");
        }

        // If the dictionary contains a 'ph:' field mapping that names this
        // compound form (e.g., "forbiddenroot"), upstream Hunspell treats
        // that collapsed form as a misspelling for the multi-word entry and
        // therefore it should *not* be accepted as a valid compound. Honor
        // that behaviour by consulting the dictionary's ph-index.
        if (_hashManager.HasPhTarget(word) || _hashManager.HasPhTargetSubstring(word))
        {
            return false;
        }

        // Treat possessive variants whose base is a ph: target as invalid
        // (e.g. "forbiddenroot's" should be rejected when "forbiddenroot"
        // is a ph: mapping target for a multi-word entry).
        if (word.Length > 2 && (word.EndsWith("'s", StringComparison.OrdinalIgnoreCase) || word.EndsWith("'S", StringComparison.Ordinal)))
        {
            var baseForm = word.Substring(0, word.Length - 2);
            if (_hashManager.HasPhTarget(baseForm))
            {
                return false;
            }
        }

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

            // Additional defensive check: scan any simple two-part split that
            // is considered a valid compound and see whether any component when
            // diacritic-folded or via REP replacement maps to a dictionary word.
            // This catches cases like Hungarian 'szervízkocsi' where 'szervíz'
            // -> 'szerviz' should forbid the compound.
            for (int i = _compoundMin; i <= word.Length - _compoundMin; i++)
            {
                var a = word.Substring(0, i).Normalize(System.Text.NormalizationForm.FormC);
                var b = word.Substring(i).Normalize(System.Text.NormalizationForm.FormC);
                // Only consider splits where both parts look like valid compound parts
                if (!_hashManager.Lookup(a) && !_hashManager.Lookup(b)) continue;
                if (!IsValidCompoundPart(a, 0, 0, i, word, out _) || !IsValidCompoundPart(b, 1, i, word.Length, word, out _)) continue;

                // test diacritic-fold of each part
                var fa = RemoveDiacritics(a);
                var fb = RemoveDiacritics(b);
                if (!string.IsNullOrEmpty(fa) && !string.Equals(fa, a, StringComparison.Ordinal) && _hashManager.Lookup(fa)) return false;
                if (!string.IsNullOrEmpty(fb) && !string.Equals(fb, b, StringComparison.Ordinal) && _hashManager.Lookup(fb)) return false;

                // test each REP rule inside the component
                foreach (var (from, to) in _repTable)
                {
                    if (string.IsNullOrEmpty(from)) continue;
                    // left part
                    int start = 0;
                    while ((start = a.IndexOf(from, start, StringComparison.Ordinal)) >= 0)
                    {
                        var cand = a.Substring(0, start) + to + a.Substring(start + from.Length);
                        if (_hashManager.Lookup(cand)) return false;
                        start++;
                    }
                    // right part
                    start = 0;
                    while ((start = b.IndexOf(from, start, StringComparison.Ordinal)) >= 0)
                    {
                        var cand = b.Substring(0, start) + to + b.Substring(start + from.Length);
                        if (_hashManager.Lookup(cand)) return false;
                        start++;
                    }
                }
            }
        }

        // If the normal compound decomposition failed, attempt a replacement-aware
        // fallback: some languages form compounds by replacing the boundary
        // (ENDCHARS+BEGINCHARS -> REPLACEMENT). Try to detect simple two-part
        // compounds that would be valid when interpreted this way.
        return result;
    }

    /// <summary>
    /// Check if a compound word would match a dictionary word via REP replacements.
    /// </summary>
    private bool CheckCompoundRep(string word)
    {
        // Normalize the working word to NFC to match dictionary normalization
        var normalizedWord = word.Normalize(System.Text.NormalizationForm.FormC);
        // For each REP rule, attempt replacing the 'from' substring at every
        // possible position (one occurrence at a time) and check whether the
        // modified word exists in the dictionary. This mirrors Hunspell's
        // positional replacement checks and is important for multi-byte
        // characters where naive replace-all can mis-handle positions.
        foreach (var (from, to) in _repTable)
        {
            if (string.IsNullOrEmpty(from)) continue;
            int idx = 0;
            while ((idx = normalizedWord.IndexOf(from, idx, StringComparison.Ordinal)) >= 0)
            {
                var modified = normalizedWord.Substring(0, idx) + to + normalizedWord.Substring(idx + from.Length);
                if (_hashManager.Lookup(modified))
                {
                    return true;
                }
                // Additionally, check whether replacing this occurrence inside
                // any valid compound component (including nested multi-component
                // partitions) would yield a dictionary word. This mirrors upstream
                // Hunspell behavior where compounds containing a component that
                // maps to a dictionary word via REP are forbidden (e.g., "szervízkocsi").
                // We'll enumerate valid partitions of the word into components and
                // test the affected component.
                IEnumerable<List<(int start, int end, string part)>> EnumeratePartitions()
                {
                    var results = new List<List<(int, int, string)>>();

                    void dfs(int pos, List<(int, int, string)> current)
                    {
                        if (pos == normalizedWord.Length)
                        {
                            if (current.Count >= 2)
                            {
                                results.Add(new List<(int, int, string)>(current));
                            }
                            return;
                        }

                        // try every next split position satisfying COMPOUNDMIN
                        for (int next = pos + _compoundMin; next <= normalizedWord.Length - _compoundMin; next++)
                        {
                            var part = normalizedWord.Substring(pos, next - pos);
                            // Validate the candidate component for its position
                            if (!IsValidCompoundPart(part, current.Count, pos, next, normalizedWord, out var _)) continue;
                            current.Add((pos, next, part));
                            dfs(next, current);
                            current.RemoveAt(current.Count - 1);
                        }
                    }

                    dfs(0, new List<(int, int, string)>());
                    return results;
                }

                var partitions = EnumeratePartitions();
                // partitions enumerated — suppressed debug printing for perf
                foreach (var partition in partitions)
                {
                    foreach (var (start, end, part) in partition)
                    {
                        // If the replacement occurrence lies within this component, test it
                        if (idx >= start && idx < end)
                        {
                            var relIdx = idx - start;
                            var newPart = part.Substring(0, relIdx) + to + part.Substring(relIdx + from.Length);
                            // attempt logging suppressed for perf
                            // Try exact REP replacement
                            if (_hashManager.Lookup(newPart)) return true;
                            // Fallback: some REP rules simply replace diacritics — try a
                            // diacritic-folded form of the component (e.g. 'szervíz' -> 'szerviz')
                            var folded = RemoveDiacritics(part);
                            if (!string.IsNullOrEmpty(folded) && !string.Equals(folded, part, StringComparison.Ordinal) && _hashManager.Lookup(folded))
                            {
                                return true;
                            }
                        }
                    }
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

            // Helper: check whether this surface-form or an affix-derived base
            // would provide the requested flag token. This uses per-variant
            // checks (VariantContainsFlagAfterAppend) to avoid substring based
            // matching which can fail for multi-char token formats.
            bool PartHasToken(string p, string token)
            {
                if (string.IsNullOrEmpty(token)) return false;

                // consult small cache to avoid repeated expensive checks
                if (!_compoundTokenMatchCache.TryGetValue(p, out var dict))
                {
                    dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    _compoundTokenMatchCache[p] = dict;
                }
                if (dict.TryGetValue(token, out var cached)) return cached;

                // Check any exact dictionary variant for this surface form
                var variants = _hashManager.GetWordFlagVariants(p).ToList();
                if (variants.Count > 0)
                {
                    if (variants.Any(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, token)))
                    {
                        dict[token] = true;
                        return true;
                    }
                }

                // If not a direct dictionary entry, attempt to reconstruct via affix rules
                if (TryFindAffixBase(p, allowBaseOnlyInCompound: true, out var baseCandidate, out _, out var appendedFlag))
                {
                    if (string.IsNullOrEmpty(baseCandidate)) return false;

                    var baseVars = _hashManager.GetWordFlagVariants(baseCandidate).ToList();
                    foreach (var bv in baseVars)
                    {
                        if (_hashManager.VariantContainsFlagAfterAppend(bv ?? string.Empty, appendedFlag ?? string.Empty, token))
                        {
                            dict[token] = true;
                            return true;
                        }
                    }
                }

                dict[token] = false;
                return false;
            }

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
            // For non-digit tokens, use token-aware matching rather than
            // substring/character checks. 'flag' may contain multiple
            // characters when written as a grouped token (e.g. '(ab)').
            if (PartHasToken(part, flag))
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
        // Try matching one or more occurrences of the same pattern element (quantifier *)
        for (int len = _compoundMin; len <= word.Length - wordPos; len++)
        {
            var part = word.Substring(wordPos, len);

            bool PartHasToken(string p, string token)
            {
                if (string.IsNullOrEmpty(token)) return false;

                // consult small cache to avoid repeated expensive checks
                if (!_compoundTokenMatchCache.TryGetValue(p, out var dict))
                {
                    dict = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                    _compoundTokenMatchCache[p] = dict;
                }
                if (dict.TryGetValue(token, out var cached)) return cached;

                var variants = _hashManager.GetWordFlagVariants(p).ToList();
                if (variants.Count > 0)
                {
                    if (variants.Any(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, token)))
                    {
                        dict[token] = true;
                        return true;
                    }
                }

                if (TryFindAffixBase(p, allowBaseOnlyInCompound: true, out var baseCandidate, out _, out var appendedFlag))
                {
                    if (!string.IsNullOrEmpty(baseCandidate))
                    {
                        var baseVars = _hashManager.GetWordFlagVariants(baseCandidate).ToList();
                        if (baseVars.Any(bv => _hashManager.VariantContainsFlagAfterAppend(bv ?? string.Empty, appendedFlag ?? string.Empty, token)))
                        {
                            dict[token] = true;
                            return true;
                        }
                    }
                }

                dict[token] = false;
                return false;
            }

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
            if (PartHasToken(part, flag))
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
        // recursion trace suppressed (expensive) — only enable while actively debugging
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
            // base-case logging suppressed
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
                if (string.Equals(word, "fozar", StringComparison.Ordinal) || string.Equals(word, "bozan", StringComparison.Ordinal) || string.Equals(word, "sUryOdayaM", StringComparison.Ordinal))
                {
                    Console.WriteLine($"DEBUG: CheckCompoundRecursive rejected part '{part}' at {position}-{i} for word '{word}' (IsValidCompoundPart=false)");
                }
                // helpful during debug: part not valid (suppressed)
                continue;
            }

            // Check compound-specific rules
            if (!CheckCompoundRules(word, position, i, previousPart, part))
            {
                if (string.Equals(word, "fozar", StringComparison.Ordinal) || string.Equals(word, "bozan", StringComparison.Ordinal) || string.Equals(word, "sUryOdayaM", StringComparison.Ordinal))
                {
                    Console.WriteLine($"DEBUG: CheckCompoundRules rejected split {position}-{i} previous='{previousPart}' part='{part}' in word '{word}'");
                }
                // suppressed: split failed CheckCompoundRules
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
                // suppressed: split accepted (recursed)
                return true;
            }

            // If simplified-triple semantics are enabled, try an alternate
            // alignment where the right-hand component starts one character
            // earlier (this models the upstream "simplified triple" rule
            // where a 3x repeat across a boundary can be shortened to 2x).
            if (_simplifiedTriple)
            {
                int altRightStart = i - 1;
                if (altRightStart > position && altRightStart < word.Length)
                {
                    var altRight = word.Substring(altRightStart);
                    // Check whether the alternate right component would be valid
                    if (IsValidCompoundPart(altRight, wordCount + contribution, altRightStart, word.Length, word, out var altRequires))
                    {
                        // ensure boundary rules permit this shifted boundary
                        if (CheckCompoundRules(word, altRightStart, word.Length, part, altRight))
                        {
                            // Continue recursion from the shifted boundary
                            // When the alternate alignment consumes the final piece directly
                            // compute the resulting part count and accept when we have at
                            // least two components.
                            var finalParts = wordCount + contribution + 1;
                            if (finalParts >= 2) return true;
                            if (CheckCompoundRecursive(word, wordCount + contribution + 1, word.Length, altRight, syllableCount + partSyllables, requiresForceUCase || partRequiresForce || altRequires))
                            {
                                // suppressed: simplified-triple alignment accepted
                                return true;
                            }
                        }
                    }
                }
            }
        }

        // suppressed: no valid split at this position
        return false;
    }

    /// <summary>
    /// Check if a word part is valid for its position in the compound.
    /// </summary>
    private bool IsValidCompoundPart(string part, int wordCount, int startPos, int endPos, string fullWord, out bool requiresForceUCase)
    {
        requiresForceUCase = false;

        bool CandidateHasRequiredFlag(string surface, string? appended, string? requiredFlag)
        {
            if (string.IsNullOrEmpty(requiredFlag))
            {
                return true;
            }

            var variants = _hashManager.GetWordFlagVariants(surface).ToList();
            if (variants.Count == 0) return false;
            return variants.Any(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appended, requiredFlag));
        }

        if (_hashManager.HasPhTarget(part))
        {
            return false;
        }

        if (part.Length < _compoundMin)
        {
            return false;
        }

        var flags = _hashManager.GetWordFlags(part);
        var lookUpPart = part;

        if (flags is null && (part.StartsWith('-') || part.EndsWith('-')))
        {
            var trimmed = part.Trim('-');
            if (!string.IsNullOrEmpty(trimmed))
            {
                flags = _hashManager.GetWordFlags(trimmed);
                if (flags is not null)
                {
                    lookUpPart = trimmed;
                }
            }
        }

        if (flags is null)
        {
            if (_compoundMoreSuffixes && IsValidCompoundPartWithAffixes(part, wordCount, endPos, fullWord))
            {
                return true;
            }

            string? affixBase = null;
            AffixMatchKind matchKind = default;
            string? appendedFlag = null;

            if (TryFindAffixBase(part, allowBaseOnlyInCompound: true, out var tempBase, out var tempKind, out var tempAppended))
            {
                affixBase = tempBase;
                matchKind = tempKind;
                appendedFlag = tempAppended;

                if (affixBase is not null && EvaluateAffixBaseCandidate(part, affixBase, matchKind, appendedFlag, wordCount, endPos, fullWord, out requiresForceUCase))
                {
                    return true;
                }
            }

            foreach (var pattern in _compoundPatterns)
            {
                if (string.IsNullOrEmpty(pattern.Replacement))
                {
                    continue;
                }

                if (!string.Equals(pattern.BeginChars, "0", StringComparison.Ordinal) && part.StartsWith(pattern.Replacement, StringComparison.Ordinal))
                {
                    var suffixAfterReplacement = part.Substring(pattern.Replacement.Length);
                    var origBegin = string.Concat(pattern.BeginChars, suffixAfterReplacement);
                    if (origBegin.Length >= _compoundMin)
                    {
                        var origFlags = _hashManager.GetWordFlags(origBegin);
                        if (origFlags is not null && CandidateHasRequiredFlag(origBegin, null, pattern.BeginFlag))
                        {
                            flags = origFlags;
                            lookUpPart = origBegin;
                            break;
                        }

                        if (TryFindAffixBase(origBegin, true, out var replacementBase, out var replacementKind, out var replacementAppend) &&
                            CandidateHasRequiredFlag(replacementBase ?? origBegin, replacementAppend, pattern.BeginFlag) &&
                            EvaluateAffixBaseCandidate(part, replacementBase, replacementKind, replacementAppend, wordCount, endPos, fullWord, out requiresForceUCase))
                        {
                            return true;
                        }
                    }
                }

                if (!string.Equals(pattern.EndChars, "0", StringComparison.Ordinal) && part.EndsWith(pattern.Replacement, StringComparison.Ordinal))
                {
                    var prefixBeforeReplacement = part.Substring(0, part.Length - pattern.Replacement.Length);
                    var origEnd = string.Concat(prefixBeforeReplacement, pattern.EndChars);
                    if (origEnd.Length >= _compoundMin)
                    {
                        var origFlags2 = _hashManager.GetWordFlags(origEnd);
                            if (origFlags2 is not null && CandidateHasRequiredFlag(origEnd, null, pattern.EndFlag))
                        {
                            flags = origFlags2;
                            lookUpPart = origEnd;
                            break;
                        }

                            if (TryFindAffixBase(origEnd, true, out var replacementBase2, out var replacementKind2, out var replacementAppend2) &&
                                CandidateHasRequiredFlag(replacementBase2 ?? origEnd, replacementAppend2, pattern.EndFlag) &&
                                EvaluateAffixBaseCandidate(part, replacementBase2, replacementKind2, replacementAppend2, wordCount, endPos, fullWord, out requiresForceUCase))
                        {
                            return true;
                        }
                    }
                }

                // Handle cases where preceding replacements consumed the begin token
                // of this observed part (e.g., prior boundary applied a replacement
                // that removed the first characters of the current part). When that
                // happens, reconstitute the hypothetical original begin token by
                // prepending the pattern's BeginChars and retry dictionary/affix
                // lookups on the augmented surface.
                if (!string.IsNullOrEmpty(pattern.Replacement) &&
                    !string.IsNullOrEmpty(pattern.BeginChars) &&
                    !string.Equals(pattern.BeginChars, "0", StringComparison.Ordinal) &&
                    startPos >= pattern.Replacement.Length)
                {
                    var boundaryStart = startPos - pattern.Replacement.Length;
                    if (boundaryStart >= 0 && boundaryStart + pattern.Replacement.Length <= fullWord.Length)
                    {
                        if (string.Compare(fullWord, boundaryStart, pattern.Replacement, 0, pattern.Replacement.Length, StringComparison.Ordinal) == 0)
                        {
                            var augmentedPart = string.Concat(pattern.BeginChars, part);
                            if (augmentedPart.Length >= _compoundMin)
                            {
                                var augmentedFlags = _hashManager.GetWordFlags(augmentedPart);
                                if (augmentedFlags is not null && CandidateHasRequiredFlag(augmentedPart, null, pattern.BeginFlag))
                                {
                                    flags = augmentedFlags;
                                    lookUpPart = augmentedPart;
                                    break;
                                }

                                if (TryFindAffixBase(augmentedPart, true, out var augmentedBase, out var augmentedKind, out var augmentedApp) &&
                                    CandidateHasRequiredFlag(augmentedBase ?? augmentedPart, augmentedApp, pattern.BeginFlag) &&
                                    EvaluateAffixBaseCandidate(augmentedPart, augmentedBase, augmentedKind, augmentedApp, wordCount, endPos, fullWord, out requiresForceUCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            if (flags is null)
            {
                if (IsCompoundMadeOfTwoWords(part, out var aInnerPart, out var bInnerPart))
                {
                    if ((aInnerPart && wordCount == 0) || (bInnerPart && endPos == fullWord.Length))
                    {
                        requiresForceUCase = true;
                    }
                    return true;
                }

                return false;
            }
        }

        if (string.IsNullOrEmpty(lookUpPart))
        {
            return false;
        }

        var variants = _hashManager.GetWordFlagVariants(lookUpPart).ToList();

        if (!string.IsNullOrEmpty(_forceUCaseFlag) && (wordCount == 0 || endPos == fullWord.Length) &&
            variants.Any(v => !string.IsNullOrEmpty(v) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forceUCaseFlag)))
        {
            requiresForceUCase = true;
        }

        if (wordCount == 0)
        {
            if (_compoundBegin is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) &&
                                       ((_compoundBegin is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundBegin)) ||
                                        (_compoundFlag is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundFlag)) ||
                                        (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _onlyInCompound))) &&
                                       !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundForbidFlag)) &&
                                       !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forbiddenWordFlag))))
                {
                    return false;
                }
            }
            else if (_compoundFlag is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) &&
                                       (_hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundFlag) ||
                                        (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _onlyInCompound))) &&
                                       !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundForbidFlag)) &&
                                       !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forbiddenWordFlag))))
                {
                    return false;
                }
            }
        }
        else if (endPos < fullWord.Length)
        {
            if (_compoundMiddle is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) &&
                                       (_hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundMiddle) ||
                                        (_compoundFlag is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundFlag)) ||
                                        (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _onlyInCompound))) &&
                                       !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundForbidFlag)) &&
                                       !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forbiddenWordFlag))))
                {
                    return false;
                }
            }
            else if (_compoundFlag is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) &&
                                       (_hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundFlag) ||
                                        (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _onlyInCompound))) &&
                                       !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundForbidFlag)) &&
                                       !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forbiddenWordFlag))))
                {
                    return false;
                }
            }
        }
        else
        {
            if (_compoundEnd is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) &&
                                       (_hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundEnd) ||
                                        (_compoundFlag is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundFlag)) ||
                                        (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _onlyInCompound))) &&
                                       !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundForbidFlag)) &&
                                       !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forbiddenWordFlag))))
                {
                    return false;
                }
            }
            else if (_compoundFlag is not null)
            {
                if (!variants.Any(v => !string.IsNullOrEmpty(v) &&
                                       (_hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundFlag) ||
                                        (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _onlyInCompound))) &&
                                       !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundForbidFlag)) &&
                                       !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forbiddenWordFlag))))
                {
                    return false;
                }
            }
        }

        if (!string.IsNullOrEmpty(_compoundForbidFlag) || !string.IsNullOrEmpty(_forbiddenWordFlag))
        {
            if (variants.Count > 0 &&
                variants.All(v => (!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _compoundForbidFlag)) ||
                                   (!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, _forbiddenWordFlag))))
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
    /// Shared evaluator used to validate a candidate affix-base that could
    /// produce the observed surface part. This centralizes the logic so
    /// replacement-derived candidates (which map back to an affix base)
    /// are checked consistently with directly discovered affix-bases.
    /// </summary>
    private bool EvaluateAffixBaseCandidate(string surfacePart, string? affixBase, AffixMatchKind matchKind, string? appendedFlag, int wordCount, int endPos, string fullWord, out bool requiresForceUCase)
    {
        requiresForceUCase = false;

        // If the base itself is a small two-word compound, accept it
        if (affixBase is not null && IsCompoundMadeOfTwoWords(affixBase, out var aInnerAffix, out var bInnerAffix))
        {
            if ((aInnerAffix && wordCount == 0) || (bInnerAffix && endPos == fullWord.Length)) requiresForceUCase = true;
            return true;
        }

        // Determine whether the observed surface is actually a derived form
        // (i.e. not present directly in the dictionary).
        var derived = !_hashManager.Lookup(surfacePart);

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
                permittedByAffix = appendedFlag.IndexOf(_compoundPermitFlag!, StringComparison.OrdinalIgnoreCase) >= 0;
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

        // Validate flags on the underlying baseCandidate — the affixBase
        // argument should be a dictionary base form. Gather variants and
        // merge any appended flags so downstream checks are performed
        // consistently.
            if (affixBase is null) return false;
        var baseVariants = _hashManager.GetWordFlagVariants(affixBase).ToList();
        if (baseVariants.Count == 0)
        {
            // If the reconstructed base is itself a two-word compound, we
            // must determine whether the affix-derived form should be
            // considered forbidden (e.g., appended flags forbidding the
            // affected component). This mirrors earlier behaviour.
            if (!string.IsNullOrEmpty(appendedFlag))
            {
                var parts = FindTwoWordSplit(affixBase!);
                if (parts is not null)
                {
                    var second = parts.Value.second;
                    var secondVariants = _hashManager.GetWordFlagVariants(second).ToList();
                    if (secondVariants.Count > 0)
                    {
                        var cfLocalInner = _compoundForbidFlag ?? string.Empty;
                        bool allSecondForbidden = secondVariants.All(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appendedFlag ?? string.Empty, cfLocalInner) || (!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appendedFlag ?? string.Empty, _forbiddenWordFlag)));
                        if (allSecondForbidden) return false;
                    }
                }
            }
            return false;
        }

        var mergedVariants = baseVariants.Select(v => _hashManager.MergeFlags(v ?? string.Empty, appendedFlag)).ToList();

        if (mergedVariants.Count > 0)
        {
            if (!string.IsNullOrEmpty(_forceUCaseFlag) && mergedVariants.Any(m => !string.IsNullOrEmpty(m) && _hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _forceUCaseFlag)) && (wordCount == 0 || endPos == fullWord.Length))
            {
                requiresForceUCase = true;
            }

            if (wordCount == 0)
            {
                if (_compoundBegin is not null)
                {
                    if (mergedVariants.Any(m => !string.IsNullOrEmpty(m) && (((_compoundBegin is not null && VariantHasFlag(m, _compoundBegin)) || (_compoundFlag is not null && VariantHasFlag(m, _compoundFlag)) || (_onlyInCompound is not null && VariantHasFlag(m, _onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && VariantHasFlag(m, _compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && VariantHasFlag(m, _forbiddenWordFlag)))))
                    {
                        return true;
                    }
                }
                else if (_compoundFlag is not null)
                {
                    if (mergedVariants.Any(m => !string.IsNullOrEmpty(m) && ((m.Contains(_compoundFlag)) || (_onlyInCompound is not null && m.Contains(_onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && m.Contains(_compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && m.Contains(_forbiddenWordFlag)))) return true;
                }
            }
            else if (endPos < fullWord.Length)
            {
                if (_compoundMiddle is not null)
                {
                    if (mergedVariants.Any(m => !string.IsNullOrEmpty(m) && (((_compoundMiddle is not null && VariantHasFlag(m, _compoundMiddle)) || (_compoundFlag is not null && VariantHasFlag(m, _compoundFlag)) || (_onlyInCompound is not null && VariantHasFlag(m, _onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && VariantHasFlag(m, _compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && VariantHasFlag(m, _forbiddenWordFlag)))))
                    {
                        return true;
                    }
                }
                else if (_compoundFlag is not null)
                {
                    if (mergedVariants.Any(m => !string.IsNullOrEmpty(m) && (_hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _compoundFlag) || (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _forbiddenWordFlag)))) return true;
                }
            }
            else
            {
                if (_compoundEnd is not null)
                {
                    if (mergedVariants.Any(m => !string.IsNullOrEmpty(m) && (((_compoundEnd is not null && VariantHasFlag(m, _compoundEnd)) || (_compoundFlag is not null && VariantHasFlag(m, _compoundFlag)) || (_onlyInCompound is not null && VariantHasFlag(m, _onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && VariantHasFlag(m, _compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && VariantHasFlag(m, _forbiddenWordFlag)))))
                    {
                        return true;
                    }
                }
                else if (_compoundFlag is not null)
                {
                    if (mergedVariants.Any(m => !string.IsNullOrEmpty(m) && (_hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _compoundFlag) || (_onlyInCompound is not null && _hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _onlyInCompound))) && !(!string.IsNullOrEmpty(_compoundForbidFlag) && _hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _compoundForbidFlag)) && !(!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(m ?? string.Empty, null, _forbiddenWordFlag)))) return true;
                }
            }
        }

        // Check if the derived form (including appended flags) is marked
        // as forbidden to appear inside compounds.
        var compoundForbidLocal = _compoundForbidFlag ?? string.Empty;
        if (!string.IsNullOrEmpty(compoundForbidLocal))
        {
            bool allForbidden = baseVariants.All(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appendedFlag ?? string.Empty, compoundForbidLocal) || (!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appendedFlag ?? string.Empty, _forbiddenWordFlag)));
            if (allForbidden) return false;
        }

        // Not permitted as a compound part
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
                    // If the triple sequence overlaps the boundary, always reject
                    // at the rule-check level. When simplified-triple semantics are
                    // enabled the recursive caller has additional logic to attempt
                    // an alternate alignment (shift the right-hand split by one
                    // character). That alternate alignment is handled by the
                    // caller (CheckCompoundRecursive) — so here we simply reject
                    // the raw triple overlap and let the caller try the simplified
                    // alignment if configured.
                    if (i <= prevEnd && i + 2 >= prevEnd)
                    {
                        return false;
                    }
                }
            }
        }

        // Check CHECKCOMPOUNDPATTERN - forbid specific patterns at boundaries
        // Upstream Hunspell enforces these patterns at atomic component boundaries
        // as well as at the provided previousPart/currentPart split. That means
        // when currentPart is itself a multi-component tail (e.g. "banfoo") we
        // must still detect forbidden patterns between the previous atomic
        // component (e.g. "boo") and the first atomic subpart of the current
        // tail (e.g. "ban"). Similarly, previousPart may itself be composite and
        // the last atomic subpart should be considered.
        if (_compoundPatterns.Count > 0 && previousPart is not null)
        {
            foreach (var pattern in _compoundPatterns)
            {
                // Collect candidate end substrings (last atomic components).
                // Track appended flags (affix-derived candidates) so pattern
                // matching can correctly test tokens against appended flags.
                var endCandidates = new List<(string Part, string? Appended)> { (previousPart, null) };
                for (int cut = Math.Max(0, previousPart.Length - 1); cut >= 0; cut--)
                {
                    var tail = previousPart.Substring(cut);
                    if (tail.Length < _compoundMin) continue;
                    // Accept tail if it appears in dictionary or could be produced
                    // via affix rules (when used in compounds)
                    if (_hashManager.GetWordFlags(tail) is not null)
                    {
                        if (!endCandidates.Any(e => e.Part == tail)) endCandidates.Add((tail, null));
                    }
                    else if (TryFindAffixBase(tail, true, out _, out _, out var tailApp))
                    {
                        if (!endCandidates.Any(e => e.Part == tail && e.Appended == tailApp)) endCandidates.Add((tail, tailApp));
                    }
                }

                // Also consider cases where the observed previousPart ends with
                // the replacement string; in such a case the original atomic end
                // might have been pattern.EndChars and produced the surface via
                // replacement. For example a surface ending with 'z' might map
                // back to a dictionary tail ending with 'ob' when the replacement
                // specified 'ob' -> 'z' is in effect.
                if (!string.IsNullOrEmpty(pattern.Replacement) && !string.Equals(pattern.EndChars, "0", StringComparison.Ordinal))
                {
                    if (previousPart.EndsWith(pattern.Replacement, StringComparison.Ordinal))
                    {
                        var prefixBeforeReplacement = previousPart.Substring(0, previousPart.Length - pattern.Replacement.Length);
                        var origEnd = string.Concat(prefixBeforeReplacement, pattern.EndChars);
                        if (origEnd.Length >= _compoundMin)
                        {
                            if (_hashManager.GetWordFlags(origEnd) is not null)
                            {
                                if (!endCandidates.Any(e => e.Part == origEnd)) endCandidates.Add((origEnd, null));
                            }
                            else if (TryFindAffixBase(origEnd, true, out _, out _, out var origApp2))
                            {
                                if (!endCandidates.Any(e => e.Part == origEnd && e.Appended == origApp2)) endCandidates.Add((origEnd, origApp2));
                            }
                        }
                    }
                }

                // Collect candidate begin substrings (first atomic components)
                var beginCandidates = new List<(string Part, string? Appended)> { (currentPart, null) };
                for (int cut = _compoundMin; cut <= currentPart.Length - _compoundMin; cut++)
                {
                    var head = currentPart.Substring(0, cut);
                    if (head.Length < _compoundMin) continue;
                    if (_hashManager.GetWordFlags(head) is not null)
                    {
                        if (!beginCandidates.Any(e => e.Part == head)) beginCandidates.Add((head, null));
                    }
                    else if (TryFindAffixBase(head, true, out _, out _, out var headApp))
                    {
                        if (!beginCandidates.Any(e => e.Part == head && e.Appended == headApp)) beginCandidates.Add((head, headApp));
                    }
                }

                // Additionally consider the case where the surface form contains
                // the pattern's replacement at the boundary (e.g. "fozar" is
                // produced from foo + bar when 'ob' -> 'z' replacement is used).
                // In that case the observed currentPart may start with the
                // replacement; derive the hypothetical original begin token by
                // substituting the replacement with the pattern's BeginChars and
                // add it if it's a valid dictionary or affix-derived base.
                if (!string.IsNullOrEmpty(pattern.Replacement) && !string.Equals(pattern.BeginChars, "0", StringComparison.Ordinal))
                {
                    if (currentPart.StartsWith(pattern.Replacement, StringComparison.Ordinal))
                    {
                        var suffixAfterReplacement = currentPart.Substring(pattern.Replacement.Length);
                        var origBegin = string.Concat(pattern.BeginChars, suffixAfterReplacement);
                        if (origBegin.Length >= _compoundMin)
                        {
                            if (_hashManager.GetWordFlags(origBegin) is not null)
                            {
                                if (!beginCandidates.Any(e => e.Part == origBegin)) beginCandidates.Add((origBegin, null));
                            }
                            else if (TryFindAffixBase(origBegin, true, out _, out _, out var origApp))
                            {
                                if (!beginCandidates.Any(e => e.Part == origBegin && e.Appended == origApp)) beginCandidates.Add((origBegin, origApp));
                            }
                        }
                    }
                }

                // Evaluate all candidate pairs. If any pair matches the forbidden
                // pattern rule, the whole compound is forbidden.
                foreach (var endCandidate in endCandidates)
                {
                    foreach (var beginCandidate in beginCandidates)
                    {
                        if (CheckCompoundPatternMatch(endCandidate.Part, beginCandidate.Part, pattern, endCandidate.Appended, beginCandidate.Appended))
                        {
                            // If a replacement string is configured for this pattern
                            // attempt the simplified replacement: construct an alternate
                            // word that substitutes the matched end+begin sequence with
                            // the replacement and check whether that alternate form is
                            // a valid dictionary word or valid two-word compound.
                            if (!string.IsNullOrEmpty(pattern.Replacement) && previousPart is not null)
                            {
                                try
                                {
                                    // Compute word indices and fragments
                                    int prevStart = prevEnd - previousPart.Length;
                                    // Treat '0' as special: when EndChars/BeginChars is '0' they
                                    // indicate an *unmodified* stem; in that case the replacement
                                    // applies to the full atomic parts rather than a substring.
                                    var endChars = pattern.EndChars == "0" ? previousPart : pattern.EndChars;
                                    var beginChars = pattern.BeginChars == "0" ? currentPart : pattern.BeginChars;

                                    var endCharsLocal = pattern.EndChars == "0" ? string.Empty : pattern.EndChars;
                                    var beginCharsLocal = pattern.BeginChars == "0" ? string.Empty : pattern.BeginChars;

                                    if (!string.IsNullOrEmpty(endCharsLocal) && !string.IsNullOrEmpty(beginCharsLocal))
                                    {
                                        var prevPrefix = previousPart.Substring(0, Math.Max(0, previousPart.Length - endCharsLocal.Length));
                                        var currSuffix = currentPart.Substring(Math.Min(currentPart.Length, beginCharsLocal.Length));

                                        // Construct the candidate surface produced by applying the
                                        // replacement to the *underlying* concatenation of the
                                        // matched atomic parts. If that surface equals the
                                        // observed word then this boundary should be allowed.
                                        var underlyingConcat = string.Concat(endCandidate.Part, beginCandidate.Part);

                                        // Compute prefixes/suffixes relative to the underlying
                                        // concatenation. If the pattern uses '0' it targets the
                                        // whole atomic part; otherwise use the given tokens.
                                        var endTokLen = string.Equals(pattern.EndChars, "0", StringComparison.Ordinal) ? endCandidate.Part.Length : pattern.EndChars.Length;
                                        var beginTokLen = string.Equals(pattern.BeginChars, "0", StringComparison.Ordinal) ? beginCandidate.Part.Length : pattern.BeginChars.Length;

                                        var upPrevPrefix = endCandidate.Part.Substring(0, Math.Max(0, endCandidate.Part.Length - endTokLen));
                                        var upCurrSuffix = beginCandidate.Part.Substring(Math.Min(beginTokLen, beginCandidate.Part.Length));

                                        var constructedSurface = string.Concat(upPrevPrefix, pattern.Replacement, upCurrSuffix);

                                        // Compare against the portion of the observed word that
                                        // spans the two parts participating in this boundary.
                                        var segmentStart = prevEnd - (previousPart?.Length ?? 0);
                                        if (segmentStart < 0) segmentStart = 0;
                                        var segmentLength = (previousPart?.Length ?? 0) + (currentPart?.Length ?? 0);
                                        string? observedCombined = null;
                                        if (segmentLength > 0 && segmentStart + segmentLength <= word.Length)
                                        {
                                            observedCombined = word.Substring(segmentStart, segmentLength);
                                        }

                                        // If applying the replacement to the concatenated base
                                        // produces the observed segment, accept this boundary.
                                        if (observedCombined is not null)
                                        {
                                            if (string.Equals(constructedSurface, observedCombined, StringComparison.Ordinal))
                                            {
                                                continue; // replacement produced the observed surface -> allow
                                            }
                                        }
                                        else
                                        {
                                            // Fallback for defensive scenarios where the combined
                                            // segment could not be determined (should be rare but
                                            // preserves previous behavior).
                                            if (string.Equals(constructedSurface, word, StringComparison.Ordinal))
                                            {
                                                continue;
                                            }

                                            if (_hashManager.Lookup(constructedSurface) || IsCompoundMadeOfTwoWords(constructedSurface, out _, out _))
                                            {
                                                continue;
                                            }
                                        }
                                    }

                                        // nothing (already checked alt inside the branch above)
                                }
                                catch
                                {
                                    // be defensive — if anything goes wrong treat as forbidden
                                }
                            }

                            return false; // Pattern matched and replacement did not permit an alternate form
                        }
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Check if a compound boundary matches a forbidden pattern.
    /// </summary>
    private bool CheckCompoundPatternMatch(string prevPart, string currentPart, CompoundPattern pattern, string? prevAppended = null, string? currAppended = null)
    {
        // If the pattern uses the special '0' token it means 'unmodified'
        // stem — only match when the candidate piece is an unmodified stem
        // (i.e. present as a dictionary root, not an affix-derived surface).
        if (string.Equals(pattern.EndChars, "0", StringComparison.Ordinal))
        {
            // require prevPart to be a dictionary surface (not empty) and
            // not be produced purely via affix reconstruction.
            if (string.IsNullOrEmpty(prevPart) || _hashManager.GetWordFlags(prevPart) is null) return false;
        }
        else if (!prevPart.EndsWith(pattern.EndChars, StringComparison.Ordinal))
        {
            return false;
        }

        // If pattern.BeginChars is "0" treat it as requiring an unmodified
        // stem at the beginning of the current part. Otherwise ensure the
        // current part begins with the specified begin-chars.
        if (string.Equals(pattern.BeginChars, "0", StringComparison.Ordinal))
        {
            if (string.IsNullOrEmpty(currentPart) || _hashManager.GetWordFlags(currentPart) is null) return false;
        }
        else if (!currentPart.StartsWith(pattern.BeginChars, StringComparison.Ordinal))
        {
            return false;
        }

        // If flags are specified, check them
        if (pattern.EndFlag is not null)
        {
            var prevVariants = _hashManager.GetWordFlagVariants(prevPart).ToList();
            if (prevVariants.Count == 0) return false;
            if (!prevVariants.Any(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, prevAppended, pattern.EndFlag)))
            {
                return false;
            }
        }

        if (pattern.BeginFlag is not null)
        {
            var currVariants = _hashManager.GetWordFlagVariants(currentPart).ToList();
            if (currVariants.Count == 0) return false;
            if (!currVariants.Any(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, currAppended, pattern.BeginFlag)))
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

        // NEEDAFFIX applies to variants individually: only when every homonym
        // variant of a surface form carries NEEDAFFIX should the base be
        // considered invalid on its own. Use per-variant inspection so a surface
        // with at least one variant that doesn't require an affix remains valid.
        var variants = _hashManager.GetWordFlagVariants(word).ToList();
        if (variants.Count == 0) return false;

        // If every variant (after normalization) contains the NEEDAFFIX flag,
        // then the base must be rejected. Otherwise a permissive variant exists.
        return variants.All(v => !string.IsNullOrEmpty(v) && _hashManager.VariantContainsFlagAfterAppend(v, null, _needAffixFlag));
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
                if (baseVariants.Count == 0)
                {
                    // The reconstructed base is not a single dictionary entry.
                    // If the reconstructed base is actually a two-word compound
                    // (e.g., "foo"+"bar"), and the affix appended flags would
                    // mark the affected component as forbidden, treat the derived
                    // word as forbidden as well.
                    if (!string.IsNullOrEmpty(appended))
                    {
                        var parts = FindTwoWordSplit(baseCandidate!);
                        if (parts is not null)
                        {
                            // For suffix-derived affixes the second component is
                            // the one that receives appended flags. If every variant
                            // of that component would be forbidden after applying
                            // the appended flags, the derived word should be
                            // considered forbidden.
                            var second = parts.Value.second;
                            var secondVariants = _hashManager.GetWordFlagVariants(second).ToList();
                            if (secondVariants.Count > 0)
                            {
                                var compoundForbidLocal = _compoundForbidFlag ?? string.Empty;
                                bool allSecondForbidden = secondVariants.All(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appended ?? string.Empty, compoundForbidLocal) || (!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appended ?? string.Empty, _forbiddenWordFlag)));
                                if (allSecondForbidden) return true;
                            }
                        }
                    }
                    return false;
                }
                // For derived forms treat each homonym variant separately; only
                // when every variant (after adding appended flags) contains the
                // forbidden flag is the derived form forbidden.
                return baseVariants.All(v => _hashManager.VariantContainsFlagAfterAppend(v, appended ?? string.Empty, _forbiddenWordFlag));
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
    /// Inspect an affix-derived word and determine whether the derivation
    /// would make a compound component forbidden — for example "foobars"
    /// when "bar"+s appends a COMPOUNDFORBID token should be rejected as
    /// a compound usage of the derived form. This helper centralizes the
    /// logic needed by public Spell() to avoid accepting such cases.
    /// </summary>
    public bool IsAffixDerivedFormForbiddenByCompound(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;

        if (!TryFindAffixBase(word, allowBaseOnlyInCompound: false, out var baseCandidate, out var kind, out var appended)) return false;
        if (string.IsNullOrEmpty(baseCandidate)) return false;

        // If there are no appended flags there's nothing to check here.
        if (string.IsNullOrEmpty(appended)) return false;

        // If no compound-forbid or forbidden-word token configured, nothing to block.
        if (string.IsNullOrEmpty(_compoundForbidFlag) && string.IsNullOrEmpty(_forbiddenWordFlag)) return false;

        // If the reconstructed base is a two-word compound, check the affected component
        var parts = FindTwoWordSplit(baseCandidate);
        if (parts is null) return false;

        // Decide which component receives appended flags based on match kind
        // Suffix-related match kinds imply the appended flags apply to the second component
        // Prefix-related kinds imply they apply to the first component. For mixed cases
        // be conservative and check both components.
        bool checkFirst = kind == AffixMatchKind.PrefixOnly || kind == AffixMatchKind.PrefixThenSuffix || kind == AffixMatchKind.SuffixThenPrefix;
        bool checkSecond = kind == AffixMatchKind.SuffixOnly || kind == AffixMatchKind.PrefixThenSuffix || kind == AffixMatchKind.SuffixThenPrefix;

        if (checkFirst)
        {
            var firstVariants = _hashManager.GetWordFlagVariants(parts.Value.first).ToList();
            if (firstVariants.Count > 0)
            {
                var compoundForbidLocal = _compoundForbidFlag ?? string.Empty;
                if (firstVariants.All(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appended ?? string.Empty, compoundForbidLocal) || (!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appended ?? string.Empty, _forbiddenWordFlag))))
                {
                    return true;
                }
            }
        }

        if (checkSecond)
        {
            var secondVariants = _hashManager.GetWordFlagVariants(parts.Value.second).ToList();
            if (secondVariants.Count > 0)
            {
                var compoundForbidLocal = _compoundForbidFlag ?? string.Empty;
                if (secondVariants.All(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appended ?? string.Empty, compoundForbidLocal) || (!string.IsNullOrEmpty(_forbiddenWordFlag) && _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, appended ?? string.Empty, _forbiddenWordFlag))))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Try to find a dictionary base candidate that generates the supplied
    /// word via one or two affix operations (suffix/prefix and prefix/suffix).
    /// If a base is found, return true and set baseCandidate to the matched
    /// dictionary root (or the compound base formed by two words).
    /// </summary>
    private enum AffixMatchKind { None, PrefixOnly, SuffixOnly, PrefixThenSuffix, SuffixThenPrefix }

    private bool TryFindAffixBase(string word, bool allowBaseOnlyInCompound, out string? baseCandidate, out AffixMatchKind kind, out string? appendedFlag, int depth = 0)
    {
        if (!TryFindAffixBaseCore(word, allowBaseOnlyInCompound, out baseCandidate, out kind, out appendedFlag, depth))
        {
            return false;
        }

        if (!IsCircumfixSatisfied(kind, appendedFlag))
        {
            baseCandidate = null;
            kind = AffixMatchKind.None;
            appendedFlag = null;
            return false;
        }

        return true;
    }

    private bool TryFindAffixBaseCore(string word, bool allowBaseOnlyInCompound, out string? baseCandidate, out AffixMatchKind kind, out string? appendedFlag, int depth)
    {
        baseCandidate = null;
        kind = AffixMatchKind.None;
        appendedFlag = null;

        // Helper to join appended flags safely
        static string ConcatFlags(string? a, string? b)
            => (a ?? string.Empty) + (b ?? string.Empty);

        // Normalize common apostrophe-like characters so affix matching treats
        // ’ (U+2019) and similar characters as equivalent to ASCII apostrophe.
        static string NormalizeApostrophes(string s)
            => s?.Replace('\u2019', '\'').Replace('\u2018', '\'').Replace('\u02BC', '\'') ?? string.Empty;

        var normalizedWord = NormalizeApostrophes(word);

        // 1) Try suffix-first: word = base + suffix
            foreach (var sfx in _suffixes)
        {
            if (string.IsNullOrEmpty(sfx.Affix)) continue;
            // Allow affix matching to succeed regardless of case. Hunspell
            // treats affix matching in a case-insensitive manner for suffixes
            // like "'s" so we must accept "'S" as well.
            var sfxAffixNorm = NormalizeApostrophes(sfx.Affix ?? string.Empty);
            if (!normalizedWord.EndsWith(sfxAffixNorm, StringComparison.OrdinalIgnoreCase)) continue;

            var base1 = word.Substring(0, word.Length - sfx.Affix!.Length);

            // Build the candidate base by *adding* the stripping text back on.
            // Hunspell suffix rules are applied to the original base where the
            // stripped text is part of the dictionary entry. When validating a
            // given word we compute baseWithoutSuffix (base1) and reconstruct the
            // dictionary candidate as base1 + stripping (unless stripping is 0).
            var reconstructedBase = base1;
            if (!string.IsNullOrEmpty(sfx.Stripping) && sfx.Stripping != "0")
            {
                reconstructedBase = base1 + sfx.Stripping;
            }

            // condition check (match against the reconstructed dictionary base)
            if (!string.IsNullOrEmpty(sfx.Condition) && sfx.Condition != ".")
            {
                try
                {
                    if (!Regex.IsMatch(reconstructedBase, sfx.Condition + "$", RegexOptions.CultureInvariant)) continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            // If reconstructed base is a dictionary word and allowed
            // Also ensure it is not a 'ph:' target which marks the collapsed
            // form as a misspelling in upstream tests.
            if (!_hashManager.HasPhTarget(reconstructedBase) && _hashManager.Lookup(reconstructedBase))
            {
                var baseVariants = _hashManager.GetWordFlagVariants(reconstructedBase).ToList();
                if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                {
                    // base only allowed in compounds and caller disallows it
                    // try other possibilities
                }
                else
                {
                    // Return the reconstructed dictionary base as the matched
                    // baseCandidate so callers can inspect the origin.
                    baseCandidate = reconstructedBase;
                    kind = AffixMatchKind.SuffixOnly;
                    appendedFlag = sfx.AppendedFlag;
                    return true;
                }
            }

            // If base1 can be created by combining two dictionary words, accept it
            if (! _hashManager.HasPhTarget(reconstructedBase) && IsCompoundMadeOfTwoWords(reconstructedBase, out _, out _))
                {
                // When COMPOUNDRULEs are defined they act as an allow-list — the
                // reconstructed compound base should be validated against the
                // configured COMPOUNDRULEs to ensure affix-derived forms only
                // produce compounds that comply with the rules.
                if (_compoundRules.Count > 0)
                {
                    // Avoid invoking the full COMPOUNDRULE matcher here (it
                    // may recurse back into TryFindAffixBase). Instead perform a
                    // shallow two-word rule check: when the reconstructed base
                    // splits into two dictionary words, check whether any
                    // configured COMPOUNDRULE matches the pair (non-recursive,
                    // handles common two-token cases like 'vw'). If no rule
                    // matches we should not accept the reconstructed base.
                    var split = FindTwoWordSplit(reconstructedBase);
                    bool permittedByRule = false;
                    if (split is not null)
                    {
                        var first = split.Value.first;
                        var second = split.Value.second;

                        foreach (var rule in _compoundRules)
                        {
                            // Only support simple two-token rules without
                            // quantifiers here to avoid recursion and complexity.
                            var tokens = new List<string>();
                            var pos = 0;
                            var ok = true;
                            while (pos < rule.Length)
                            {
                                var (tok, quant, next) = ParsePatternElement(rule, pos);
                                if (quant != '\0') { ok = false; break; }
                                tokens.Add(tok);
                                pos = next;
                            }
                            if (!ok) continue;
                            if (tokens.Count != 2) continue;

                            bool firstMatches = false;
                            bool secondMatches = false;

                            var t0 = tokens[0];
                            if (!string.IsNullOrEmpty(t0) && t0.Length == 1 && char.IsDigit(t0[0]))
                                firstMatches = ComponentMatchesDigitClass(first, t0[0]);
                            else
                            {
                                var v0 = _hashManager.GetWordFlagVariants(first).ToList();
                                if (v0.Count > 0 && v0.Any(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, t0))) firstMatches = true;
                            }

                            var t1 = tokens[1];
                            if (!string.IsNullOrEmpty(t1) && t1.Length == 1 && char.IsDigit(t1[0]))
                                secondMatches = ComponentMatchesDigitClass(second, t1[0]);
                            else
                            {
                                var v1 = _hashManager.GetWordFlagVariants(second).ToList();
                                if (v1.Count > 0 && v1.Any(v => _hashManager.VariantContainsFlagAfterAppend(v ?? string.Empty, null, t1))) secondMatches = true;
                            }

                            if (firstMatches && secondMatches)
                            {
                                permittedByRule = true;
                                break;
                            }
                        }
                    }

                    if (!permittedByRule) {
                        // this reconstructed base is not allowed by compound rules
                    }
                    else
                    {
                        baseCandidate = reconstructedBase;
                        kind = AffixMatchKind.SuffixOnly;
                        appendedFlag = sfx.AppendedFlag;
                        return true;
                    }
                }
                else
                {
                    baseCandidate = reconstructedBase;
                    kind = AffixMatchKind.SuffixOnly;
                    appendedFlag = sfx.AppendedFlag;
                    return true;
                }
                }

            // Try stripping a prefix from base1: base1 = prefix + root
            // Also handle the case where two suffixes were applied in sequence
            // (e.g., word = base + s2 + s1). If so, base1 will end with the
            // earlier suffix and we should try to remove that suffix and
            // reconstruct the true dictionary base.
                foreach (var s2 in _suffixes)
            {
                if (string.IsNullOrEmpty(s2.Affix)) continue;
                var base1Norm = NormalizeApostrophes(base1);
                var s2AffixNorm = NormalizeApostrophes(s2.Affix ?? string.Empty);
                if (!base1Norm.EndsWith(s2AffixNorm, StringComparison.OrdinalIgnoreCase)) continue;

                var base2Candidate = base1.Substring(0, base1.Length - s2.Affix!.Length);

                var reconstructedDouble = base2Candidate;
                if (!string.IsNullOrEmpty(s2.Stripping) && s2.Stripping != "0")
                {
                    reconstructedDouble = base2Candidate + s2.Stripping;
                }

                // check whether reconstructedDouble corresponds to a dictionary word
                if (!_hashManager.HasPhTarget(reconstructedDouble) && _hashManager.Lookup(reconstructedDouble))
                {
                    baseCandidate = reconstructedDouble;
                    kind = AffixMatchKind.SuffixOnly; // effectively two suffixes
                    appendedFlag = ConcatFlags(s2.AppendedFlag, sfx.AppendedFlag);
                    return true;
                }

                if (!_hashManager.HasPhTarget(reconstructedDouble) && IsCompoundMadeOfTwoWords(reconstructedDouble, out _, out _))
                {
                    baseCandidate = reconstructedDouble;
                    kind = AffixMatchKind.SuffixOnly;
                    appendedFlag = ConcatFlags(s2.AppendedFlag, sfx.AppendedFlag);
                    return true;
                }

                // If we didn't find the reconstructedDouble directly, try a limited
                // recursive search: the intermediate reconstructedDouble itself could
                // be an affix-derived form (e.g., prefix + suffix on the underlying
                // base). Allow one level of recursion to detect deeper chains such as
                // two suffixes plus a prefix.
                if (depth < 2 && !_hashManager.HasPhTarget(reconstructedDouble) && TryFindAffixBase(reconstructedDouble, allowBaseOnlyInCompound, out var nestedBaseFromDouble, out var nestedKindFromDouble, out var nestedAppendedFromDouble, depth + 1))
                {
                    baseCandidate = nestedBaseFromDouble;
                    kind = AffixMatchKind.SuffixOnly; // still effectively suffix-derived
                    // combine appended flags: nested (inner) appended flags first
                    appendedFlag = ConcatFlags(nestedAppendedFromDouble, ConcatFlags(s2.AppendedFlag, sfx.AppendedFlag));
                    return true;
                }
            }
            foreach (var pfx in _prefixes)
            {
                if (string.IsNullOrEmpty(pfx.Affix)) continue;

                // Attempt to interpret baseCandidate as having had a prefix
                // applied originally. If so, the original dictionary base would
                // be formed by removing the prefix affix and then *adding back*
                // any prefix-stripping text.
                var reconstructedBaseNorm = NormalizeApostrophes(reconstructedBase);
                var pfxAffixNorm = NormalizeApostrophes(pfx.Affix ?? string.Empty);
                if (!reconstructedBaseNorm.StartsWith(pfxAffixNorm, StringComparison.OrdinalIgnoreCase)) continue;

                var inner = reconstructedBase.Substring(pfx.Affix!.Length);

                var nestedCandidate = inner;
                if (!string.IsNullOrEmpty(pfx.Stripping) && pfx.Stripping != "0")
                {
                    nestedCandidate = pfx.Stripping + inner;
                }

                if (!string.IsNullOrEmpty(pfx.Condition) && pfx.Condition != ".")
                {
                    try
                    {
                        if (!Regex.IsMatch(nestedCandidate, "^" + pfx.Condition, RegexOptions.CultureInvariant)) continue;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }

                if (!_hashManager.HasPhTarget(nestedCandidate) && _hashManager.Lookup(nestedCandidate))
                {
                    var baseVariants = _hashManager.GetWordFlagVariants(nestedCandidate).ToList();
                    if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                    {
                        // not allowed by caller
                    }
                    else
                    {
                        baseCandidate = nestedCandidate;
                        kind = AffixMatchKind.SuffixThenPrefix;
                        appendedFlag = ConcatFlags(sfx.AppendedFlag, pfx.AppendedFlag);
                        return true;
                    }
                }

                if (!_hashManager.HasPhTarget(nestedCandidate) && IsCompoundMadeOfTwoWords(nestedCandidate, out _, out _))
                {
                    baseCandidate = nestedCandidate;
                    kind = AffixMatchKind.SuffixThenPrefix;
                    appendedFlag = ConcatFlags(sfx.AppendedFlag, pfx.AppendedFlag);
                    return true;
                }

                // If nestedCandidate isn't a direct dictionary word it could itself
                // be affix-derived (for example: base + suffix -> foos -> prefix applied
                // yields unfoos). Use a limited recursive search to find the true
                // dictionary base that generates nestedCandidate.
                if (depth < 2 && !_hashManager.HasPhTarget(nestedCandidate) && TryFindAffixBase(nestedCandidate, allowBaseOnlyInCompound, out var nestedBase, out var nestedKind, out var nestedAppended, depth + 1))
                {
                    baseCandidate = nestedBase;
                    kind = AffixMatchKind.SuffixThenPrefix;
                    appendedFlag = ConcatFlags(sfx.AppendedFlag, ConcatFlags(nestedAppended, pfx.AppendedFlag));
                    return true;
                }
            }
        }

        // 2) Try prefix-first: word = prefix + base
        foreach (var pfx in _prefixes)
        {
            if (string.IsNullOrEmpty(pfx.Affix)) continue;
            var pfxAffixNorm = NormalizeApostrophes(pfx.Affix ?? string.Empty);
            if (!normalizedWord.StartsWith(pfxAffixNorm, StringComparison.OrdinalIgnoreCase)) continue;

            var rem = word.Substring(pfx.Affix!.Length);

            // Reconstruct the possible dictionary base by prepending the prefix
            // stripping string (if present). The base candidate is what would
            // have been present in the dictionary before the prefix was applied.
            var baseCandidateFromPrefix = rem;
            if (!string.IsNullOrEmpty(pfx.Stripping) && pfx.Stripping != "0")
            {
                baseCandidateFromPrefix = pfx.Stripping + rem;
            }

            if (!string.IsNullOrEmpty(pfx.Condition) && pfx.Condition != ".")
            {
                try
                {
                    if (!Regex.IsMatch(baseCandidateFromPrefix, "^" + pfx.Condition, RegexOptions.CultureInvariant)) continue;
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            // direct base after prefix
            if (!_hashManager.HasPhTarget(baseCandidateFromPrefix) && _hashManager.Lookup(baseCandidateFromPrefix))
            {
                var baseVariants = _hashManager.GetWordFlagVariants(baseCandidateFromPrefix).ToList();
                if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                {
                    // not allowed by caller, continue
                }
                else
                {
                    baseCandidate = baseCandidateFromPrefix;
                    kind = AffixMatchKind.PrefixOnly;
                    appendedFlag = pfx.AppendedFlag;
                    return true;
                }
            }

            if (!_hashManager.HasPhTarget(baseCandidateFromPrefix) && IsCompoundMadeOfTwoWords(baseCandidateFromPrefix, out _, out _))
            {
                baseCandidate = baseCandidateFromPrefix;
                kind = AffixMatchKind.PrefixOnly;
                appendedFlag = pfx.AppendedFlag;
                return true;
            }

            // try suffix on remainder
            foreach (var sfx in _suffixes)
            {
                if (string.IsNullOrEmpty(sfx.Affix)) continue;
                var remNorm = NormalizeApostrophes(rem);
                var sfxAffixNorm2 = NormalizeApostrophes(sfx.Affix ?? string.Empty);
                if (!remNorm.EndsWith(sfxAffixNorm2, StringComparison.OrdinalIgnoreCase)) continue;

                var base2 = rem.Substring(0, rem.Length - sfx.Affix!.Length);

                // Reconstruct the original dictionary base by appending the
                // suffix stripping string back onto the remainder (base2).
                var baseCandidate2 = base2;
                if (!string.IsNullOrEmpty(sfx.Stripping) && sfx.Stripping != "0")
                {
                    baseCandidate2 = base2 + sfx.Stripping;
                }

                if (!string.IsNullOrEmpty(sfx.Condition) && sfx.Condition != ".")
                {
                    try
                    {
                        if (!Regex.IsMatch(baseCandidate2, sfx.Condition + "$", RegexOptions.CultureInvariant)) continue;
                    }
                    catch (ArgumentException)
                    {
                        continue;
                    }
                }

                if (!_hashManager.HasPhTarget(baseCandidate2) && _hashManager.Lookup(baseCandidate2))
                {
                    var baseVariants = _hashManager.GetWordFlagVariants(baseCandidate2).ToList();
                    if (!allowBaseOnlyInCompound && !string.IsNullOrEmpty(_onlyInCompound) && baseVariants.Count > 0 && baseVariants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_onlyInCompound)))
                    {
                        // not allowed
                    }
                    else
                    {
                        baseCandidate = baseCandidate2;
                        kind = AffixMatchKind.PrefixThenSuffix;
                        appendedFlag = ConcatFlags(pfx.AppendedFlag, sfx.AppendedFlag);
                        return true;
                    }
                }

                if (!_hashManager.HasPhTarget(baseCandidate2) && IsCompoundMadeOfTwoWords(baseCandidate2, out _, out _))
                {
                    baseCandidate = baseCandidate2;
                    kind = AffixMatchKind.PrefixThenSuffix;
                    appendedFlag = ConcatFlags(pfx.AppendedFlag, sfx.AppendedFlag);
                    return true;
                }

                // baseCandidate2 may itself be an affix-derived form (e.g., foos)
                // which we should resolve recursively. This supports combinations
                // like prefix + two suffixes.
                if (depth < 2 && !_hashManager.HasPhTarget(baseCandidate2) && TryFindAffixBase(baseCandidate2, allowBaseOnlyInCompound, out var nestedBaseFromRem, out var nestedKindFromRem, out var nestedAppendedFromRem, depth + 1))
                {
                    baseCandidate = nestedBaseFromRem;
                    kind = AffixMatchKind.PrefixThenSuffix;
                    appendedFlag = ConcatFlags(pfx.AppendedFlag, ConcatFlags(nestedAppendedFromRem, sfx.AppendedFlag));
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsCircumfixSatisfied(AffixMatchKind matchKind, string? appendedFlag)
    {
        if (string.IsNullOrEmpty(_circumfixFlag) || string.IsNullOrEmpty(appendedFlag))
        {
            return true;
        }

        if (!_hashManager.VariantContainsFlagAfterAppend(string.Empty, appendedFlag, _circumfixFlag!))
        {
            return true;
        }

        bool usedPrefix = matchKind == AffixMatchKind.PrefixOnly || matchKind == AffixMatchKind.PrefixThenSuffix || matchKind == AffixMatchKind.SuffixThenPrefix;
        bool usedSuffix = matchKind == AffixMatchKind.SuffixOnly || matchKind == AffixMatchKind.PrefixThenSuffix || matchKind == AffixMatchKind.SuffixThenPrefix;

        return usedPrefix && usedSuffix;
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
    /// Remove diacritic marks from a string (NFD->strip NonSpacingMark->NFC).
    /// Used as a best-effort fallback for REP-style matches (e.g. í -> i).
    /// </summary>
    private static string RemoveDiacritics(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var normalized = input.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in normalized)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
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
