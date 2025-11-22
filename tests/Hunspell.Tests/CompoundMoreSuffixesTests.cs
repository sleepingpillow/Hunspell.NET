namespace Hunspell.Tests;

public class CompoundMoreSuffixesTests
{
    [Fact]
    public void CompoundMoreSuffixes_BasicCompounds_ShouldWork()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundmoresuffixes/basic.aff",
            "../../../dictionaries/compoundmoresuffixes/basic.dic");

        // Act & Assert - Basic compounds should work
        Assert.True(spellChecker.Spell("bookshelf")); // book + shelf
        Assert.True(spellChecker.Spell("bookcase")); // book + case
        Assert.True(spellChecker.Spell("bookend")); // book + end
    }

    [Fact]
    public void CompoundMoreSuffixes_CompoundWithSuffixes_ShouldWork()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundmoresuffixes/basic.aff",
            "../../../dictionaries/compoundmoresuffixes/basic.dic");

        // Act & Assert - With COMPOUNDMORESUFFIXES, suffixed forms can work in compounds
        // Note: This is a simplified implementation. Full affix support would require
        // deep integration with the affix application system.
        
        // Simplified implementation supports common suffix stripping (like -s, -es, -ed, etc.)
        Assert.True(spellChecker.Spell("bookscase")); // books (book + s) + case
        
        // Basic compounds without suffixes should always work
        Assert.True(spellChecker.Spell("bookshelf")); // book + shelf (no suffixes)
    }

    [Fact]
    public void CompoundMoreSuffixes_InvalidCompounds_ShouldFail()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundmoresuffixes/basic.aff",
            "../../../dictionaries/compoundmoresuffixes/basic.dic");

        // Act & Assert - Invalid compounds should still fail
        Assert.False(spellChecker.Spell("bookxyz")); // xyz not in dictionary
        Assert.False(spellChecker.Spell("xyzshelf")); // xyz not in dictionary
    }

    [Fact]
    public void CompoundMoreSuffixes_FlagRespected()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundmoresuffixes/basic.aff",
            "../../../dictionaries/compoundmoresuffixes/basic.dic");

        // Act & Assert - Verify the flag is parsed and basic functionality works
        // This test documents that COMPOUNDMORESUFFIXES is recognized
        Assert.True(spellChecker.Spell("bookshelf"));
        Assert.True(spellChecker.Spell("shelfbook")); // Reverse should also work
    }
}
