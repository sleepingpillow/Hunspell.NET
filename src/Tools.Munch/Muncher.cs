using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Tools.Munch
{
    public class MunchResult
    {
        public int KeptCount { get; set; }
        public List<string> Lines { get; } = new List<string>();
    }

    public class Muncher
    {
        private class WordEntry
        {
            public string Word { get; set; } = string.Empty;
            public string Flags { get; set; } = string.Empty;
            public bool HasFlag(char f) => !string.IsNullOrEmpty(Flags) && Flags.IndexOf(f) >= 0;
            public bool HasAnyFlag(IEnumerable<char> chars) => !string.IsNullOrEmpty(Flags) && Flags.Any(c => chars.Contains(c));
        }

        private static (string baseWord, string? flags) ParseDicLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return (string.Empty, null);
            var idx = line.LastIndexOf('/');
            if (idx <= 0 || idx >= line.Length - 1) return (line, null);
            // treat suffix after last '/' as flags if it doesn't contain whitespace
            var after = line.Substring(idx + 1);
            if (after.IndexOfAny(new char[] { ' ', '\t' }) >= 0) return (line, null);
            var baseWord = line.Substring(0, idx);
            return (baseWord, after);
        }
        /// <summary>
        /// Run munch on a word list + affix set.
        /// This is a pragmatic, minimal port which aims to create a compact root-word+affix representation
        /// sufficient for many dictionaries. It does not attempt to replicate every corner-case of the
        /// original C++ munch tool but follows the common remove/append pattern for PFX/SFX entries.
        /// </summary>
        public Task<MunchResult> RunAsync(string wordListPath, AffixSet aff)
        {
            var allLines = File.ReadAllLines(wordListPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#"))
                .ToArray();

            // hunspell's munch originally expects a special first-line tablesize when operating in conjunction
            // with other tools, but we accept normal word-lists. If the first line is numeric we skip it.
            int startIndex = 0;
            if (allLines.Length > 0 && int.TryParse(allLines[0], out var _))
                startIndex = 1;

            var words = allLines.Skip(startIndex).ToList();

            // create word entries mapping base word -> flags
            var wordEntries = new List<WordEntry>();
            foreach (var line in words)
            {
                var (baseWord, flags) = ParseDicLine(line);
                wordEntries.Add(new WordEntry { Word = baseWord, Flags = flags ?? string.Empty });
            }

            var wordMap = new Dictionary<string, WordEntry>(StringComparer.Ordinal);
            foreach (var we in wordEntries)
            {
                if (!wordMap.ContainsKey(we.Word))
                    wordMap[we.Word] = we;
                else if (!string.IsNullOrEmpty(we.Flags))
                    wordMap[we.Word].Flags += we.Flags; // merge flags if duplicates exist
            }

            var baseWordSet = new HashSet<string>(wordMap.Keys, StringComparer.Ordinal);

            // Track which words can be generated from another root (and thus might be removable)
            var keep = words.ToDictionary(w => w, w => true);

            // Track affixes recorded for roots
            var affMap = new Dictionary<string, HashSet<char>>(StringComparer.Ordinal);

            // --- Upstream-style candidate collection and validation ---
            // For each word, collect all possible root candidates (via prefix/suffix/cross-product)
            var rootCandidates = new Dictionary<string, List<(string root, string affixType, char affixFlag)>>(StringComparer.Ordinal);
            foreach (var w in words)
            {
                var candidates = new List<(string root, string affixType, char affixFlag)>();
                // Suffix roots
                foreach (var sx in aff.Suffixes)
                {
                    foreach (var ent in sx.Entries)
                    {
                        var app = ent.Appnd ?? string.Empty;
                        if (app.Length > 0 && !w.EndsWith(app, StringComparison.Ordinal))
                            continue;
                        int tlen = w.Length - app.Length;
                        if (tlen <= 0) continue;
                        if (tlen + (ent.Strip?.Length ?? 0) < ent.NumConds) continue;
                        var stripped = w.Substring(0, tlen);
                        if (!string.IsNullOrEmpty(ent.Strip))
                            stripped = stripped + ent.Strip;
                        bool condOk = true;
                        for (int condIndex = 0; condIndex < ent.NumConds; condIndex++)
                        {
                            int pos = stripped.Length - ent.NumConds + condIndex;
                            if (pos < 0 || pos >= stripped.Length) { condOk = false; break; }
                            var ch = (byte)stripped[pos];
                            var mask = ent.ConditionsMask[ch];
                            if ((mask & (1 << condIndex)) == 0) { condOk = false; break; }
                        }
                        if (!condOk) continue;
                        if (baseWordSet.Contains(stripped))
                        {
                            candidates.Add((stripped, "suffix", sx.Flag));
                        }
                    }
                }
                // Prefix roots
                foreach (var px in aff.Prefixes)
                {
                    foreach (var ent in px.Entries)
                    {
                        var app = ent.Appnd ?? string.Empty;
                        if (app.Length > 0 && !w.StartsWith(app, StringComparison.Ordinal))
                            continue;
                        int tlen = w.Length - app.Length;
                        if (tlen <= 0) continue;
                        if (tlen + (ent.Strip?.Length ?? 0) < ent.NumConds) continue;
                        var stripped = w.Substring(app.Length);
                        if (!string.IsNullOrEmpty(ent.Strip))
                            stripped = ent.Strip + stripped;
                        bool condOk = true;
                        for (int condIndex = 0; condIndex < ent.NumConds; condIndex++)
                        {
                            int pos = condIndex;
                            if (pos < 0 || pos >= stripped.Length) { condOk = false; break; }
                            var ch = (byte)stripped[pos];
                            var mask = ent.ConditionsMask[ch];
                            if ((mask & (1 << condIndex)) == 0) { condOk = false; break; }
                        }
                        if (!condOk) continue;
                        if (baseWordSet.Contains(stripped))
                        {
                            candidates.Add((stripped, "prefix", px.Flag));
                        }
                    }
                }
                // Cross-product roots
                if (aff.Prefixes.Count > 0 && aff.Suffixes.Count > 0)
                {
                    foreach (var px in aff.Prefixes)
                    {
                        foreach (var pent in px.Entries)
                        {
                            if (!pent.XProduct) continue;
                            var papp = pent.Appnd ?? string.Empty;
                            if (papp.Length > 0 && !w.StartsWith(papp, StringComparison.Ordinal)) continue;
                            foreach (var sx in aff.Suffixes)
                            {
                                foreach (var sent in sx.Entries)
                                {
                                    if (!sent.XProduct) continue;
                                    var sapp = sent.Appnd ?? string.Empty;
                                    if (sapp.Length > 0 && !w.EndsWith(sapp, StringComparison.Ordinal)) continue;
                                    var midStart = papp.Length;
                                    var midLen = w.Length - papp.Length - sapp.Length;
                                    if (midLen <= 0) continue;
                                    var mid = w.Substring(midStart, midLen);
                                    var candidateRoot = (string.IsNullOrEmpty(pent.Strip) ? string.Empty : pent.Strip) + mid + (string.IsNullOrEmpty(sent.Strip) ? string.Empty : sent.Strip);
                                    bool condOk = true;
                                    if (pent.NumConds > 0)
                                    {
                                        if (candidateRoot.Length < pent.NumConds) continue;
                                        for (int ci = 0; ci < pent.NumConds; ci++)
                                        {
                                            var ch = (byte)candidateRoot[ci];
                                            if ((pent.ConditionsMask[ch] & (1 << ci)) == 0) { condOk = false; break; }
                                        }
                                    }
                                    if (!condOk) continue;
                                    if (sent.NumConds > 0)
                                    {
                                        if (candidateRoot.Length < sent.NumConds) continue;
                                        for (int ci = 0; ci < sent.NumConds; ci++)
                                        {
                                            int pos = candidateRoot.Length - sent.NumConds + ci;
                                            var ch = (byte)candidateRoot[pos];
                                            if ((sent.ConditionsMask[ch] & (1 << ci)) == 0) { condOk = false; break; }
                                        }
                                    }
                                    if (!condOk) continue;
                                    if (baseWordSet.Contains(candidateRoot))
                                    {
                                        // For cross-product, add both prefix and suffix flags
                                        candidates.Add((candidateRoot, "cross-prefix", px.Flag));
                                        candidates.Add((candidateRoot, "cross-suffix", sx.Flag));
                                    }
                                }
                            }
                        }
                    }
                }
                if (candidates.Count > 0)
                    rootCandidates[w] = candidates;
            }

            // Now, for each word with candidates, validate and mark removable
            foreach (var kvp in rootCandidates)
            {
                var w = kvp.Key;
                var candidates = kvp.Value;
                
                // For each candidate, check if root exists and respects only-in-compound flags
                foreach (var cand in candidates)
                {
                    if (!baseWordSet.Contains(cand.root))
                        continue;
                    
                    // Check if root has only-in-compound flag
                    var rootEntry = wordMap[cand.root];
                    if (!string.IsNullOrEmpty(rootEntry.Flags) && aff.OnlyInCompound.Overlaps(rootEntry.Flags.ToCharArray()))
                        continue; // root is only valid inside compounds, skip
                    
                    // This candidate is valid, mark word removable and record affix
                    keep[w] = false;
                    if (!affMap.TryGetValue(cand.root, out var hs))
                    {
                        hs = new HashSet<char>();
                        affMap[cand.root] = hs;
                    }
                    hs.Add(cand.affixFlag);
                }
            }

            // Check compound words: try to build from existing base words
            foreach (var w in words)
            {
                // If still not removed by affix matching, attempt to see if the word is a compound of existing base words
                if (keep[w])
                {
                    var (baseWord, _) = ParseDicLine(w);
                    if (IsCompoundThatCanBeBuilt(baseWord, aff, wordMap, baseWordSet))
                    {
                        keep[w] = false;
                    }
                }

                // --- helper methods local to RunAsync
                bool IsCompoundThatCanBeBuilt(string word, AffixSet affset, Dictionary<string, WordEntry> wmap, HashSet<string> baseSet)
                {
                    if (string.IsNullOrEmpty(word)) return false;

                    int maxParts = 4; // reasonable cap
                    for (int parts = 2; parts <= maxParts; parts++)
                    {
                        if (TrySplit(0, parts)) return true;
                    }
                    return false;

                    bool TrySplit(int startIndex, int remainingParts, List<string>? partsStack = null)
                    {
                        partsStack ??= new List<string>();
                        if (remainingParts == 1)
                        {
                            var piece = word.Substring(startIndex);
                            if (!IsValidComponent(piece)) return false;
                            partsStack.Add(piece);
                            // validate assembled parts
                            var assembled = partsStack.ToArray();
                            var valid = ValidateComponentSequence(assembled, affset, baseSet, wmap);
                            partsStack.RemoveAt(partsStack.Count - 1);
                            return valid;
                        }

                        int min = affset.CompoundMin;
                        for (int i = startIndex + min; i <= word.Length - min - (remainingParts - 2) * min; i++)
                        {
                            var piece = word.Substring(startIndex, i - startIndex);
                            if (!IsValidComponent(piece))
                            {
                                // Try simplified triple heuristic if configured: shift one char over if letters repeat
                                if (affset.CheckCompoundTriple && affset.SimplifiedTriple && i < word.Length && i - 1 >= startIndex)
                                {
                                    if (word[i - 1] == word[i])
                                    {
                                        // try shifting boundary by one
                                        var shifted = word.Substring(startIndex, i - startIndex + 1);
                                        if (!IsValidComponent(shifted)) continue;
                                        if (TrySplit(i + 1, remainingParts - 1)) return true;
                                    }
                                }
                                continue;
                            }

                            partsStack.Add(piece);
                            if (TrySplit(i, remainingParts - 1, partsStack)) return true;
                            partsStack.RemoveAt(partsStack.Count - 1);
                        }
                        return false;
                    }

                    bool IsValidComponent(string component)
                    {
                        if (string.IsNullOrEmpty(component)) return false;
                        if (!baseSet.Contains(component)) return false;

                        // Check only-in-compound / other flags — components must be allowed in compound positions
                        // For this heuristic we accept components that exist. Additional checks (begin/middle/end) are done in MatchesCompoundRules stage below.
                        return true;
                    }

                    bool ValidateComponentSequence(string[] parts, AffixSet affset, HashSet<string> baseSet, Dictionary<string, WordEntry> wmap)
                    {
                        // Check duplication
                        if (affset.CheckCompoundDup)
                        {
                            for (int i = 1; i < parts.Length; i++) if (parts[i] == parts[i-1]) return false;
                        }

                        // Build token sequence
                        var tokens = new List<char>();
                        foreach (var p in parts)
                        {
                                if (string.IsNullOrEmpty(p)) return false;

                                char tok = GetCompoundTokenForComponent(p, wmap.TryGetValue(p, out var e) ? e : null, affset);
                                tokens.Add(tok);
                        }

                        var tokenStr = new string(tokens.ToArray());

                        // If there are specific rules, attempt matching them
                        if (affset.CompoundRules != null && affset.CompoundRules.Count > 0)
                        {
                            foreach (var rule in affset.CompoundRules)
                            {
                                var pattern = BuildRegexFromCompoundRule(rule);
                                try
                                {
                                    if (System.Text.RegularExpressions.Regex.IsMatch(tokenStr, pattern))
                                        return true;
                                }
                                catch { /* if regex fails, skip policy */ }
                            }
                            return false; // no rule matched
                        }

                        // CHECKCOMPOUNDPATTERN handling: check specific forbidden boundary patterns
                        if (affset.CompoundPatterns != null && affset.CompoundPatterns.Count > 0)
                        {
                            for (int b = 0; b + 1 < parts.Length; b++)
                            {
                                var prev = parts[b];
                                var cur = parts[b + 1];
                                foreach (var pattern in affset.CompoundPatterns)
                                {
                                    if (string.IsNullOrEmpty(pattern.EndChars) || string.IsNullOrEmpty(pattern.BeginChars))
                                        continue;
                                    if (!prev.EndsWith(pattern.EndChars, StringComparison.Ordinal)) continue;
                                    if (!cur.StartsWith(pattern.BeginChars, StringComparison.Ordinal)) continue;

                                    // if end/begin flags are specified, check entry flags
                                    if (pattern.EndFlag != null)
                                    {
                                        if (!wmap.TryGetValue(prev, out var prevEntry) || string.IsNullOrEmpty(prevEntry.Flags) || prevEntry.Flags.IndexOf(pattern.EndFlag.Value) < 0)
                                            continue; // flag not present -> pattern doesn't match
                                    }
                                    if (pattern.BeginFlag != null)
                                    {
                                        if (!wmap.TryGetValue(cur, out var curEntry) || string.IsNullOrEmpty(curEntry.Flags) || curEntry.Flags.IndexOf(pattern.BeginFlag.Value) < 0)
                                            continue; // flag not present -> pattern doesn't match
                                    }

                                    // matched pattern -> forbid composition
                                    return false;
                                }
                            }
                        }

                        // COMPOUNDREP handling: if enabled, disallow compounds where replacing
                        // a boundary substring (spanning the parts) with a REP target would create
                        // an existing word (i.e., the compound is 'very similar' to an existing word).
                        if (affset.CheckCompoundRep && affset.Replacements != null && affset.Replacements.Count > 0)
                        {
                            for (int i = 0; i + 1 < parts.Length; i++)
                            {
                                var left = parts[i];
                                var right = parts[i + 1];
                                var concat = left + right;
                                foreach (var (from, to) in affset.Replacements)
                                {
                                    if (string.IsNullOrEmpty(from)) continue;
                                    // handle anchors ^ and $
                                    var anchoredStart = from.StartsWith("^") ? true : false;
                                    var anchoredEnd = from.EndsWith("$") ? true : false;
                                    var rawFrom = from.Trim('^', '$');

                                    // find occurrences of rawFrom in concat
                                    int idx = 0;
                                    while ((idx = concat.IndexOf(rawFrom, idx, StringComparison.Ordinal)) >= 0)
                                    {
                                        // ensure occurrence spans the boundary
                                        if (idx < left.Length && idx + rawFrom.Length > left.Length)
                                        {
                                            if (anchoredStart && idx != 0) { idx++; continue; }
                                            if (anchoredEnd && (idx + rawFrom.Length) != concat.Length) { idx++; continue; }

                                            var replaced = concat.Substring(0, idx) + to + concat.Substring(idx + rawFrom.Length);
                                            if (baseSet.Contains(replaced))
                                            {
                                                // disallow this compound unless the replaced word is only valid inside compounds
                                                if (wmap.TryGetValue(replaced, out var repEntry))
                                                {
                                                    if (!string.IsNullOrEmpty(repEntry.Flags) && affset.OnlyInCompound.Overlaps(repEntry.Flags.ToCharArray()))
                                                    {
                                                        // replacement target is only valid inside compounds -> do NOT disallow
                                                    }
                                                    else
                                                    {
                                                        return false;
                                                    }
                                                }
                                                else
                                                {
                                                    // no entry metadata -> disallow
                                                    return false;
                                                }
                                            }
                                        }
                                        idx++;
                                    }
                                }
                            }
                        }

                        return true; // no restriction -> accept
                    }

                        char GetCompoundTokenForComponent(string component, WordEntry? entry, AffixSet affset)
                        {
                            // digits-only -> '1'
                            if (component.All(c => char.IsDigit(c))) return '1';

                            // contains any non-letter -> treat as '0' unless it's mixed digits+letters -> '3'
                            // Detect numeric middle patterns like 12-34 -> class '3'
                            if (System.Text.RegularExpressions.Regex.IsMatch(component, "^\\d+[\\-:\\.]\\d+$")) return '3';
                            bool hasLetter = component.Any(c => char.IsLetter(c));
                            bool hasDigit = component.Any(c => char.IsDigit(c));
                            if (hasDigit && hasLetter) return '3';
                            if (!hasLetter) return '0';

                            // now it's letter-only (or letter with diacritics); normalize case
                            var lower = component.ToLowerInvariant();

                            // attempt to parse spelled number value (if any)
                            if (TryParseSpelledNumber(lower, out var value, out var isOrdinal))
                            {
                                if (isOrdinal)
                                    return '7'; // ordinal class

                                // numeric classes based on value
                                if (value >= 1000) return '5';
                                if (value >= 100 && value % 100 == 0) return '4';
                                if (value >= 1 && value <= 100) return '2';
                                // fallback spelled-number class
                                return '2';
                            }

                            // ordinals (första, andra, tredje, fjärde, femte, sjätte, sjunde, åttonde, nionde, tionde)
                            var ords = new[] { "först", "andra", "tredj", "fjärd", "femt", "sjätt", "sjut", "åtton", "niond", "tiond" };
                            if (ords.Any(o => lower.Contains(o))) return '7';

                            // numeric suffix heuristics (e.g., 'år', 'års', 'tals') -> 6
                            var suffixes = new[] { "år", "års", "åring", "tals", "tal" };
                            if (suffixes.Any(s => lower.EndsWith(s))) return '6';

                            // spelled-number basic detection (units, tens, teens)
                            if (IsSpelledNumber(lower)) return '2';

                            // If entry has flags that indicate compound role, prefer those flags (A..Z)
                            if (entry != null && !string.IsNullOrEmpty(entry.Flags))
                            {
                                // prefer begin, middle, end, permit, onlyincompound
                                var chosen = entry.Flags.FirstOrDefault(ch => affset.CompoundBegin.Contains(ch) || affset.CompoundMiddle.Contains(ch) || affset.CompoundEnd.Contains(ch) || affset.CompoundPermitFlag.Contains(ch) || affset.OnlyInCompound.Contains(ch));
                                if (chosen != '\0') return chosen;
                            }

                            // default letter-only token
                            return 'x';
                        }

                        bool IsSpelledNumber(string s)
                        {
                            // very small Swedish-number lexicon to detect spelled numbers roughly
                            var tokens = new[] { "noll", "en", "ett", "två", "tre", "fyra", "fem", "sex", "sju", "åtta", "nio",
                                "tio", "elva", "tolv", "treton", "fjorton", "femton", "sexton", "sjutton", "arton", "nitton", "tjugo",
                                "trettio", "fyrtio", "femtio", "sextio", "sjuttio", "åttio", "nittio", "hundra", "tusen", "miljon", "miljoner", "miljard", "miljarder", "och" };

                            int idx = 0;
                            while (idx < s.Length)
                            {
                                bool matched = false;
                                foreach (var t in tokens.OrderByDescending(t => t.Length))
                                {
                                    if (s.Substring(idx).StartsWith(t, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        idx += t.Length;
                                        matched = true;
                                        break;
                                    }
                                }
                                if (!matched) return false;
                            }
                            return true;
                        }

                    string BuildRegexFromCompoundRule(string rule)
                    {
                        // Build regex that matches our token stream (each component mapped to single token)
                        // Our token alphabet: '1' (digits-only), '0' (non-letter), 'x' (letter-only, no flags),
                        // uppercase flag letters (A..Z). COMPOUNDRULE tokens may include digits (0..9),
                        // special grouped tokens like '>j' or ')k' meaning a specific flag j/k.
                        var sb = new System.Text.StringBuilder();
                        sb.Append('^');
                        for (int i = 0; i < rule.Length; i++)
                        {
                            char c = rule[i];
                            if (c == '?' || c == '*' || c == '+')
                            {
                                // apply quantifier to previous token
                                sb.Append(c);
                                continue;
                            }

                            // handle two-char tokens: >x or )x  -> match the specific flag token afterwards
                            if ((c == '>' || c == ')') && i + 1 < rule.Length)
                            {
                                char flag = rule[i + 1];
                                sb.Append(System.Text.RegularExpressions.Regex.Escape(char.ToUpperInvariant(flag).ToString()));
                                i++; // skip flag char
                                continue;
                            }

                            // '-' in rule means dash token in hunspell -> treat as '0' (non-letter)
                            if (c == '-')
                            {
                                sb.Append('0');
                                continue;
                            }

                            // digits are literal token characters in our token stream (1..9, 0)
                            if (char.IsDigit(c))
                            {
                                sb.Append(c);
                                continue;
                            }

                            // Letters in rule are usually flags (A..Z) — match exact flag token
                            if (char.IsLetter(c))
                            {
                                // treat upper and lower uniformly; flags are generally uppercase in our token stream
                                sb.Append(System.Text.RegularExpressions.Regex.Escape(char.ToUpperInvariant(c).ToString()));
                                continue;
                            }

                            // fallback: escape char
                            sb.Append(System.Text.RegularExpressions.Regex.Escape(c.ToString()));
                        }
                        sb.Append('$');
                        return sb.ToString();
                    }
                }

                    bool TryParseSpelledNumber(string input, out int value, out bool isOrdinal)
                    {
                        value = 0;
                        isOrdinal = false;
                        if (string.IsNullOrEmpty(input)) return false;

                        var s = input.Replace("-", "").Replace(" ", "");
                        s = s.ToLowerInvariant();

                        // quick ordinal detection and normalize common ordinal suffixes
                        // When an ordinal suffix is present (e.g. 'första', 'andra', 'tusende', 'hundrade')
                        // replace it by its cardinal equivalent so the numeric parser can compute the base value
                        var ordSuffixMap = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
                        {
                            // unit ordinals -> units
                            {"första", "en"}, {"först", "en"}, {"andra", "två"}, {"tredje", "tre"}, {"fjärde", "fyra"}, {"femte", "fem"}, {"sjätte", "sex"}, {"sjunde", "sju"}, {"åttonde", "åtta"}, {"åtton", "åtta"}, {"nionde", "nio"}, {"tionde", "tio"},
                            // multiples / scale ordinals -> map back to cardinal scale form
                            {"hundrade", "hundra"}, {"tusende", "tusen"}, {"miljonte", "miljon"}
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

                        // dictionaries of tokens
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

                            // scales
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
            }

            // For words that are not marked removable and for roots we collected affix data for, output them
            var output = new List<string>();
            foreach (var w in words)
            {
                if (!keep.TryGetValue(w, out var k) || k)
                {
                    // check if we have affs
                    if (affMap.TryGetValue(w, out var hs) && hs.Count > 0)
                    {
                        var s = new string(hs.OrderBy(c => c).ToArray());
                        output.Add($"{w}/{s}");
                    }
                    else
                    {
                        output.Add(w);
                    }
                }
            }

            var res = new MunchResult { KeptCount = output.Count };
            res.Lines.AddRange(output);

            return Task.FromResult(res);
        }
    }
}
