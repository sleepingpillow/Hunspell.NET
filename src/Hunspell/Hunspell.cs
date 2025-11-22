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

        // First check if it's in the dictionary
        if (_hashManager?.Lookup(word) ?? false)
        {
            // Check if the word is marked as ONLYINCOMPOUND
            // If so, it's only valid inside compounds, not standalone
            if (_affixManager?.IsOnlyInCompound(word) ?? false)
            {
                return false;
            }
            return true;
        }

        // If not found, check if it's a valid compound word
        return _affixManager?.CheckCompound(word) ?? false;
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
