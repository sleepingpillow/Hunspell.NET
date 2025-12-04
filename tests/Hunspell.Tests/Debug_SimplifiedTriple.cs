using System;
using Xunit;

namespace Hunspell.Tests
{
    public class Debug_SimplifiedTriple
    {
        [Fact]
        public void InspectSimplifiedTriple()
        {
            var aff = Path.Combine("dictionaries", "simplifiedtriple", "simplifiedtriple.aff");
            var dic = Path.Combine("dictionaries", "simplifiedtriple", "simplifiedtriple.dic");

            using var sp = new HunspellSpellChecker(aff, dic);
            Console.WriteLine("Spell('glassko') => " + sp.Spell("glassko"));

            var hmField = typeof(HunspellSpellChecker).GetField("_hashManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hm = hmField?.GetValue(sp);
            Assert.NotNull(hm);
            var lookup = hm!.GetType().GetMethod("Lookup");
            Console.WriteLine("Lookup(glass) => " + lookup!.Invoke(hm, new object?[] { "glass" }));
            Console.WriteLine("Lookup(sko) => " + lookup!.Invoke(hm, new object?[] { "sko" }));

            var afField = typeof(HunspellSpellChecker).GetField("_affixManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var af = afField?.GetValue(sp);
            Assert.NotNull(af);

            var isValid = af!.GetType().GetMethod("IsValidCompoundPart", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var leftValid = (bool)isValid!.Invoke(af, new object?[] { "glass", 0, 0, 5, "glassko", false })!;
            var rightValid = (bool)isValid!.Invoke(af, new object?[] { "sko", 1, 5, 8, "glassko", false })!;
            Console.WriteLine($"IsValidCompoundPart('glass') => {leftValid}");
            Console.WriteLine($"IsValidCompoundPart('sko') => {rightValid}");

            var ruleMethod = af.GetType().GetMethod("CheckCompoundRules", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var rulesOk = (bool)ruleMethod!.Invoke(af, new object?[] { "glassko", 5, 8, "glass", "sko" })!;
            Console.WriteLine($"CheckCompoundRules('glassko', prevEnd=5) => {rulesOk}");

            var compOfTwo = af.GetType().GetMethod("IsCompoundMadeOfTwoWords", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var glassCompArgs = new object?[] { "glass", false, false };
            var glassIsComp = (bool)compOfTwo!.Invoke(af, glassCompArgs)!;
            var skoCompArgs = new object?[] { "sko", false, false };
            var skoIsComp = (bool)compOfTwo.Invoke(af, skoCompArgs)!;
            Console.WriteLine($"IsCompoundMadeOfTwoWords('glass') => {glassIsComp}");
            Console.WriteLine($"IsCompoundMadeOfTwoWords('sko') => {skoIsComp}");

            var rec = af.GetType().GetMethod("CheckCompoundRecursive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var recResult = (bool)rec!.Invoke(af, new object?[] { "glassko", 0, 0, (string?)null, 0, false })!;
            Console.WriteLine($"CheckCompoundRecursive('glassko') => {recResult}");
            var recAfterFirst = (bool)rec!.Invoke(af, new object?[] { "glassko", 1, 5, "glass", 0, false })!;
            Console.WriteLine($"CheckCompoundRecursive('glassko' after first part) => {recAfterFirst}");
            var recAfterFirstWithSyll = (bool)rec!.Invoke(af, new object?[] { "glassko", 1, 5, "glass", 1, false })!;
            Console.WriteLine($"CheckCompoundRecursive('glassko' after first part, syll=1) => {recAfterFirstWithSyll}");
            var baseCaseTrue = (bool)rec!.Invoke(af, new object?[] { "glassko", 2, 8, (string?)null, 0, false })!;
            var baseCaseFalse = (bool)rec!.Invoke(af, new object?[] { "glassko", 1, 8, (string?)null, 0, false })!;
            Console.WriteLine($"CheckCompoundRecursive('glassko' wordCount=2,pos=8) => {baseCaseTrue}");
            Console.WriteLine($"CheckCompoundRecursive('glassko' wordCount=1,pos=8) => {baseCaseFalse}");

            Console.WriteLine("Running custom DFS to enumerate accepted partitions:");
            bool found = false;
            void Dfs(int pos, int wc, string? prev)
            {
                if (pos >= "glassko".Length)
                {
                    if (wc >= 2) found = true;
                    return;
                }
                for (int i = pos + 2; i <= "glassko".Length; i++)
                {
                    var part = "glassko".Substring(pos, i - pos);
                    var argsA = new object?[] { part, wc, pos, i, "glassko", false };
                    var okPart = (bool)isValid!.Invoke(af, argsA)!;
                    if (!okPart) continue;
                    var okRule = (bool)ruleMethod!.Invoke(af, new object?[] { "glassko", pos, i, prev, part })!;
                    if (!okRule) continue;
                    Dfs(i, wc + 1, part);
                    if (found) return;
                }
            }
            Dfs(0, 0, null);
            Console.WriteLine("Custom DFS found compound => " + found);
        }
    }
}
