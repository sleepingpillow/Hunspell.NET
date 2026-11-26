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

        return CheckSpelling(word);
    }

    private bool CheckSpelling(string word, List<string>? breakStack = null)
    {
        if (breakStack is not null &&
            (breakStack.Count >= AffixManager.MaxBreakDepth || breakStack.Contains(word)))
            return false;

        string prepared = PrepareInput(word);
        string trimmed = prepared.TrimEnd('.');
        if (trimmed.Length == 0 || IsNumber(trimmed)) return true;

        // Avoid a costly affix and compound search for an abbreviation explicitly
        // listed with its final dot in a large dictionary.
        if (prepared.Length != trimmed.Length && _hashManager is { WordCount: > 100_000 } &&
            !_hashManager.Lookup(trimmed) && _hashManager.Lookup(trimmed + '.') &&
            CheckWord(trimmed + '.') == WordMatch.Accepted)
            return true;

        var match = CheckWord(trimmed);
        if (match != WordMatch.Unknown) return match == WordMatch.Accepted;

        if (prepared.Length != trimmed.Length)
        {
            match = CheckWord(trimmed + '.');
            if (match != WordMatch.Unknown) return match == WordMatch.Accepted;
        }

        if (_affixManager is not { HasBreakPoints: true }) return false;
        breakStack ??= [];
        breakStack.Add(word);
        try
        {
            return _affixManager.CheckBreak(trimmed, part => CheckSpelling(part, breakStack));
        }
        finally
        {
            breakStack.RemoveAt(breakStack.Count - 1);
        }
    }

    private WordMatch CheckWord(string word)
    {
        if (_affixManager is null || _hashManager is null) return WordMatch.Unknown;

        if (_hashManager.Lookup(word))
        {
            if (_affixManager.IsForbiddenWord(word)) return WordMatch.Forbidden;

            // A restricted dictionary surface can have an independent affix analysis.
            if (!_affixManager.IsOnlyInCompound(word) && !_affixManager.RequiresAffix(word))
            {
                return _hashManager.LookupWithCaseRules(word, _affixManager.KeepCaseFlag)
                    ? WordMatch.Accepted
                    : WordMatch.Unknown;
            }
        }

        if (_affixManager.CheckAffixedWord(word))
        {
            if (_affixManager.IsAffixDerivedFormForbiddenByCompound(word) || _affixManager.IsForbiddenWord(word))
                return WordMatch.Forbidden;
            if (!_affixManager.IsAffixDerivedOnlyInCompound(word))
                return WordMatch.Accepted;
        }

        return _affixManager.CheckCompound(word) ? WordMatch.Accepted : WordMatch.Unknown;
    }

    private enum WordMatch { Unknown, Accepted, Forbidden }

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
