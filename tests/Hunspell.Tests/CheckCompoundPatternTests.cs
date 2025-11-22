namespace Hunspell.Tests;

public class CheckCompoundPatternTests
{
    [Fact]
    public void CheckCompoundPattern_ForbiddenBoundary_OOE()
    {
        // Arrange - Pattern "oo e" forbids compounds where first word ends with 'oo' and second starts with 'e'
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundpattern/basic.aff",
            "../../../dictionaries/checkcompoundpattern/basic.dic");

        // Act & Assert - Invalid: matches forbidden pattern "oo e"
        Assert.False(spellChecker.Spell("fooeat")); // foo (ends with 'oo') + eat (starts with 'e')
    }

    [Fact]
    public void CheckCompoundPattern_AllowedBoundary()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundpattern/basic.aff",
            "../../../dictionaries/checkcompoundpattern/basic.dic");

        // Act & Assert - Valid: doesn't match forbidden patterns
        Assert.True(spellChecker.Spell("foobar")); // foo + bar (no forbidden pattern)
        Assert.True(spellChecker.Spell("barfoo")); // bar + foo (no forbidden pattern)
    }

    [Fact]
    public void CheckCompoundPattern_ForbiddenBoundary_SSS()
    {
        // Arrange - Pattern "ss s" forbids compounds where first word ends with 'ss' and second starts with 's'
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundpattern/basic.aff",
            "../../../dictionaries/checkcompoundpattern/basic.dic");

        // Act & Assert - Invalid: matches forbidden pattern "ss s"
        Assert.False(spellChecker.Spell("bossset")); // boss (ends with 'ss') + set (starts with 's')
    }

    [Fact]
    public void CheckCompoundPattern_WithReplacement_ForbidsPattern()
    {
        // Arrange - Pattern "o b z" forbids "o" + "b" but allows replacement with "z"
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundpattern/replacement.aff",
            "../../../dictionaries/checkcompoundpattern/replacement.dic");

        // Act & Assert - Invalid: matches forbidden pattern "o b"
        Assert.False(spellChecker.Spell("foobar")); // foo (ends with 'o') + bar (starts with 'b')
    }

    [Fact]
    public void CheckCompoundPattern_WithReplacement_AllowsOtherCombinations()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundpattern/replacement.aff",
            "../../../dictionaries/checkcompoundpattern/replacement.dic");

        // Act & Assert - Valid: doesn't match forbidden patterns
        Assert.True(spellChecker.Spell("barfoo")); // bar + foo (no forbidden pattern)
    }
}
