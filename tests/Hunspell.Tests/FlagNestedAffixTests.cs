using System.IO;
using Xunit;

namespace Hunspell.Tests;

public class FlagNestedAffixTests
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
    public void NestedAffixDerivedForms_AcceptsUnfoosbar(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return; // skip when dataset missing

        using var sp = new HunspellSpellChecker(aff, dic);

        // earlier regressions caused 'unfoosbar' (prefix + two suffixes chain)
        // to be rejected in several flag formats; verify the word is accepted
        Assert.True(sp.Spell("unfoosbar"), $"Expected 'unfoosbar' to be accepted for {baseName}");
    }
}
