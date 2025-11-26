namespace Hunspell.Tests;

public class CompoundRuleUpstreamTests
{
    private static string D(string f) => Path.Combine("..", "..", "..", "dictionaries", "compoundrule", f);

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
    [InlineData("compoundrule")]
    [InlineData("compoundrule2")]
    [InlineData("compoundrule3")]
    [InlineData("compoundrule4")]
    [InlineData("compoundrule5")]
    [InlineData("compoundrule6")]
    [InlineData("compoundrule7")]
    [InlineData("compoundrule8")]
    public void UpstreamCompoundRule_GoodWords_ShouldPass(string baseName)
    {
        var aff = D(baseName + ".aff");
        var dic = D(baseName + ".dic");

        using var sp = new HunspellSpellChecker(aff, dic);

        var good = D(baseName + ".good");
        foreach (var w in ReadList(good))
        {
            Assert.True(sp.Spell(w), $"Expected '{w}' from {baseName}.good to be accepted");
        }
    }

    [Theory]
    [InlineData("compoundrule")]
    [InlineData("compoundrule2")]
    [InlineData("compoundrule3")]
    [InlineData("compoundrule4")]
    [InlineData("compoundrule5")]
    [InlineData("compoundrule6")]
    [InlineData("compoundrule7")]
    [InlineData("compoundrule8")]
    public void UpstreamCompoundRule_WrongWords_ShouldFail(string baseName)
    {
        var aff = D(baseName + ".aff");
        var dic = D(baseName + ".dic");

        using var sp = new HunspellSpellChecker(aff, dic);

        var wrong = D(baseName + ".wrong");
        foreach (var w in ReadList(wrong))
        {
            Assert.False(sp.Spell(w), $"Expected '{w}' from {baseName}.wrong to be rejected");
        }
    }
}
