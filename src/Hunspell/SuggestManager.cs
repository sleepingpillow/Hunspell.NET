// Copyright (C) 2025 Hunspell.NET Contributors
// This file is part of Hunspell.NET.
// Licensed under MPL 1.1/GPL 2.0/LGPL 2.1

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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

        GenerateSubstitutionSuggestions(word, suggestions, seenSuggestions);
        GenerateInsertionSuggestions(word, suggestions, seenSuggestions);
        GenerateDeletionSuggestions(word, suggestions, seenSuggestions);
        GenerateSwapSuggestions(word, suggestions, seenSuggestions);
        GenerateRepSuggestions(word, suggestions, seenSuggestions);

        if (!_affixManager.NoSplitSuggestions)
        {
            GenerateSplitSuggestions(word, suggestions, seenSuggestions);
        }

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

    public IEnumerable<string> GenerateIconvCandidates(string word)
    {
        if (string.IsNullOrEmpty(word) || _affixManager.IconvTable.Count == 0) yield break;

        var converted = ApplyIconv(word);
        if (!string.Equals(converted, word, StringComparison.Ordinal))
        {
            yield return converted;
        }
    }

    private string ApplyIconv(string word)
    {
        var converted = new StringBuilder(word.Length);

        for (int index = 0; index < word.Length;)
        {
            (string from, string to)? bestMatch = null;

            foreach (var rule in _affixManager.IconvTable)
            {
                if (string.IsNullOrEmpty(rule.from) || index + rule.from.Length > word.Length)
                {
                    continue;
                }

                if (!word.AsSpan(index, rule.from.Length).SequenceEqual(rule.from))
                {
                    continue;
                }

                if (bestMatch is null || rule.from.Length > bestMatch.Value.from.Length)
                {
                    bestMatch = rule;
                }
            }

            if (bestMatch is { } match)
            {
                converted.Append(match.to);
                index += match.from.Length;
            }
            else
            {
                converted.Append(word[index]);
                index++;
            }
        }

        return converted.ToString();
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

            if (_hashManager.Lookup(cand1))
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
                    if (_hashManager.Lookup(cand2))
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
                if (_hashManager.Lookup(cand2))
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
                    if (_hashManager.Lookup(cand2))
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
                if (_hashManager.Lookup(cand2))
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
                    if (d >= 0 && d <= maxDist)
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

                if (_hashManager.Lookup(suggestion))
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

                if (_hashManager.Lookup(suggestion))
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

            if (_hashManager.Lookup(suggestion))
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

            if (_hashManager.Lookup(suggestion))
            {
                TryAddSuggestion(suggestions, seenSuggestions, suggestion);
            }
        }
    }

    private void GenerateRepSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        if (_affixManager.RepTable.Count > 0)
        {
            foreach (var (from, to) in _affixManager.RepTable)
            {
                if (string.IsNullOrEmpty(from)) continue;

                int start = 0;
                while ((start = word.IndexOf(from, start, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    var candidate = word.Substring(0, start) + to + word.Substring(start + from.Length);
                    if (_hashManager.Lookup(candidate) || _affixManager.CheckAffixedWord(candidate))
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
                if (_hashManager.Lookup(candidate) || _affixManager.CheckAffixedWord(candidate))
                {
                    if (TryAddSuggestion(suggestions, seenSuggestions, candidate) && suggestions.Count >= 10) break;
                }
            }
        }
        catch
        {
        }
    }

    private void GenerateSplitSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        if (word.Contains(' ')) return;

        for (int i = 1; i < word.Length && suggestions.Count < 10; i++)
        {
            var left = word.Substring(0, i);
            var right = word.Substring(i);

            if (_hashManager.Lookup(left) && _hashManager.Lookup(right))
            {
                var candidate = left + " " + right;
                TryAddSuggestion(suggestions, seenSuggestions, candidate);
            }

            if (right.Contains('"') || left.Contains('"'))
            {
                var candidate2 = word.Replace('"', ' ');
                var parts = candidate2.Split(' ');
                if (parts.Length == 2 && _hashManager.Lookup(parts[0]) && _hashManager.Lookup(parts[1]))
                {
                    TryAddSuggestion(suggestions, seenSuggestions, candidate2);
                }
            }
        }

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
                    TryAddSuggestion(suggestions, seenSuggestions, candidate);
                }
            }
        }

        for (int i = 1; i < word.Length && suggestions.Count < 10; i++)
        {
            var left = word.Substring(0, i);
            var right = word.Substring(i);

            var leftCand = GetSingleWordCandidates(left, 12).ToList();
            var rightCand = GetSingleWordCandidates(right, 12).ToList();

            if (!leftCand.Contains(left)) leftCand.Insert(0, left);
            if (!rightCand.Contains(right)) rightCand.Insert(0, right);

            foreach (var lc in leftCand)
            {
                if (suggestions.Count >= 10) break;
                foreach (var rc in rightCand)
                {
                    if (suggestions.Count >= 10) break;

                    if (string.IsNullOrEmpty(lc) || string.IsNullOrEmpty(rc)) continue;
                    var phrase = lc + " " + rc;
                    if (_hashManager.Lookup(lc) && _hashManager.Lookup(rc))
                    {
                        TryAddSuggestion(suggestions, seenSuggestions, phrase);
                        if (suggestions.Count >= 10) break;
                    }

                    var concat = lc + rc;
                    if (_hashManager.Lookup(concat))
                    {
                        TryAddSuggestion(suggestions, seenSuggestions, concat);
                        if (suggestions.Count >= 10) break;
                    }
                }
            }

            if (suggestions.Count < 10 && _hashManager.Lookup(left))
            {
                if (right.Length <= 4 || right.EndsWith("s", StringComparison.OrdinalIgnoreCase) || right.Contains('"'))
                {
                    TryAddSuggestion(suggestions, seenSuggestions, left);
                }
            }

            if (suggestions.Count < 10 && _hashManager.Lookup(left))
            {
                var normalizedRight = right;
                foreach (var (from, to) in _affixManager.RepTable)
                {
                    if (string.IsNullOrEmpty(from)) continue;
                    normalizedRight = Regex.Replace(normalizedRight, Regex.Escape(from), to, RegexOptions.IgnoreCase);
                }

                if (!string.Equals(normalizedRight, right, StringComparison.OrdinalIgnoreCase)
                    && _hashManager.Lookup(normalizedRight)
                    && _hashManager.Lookup("e"))
                {
                    TryAddSuggestion(suggestions, seenSuggestions, left + " e " + normalizedRight);
                }
            }
        }
    }

    private void GeneratePossessiveSuggestions(string word, List<string> suggestions, HashSet<string> seenSuggestions)
    {
        if (word.Length <= 2) return;
        if (!word.EndsWith("s", StringComparison.OrdinalIgnoreCase)) return;

        var stem = word.Substring(0, word.Length - 1);
        if (_hashManager.Lookup(stem))
        {
            var cand = stem + "'s";
            TryAddSuggestion(suggestions, seenSuggestions, cand);
        }

        if (_affixManager.FullStrip)
        {
            if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) && word.Length > 2)
            {
                var stemEs = word.Substring(0, word.Length - 2);
                if (_hashManager.Lookup(stemEs))
                {
                    var cand2 = stemEs + "'s";
                    TryAddSuggestion(suggestions, seenSuggestions, cand2);
                }
            }

            if (_hashManager.Lookup(stem))
            {
                TryAddSuggestion(suggestions, seenSuggestions, stem);
            }
        }
    }

    private IEnumerable<string> GetSingleWordCandidates(string part, int cap = 20)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(part)) yield break;

        if (_hashManager.Lookup(part) && seen.Add(part)) yield return part;

        var tryChars = _affixManager.TryCharacters;

        var normalizedPart = part.Normalize(NormalizationForm.FormC);
        foreach (var (from, to) in _affixManager.RepTable)
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

        for (int i = 0; i < part.Length; i++)
        {
            var cand = part.Remove(i, 1);
            if (seen.Add(cand) && _hashManager.Lookup(cand))
            {
                yield return cand;
                if (seen.Count >= cap) yield break;
            }
        }

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

        if (seen.Count < cap)
        {
            int maxDist = part.Length <= 3 ? 3 : 2;
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
}
