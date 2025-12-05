// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1
// Based on Hunspell by László Németh and contributors

namespace Hunspell;

/// <summary>
/// Main Hunspell spell checker class.
/// Provides spell checking, suggestion generation, and morphological analysis.
/// </summary>
public sealed class HunspellSpellChecker : IDisposable
{
    private readonly AffixManager? _affixManager;
    private readonly HashManager? _hashManager;
    private bool _disposed;

    /// <summary>
    /// Creates a new Hunspell instance from affix and dictionary files.
    /// </summary>
    /// <param name="affixPath">Path to the .aff file</param>
    /// <param name="dictionaryPath">Path to the .dic file</param>
    public HunspellSpellChecker(string affixPath, string dictionaryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(affixPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dictionaryPath);

        try
        {
            // Read declared encoding from affix (if present) so the dictionary
            // can be read using the correct encoding. Many upstream tests put
            // SET <encoding> into the .aff file and the dictionary needs that
            // information to decode correctly.
            var encodingHint = AffixManager.ReadDeclaredEncodingFromAffix(affixPath);
            _hashManager = new HashManager(dictionaryPath, encodingHint);
            _affixManager = new AffixManager(affixPath, _hashManager);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Check if a word is spelled correctly.
    /// </summary>
    /// <param name="word">The word to check</param>
    /// <returns>True if the word is spelled correctly, false otherwise</returns>
    public bool Spell(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        // Helper that performs the normal lookup/affix/compound checks for a single word
        bool CheckWord(string w)
        {
            // First check if it's in the dictionary
            if (_hashManager == null) { /* no dictionary */ }
            if (_hashManager?.Lookup(w) ?? false)
            {
                // If the word is marked as FORBIDDENWORD in the dictionary or via
                // derived affix flags, it must be rejected.
                if (_affixManager?.IsForbiddenWord(w) ?? false)
                {
                    return false;
                }
                // Check if the word is marked as ONLYINCOMPOUND
                // If so, it's only valid inside compounds, not standalone
                if (_affixManager?.IsOnlyInCompound(w) ?? false)
                {
                    return false;
                }
                // If this dictionary entry requires an affix (NEEDAFFIX), a bare
                // dictionary form should not be accepted by itself.
                if (_affixManager?.RequiresAffix(w) ?? false)
                {
                    return false;
                }
                // enforce KEEPCASE: if the dictionary entry(s) are flagged
                // with the KEEPCASE flag, reject forms that are ALLCAP or
                // initial-capitalized (e.g., "ABC" or "Abc")
                if (_affixManager?.KeepCaseFlag is string kf)
                {
                    var variants = _hashManager.GetWordFlagVariants(w).ToList();
                    if (variants.Count > 0 && variants.Any(v => !string.IsNullOrEmpty(v) && v.Contains(kf)))
                    {
                        // detect capitalization classes
                        bool isAllCaps = string.Equals(w, w.ToUpperInvariant(), StringComparison.Ordinal);
                        bool isInitCap = char.IsUpper(w[0]) && string.Equals(w.Substring(1), w.Substring(1).ToLowerInvariant(), StringComparison.Ordinal);
                        if (isAllCaps || isInitCap)
                        {
                            // CHECKSHARPS exception: if enabled and word contains ß, allow InitCap
                            // (German words with ß are allowed to be capitalized even if KEEPCASE is set)
                            if (_affixManager.CheckSharps && w.Contains('ß') && isInitCap)
                            {
                                // allowed
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }

            // If it's an affix-derived form (e.g., foos from foo + SFX 's'), accept it
            if (_affixManager?.CheckAffixedWord(w) ?? false)
            {
                // If this affix-derived form would make a compound component
                // forbidden due to appended flags (e.g., foobars where bars becomes
                // COMPOUNDFORBID after appending), reject it.
                if (_affixManager?.IsAffixDerivedFormForbiddenByCompound(w) ?? false)
                {
                    return false;
                }
                // Accept affixed-derived forms only when they're not forbidden by flags
                if (_affixManager?.IsForbiddenWord(w) ?? false)
                {
                    return false;
                }
                return true;
            }

            // Check if word can be broken at break points and all parts are valid
            if (_affixManager?.CheckBreak(w) ?? false)
            {
                return true;
            }

            // If not found, check if it's a valid compound word
            return _affixManager?.CheckCompound(w) ?? false;
        }

        // Try the original word first
        if (CheckWord(word)) return true;

        // If the affix file defines WORDCHARS and the input consists only
        // of characters from that set, accept it as valid. This mirrors
        // upstream Hunspell where numeric tokens or other sequences built
        // from WORDCHARS are treated as valid words.
        if (_affixManager?.WordChars is string wc && !string.IsNullOrEmpty(wc))
        {
            bool allAllowed = true;
            foreach (var ch in word)
            {
                if (wc.IndexOf(ch) < 0)
                {
                    allAllowed = false;
                    break;
                }
            }
            if (allAllowed)
            {
                // When WORDCHARS contains punctuation-like characters we should
                // still reject obviously malformed tokens such as those that
                // start or end with a punctuation character or contain two
                // punctuation characters in a row. Build a punctuation set
                // derived from WORDCHARS and apply simple sanity rules.
                var punctSet = new HashSet<char>();
                foreach (var ch in wc)
                {
                    if (!char.IsLetterOrDigit(ch)) punctSet.Add(ch);
                }

                // Accept only if token doesn't start or end with punctuation
                if (punctSet.Contains(word[0]) || punctSet.Contains(word[^1]))
                {
                    // malformed (starts/ends with punctuation)
                }
                else
                {
                    // Reject sequences with consecutive punctuation characters
                    bool hasConsecutivePunct = false;
                    for (int j = 1; j < word.Length; j++)
                    {
                        if (punctSet.Contains(word[j]) && punctSet.Contains(word[j - 1]))
                        {
                            hasConsecutivePunct = true;
                            break;
                        }
                    }
                    if (!hasConsecutivePunct)
                    {
                        return true;
                    }
                }
            }
        }

        // If the affix file defines IGNORE characters, try removing them and
        // re-checking the stripped word. This permits words that only differ
        // by optional punctuation markers (e.g., Armenian punctuation) to be
        // accepted when the underlying dictionary contains the base form.
        if (_affixManager?.IgnoreChars is string ignore && !string.IsNullOrEmpty(ignore))
        {
            var cleaned = new string(word.Where(ch => !ignore.Contains(ch)).ToArray());
            if (!string.Equals(cleaned, word, StringComparison.Ordinal) && CheckWord(cleaned)) return true;
        }

        // If ICONV rules exist, try transforming the input using ICONV mappings
        // and re-run the checks on each transformed candidate.
        if (_affixManager is not null)
        {
            foreach (var cand in _affixManager.GenerateIconvCandidates(word))
            {
                if (CheckWord(cand)) return true;
            }
        }

        // The exact (unsuccessful) checks above can sometimes fail due to
        // trailing punctuation (e.g. "etc.", "HUNSPELL..."). Upstream Hunspell
        // treats many trailing dots as sentence punctuation and still accepts
        // the base word — try stripping trailing '.' characters and re-run
        // the same checks.
        // Note: we intentionally only trim '.' here to avoid accidentally
        // transforming other meaningful characters that may belong to words
        // (e.g. inner dots in 'OpenOffice.org').
        var trimmed = word.TrimEnd('.');
        if (!string.Equals(trimmed, word, StringComparison.Ordinal))
        {
            if (CheckWord(trimmed)) return true;

            // Also try ICONV candidates for trimmed input
            if (_affixManager is not null)
            {
                foreach (var cand in _affixManager.GenerateIconvCandidates(trimmed))
                {
                    if (CheckWord(cand)) return true;
                }
            }
        }

        return false;

    }

    /// <summary>
    /// Generate spelling suggestions for a misspelled word.
    /// </summary>
    /// <param name="word">The misspelled word</param>
    /// <returns>A list of suggested corrections</returns>
    public List<string> Suggest(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var suggestions = new List<string>();

        // If the word is correctly spelled, no suggestions needed
        if (Spell(word))
        {
            return suggestions;
        }

        // Generate suggestions using the suggestion manager
        _affixManager?.GenerateSuggestions(word, suggestions);

        return suggestions;
    }

    /// <summary>
    /// Add a word to the runtime dictionary.
    /// </summary>
    /// <param name="word">The word to add</param>
    /// <returns>True if the word was added successfully</returns>
    public bool Add(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        return _hashManager?.Add(word) ?? false;
    }

    /// <summary>
    /// Remove a word from the runtime dictionary.
    /// </summary>
    /// <param name="word">The word to remove</param>
    /// <returns>True if the word was removed successfully</returns>
    public bool Remove(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        return _hashManager?.Remove(word) ?? false;
    }

    /// <summary>
    /// Get the dictionary encoding.
    /// </summary>
    public string DictionaryEncoding => _affixManager?.Encoding ?? "UTF-8";

    /// <summary>
    /// Releases all resources used by the HunspellSpellChecker.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _affixManager?.Dispose();
            _hashManager?.Dispose();
            _disposed = true;
        }
    }
}
