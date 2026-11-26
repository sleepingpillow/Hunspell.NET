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
            var (affixContent, encodingHint) = AffixManager.ReadAffixContentAndEncoding(affixPath);
            _hashManager = new HashManager(dictionaryPath, encodingHint);
            _affixManager = new AffixManager(affixPath, _hashManager, affixContent);
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
                // ONLYINCOMPOUND and NEEDAFFIX reject the dictionary surface,
                // but an independent affix analysis may still validate it.
                if (!(_affixManager?.IsOnlyInCompound(w) ?? false) &&
                    !(_affixManager?.RequiresAffix(w) ?? false))
                {
                    return _affixManager?.KeepCaseFlag is not string keepCaseFlag ||
                        _hashManager.LookupWithCaseRules(w, keepCaseFlag);
                }
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
                // Reject affix-derived forms that are allowed only inside
                // compounds due to appended ONLYINCOMPOUND flags.
                if (_affixManager?.IsAffixDerivedOnlyInCompound(w) ?? false)
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

        word = PrepareInput(word);
        var trimmed = word.TrimEnd('.');
        if (trimmed.Length == 0 || IsNumber(trimmed)) return true;

        if (trimmed.Length != word.Length && _hashManager?.Lookup(trimmed + '.') == true && CheckWord(trimmed + '.'))
            return true;

        return CheckWord(trimmed) || (trimmed.Length != word.Length && CheckWord(trimmed + '.'));
    }

    private string PrepareInput(string word)
    {
        word = _affixManager?.ConvertInput(word) ?? word;
        if (_affixManager?.IgnoreChars is { Length: > 0 } ignored &&
            word.AsSpan().IndexOfAny(ignored.AsSpan()) >= 0)
        {
            word = new string(word.Where(ch => !ignored.Contains(ch)).ToArray());
        }

        return word.TrimStart(' ');
    }

    private static bool IsNumber(string word)
    {
        bool needsDigit = true;
        foreach (char ch in word)
        {
            if (ch is (>= '0' and <= '9') or (>= '\u0660' and <= '\u0669') or (>= '\u06F0' and <= '\u06F9'))
                needsDigit = false;
            else if (!needsDigit && ch is '.' or '-' or ',')
                needsDigit = true;
            else
                return false;
        }

        return !needsDigit;
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

        // If the word is correctly spelled, no suggestions needed. For very large
        // dictionaries, avoid expensive compound/affix recursion in Spell by
        // first doing a fast lookup and skipping the full Spell when the dict is
        // extremely large; this prevents pathological hangs in suggestion paths
        // (e.g., Swedish sv_FI full wordlist).
        bool isCorrect;
        if (_hashManager is not null && _hashManager.WordCount > 100_000)
        {
            isCorrect = _hashManager.Lookup(word);
        }
        else
        {
            isCorrect = Spell(word);
        }

        if (isCorrect)
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
    /// Get stems (base forms) of a word.
    /// </summary>
    /// <param name="word">The word to analyze</param>
    /// <returns>A list of possible stems for the word</returns>
    public List<string> Stem(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var stems = new List<string>();
        
        if (_affixManager == null || _hashManager == null)
        {
            return stems;
        }

        // Get stems from the affix manager
        _affixManager.GetStems(word, stems);
        
        return stems;
    }

    /// <summary>
    /// Analyze a word and return morphological descriptions.
    /// </summary>
    /// <param name="word">The word to analyze</param>
    /// <returns>A list of morphological analyses for the word</returns>
    public List<string> Analyze(string word)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(word);

        var analyses = new List<string>();
        
        if (_affixManager == null || _hashManager == null)
        {
            return analyses;
        }

        // Get morphological analyses from the affix manager
        _affixManager.GetAnalyses(word, analyses);
        
        return analyses;
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
