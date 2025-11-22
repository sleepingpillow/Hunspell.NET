namespace Hunspell.Tests;

/// <summary>
/// Tests for Swedish affix file directives support.
/// Verifies that all directives used in Swedish dictionaries are properly parsed.
/// </summary>
public class SwedishDirectivesTests
{
    [Fact]
    public void SwedishDirectives_AffixFileShouldLoad()
    {
        // Arrange & Act - Just loading should work without errors
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Assert - Basic word should work
        Assert.True(spellChecker.Spell("hello"));
        Assert.True(spellChecker.Spell("test"));
    }

    [Fact]
    public void SwedishDirectives_BasicWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Act & Assert - Basic words should be recognized
        Assert.True(spellChecker.Spell("hello"));
        Assert.True(spellChecker.Spell("test"));
        // Note: Suffix application (test -> tests) is an advanced feature
        // The main goal here is to verify the directives parse without error
    }

    [Fact]
    public void SwedishDirectives_InvalidWords_ShouldBeInvalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Act & Assert
        Assert.False(spellChecker.Spell("xyz"));
        Assert.False(spellChecker.Spell("notaword"));
    }

    [Fact]
    public void SwedishDirectives_CompoundFeatures_ShouldParse()
    {
        // Arrange & Act - Should parse without errors
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Assert - File loaded successfully
        Assert.NotNull(spellChecker);
        Assert.Equal("UTF-8", spellChecker.DictionaryEncoding);
    }

    [Fact]
    public void SwedishDirectives_SuggestionOptions_ShouldParse()
    {
        // Arrange & Act - Should parse MAXCPDSUGS, MAXDIFF, ONLYMAXDIFF, NOSPLITSUGS, FULLSTRIP
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Assert - File loaded and can check words
        Assert.True(spellChecker.Spell("hello"));
        
        // Suggestions should work (implementation may use the parsed options)
        var suggestions = spellChecker.Suggest("helo");
        Assert.NotNull(suggestions);
    }

    [Fact]
    public void SwedishDirectives_WordAttributeFlags_ShouldParse()
    {
        // Arrange & Act - Should parse NOSUGGEST, FORBIDDENWORD, NEEDAFFIX, FORCEUCASE
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Assert - File loaded successfully
        Assert.True(spellChecker.Spell("hello"));
        Assert.True(spellChecker.Spell("word")); // Has NOSUGGEST flag
    }

    [Fact]
    public void SwedishDirectives_BreakAndMap_ShouldParse()
    {
        // Arrange & Act - Should parse BREAK and MAP directives
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Assert - File loaded successfully
        Assert.NotNull(spellChecker);
    }

    [Fact]
    public void SwedishDirectives_RepTable_ShouldParse()
    {
        // Arrange & Act - Should parse REP directives
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/directives.aff",
            "../../../dictionaries/swedish/directives.dic");

        // Assert - File loaded and can provide suggestions
        var suggestions = spellChecker.Suggest("tast");
        Assert.NotNull(suggestions);
        // REP table may help suggest "test"
    }
}
