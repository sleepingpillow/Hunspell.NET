namespace Hunspell.Tests;

public class CheckCompoundRepTests
{
    [Fact]
    public void CheckCompoundRep_ValidCompound_NoRepMatch()
    {
        // Arrange - CHECKCOMPOUNDREP forbids compounds that match dictionary words via REP
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundrep/basic.aff",
            "../../../dictionaries/checkcompoundrep/basic.dic");

        // Act & Assert - Valid: víz + szer doesn't match any dictionary word
        Assert.True(spellChecker.Spell("vízszer"));
        
        // Valid: szer + kocsi doesn't match any dictionary word via REP
        Assert.True(spellChecker.Spell("szerkocsi"));
    }

    [Fact]
    public void CheckCompoundRep_CompoundMatchesDictionaryViaRep_ShouldBeForbidden()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundrep/basic.aff",
            "../../../dictionaries/checkcompoundrep/basic.dic");

        // Act & Assert - Invalid: szervíz (szer + víz) matches "szerviz" when í->i REP is applied
        Assert.False(spellChecker.Spell("szervíz"));
        
        // Full Hunspell also forbids compounds containing szervíz (like szervízkocsi)
        // We now implement the recursive CHECKCOMPOUNDREP check for compound parts
        Assert.False(spellChecker.Spell("szervízkocsi")); // szervíz + kocsi
        Assert.False(spellChecker.Spell("kocsiszervíz")); // kocsi + szervíz
    }

    [Fact]
    public void CheckCompoundRep_DictionaryWordStillValid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundrep/basic.aff",
            "../../../dictionaries/checkcompoundrep/basic.dic");

        // Act & Assert - Dictionary words are still valid
        Assert.True(spellChecker.Spell("szerviz")); // Dictionary word
        Assert.True(spellChecker.Spell("szer")); // Dictionary word
        Assert.True(spellChecker.Spell("víz")); // Dictionary word
    }
}
