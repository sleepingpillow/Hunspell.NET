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
    [InlineData("compoundrule/basic")]
    [InlineData("compoundrule/compoundrule")]
    [InlineData("compoundrule/compoundrule2")]
    [InlineData("compoundrule/compoundrule3")]
    [InlineData("compoundrule/compoundrule4")]
    [InlineData("compoundrule/compoundrule5")]
    [InlineData("compoundrule/compoundrule6")]
    [InlineData("compoundrule/compoundrule7")]
    [InlineData("compoundrule/compoundrule8")]
    [InlineData("compoundrule/star")]
    [InlineData("compoundmoresuffixes/basic")]
    [InlineData("compoundsyllable/syllable")]
    [InlineData("condition/condition")]
    [InlineData("swedish/directives")]
    [InlineData("swedish/sv_FI")]
    [InlineData("swedish/sv_SE")]
    [InlineData("affixes/affixes")]
    [InlineData("allcaps")]
    [InlineData("allcaps2")]
    [InlineData("alias")]
    [InlineData("allcaps3")]
    [InlineData("compoundaffix")]
    [InlineData("compoundaffix2")]

    [InlineData("compoundaffix3")]
    [InlineData("compoundforbid")]
    [InlineData("compoundflag")]
    [InlineData("forceucase")]
    [InlineData("fullstrip")]

    [InlineData("break/basic")]
    [InlineData("checkcompoundrep/basic")]
    [InlineData("checkcompoundpattern/basic")]
    [InlineData("checkcompoundpattern/replacement")]
    [InlineData("checkcompoundrep2")]
    [InlineData("checkcompoundcase")]
    [InlineData("checkcompounddup")]
    [InlineData("checkcompoundtriple")]
    [InlineData("needaffix")]
    [InlineData("onlyincompound")]
    [InlineData("base")]
    [InlineData("base_utf")]
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
    [InlineData("compoundrule/basic")]
    [InlineData("compoundrule/compoundrule")]
    [InlineData("compoundrule/compoundrule2")]
    [InlineData("compoundrule/compoundrule3")]
    [InlineData("compoundrule/compoundrule4")]
    [InlineData("compoundrule/compoundrule5")]
    [InlineData("compoundrule/compoundrule6")]
    [InlineData("compoundrule/compoundrule7")]
    [InlineData("compoundrule/compoundrule8")]
    [InlineData("compoundrule/star")]
    [InlineData("compoundmoresuffixes/basic")]
    [InlineData("compoundsyllable/syllable")]
    [InlineData("condition/condition")]
    [InlineData("swedish/directives")]
    [InlineData("swedish/sv_FI")]
    [InlineData("swedish/sv_SE")]
    [InlineData("affixes/affixes")]
    [InlineData("allcaps")]
    [InlineData("allcaps2")]
    [InlineData("alias")]
    [InlineData("allcaps3")]
    [InlineData("compoundaffix")]
    [InlineData("compoundaffix2")]
    [InlineData("compoundforbid")]
    [InlineData("compoundflag")]
    [InlineData("forbiddenword")]
    [InlineData("forceucase")]
    [InlineData("fullstrip")]
    [InlineData("break/basic")]
    [InlineData("checkcompoundrep/basic")]
    [InlineData("checkcompoundpattern/basic")]
    [InlineData("checkcompoundpattern/replacement")]
    [InlineData("checkcompoundrep2")]
    [InlineData("checkcompoundcase")]
    [InlineData("checkcompounddup")]
    [InlineData("checkcompoundtriple")]
    [InlineData("needaffix")]
    [InlineData("onlyincompound")]
    [InlineData("base")]
    [InlineData("base_utf")]
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
