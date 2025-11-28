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
    [InlineData("allcaps_utf")]
    [InlineData("breakdefault")]
    [InlineData("breakoff")]
    [InlineData("keepcase")]
    [InlineData("dotless_i")]
    [InlineData("map")]
    [InlineData("phone")]
    [InlineData("ph")]
    [InlineData("sug")]
    [InlineData("sug2")]
    [InlineData("ph2")]
    [InlineData("maputf")]
    [InlineData("reputf")]
    [InlineData("wordpair")]
    [InlineData("slash")]
    [InlineData("ignore")]
    [InlineData("ignoreutf")]
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
    [InlineData("allcaps_utf")]
    [InlineData("breakdefault")]
    [InlineData("breakoff")]
    [InlineData("keepcase")]
    [InlineData("dotless_i")]
    [InlineData("map")]
    [InlineData("phone")]
    [InlineData("ph")]
    [InlineData("sug")]
    [InlineData("sug2")]
    [InlineData("sugutf")]
    [InlineData("ph2")]
    [InlineData("maputf")]
    [InlineData("reputf")]
    [InlineData("slash")]
    [InlineData("ignore")]
    [InlineData("ignoreutf")]
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

    [Theory]
    [InlineData("sug")]
    [InlineData("sug2")]
    public void UpstreamAffix_Suggestions_ShouldContainExpected(string baseName)
    {
        var aff = D(baseName, baseName + ".aff");
        var dic = D(baseName, baseName + ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var wrong = ReadList(D(baseName, baseName + ".wrong")).ToList();
        var sug = ReadList(D(baseName, baseName + ".sug")).ToList();

        if (!wrong.Any() || !sug.Any()) return;

        var count = Math.Min(wrong.Count, sug.Count);
        for (int i = 0; i < count; i++)
        {
            var miss = wrong[i];
            var expectedLine = sug[i];
            // expected format: "miss:expected1,expected2" or just "expected"
            var parts = expectedLine.Split(':', 2);
            var expectedPart = parts.Length > 1 ? parts[1] : parts[0];
            var expected = expectedPart.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0).Select(s => s.ToLowerInvariant()).ToList();

            var suggestions = sp.Suggest(miss).Select(s => s.ToLowerInvariant()).ToList();

            Assert.True(suggestions.Count > 0, $"Expected suggestions for '{miss}' in {baseName}.sug but found none.");

            // At least one expected suggestion should be present in returned suggestions
            Assert.True(expected.Any(e => suggestions.Contains(e)),
                $"Expected at least one of [{string.Join(",", expected)}] for '{miss}' but suggestions were [{string.Join(",", suggestions)}]");
        }
    }
}
