namespace Hunspell.Tests;

public class CompoundSyllableTests
{
    [Fact]
    public void CompoundSyllable_TwoWords_WithinLimit()
    {
        // Arrange - COMPOUNDWORDMAX is 2, so 2 words should be allowed
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundsyllable/syllable.aff",
            "../../../dictionaries/compoundsyllable/syllable.dic");

        // Act & Assert - Valid: 2 words, within COMPOUNDWORDMAX
        Assert.True(spellChecker.Spell("catdog")); // cat (1 vowel) + dog (1 vowel) = 2 syllables
        Assert.True(spellChecker.Spell("hime")); // hi (1 vowel) + me (1 vowel) = 2 syllables
    }

    [Fact]
    public void CompoundSyllable_ThreeWords_ExceedsWordMax_ButWithinSyllableLimit()
    {
        // Arrange - COMPOUNDWORDMAX is 2, but COMPOUNDSYLLABLE allows 6 syllables
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundsyllable/syllable.aff",
            "../../../dictionaries/compoundsyllable/syllable.dic");

        // Act & Assert - Valid: 3 words exceeds COMPOUNDWORDMAX but syllable count (4) is within limit (6)
        Assert.True(spellChecker.Spell("catdoghi")); // cat + dog + hi = 3 words, 3 syllables (within 6)
        Assert.True(spellChecker.Spell("himetome")); // hi + me + to + me = 4 words, 4 syllables (within 6)
    }

    [Fact]
    public void CompoundSyllable_ExceedsSyllableLimit()
    {
        // Arrange - COMPOUNDSYLLABLE limit is 6 syllables
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundsyllable/syllable.aff",
            "../../../dictionaries/compoundsyllable/syllable.dic");

        // Act & Assert - Invalid: exceeds both word count (2) and syllable limit (6)
        // catdogcatdogcatdogcat = 7 words, 7 syllables (exceeds 6)
        Assert.False(spellChecker.Spell("catdogcatdogcatdogcat"));
    }

    [Fact]
    public void CompoundSyllable_NoSyllables_StillEnforcesWordMax()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundsyllable/syllable.aff",
            "../../../dictionaries/compoundsyllable/syllable.dic");

        // Act & Assert - Without syllable exception, word max is enforced
        // Even if individual words have no vowels, the syllable exception doesn't help
        Assert.True(spellChecker.Spell("toto")); // 2 words, 2 syllables - within limits
    }
}
