using System.IO;
using Xunit;

namespace Hunspell.Tests;

public class ConditionTests
{
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
}
