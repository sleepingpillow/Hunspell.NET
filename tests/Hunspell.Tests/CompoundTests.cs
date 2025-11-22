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

    [Fact]
    public void CompoundPosition_RespectPositionFlags_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/position.aff",
            "../../../dictionaries/compound/position.dic");

        // Act & Assert - valid position combinations
        Assert.True(spellChecker.Spell("startend"));
        Assert.True(spellChecker.Spell("startmidend"));
        Assert.True(spellChecker.Spell("anyany"));
    }

    [Fact]
    public void CompoundPosition_WrongPosition_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/position.aff",
            "../../../dictionaries/compound/position.dic");

        // Act & Assert - invalid position combinations
        Assert.False(spellChecker.Spell("endstart")); // end can't be first
        Assert.False(spellChecker.Spell("midend")); // mid can't be first
        Assert.False(spellChecker.Spell("startmid")); // mid can't be last
    }

    [Fact]
    public void OnlyInCompound_AsStandalone_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/onlyincompound.aff",
            "../../../dictionaries/compound/onlyincompound.dic");

        // Act & Assert - ONLYINCOMPOUND words can't stand alone
        Assert.False(spellChecker.Spell("fuge"));
    }

    [Fact]
    public void OnlyInCompound_InCompound_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/onlyincompound.aff",
            "../../../dictionaries/compound/onlyincompound.dic");

        // Act & Assert - ONLYINCOMPOUND words valid inside compounds
        Assert.True(spellChecker.Spell("foofugebar"));
    }

    [Fact]
    public void CompoundForbid_ForbiddenWord_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/forbid.aff",
            "../../../dictionaries/compound/forbid.dic");

        // Act & Assert - words with COMPOUNDFORBIDFLAG can't be in compounds
        Assert.False(spellChecker.Spell("foobad"));
        Assert.False(spellChecker.Spell("badbar"));
    }

    [Fact]
    public void CompoundForbid_AllowedWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/forbid.aff",
            "../../../dictionaries/compound/forbid.dic");

        // Act & Assert - words without COMPOUNDFORBIDFLAG can be in compounds
        Assert.True(spellChecker.Spell("foobar"));
        Assert.True(spellChecker.Spell("barfoo"));
    }

    [Fact]
    public void CompoundForbid_ForbiddenWordStandalone_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/forbid.aff",
            "../../../dictionaries/compound/forbid.dic");

        // Act & Assert - forbidden word is still valid as standalone
        Assert.True(spellChecker.Spell("bad"));
    }

    [Fact]
    public void CompoundMaxWords_TwoWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/maxwords.aff",
            "../../../dictionaries/compound/maxwords.dic");

        // Act & Assert - max is 2 words
        Assert.True(spellChecker.Spell("foobar"));
        Assert.True(spellChecker.Spell("barbaz"));
    }

    [Fact]
    public void CompoundMaxWords_ThreeWords_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/maxwords.aff",
            "../../../dictionaries/compound/maxwords.dic");

        // Act & Assert - max is 2 words, so 3 words should be invalid
        Assert.False(spellChecker.Spell("foobarbaz"));
    }

    [Fact]
    public void CompoundDup_NonConsecutiveDuplicates_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compound/dup.aff",
            "../../../dictionaries/compound/dup.dic");

        // Act & Assert - CHECKCOMPOUNDDUP only checks consecutive parts
        // "foo-bar-foo" should be valid because foo and foo are not consecutive
        Assert.True(spellChecker.Spell("foobarfoo"));
    }
}
