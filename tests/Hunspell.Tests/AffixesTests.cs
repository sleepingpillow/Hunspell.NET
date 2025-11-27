using System.IO;
using Xunit;

namespace Hunspell.Tests;

public class AffixesTests
{
    [Fact]
    public void PrefixAndSuffix_WorkTogether()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
        var aff = Path.Combine(repoRoot, "tests", "Hunspell.Tests", "dictionaries", "affixes", "affixes.aff");
        var dic = Path.Combine(repoRoot, "tests", "Hunspell.Tests", "dictionaries", "affixes", "affixes.dic");

        using var spellChecker = new HunspellSpellChecker(aff, dic);

        // basic sanity for the sample aff/dic: present derived words should be 'recognized'
        Assert.True(spellChecker.Spell("work"));
        Assert.True(spellChecker.Spell("worked"));

        // prefix + suffix cross-product: re + work -> rework (explicit in dic) and reworked not present
        Assert.True(spellChecker.Spell("rework"));
    }
}
