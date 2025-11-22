namespace Hunspell.Tests;

public class BreakTests
{
    [Fact]
    public void Break_WordsInDictionary_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/break/basic.aff",
            "../../../dictionaries/break/basic.dic");

        // Act & Assert - Words in dictionary are valid
        Assert.True(spellChecker.Spell("foo"));
        Assert.True(spellChecker.Spell("bar"));
        Assert.True(spellChecker.Spell("e-mail")); // Hyphenated word in dictionary
    }

    [Fact]
    public void Break_ValidBrokenParts_ShouldBeValid()
    {
        // Arrange - BREAK points are '-' and '/'
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/break/basic.aff",
            "../../../dictionaries/break/basic.dic");

        // Act & Assert - foo-bar breaks into foo + bar (both in dictionary)
        Assert.True(spellChecker.Spell("foo-bar"));
        Assert.True(spellChecker.Spell("bar-foo"));
        Assert.True(spellChecker.Spell("foo/bar")); // Also break at '/'
    }

    [Fact]
    public void Break_RecursiveBreaking_ShouldWork()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/break/basic.aff",
            "../../../dictionaries/break/basic.dic");

        // Act & Assert - foo-bar-baz breaks recursively: foo, bar, baz
        Assert.True(spellChecker.Spell("foo-bar-baz"));
        Assert.True(spellChecker.Spell("foo-bar-foo"));
    }

    [Fact]
    public void Break_InvalidParts_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/break/basic.aff",
            "../../../dictionaries/break/basic.dic");

        // Act & Assert - invalid if any part is not in dictionary
        Assert.False(spellChecker.Spell("foo-xyz")); // xyz not in dictionary
        Assert.False(spellChecker.Spell("abc-bar")); // abc not in dictionary
    }

    [Fact]
    public void Break_EmptyParts_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/break/basic.aff",
            "../../../dictionaries/break/basic.dic");

        // Act & Assert - break at start or end creates empty parts
        Assert.False(spellChecker.Spell("-foo")); // Empty before
        Assert.False(spellChecker.Spell("foo-")); // Empty after
    }
}
