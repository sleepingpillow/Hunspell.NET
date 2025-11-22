namespace Hunspell.Tests;

public class CompoundTests
{
    [Fact]
    public void BasicCompound_TwoWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/basic.aff",
            "../../../dictionaries/compound/basic.dic");

        // Act & Assert
        Assert.True(spellChecker.Spell("foobar"));
        Assert.True(spellChecker.Spell("barfoo"));
        Assert.True(spellChecker.Spell("footest"));
    }

    [Fact]
    public void BasicCompound_ThreeWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/basic.aff",
            "../../../dictionaries/compound/basic.dic");

        // Act & Assert
        Assert.True(spellChecker.Spell("foobartest"));
        Assert.True(spellChecker.Spell("testfoobar"));
    }

    [Fact]
    public void BasicCompound_SingleWord_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/basic.aff",
            "../../../dictionaries/compound/basic.dic");

        // Act & Assert - single words should still be valid
        Assert.True(spellChecker.Spell("foo"));
        Assert.True(spellChecker.Spell("bar"));
        Assert.True(spellChecker.Spell("test"));
    }

    [Fact]
    public void BasicCompound_InvalidWord_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/basic.aff",
            "../../../dictionaries/compound/basic.dic");

        // Act & Assert
        Assert.False(spellChecker.Spell("foobaz"));
        Assert.False(spellChecker.Spell("bazbar"));
    }

    [Fact]
    public void BasicCompound_TooShortPart_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/basic.aff",
            "../../../dictionaries/compound/basic.dic");

        // Act & Assert - COMPOUNDMIN is 3, so parts must be at least 3 chars
        // Even if we had "ab/A" in dictionary, "abfoo" would be invalid
        Assert.False(spellChecker.Spell("fofoo")); // "fo" is too short
    }

    [Fact]
    public void CompoundDup_DuplicatedWords_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/dup.aff",
            "../../../dictionaries/compound/dup.dic");

        // Act & Assert
        Assert.False(spellChecker.Spell("foofoo")); // Duplicate
        Assert.False(spellChecker.Spell("barbar")); // Duplicate
    }

    [Fact]
    public void CompoundDup_DifferentWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/dup.aff",
            "../../../dictionaries/compound/dup.dic");

        // Act & Assert
        Assert.True(spellChecker.Spell("foobar")); // Different words
        Assert.True(spellChecker.Spell("barfoo")); // Different words
    }

    [Fact]
    public void CompoundCase_UppercaseAtBoundary_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/case.aff",
            "../../../dictionaries/compound/case.dic");

        // Act & Assert - lowercase followed by uppercase at boundary is forbidden
        Assert.False(spellChecker.Spell("fooBar"));
        Assert.False(spellChecker.Spell("fooBAZ"));
    }

    [Fact]
    public void CompoundCase_ValidCases_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/case.aff",
            "../../../dictionaries/compound/case.dic");

        // Act & Assert
        Assert.True(spellChecker.Spell("Barfoo")); // uppercase at start followed by lowercase is OK
        Assert.True(spellChecker.Spell("BAZfoo")); // uppercase followed by lowercase is OK
    }
}
