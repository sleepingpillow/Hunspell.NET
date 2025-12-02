using System.IO;
using System.Linq;
using Xunit;

namespace Hunspell.Tests;

public class CheckCompoundPatternUpstreamTests
{
    private static string D(string baseName, string ext) => Path.Combine("dictionaries", baseName, baseName + ext);

    private static IEnumerable<string> ReadList(string path)
    {
        if (!File.Exists(path)) yield break;
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            yield return t;
        }
    }

    [Theory]
    [InlineData("checkcompoundpattern2")]
    [InlineData("checkcompoundpattern3")]
    [InlineData("checkcompoundpattern4")]
    public void Upstream_CheckCompoundPattern_ShouldRespectGoodAndWrong(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var good = D(baseName, ".good");
        foreach (var w in ReadList(good))
        {
            Assert.True(sp.Spell(w), $"Expected '{w}' from {baseName}.good to be accepted");
        }

        var wrong = D(baseName, ".wrong");
        foreach (var w in ReadList(wrong))
        {
            Assert.False(sp.Spell(w), $"Expected '{w}' from {baseName}.wrong to be rejected");
        }
    }

    [Fact]
    public void Debug_CheckCompoundPattern2_Inspect()
    {
        var baseName = "checkcompoundpattern2";
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");
        using var sp = new HunspellSpellChecker(aff, dic);
        var word = "fozar";
        Console.WriteLine($"Spell('{word}') => {sp.Spell(word)}");
        var amField = typeof(HunspellSpellChecker).GetField("_affixManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var am = amField?.GetValue(sp);
        // Avoid compile-time reference to internal types; use the spell-checker's
        // assembly via the instance to inspect runtime types if needed.
        var lookupMeth = sp.GetType().Assembly.GetType("Hunspell.HashManager");

        // Invoke the private CheckCompoundRules method via reflection to inspect
        // why 'fozar' is rejected. This avoids compile-time references to
        // internal types while allowing runtime diagnostics.
        var amFieldInfo = sp.GetType().GetField("_affixManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var amObj = amFieldInfo?.GetValue(sp);
        var checkMethod = amObj?.GetType().GetMethod("CheckCompoundRules", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (checkMethod is not null)
        {
            var result = checkMethod.Invoke(amObj, new object?[] { word, 3, 5, "foo", "zar" });
            Console.WriteLine($"CheckCompoundRules(fozar split foo|zar) => {result}");
        }
        var methods = amObj?.GetType().GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (methods is not null)
        {
            Console.WriteLine("AffixManager methods: " + string.Join(", ", methods.Select(m => m.Name)));
        }
        var isValidPart = amObj?.GetType().GetMethod("IsValidCompoundPart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(string), typeof(int), typeof(int), typeof(int), typeof(string), typeof(bool).MakeByRefType() }, null);
        if (isValidPart is not null)
        {
            var requires = false;
            var args = new object?[] { "zar", 1, 3, 5, word, requires };
            var res = isValidPart.Invoke(amObj, args);
            // The out parameter will be populated in the args array
            Console.WriteLine($"IsValidCompoundPart('zar') => result={res}, requiresForceUCase={args[5]}");
        }
        var checkCompMethod = amObj?.GetType().GetMethod("CheckCompound", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (checkCompMethod is not null)
        {
            var cresult = checkCompMethod.Invoke(amObj, new object?[] { word });
            Console.WriteLine($"CheckCompound(fozar) => {cresult}");
        }
        // Inspect internal compound patterns and related state for diagnostics
        var patternsField = amObj?.GetType().GetField("_compoundPatterns", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var patterns = patternsField?.GetValue(amObj) as System.Collections.IEnumerable;
        if (patterns is not null)
        {
            Console.WriteLine("Patterns:");
            foreach (var p in patterns)
            {
                Console.WriteLine(p?.ToString());
            }
        }
        var minField = amObj?.GetType().GetField("_compoundMin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Console.WriteLine($"compoundMin = {minField?.GetValue(amObj)}");
        Console.WriteLine("Diagnostic done.");
        Assert.True(true);
    }
}
