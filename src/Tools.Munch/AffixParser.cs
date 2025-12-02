using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tools.Munch
{
    // Minimal affix representation used by the munch port.
    public class AffEntry
    {
        public string Strip { get; set; } = string.Empty; // what to put back on the root when reversing
        public string Appnd { get; set; } = string.Empty; // what is appended in derived form
        // conditions are stored as bitmasks per character (0..255). For each character code c,
        // ConditionsMask[c] has bit 'i' set if condition index i accepts that character. numConds
        // is the number of conditions for this entry.
        public int NumConds { get; set; }
        public int[] ConditionsMask { get; } = new int[256];
        public string CondsRaw { get; set; } = ".";
        public bool XProduct { get; set; }
    }

    public class Affix
    {
        public char Flag { get; set; }
        public bool IsPrefix { get; set; }
        public bool XProduct { get; set; }
        public List<AffEntry> Entries { get; } = new List<AffEntry>();
    }

    public class AffixSet
    {
        public List<Affix> Prefixes { get; } = new List<Affix>();
        public List<Affix> Suffixes { get; } = new List<Affix>();
        // Compound related directives
        public int CompoundMin { get; set; } = 1;
        public bool CheckCompoundTriple { get; set; }
        public bool SimplifiedTriple { get; set; }
        public bool CheckCompoundDup { get; set; }
        public bool CheckCompoundRep { get; set; }
        public HashSet<char> CompoundPermitFlag { get; } = new HashSet<char>();
        public HashSet<char> CompoundBegin { get; } = new HashSet<char>();
        public HashSet<char> CompoundMiddle { get; } = new HashSet<char>();
        public HashSet<char> CompoundEnd { get; } = new HashSet<char>();
        public HashSet<char> OnlyInCompound { get; } = new HashSet<char>();
        public List<string> CompoundRules { get; } = new List<string>();
        // CHECKCOMPOUNDPATTERN entries
        public List<CompoundPattern> CompoundPatterns { get; } = new List<CompoundPattern>();
        // Replacement rules (REP lines) used by suggestions and compound heuristics
        public List<(string From, string To)> Replacements { get; } = new List<(string From, string To)>();
    }

    public class CompoundPattern
    {
        public string EndChars { get; }
        public char? EndFlag { get; }
        public string BeginChars { get; }
        public char? BeginFlag { get; }
        public string? Replacement { get; }

        public CompoundPattern(string endChars, char? endFlag, string beginChars, char? beginFlag, string? replacement)
        {
            EndChars = endChars ?? string.Empty;
            EndFlag = endFlag;
            BeginChars = beginChars ?? string.Empty;
            BeginFlag = beginFlag;
            Replacement = replacement;
        }
    }

    public static class AffixParser
    {
        // Extremely small, forgiving affix parser ported for convenience from hunspell's tools/munch
        public static AffixSet Parse(string affFile)
        {
            var set = new AffixSet();
            var lines = File.ReadAllLines(affFile);

            Affix? current = null;
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3)
                    continue;

                // Distinguish header line from entry line by checking if parts[3] is numeric
                if ((parts[0] == "PFX" || parts[0] == "SFX") && parts.Length >= 4 && int.TryParse(parts[3], out var _))
                {
                    // header line: PFX A Y <num>
                    current = new Affix { Flag = parts[1][0], IsPrefix = parts[0] == "PFX" };
                    if (parts[2] == "Y")
                        current.XProduct = true;
                    if (current.IsPrefix)
                        set.Prefixes.Add(current);
                    else
                        set.Suffixes.Add(current);
                    continue;
                }

                // Compound directives
                if (parts[0] == "COMPOUNDMIN" && parts.Length >= 2 && int.TryParse(parts[1], out var cm))
                {
                    set.CompoundMin = cm;
                    continue;
                }
                if (parts[0] == "CHECKCOMPOUNDTRIPLE")
                {
                    set.CheckCompoundTriple = true;
                    continue;
                }
                if (parts[0] == "SIMPLIFIEDTRIPLE")
                {
                    set.SimplifiedTriple = true;
                    continue;
                }
                if (parts[0] == "CHECKCOMPOUNDDUP")
                {
                    set.CheckCompoundDup = true;
                    continue;
                }
                if (parts[0] == "CHECKCOMPOUNDREP")
                {
                    set.CheckCompoundRep = true;
                    continue;
                }
                if (parts[0] == "COMPOUNDPERMITFLAG" && parts.Length >= 2)
                {
                    foreach (var ch in parts[1]) set.CompoundPermitFlag.Add(ch);
                    continue;
                }
                if (parts[0] == "COMPOUNDBEGIN" && parts.Length >= 2)
                {
                    foreach (var ch in parts[1]) set.CompoundBegin.Add(ch);
                    continue;
                }
                if (parts[0] == "COMPOUNDMIDDLE" && parts.Length >= 2)
                {
                    foreach (var ch in parts[1]) set.CompoundMiddle.Add(ch);
                    continue;
                }
                if (parts[0] == "COMPOUNDEND" && parts.Length >= 2)
                {
                    foreach (var ch in parts[1]) set.CompoundEnd.Add(ch);
                    continue;
                }
                if (parts[0] == "ONLYINCOMPOUND" && parts.Length >= 2)
                {
                    foreach (var ch in parts[1]) set.OnlyInCompound.Add(ch);
                    continue;
                }
                if (parts[0] == "COMPOUNDRULE" && parts.Length >= 2)
                {
                    // store raw rule for later evaluation
                    set.CompoundRules.Add(parts[1]);
                    continue;
                }

                if (parts[0] == "CHECKCOMPOUNDPATTERN")
                {
                    if (parts.Length >= 2)
                    {
                        // count line (ignore) or pattern: endchars[/flag] beginchars[/flag] [replacement]
                        if (int.TryParse(parts[1], out _))
                        {
                            // count, ignore
                        }
                        else if (parts.Length >= 3)
                        {
                            // parse optional /flag suffixes
                            (string endChars, char? endFlag) = ParseFlaggedPart(parts[1]);
                            (string beginChars, char? beginFlag) = ParseFlaggedPart(parts[2]);
                            string? replacement = parts.Length > 3 ? parts[3] : null;
                            set.CompoundPatterns.Add(new CompoundPattern(endChars, endFlag, beginChars, beginFlag, replacement));
                        }
                    }
                    continue;
                }

                if (parts[0] == "REP" && parts.Length >= 3)
                {
                    // REP can be simple or regex-like; store raw strings for heuristics later
                    set.Replacements.Add((parts[1], parts[2]));
                    continue;
                }

                // entry-line expected for an existing current affix
                if (current != null && (parts[0] == "PFX" || parts[0] == "SFX"))
                {
                    // entry form may be: SFX A 0 s [^ae]
                    if (parts.Length >= 4)
                    {
                        var e = new AffEntry();
                        // parts[2] is strip (often 0 for empty)
                        e.Strip = parts[2] == "0" ? string.Empty : parts[2];
                        e.Appnd = parts[3] == "0" ? string.Empty : parts[3];
                            if (parts.Length >= 5)
                                e.CondsRaw = string.Join(' ', parts.Skip(4));
                        // entries inherit cross-product flag from the header
                        e.XProduct = current.XProduct;

                        // encode conditions into a per-character bitmask similar to the original encodeit
                        EncodeConditions(e, e.CondsRaw);

                        current.Entries.Add(e);
                    }
                }
            }

            return set;
        }

        private static void EncodeConditions(AffEntry e, string cs)
        {
            // '.' means "no conditions"
            if (string.IsNullOrEmpty(cs) || cs == ".")
            {
                e.NumConds = 0;
                return;
            }

            int n = 0;
            for (int i = 0; i < cs.Length; ++i)
            {
                char c = cs[i];
                if (c == '[')
                {
                    bool neg = false;
                    var members = new List<int>();
                    i++;
                    if (i < cs.Length && cs[i] == '^')
                    {
                        neg = true;
                        i++;
                    }
                    // gather until ']'
                    while (i < cs.Length && cs[i] != ']')
                    {
                        char a = cs[i];
                        // handle simple range like a-z
                        if (i + 2 < cs.Length && cs[i + 1] == '-' && cs[i + 2] != ']')
                        {
                            char b = cs[i + 2];
                            for (char x = a; x <= b; x++)
                                members.Add((int)x);
                            i += 3;
                            continue;
                        }
                        members.Add((int)a);
                        i++;
                    }

                    if (neg)
                    {
                        // set bit for all chars except the members
                        var set = new HashSet<int>(members);
                        for (int ch = 0; ch < 256; ch++)
                        {
                            if (!set.Contains(ch))
                                e.ConditionsMask[ch] |= (1 << n);
                        }
                    }
                    else
                    {
                        foreach (var m in members)
                            e.ConditionsMask[m] |= (1 << n);
                    }
                    // end group -> advance condition index
                    n++;
                    continue;
                }

                // single char token
                if (!char.IsWhiteSpace(c))
                {
                    int code = (int)c;
                    e.ConditionsMask[code] |= (1 << n);
                    n++;
                }
            }

            e.NumConds = n;
        }

        private static (string, char?) ParseFlaggedPart(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return (string.Empty, null);
            var parts = raw.Split('/', 2);
            if (parts.Length == 1) return (parts[0], null);
            if (string.IsNullOrEmpty(parts[1])) return (parts[0], null);
            return (parts[0], parts[1][0]);
        }
    }
}
