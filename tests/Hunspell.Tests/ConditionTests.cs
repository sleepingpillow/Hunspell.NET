using System.IO;
using Xunit;

namespace Hunspell.Tests;

public class ConditionTests
{
    // NOTE: This test was written for the old simplified condition test (one suffix rule).
    // The condition test files have been replaced with upstream versions that have 18 complex
    // suffix/prefix rules with conditions. The upstream test is covered by
    // UpstreamAffixAndCompoundTests.UpstreamGoodWords_RootLevel_ShouldPass(condition).
    // Commenting out this old test to avoid confusion with mismatched test data.
    /*
    [Fact]
    public void Condition_SuffixOnlyWhenPrevIsConsonant()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", ".."));
        var aff = Path.Combine(repoRoot, "tests", "Hunspell.Tests", "dictionaries", "condition", "condition.aff");
        var dic = Path.Combine(repoRoot, "tests", "Hunspell.Tests", "dictionaries", "condition", "condition.dic");

        using var spellChecker = new HunspellSpellChecker(aff, dic);

        // 'cats' should be accepted because 'cat' + 's' is allowed (previous char 't' isn't a vowel)
        Assert.True(spellChecker.Spell("cats"));

        // 'areas' should NOT be produced from 'area' by the rule (previous char 'a' is a vowel)
        Assert.False(spellChecker.Spell("areas"));
    }
    */
}
