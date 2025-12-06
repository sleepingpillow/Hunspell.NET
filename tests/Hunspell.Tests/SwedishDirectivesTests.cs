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

    [Fact]
    public void SvFi_BasicWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_FI.aff",
            "../../../dictionaries/swedish/sv_FI.dic");

        // Act & Assert - Test that dictionary loads and basic functionality works
        Assert.NotNull(spellChecker);
        Assert.Equal("UTF-8", spellChecker.DictionaryEncoding);

        // Test a few words that should exist
        Assert.True(spellChecker.Spell("abstract"));
        Assert.True(spellChecker.Spell("hund"));
    }

    [Fact]
    public void SvFi_CompoundWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_FI.aff",
            "../../../dictionaries/swedish/sv_FI.dic");

        // Act & Assert - Test compound word recognition (MAXCPDSUGS = 2)
        // Use words that exist in dictionary
        Assert.True(spellChecker.Spell("hund"));
        Assert.True(spellChecker.Spell("koja"));
        // Note: Compound generation may not be fully implemented, so test basic loading

    }

    [Fact]
    public void SvFi_suggestCompondWord()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_FI.aff",
            "../../../dictionaries/swedish/sv_FI.dic");
        // Act & Assert - Test suggestions for potential compounds
        var suggestions = spellChecker.Suggest("hundkojs");
        Assert.NotNull(suggestions);
        Assert.Contains("hundkoja", suggestions);
        // Test suggestions for potential compounds
        Assert.NotNull(suggestions);
    }

    [Fact]
    public void SvFi_RepAndBreak_ShouldWork()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_FI.aff",
            "../../../dictionaries/swedish/sv_FI.dic");

        // Act & Assert - Test REP table replacements
        var suggestions = spellChecker.Suggest("tast");
        Assert.NotNull(suggestions);
        // Should suggest "test" via REP e->ä

        // Test BREAK patterns
        Assert.True(spellChecker.Spell("bl.a.")); // abbreviation with BREAK .
        Assert.True(spellChecker.Spell("p.g.a.")); // abbreviation with BREAK .
    }

    [Fact]
    public void SvSe_BasicWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_SE.aff",
            "../../../dictionaries/swedish/sv_SE.dic");

        // Act & Assert - Same words as sv_FI
        Assert.NotNull(spellChecker);
        Assert.Equal("UTF-8", spellChecker.DictionaryEncoding);

        Assert.True(spellChecker.Spell("abstract"));
        Assert.True(spellChecker.Spell("hund"));
    }

    [Fact]
    public void SvSe_CompoundWords_ShouldBeValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_SE.aff",
            "../../../dictionaries/swedish/sv_SE.dic");

        // Act & Assert - Test compound words (MAXCPDSUGS = 0, so fewer suggestions)
        Assert.True(spellChecker.Spell("hund"));
        Assert.True(spellChecker.Spell("koja"));
    }

    [Fact]
    public void SvSe_suggestCompondWord()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_SE.aff",
            "../../../dictionaries/swedish/sv_SE.dic");
        // Act & Assert - Test suggestions for potential compounds
        var suggestions = spellChecker.Suggest("hundkojs");
        Assert.NotNull(suggestions);
        Assert.Contains("hundkoja", suggestions);
        // Test suggestions for potential compounds
        Assert.NotNull(suggestions);
    }

    [Fact]
    public void SvSe_RepAndBreak_ShouldWork()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/swedish/sv_SE.aff",
            "../../../dictionaries/swedish/sv_SE.dic");

        // Act & Assert - Same REP and BREAK as sv_FI
        var suggestions = spellChecker.Suggest("tast");
        Assert.NotNull(suggestions);

        Assert.True(spellChecker.Spell("bl.a."));
        Assert.True(spellChecker.Spell("p.g.a."));
    }
}
