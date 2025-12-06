using System;
using System.IO;
using Xunit;

namespace Hunspell.Tests;

public class FlagCompoundForbidEdgeTests
{
    private static (string affPath, string dicPath) WriteTempFiles(string affContent, string dicContent)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "hunspell_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var affPath = Path.Combine(tmp, "temp.aff");
        var dicPath = Path.Combine(tmp, "temp.dic");
        File.WriteAllText(affPath, affContent);
        File.WriteAllText(dicPath, dicContent);
        return (affPath, dicPath);
    }

    [Theory]
    [InlineData("long")]
    [InlineData("num")]
    [InlineData("utf8")]
    public void AppendedCompoundForbid_DisallowsCompoundPart(string flagType)
    {
        string aff;
        string dic;

        if (flagType == "long")
        {
            aff = string.Join("\n", new[] {
                "FLAG long",
                "COMPOUNDMIN 4",
                "COMPOUNDFORBIDFLAG BB",
                "COMPOUNDFLAG AA",
                "SFX S Y 1",
                "SFX S 0 s/BB ." // appends the compound-forbid token
            }) + "\n";

            // ensure both foo and bar are valid compound parts before suffix applied
            dic = string.Join("\n", new[] { "2", "tool/AA", "bark/AA" }) + "\n";
        }
        else if (flagType == "num")
        {
            aff = string.Join("\n", new[] {
                "FLAG num",
                "COMPOUNDMIN 4",
                "COMPOUNDFORBIDFLAG 99",
                "COMPOUNDFLAG 11",
                "SFX S Y 1",
                "SFX S 0 s/99 ."
            }) + "\n";

            dic = string.Join("\n", new[] { "2", "tool/11", "bark/11" }) + "\n";
        }
        else // utf8
        {
            aff = string.Join("\n", new[] {
                "FLAG UTF-8",
                "COMPOUNDMIN 4",
                "COMPOUNDFORBIDFLAG Ü",
                "COMPOUNDFLAG A",
                "SFX S Y 1",
                "SFX S 0 s/Ü ."
            }) + "\n";

            dic = string.Join("\n", new[] { "2", "tool/A", "bark/A" }) + "\n";
        }

        var (affPath, dicPath) = WriteTempFiles(aff, dic);
        try
        {
            using var sp = new HunspellSpellChecker(affPath, dicPath);

            // 'bar' when suffixed becomes 'bars' and gains COMPOUNDFORBID; when evaluating
            // this as a compound (foo + bars) the compound checker should reject it.
            var afField = typeof(HunspellSpellChecker).GetField("_affixManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var af = afField?.GetValue(sp);
            Assert.NotNull(af);
            var checkCompoundMethod = af!.GetType().GetMethod("CheckCompound");
            var compoundOk = (bool)checkCompoundMethod!.Invoke(af, new object[] { "toolbarks" })!;
            Assert.False(compoundOk, $"Expected compound check for 'foobars' to be rejected due to COMPOUNDFORBID ({flagType})");

            // When the suffix is not applied, 'foobar' (two valid parts) should be a valid compound
            compoundOk = (bool)checkCompoundMethod!.Invoke(af, new object[] { "toolbark" })!;
            Assert.True(compoundOk, "Expected compound check for 'foobar' to be accepted when no appended forbid is present");
        }
        finally
        {
            try { File.Delete(affPath); } catch { }
            try { File.Delete(dicPath); } catch { }
            try { Directory.Delete(Path.GetDirectoryName(affPath)!, true); } catch { }
        }
    }

    [Fact]
    public void Debug_Long_MergedVariantTokens()
    {
        var aff = string.Join("\n", new[] {
            "FLAG long",
            "COMPOUNDFORBIDFLAG BB",
            "COMPOUNDFLAG AA",
            "SFX S Y 1",
            "SFX S 0 s/BB ."
        }) + "\n";
        var dic = string.Join("\n", new[] { "2", "foo/AA", "bar/AA" }) + "\n";

        var (affPath, dicPath) = WriteTempFiles(aff, dic);
        try
        {
            using var sp = new HunspellSpellChecker(affPath, dicPath);

            // use reflection to inspect the underlying hash manager state
            var hmField = typeof(HunspellSpellChecker).GetField("_hashManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var hm = hmField?.GetValue(sp);
            Assert.NotNull(hm);

            var gvMethod = hm!.GetType().GetMethod("GetWordFlagVariants");
            var mergeMethod = hm!.GetType().GetMethod("MergeFlags");
            var varContains = hm!.GetType().GetMethod("VariantContainsFlagAfterAppend");

            var variants = (System.Collections.IEnumerable)gvMethod!.Invoke(hm, new object[] { "bar" })!;
            Console.WriteLine("Variants for 'bar':");
            foreach (var v in variants) Console.WriteLine($"  variant='{v}'");

            var merged = (string)mergeMethod!.Invoke(hm, new object[] { "AA", "BB" })!;
            Console.WriteLine($"Merged AA + BB => '{merged}'");

            var containsForbid = (bool)varContains!.Invoke(hm, new object[] { "AA", "BB", "BB" })!;
            Console.WriteLine($"VariantContainsFlagAfterAppend(AA,BB,'BB') => {containsForbid}");

            // final checks: show intermediate results
            Console.WriteLine($"Spell('bars') => {sp.Spell("bars")}");
            var sugg = sp.Suggest("bars");
            Console.WriteLine($"Suggest('bars') => [{string.Join(", ", sugg)}]");

                // Inspect compound check directly via AffixManager internal instance
                var afField = typeof(HunspellSpellChecker).GetField("_affixManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var af = afField?.GetValue(sp);
                Assert.NotNull(af);
                var checkCompoundMethod = af!.GetType().GetMethod("CheckCompound");
                var compoundOk = (bool)checkCompoundMethod!.Invoke(af, new object[] { "foobars" })!;
                Console.WriteLine($"AffixManager.CheckCompound('foobars') => {compoundOk}");

                var compoundMinField = af.GetType().GetField("_compoundMin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var compoundMinVal = compoundMinField != null ? compoundMinField.GetValue(af) : null;
                Console.WriteLine($"AffixManager._compoundMin = {compoundMinVal}");
                var affText = File.ReadAllText(affPath);
                Console.WriteLine("Aff file contents:\n" + affText);

                var checkAff = (bool)af.GetType().GetMethod("CheckAffixedWord")!.Invoke(af, new object[] { "foobars", true })!;
                Console.WriteLine($"AffixManager.CheckAffixedWord('foobars') => {checkAff}");

                // Inspect TryFindAffixBase internals for foobars
                var tfMethod = af.GetType().GetMethod("TryFindAffixBase", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                // signature: string word, bool allowBaseOnlyInCompound, out string? baseCandidate, out AffixMatchKind kind,
                // out string? appendedFlag, out int affixCount, out int cleanAffixCount, IReadOnlyList<string>? requiredFlags = null,
                // int depth = 0, bool skipPartialCircumfix = false
                var parameters = new object?[] { "foobars", true, null, null, null, 0, 0, null, 0, false };
                var found = (bool)tfMethod!.Invoke(af, parameters)!;
                Console.WriteLine($"TryFindAffixBase('foobars') => {found} (base='{parameters[2]}', kind='{parameters[3]}', appended='{parameters[4]}', count='{parameters[5]}')");

                var checkBreak = (bool)af.GetType().GetMethod("CheckBreak")!.Invoke(af, new object[] { "foobars" })!;
                Console.WriteLine($"AffixManager.CheckBreak('foobars') => {checkBreak}");

                var isForbidden = (bool)af.GetType().GetMethod("IsForbiddenWord")!.Invoke(af, new object[] { "foobars" })!;
                Console.WriteLine($"AffixManager.IsForbiddenWord('foobars') => {isForbidden}");

            Assert.True(sp.Spell("foobar"));
            Assert.False(sp.Spell("foobars"));
        }
        finally
        {
            try { File.Delete(affPath); } catch { }
            try { File.Delete(dicPath); } catch { }
            try { Directory.Delete(Path.GetDirectoryName(affPath)!, true); } catch { }
        }
    }

    [Theory]
    [InlineData("long")]
    [InlineData("num")]
    [InlineData("utf8")]
    public void MixedVariant_AppendedCompoundForbid_OnlyRejectsWhenAllVariantsForbidden(string flagType)
    {
        // Base word 'bar' has two variants: one that will become forbidden when suffixed
        // (appends the COMPOUNDFORBID token) and one that does not. The compound should
        // be allowed because at least one derived variant remains non-forbidden.
        string aff;
        string dic;

        if (flagType == "long")
        {
            aff = string.Join("\n", new[] {
                "FLAG long",
                "COMPOUNDMIN 4",
                "COMPOUNDFORBIDFLAG BB",
                "COMPOUNDFLAG AA",
                "SFX S Y 1",
                // suffix appends a DIFFERENT token so mixed variants can remain non-forbidden
                "SFX S 0 s/DD ."
            }) + "\n";

            // bar has two variants; one is COMPOUNDFORBID (BB) and one is a compound-allowed (AA)
            dic = string.Join("\n", new[] { "3", "tool/AA", "bark/BB", "bark/AA" }) + "\n";
        }
        else if (flagType == "num")
        {
            aff = string.Join("\n", new[] {
                "FLAG num",
                "COMPOUNDMIN 4",
                "COMPOUNDFORBIDFLAG 99",
                "COMPOUNDFLAG 11",
                "SFX S Y 1",
                "SFX S 0 s/77 ."
            }) + "\n";

            dic = string.Join("\n", new[] { "3", "tool/11", "bark/99", "bark/11" }) + "\n";
        }
        else
        {
            aff = string.Join("\n", new[] {
                "FLAG UTF-8",
                "COMPOUNDMIN 4",
                "COMPOUNDFORBIDFLAG Ü",
                "COMPOUNDFLAG A",
                "SFX S Y 1",
                "SFX S 0 s/Å ."
            }) + "\n";

            dic = string.Join("\n", new[] { "3", "tool/A", "bark/Ü", "bark/A" }) + "\n";
        }

        var (affPath, dicPath) = WriteTempFiles(aff, dic);
        try
        {
            using var sp = new HunspellSpellChecker(affPath, dicPath);

            // 'bar' derived variant that gets the forbid exists but not all variants are forbidden
            Assert.True(sp.Spell("toolbarks"), $"Expected 'toolbarks' to be accepted because some derived variants remain non-forbidden ({flagType})");
        }
        finally
        {
            try { File.Delete(affPath); } catch { }
            try { File.Delete(dicPath); } catch { }
            try { Directory.Delete(Path.GetDirectoryName(affPath)!, true); } catch { }
        }
    }
}
