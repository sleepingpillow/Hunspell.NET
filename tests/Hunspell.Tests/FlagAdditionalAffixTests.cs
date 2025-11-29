using System.IO;
using System.Diagnostics;
using Xunit;

namespace Hunspell.Tests;

public class FlagAdditionalAffixTests
{
    private static string D(string baseName, string ext)
    {
        var parts = baseName.Split('/');
        var fileName = parts[^1];
        return Path.Combine("..", "..", "..", "dictionaries", baseName, fileName + ext);
    }

    [Theory]
    [InlineData("flag")]
    [InlineData("flaglong")]
    [InlineData("flagnum")]
    [InlineData("flagutf8")]
    public void TwoSuffixChain_AcceptsFoosbar(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        // Two-suffix chain: foo + s + bar -> foosbar
        Assert.True(sp.Spell("foosbar"), $"Expected 'foosbar' to be accepted for {baseName}");
    }

    [Theory]
    [InlineData("flag")]
    [InlineData("flaglong")]
    [InlineData("flagnum")]
    [InlineData("flagutf8")]
    public void Perf_Check_TryFindAffixBase(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        // warmup
        for (int i = 0; i < 20; i++) sp.Spell("unfoosbar");

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++) sp.Spell("unfoosbar");
        sw.Stop();

        // Ensure the path remains reasonably fast (< 1s on local dev). This is a loose check
        // meant to detect pathological regressions in complexity. It may be adjusted later.
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Affix base resolution too slow: {sw.ElapsedMilliseconds} ms for {baseName}");
    }
}
