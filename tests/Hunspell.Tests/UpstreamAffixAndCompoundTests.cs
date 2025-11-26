using System.IO;
using Xunit;

namespace Hunspell.Tests;

public class UpstreamAffixAndCompoundTests
{
    private static string D(string baseDir, string f) => Path.Combine("..", "..", "..", "dictionaries", baseDir, f);

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
    [InlineData("rep")]
    [InlineData("checkcompoundrep2")]
    [InlineData("checkcompoundcase")]
    [InlineData("checkcompounddup")]
    [InlineData("checkcompoundtriple")]
    [InlineData("needaffix")]
    public void UpstreamAffixCompound_GoodWords_ShouldPass(string baseName)
    {
        var aff = D(baseName, baseName + ".aff");
        var dic = D(baseName, baseName + ".dic");

        // if both aff and dic missing, skip
        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var good = D(baseName, baseName + ".good");
        foreach (var w in ReadList(good))
        {
            Assert.True(sp.Spell(w), $"Expected '{w}' from {baseName}.good to be accepted");
        }
    }

    [Theory]
    [InlineData("rep")]
    [InlineData("checkcompoundrep2")]
    [InlineData("checkcompoundcase")]
    [InlineData("checkcompounddup")]
    [InlineData("checkcompoundtriple")]
    [InlineData("needaffix")]
    public void UpstreamAffixCompound_WrongWords_ShouldFail(string baseName)
    {
        var aff = D(baseName, baseName + ".aff");
        var dic = D(baseName, baseName + ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var wrong = D(baseName, baseName + ".wrong");
        foreach (var w in ReadList(wrong))
        {
            Assert.False(sp.Spell(w), $"Expected '{w}' from {baseName}.wrong to be rejected");
        }
    }
}
