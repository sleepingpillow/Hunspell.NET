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
    // Flag representation format for dictionary entries. Default is single-character flags.
    private enum FlagFormat { Single, Long, Num, Utf8 }
    private FlagFormat _flagFormat = FlagFormat.Single;

    public HashManager(string dictionaryPath, string? encodingHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionaryPath);
        LoadDictionary(dictionaryPath, encodingHint);
    }

    private void LoadDictionary(string dictionaryPath, string? encodingHint = null)
    {
        if (!File.Exists(dictionaryPath))
        {
            throw new FileNotFoundException($"Dictionary file not found: {dictionaryPath}");
        }

        // Attempt to read the dictionary with UTF-8 first. Some upstream
        // test data uses legacy single-byte encodings (e.g., CP1250 /
        // ISO-8859-2). If UTF-8 decoding produces replacement characters
        // we try a small set of fallback encodings. Register the codepage
        // provider to support CodePages encodings on all runtimes.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        string fileContent;
        var triedEncodings = new List<System.Text.Encoding>() { System.Text.Encoding.UTF8 };

        // If the affix file asked for a particular encoding, try it first
        if (!string.IsNullOrEmpty(encodingHint))
        {
            try
            {
                var e = System.Text.Encoding.GetEncoding(encodingHint);
                // prefer the declared encoding first
                triedEncodings.Insert(0, e);
            }
            catch
            {
                // try common normalizations (e.g., ISO8859-15 -> ISO-8859-15)
                try
                {
                    var norm = encodingHint.Replace("ISO", "ISO-").Replace('_', '-');
                    var e = System.Text.Encoding.GetEncoding(norm);
                    triedEncodings.Insert(0, e);
                }
                catch { }
            }
        }

        // Read with the preferred encoding first (declared encoding if available,
        // otherwise UTF-8). Use BOM detection when reading the first attempt.
        using (var stream = File.OpenRead(dictionaryPath))
        using (var reader = new StreamReader(stream, triedEncodings[0], detectEncodingFromByteOrderMarks: true))
        {
            Console.WriteLine("HashManager: reading dictionary using encoding=" + triedEncodings[0].WebName);
            fileContent = reader.ReadToEnd();
        }

        // If we observed replacement characters in the decoded text then
        // attempt a few common legacy encodings used by upstream test data.
        bool looksBad = fileContent.Contains('\uFFFD');
        if (looksBad)
        {
            // Try preferred fallbacks for Central/Eastern Europe (Hungarian etc.)
            var fallbackCodes = new[] { 1250 /* Windows-1250 */, 28592 /* ISO-8859-2 */, 1252 /* Windows-1252 */, 28591 /* ISO-8859-1 */, 28605 /* ISO-8859-15 */ };
            foreach (var cp in fallbackCodes)
            {
                try
                {
                    var enc = System.Text.Encoding.GetEncoding(cp);
                    triedEncodings.Add(enc);
                    using var stream = File.OpenRead(dictionaryPath);
                    using var reader = new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: false);
                    var attempt = reader.ReadToEnd();
                    if (!attempt.Contains('\uFFFD'))
                    {
                        fileContent = attempt;
                        looksBad = false;
                        break;
                    }
                }
                catch
                {
                    // ignore failed encodings and continue
                }
            }
        }

        // Use the selected file content to parse lines. This ensures we
        // correctly decode accented characters from legacy encodings when
        // present in test dictionaries.
        var lines = fileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        // Handle optional count line at top
        int idx = 0;
        if (lines.Length == 0) return;
        var firstLine = lines[0];
        if (int.TryParse(firstLine.Trim(), out _))
        {
            idx = 1; // skip count line
        }

        for (; idx < lines.Length; idx++)
        {
            ProcessDictionaryLine(lines[idx]);
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
            var word = parts[0]?.Normalize(System.Text.NormalizationForm.FormC) ?? string.Empty;
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
            // Parse flags depending on the configured flag format and return
            // a concatenated representation that callers typically use via
            // .Contains(flag) checks. For multi-character flag formats (Long/Num)
            // we'll return a comma-separated list of tokens. For Single/UTF8
            // we'll return a concatenated string of unique characters/runes.
            var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                var f = e.Flags ?? string.Empty;
                if (string.IsNullOrEmpty(f)) continue;
                switch (_flagFormat)
                {
                    case FlagFormat.Long:
                        for (int i = 0; i < f.Length; i += 2)
                        {
                            int len = Math.Min(2, f.Length - i);
                            tokens.Add(f.Substring(i, len));
                        }
                        break;
                    case FlagFormat.Num:
                        foreach (var part in f.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            tokens.Add(part);
                        }
                        break;
                    case FlagFormat.Utf8:
                    case FlagFormat.Single:
                    default:
                        // For single-char and UTF-8 flag formats treat each text
                        // character as an atomic flag. (This handles BMP characters
                        // such as 'Ü'. Surrogate pairs are unlikely in flag tokens
                        // in our test corpus.)
                        foreach (var ch in f)
                        {
                            tokens.Add(ch.ToString());
                        }
                        break;
                }
            }

            if (tokens.Count == 0) return string.Empty;
            if (_flagFormat == FlagFormat.Long || _flagFormat == FlagFormat.Num)
            {
                return string.Join(',', tokens);
            }
            return string.Concat(tokens);
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

    /// <summary>
    /// Configure the flag format used for parsing dictionary flag strings.
    /// This can be called by AffixManager after the .aff file is parsed.
    /// </summary>
    public void SetFlagFormat(string flagDirective)
    {
        if (string.IsNullOrEmpty(flagDirective))
        {
            _flagFormat = FlagFormat.Single;
            return;
        }

        var normalized = flagDirective.Trim();
        if (string.Equals(normalized, "long", StringComparison.OrdinalIgnoreCase)) _flagFormat = FlagFormat.Long;
        else if (string.Equals(normalized, "num", StringComparison.OrdinalIgnoreCase)) _flagFormat = FlagFormat.Num;
        else if (string.Equals(normalized, "UTF-8", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "UTF8", StringComparison.OrdinalIgnoreCase) || string.Equals(normalized, "utf-8", StringComparison.OrdinalIgnoreCase)) _flagFormat = FlagFormat.Utf8;
        else _flagFormat = FlagFormat.Single;
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

    /// <summary>
    /// Merge a serialized flags string (as returned by GetWordFlags) with an appended
    /// appended-flag token string produced by an affix rule and return a normalized
    /// flags string in the same serialized form as GetWordFlags.
    /// </summary>
    public string MergeFlags(string baseFlagsSerialized, string? appendedFlagsRaw)
    {
        // Represent tokens as a set for de-duplication
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // helper to add tokens depending on current format
        void addTokensFromSerialized(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (_flagFormat == FlagFormat.Long || _flagFormat == FlagFormat.Num)
            {
                foreach (var part in s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) tokens.Add(part);
            }
            else
            {
                foreach (var ch in s) tokens.Add(ch.ToString());
            }
        }

        void addTokensFromRawAppended(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            if (_flagFormat == FlagFormat.Long)
            {
                for (int i = 0; i < s.Length; i += 2)
                {
                    var len = Math.Min(2, s.Length - i);
                    tokens.Add(s.Substring(i, len));
                }
            }
            else if (_flagFormat == FlagFormat.Num)
            {
                foreach (var part in s.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) tokens.Add(part);
            }
            else
            {
                foreach (var ch in s) tokens.Add(ch.ToString());
            }
        }

        addTokensFromSerialized(baseFlagsSerialized ?? string.Empty);
        if (!string.IsNullOrEmpty(appendedFlagsRaw)) addTokensFromRawAppended(appendedFlagsRaw);

        if (tokens.Count == 0) return string.Empty;
        if (_flagFormat == FlagFormat.Long || _flagFormat == FlagFormat.Num)
        {
            return string.Join(',', tokens);
        }
        return string.Concat(tokens);
    }

    /// <summary>
    /// Check whether a variant (raw flags as stored in the dictionary entry) would
    /// contain a particular flag token when combined with an appended-flag string.
    /// This allows callers to check a specific token equality instead of substring
    /// matching which could be ambiguous for multi-character tokens.
    /// </summary>
    public bool VariantContainsFlagAfterAppend(string variantRaw, string? appendedRaw, string flagToCheck)
    {
        if (string.IsNullOrEmpty(flagToCheck)) return false;

        // Tokenize variantRaw according to current flag format
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(variantRaw))
        {
            if (_flagFormat == FlagFormat.Long)
            {
                // Some variants may already be comma-separated; split if so.
                if (variantRaw.Contains(','))
                {
                    foreach (var p in variantRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) tokens.Add(p);
                }
                else
                {
                    for (int i = 0; i < variantRaw.Length; i += 2)
                    {
                        var len = Math.Min(2, variantRaw.Length - i);
                        tokens.Add(variantRaw.Substring(i, len));
                    }
                }
            }
            else if (_flagFormat == FlagFormat.Num)
            {
                foreach (var p in variantRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) tokens.Add(p);
            }
            else
            {
                foreach (var ch in variantRaw) tokens.Add(ch.ToString());
            }
        }

        // parse appended flags
        if (!string.IsNullOrEmpty(appendedRaw))
        {
            if (_flagFormat == FlagFormat.Long)
            {
                for (int i = 0; i < appendedRaw.Length; i += 2)
                {
                    var len = Math.Min(2, appendedRaw.Length - i);
                    tokens.Add(appendedRaw.Substring(i, len));
                }
            }
            else if (_flagFormat == FlagFormat.Num)
            {
                foreach (var p in appendedRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) tokens.Add(p);
            }
            else
            {
                foreach (var ch in appendedRaw) tokens.Add(ch.ToString());
            }
        }

        return tokens.Contains(flagToCheck);
    }
}
