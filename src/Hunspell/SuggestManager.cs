// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

using System.Collections.Generic;
using System.Linq;

namespace Hunspell;

internal sealed class SuggestManager
{
    private readonly HashManager _hashManager;
    private readonly AffixManager _affixManager;

    internal SuggestManager(HashManager hashManager, AffixManager affixManager)
    {
        _hashManager = hashManager;
        _affixManager = affixManager;
    }

    public void GenerateSuggestions(string word, List<string> suggestions)
    {
        var seenSuggestions = new HashSet<string>(suggestions, StringComparer.Ordinal);

        foreach (var variant in _hashManager.GetCaseVariants(word))
        {
            if (_affixManager.CanSuggestWord(variant))
                TryAddSuggestion(suggestions, seenSuggestions, variant);
        }

        GenerateSubstitutionSuggestions(word, suggestions, seenSuggestions);
        GenerateInsertionSuggestions(word, suggestions, seenSuggestions);
        GenerateDeletionSuggestions(word, suggestions, seenSuggestions);
        GenerateSwapSuggestions(word, suggestions, seenSuggestions);
        GenerateRepSuggestions(word, suggestions, seenSuggestions);

        if (GenerateSplitSuggestions(word, suggestions, seenSuggestions))
            return;

        GeneratePossessiveSuggestions(word, suggestions, seenSuggestions);

        if (suggestions.Count < 10)
        {
            GenerateTwoEditSuggestions(word, suggestions, seenSuggestions);
        }

        if (_affixManager.OnlyMaxDiff && _affixManager.MaxDiff > 0)
        {
            var filter = suggestions.Where(s => BoundedLevenshtein(word, s, _affixManager.MaxDiff) >= 0).ToList();
            suggestions.Clear();
            suggestions.AddRange(filter);
        }

        if (suggestions.Count > 10)
        {
            suggestions.RemoveRange(10, suggestions.Count - 10);
        }
    }

    private static bool TryAddSuggestion(List<string> suggestions, HashSet<string> seenSuggestions, string? suggestion)
    {
        if (string.IsNullOrEmpty(suggestion) || !seenSuggestions.Add(suggestion))
        {
            return false;
        }

        suggestions.Add(suggestion);
        return true;
    }

    private void GenerateTwoEditSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        const int candidate1Cap = 500;
        const int candidate2Cap = 2000;

        var tryChars = _affixManager.TryCharacters;

        var seenCandidates = new HashSet<string>(StringComparer.Ordinal);
        var candidates1 = new List<string>();

        for (int i = 0; i < word.Length; i++)
        {
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

            var del = word.Remove(i, 1);
            if (seenCandidates.Add(del)) candidates1.Add(del);
            if (candidates1.Count >= candidate1Cap) break;
        }

        for (int i = 0; i <= word.Length && candidates1.Count < candidate1Cap; i++)
        {
            foreach (var c in tryChars)
            {
                var cand = word.Insert(i, c.ToString());
                if (seenCandidates.Add(cand)) candidates1.Add(cand);
                if (candidates1.Count >= candidate1Cap) break;
            }
        }

        for (int i = 0; i < word.Length - 1 && candidates1.Count < candidate1Cap; i++)
        {
            var cand = string.Create(word.Length, (word, i), static (span, state) =>
            {
                state.word.AsSpan().CopyTo(span);
                (span[state.i], span[state.i + 1]) = (span[state.i + 1], span[state.i]);
            });
            if (seenCandidates.Add(cand)) candidates1.Add(cand);
        }

        int candidate2Seen = 0;
        foreach (var cand1 in candidates1)
        {
            if (suggestions.Count >= 10) break;

            if (_affixManager.CanSuggestWord(cand1))
            {
                TryAddSuggestion(suggestions, seenSuggestions, cand1);
                if (suggestions.Count >= 10) break;
            }

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
                    if (_affixManager.CanSuggestWord(cand2))
                    {
                        TryAddSuggestion(suggestions, seenSuggestions, cand2);
                        if (suggestions.Count >= 10) break;
                    }
                    if (candidate2Seen >= candidate2Cap) break;
                }
            }

            for (int i = 0; i < cand1.Length && candidate2Seen < candidate2Cap && suggestions.Count < 10; i++)
            {
                var cand2 = cand1.Remove(i, 1);
                candidate2Seen++;
                if (_affixManager.CanSuggestWord(cand2))
                {
                    TryAddSuggestion(suggestions, seenSuggestions, cand2);
                    if (suggestions.Count >= 10) break;
                }
            }

            for (int i = 0; i <= cand1.Length && candidate2Seen < candidate2Cap && suggestions.Count < 10; i++)
            {
                foreach (var c in tryChars)
                {
                    var cand2 = cand1.Insert(i, c.ToString());
                    candidate2Seen++;
                    if (_affixManager.CanSuggestWord(cand2))
                    {
                        TryAddSuggestion(suggestions, seenSuggestions, cand2);
                        if (suggestions.Count >= 10) break;
                    }
                    if (candidate2Seen >= candidate2Cap) break;
                }
            }

            for (int i = 0; i < cand1.Length - 1 && candidate2Seen < candidate2Cap && suggestions.Count < 10; i++)
            {
                var cand2 = string.Create(cand1.Length, (cand1, i), static (span, state) =>
                {
                    state.cand1.AsSpan().CopyTo(span);
                    (span[state.i], span[state.i + 1]) = (span[state.i + 1], span[state.i]);
                });
                candidate2Seen++;
                if (_affixManager.CanSuggestWord(cand2))
                {
                    TryAddSuggestion(suggestions, seenSuggestions, cand2);
                    if (suggestions.Count >= 10) break;
                }
            }
        }

        if (suggestions.Count < 10)
        {
            var wordCount = _hashManager.WordCount;
            const int MaxScan = 2000;
            if (wordCount <= 20000)
            {
                var words = _hashManager.GetAllWords();
                int scanned = 0;
                int maxDist = word.Length <= 3 ? 3 : 2;
                foreach (var w in words)
                {
                    if (suggestions.Count >= 10 || scanned >= MaxScan) break;
                    scanned++;
                    if (string.Equals(w, word, StringComparison.OrdinalIgnoreCase)) continue;
                    int d = BoundedLevenshtein(word, w, maxDist);
                    if (d >= 0 && _affixManager.CanSuggestWord(w))
                    {
                        TryAddSuggestion(suggestions, seenSuggestions, w);
                    }
                }
            }
        }
    }

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

    private void GenerateSubstitutionSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        var tryChars = _affixManager.TryCharacters;

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

                if (_affixManager.CanSuggestWord(suggestion))
                {
                    TryAddSuggestion(suggestions, seenSuggestions, suggestion);
                }
            }
        }
    }

    private void GenerateInsertionSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        var tryChars = _affixManager.TryCharacters;

        for (int i = 0; i <= word.Length; i++)
        {
            foreach (var c in tryChars)
            {
                var suggestion = word.Insert(i, c.ToString());

                if (_affixManager.CanSuggestWord(suggestion))
                {
                    TryAddSuggestion(suggestions, seenSuggestions, suggestion);
                }
            }
        }
    }

    private void GenerateDeletionSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        for (int i = 0; i < word.Length; i++)
        {
            var suggestion = word.Remove(i, 1);

            if (_affixManager.CanSuggestWord(suggestion))
            {
                TryAddSuggestion(suggestions, seenSuggestions, suggestion);
            }
        }
    }

    private void GenerateSwapSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        for (int i = 0; i < word.Length - 1; i++)
        {
            var suggestion = string.Create(word.Length, (word, i), static (span, state) =>
            {
                state.word.AsSpan().CopyTo(span);
                (span[state.i], span[state.i + 1]) = (span[state.i + 1], span[state.i]);
            });

            if (_affixManager.CanSuggestWord(suggestion))
            {
                TryAddSuggestion(suggestions, seenSuggestions, suggestion);
            }
        }
    }

    private void GenerateRepSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        // For very large dictionaries the affix-derivation check
        // (CheckAffixedWord -> TryFindAffixBase) is expensive and can become
        // pathological for borderline candidates (e.g. Swedish sv_FI with
        // ~150k words, ~492 SFX/PFX rules, COMPOUNDMIN 1). In that regime,
        // restrict REP-derived candidates to those present directly in the
        // dictionary; this preserves the most useful REP suggestions while
        // avoiding the slow affix walk per candidate.
        bool useAffixCheck = _hashManager.WordCount <= 50_000;

        if (_affixManager.RepTable.Count > 0)
        {
            foreach (var (from, to) in _affixManager.RepTable)
            {
                if (string.IsNullOrEmpty(from)) continue;

                int start = 0;
                while ((start = word.IndexOf(from, start, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var candidate = word.Substring(0, start) + to + word.Substring(start + from.Length);
                    if (IsReplacementCandidate(candidate, useAffixCheck))
                    {
                        if (TryAddSuggestion(suggestions, seenSuggestions, candidate) && suggestions.Count >= 10) return;
                    }

                    start += 1;
                }
            }
        }

        try
        {
            foreach (var candidate in _hashManager.GetPhReplacementCandidates(word))
            {
                if (suggestions.Count >= 10) break;
                if (IsReplacementCandidate(candidate, useAffixCheck))
                {
                    if (TryAddSuggestion(suggestions, seenSuggestions, candidate) && suggestions.Count >= 10) break;
                }
            }
        }
        catch
        {
        }
    }

    private bool IsReplacementCandidate(string candidate, bool allowAffixes) =>
        _affixManager.CanSuggestWord(candidate, allowAffixes) ||
        (candidate.Contains(' ') && candidate.Split(' ').All(part => _affixManager.CanSuggestWord(part, allowAffixes)));

    private bool GenerateSplitSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        if (word.Length < 3) return false;

        bool allowAffixes = _hashManager.WordCount <= 50_000;
        bool allowDash = _affixManager.SuggestHyphenatedSplits;
        var listedPairs = new List<string>();
        var splitPairs = new List<string>();
        for (int i = 1; i < word.Length; i++)
        {
            if (char.IsLowSurrogate(word[i])) continue;

            var left = word[..i];
            var right = word[i..];
            var spaced = left + ' ' + right;
            var hyphenated = allowDash ? left + '-' + right : null;
            if (_affixManager.CanSuggestWord(spaced, allowAffixes))
                listedPairs.Add(spaced);
            if (hyphenated is not null && _affixManager.CanSuggestWord(hyphenated, allowAffixes))
                listedPairs.Add(hyphenated);

            if (_affixManager.NoSplitSuggestions || listedPairs.Count > 0) continue;
            if (_affixManager.CanSuggestWord(left, allowAffixes) && _affixManager.CanSuggestWord(right, allowAffixes))
            {
                splitPairs.Add(spaced);
                if (hyphenated is not null && left.Length > 1 && right.Length > 1)
                    splitPairs.Add(hyphenated);
            }
        }

        if (listedPairs.Count > 0)
        {
            suggestions.Clear();
            listedPairs.Reverse();
            suggestions.AddRange(listedPairs.Take(10));
            return true;
        }

        foreach (var pair in splitPairs)
        {
            if (suggestions.Count >= 10) break;
            TryAddSuggestion(suggestions, seenSuggestions, pair);
        }
        return false;
    }

    private void GeneratePossessiveSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        if (word.Length <= 2) return;
        if (!word.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return;

        var stem = word.Substring(0, word.Length - 1);
        if (_affixManager.CanSuggestWord(stem))
        {
            var cand = stem + "'s";
            TryAddSuggestion(suggestions, seenSuggestions, cand);
        }

        if (_affixManager.FullStrip)
        {
            if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
            {
                var stemEs = word.Substring(0, word.Length - 2);
                if (_affixManager.CanSuggestWord(stemEs))
                {
                    var cand2 = stemEs + "'s";
                    TryAddSuggestion(suggestions, seenSuggestions, cand2);
                }
            }

            if (_affixManager.CanSuggestWord(stem))
            {
                TryAddSuggestion(suggestions, seenSuggestions, stem);
            }
        }
    }
}
