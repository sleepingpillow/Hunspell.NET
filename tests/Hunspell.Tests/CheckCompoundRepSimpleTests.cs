namespace Hunspell.Tests;

public class CheckCompoundRepSimpleTests
{
    [Fact]
    public void Debug_CheckIfCompoundIsRecognized()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/checkcompoundrep/basic.aff",
            "../../../dictionaries/checkcompoundrep/basic.dic");

        // Check if individual words work
        Assert.True(spellChecker.Spell("szer"));
        Assert.True(spellChecker.Spell("víz"));
        Assert.True(spellChecker.Spell("szerviz")); // dictionary word
        
        // Without CHECKCOMPOUNDREP, szervíz would be valid as szer+víz compound
        // With CHECKCOMPOUNDREP, it should be forbidden because szervíz -> szerviz (REP) is in dictionary
        var result = spellChecker.Spell("szervíz");
        
        // This should be FALSE because szervíz matches szerviz via REP
        Assert.False(result);
    }
}
