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
            _hashManager = new HashManager(dictionaryPath);
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
                            return false;
                        }
                    }
                }

                return true;
            }

            // If it's an affix-derived form (e.g., foos from foo + SFX 's'), accept it
            if (_affixManager?.CheckAffixedWord(w) ?? false)
            {
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
