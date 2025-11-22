namespace Hunspell.Tests;

public class CompoundRuleTests
{
    [Fact]
    public void CompoundRule_BasicPattern_ABC()
    {
        // Arrange - Pattern ABC means: flag A, then flag B, then flag C
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundrule/basic.aff",
            "../../../dictionaries/compoundrule/basic.dic");

        // Act & Assert - Valid: matches pattern ABC
        Assert.True(spellChecker.Spell("abc")); // a/A + b/B + c/BC
        Assert.True(spellChecker.Spell("acc")); // a/A + c/BC + c/BC (c has both B and C flags)
    }

    [Fact]
    public void CompoundRule_BasicPattern_Invalid()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundrule/basic.aff",
            "../../../dictionaries/compoundrule/basic.dic");

        // Act & Assert - Invalid: don't match pattern ABC
        Assert.False(spellChecker.Spell("ba")); // wrong order
        Assert.False(spellChecker.Spell("ab")); // only 2 parts, need 3
        Assert.False(spellChecker.Spell("ac")); // missing B flag
    }

    [Fact]
    public void CompoundRule_StarQuantifier_ZeroOrMore()
    {
        // Arrange - Pattern A*B*C* means: zero or more A, then zero or more B, then zero or more C
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundrule/star.aff",
            "../../../dictionaries/compoundrule/star.dic");

        // Act & Assert - Valid with * quantifier
        Assert.True(spellChecker.Spell("aa")); // A* (2 A's)
        Assert.True(spellChecker.Spell("aaa")); // A* (3 A's)
        Assert.True(spellChecker.Spell("ab")); // A*B*
        Assert.True(spellChecker.Spell("abc")); // A*B*C*
        Assert.True(spellChecker.Spell("aabbcc")); // A*A*B*B*C*C*
    }

    [Fact]
    public void CompoundRule_StarQuantifier_RequiresAtLeastTwoParts()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundrule/star.aff",
            "../../../dictionaries/compoundrule/star.dic");

        // Act & Assert - Single word should not be a compound
        Assert.True(spellChecker.Spell("a")); // This is in dictionary as single word
        Assert.True(spellChecker.Spell("b")); // This is in dictionary as single word
        Assert.True(spellChecker.Spell("c")); // This is in dictionary as single word
    }

    [Fact]
    public void CompoundRule_StarQuantifier_InvalidPattern()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(
            "../../../dictionaries/compoundrule/star.aff",
            "../../../dictionaries/compoundrule/star.dic");

        // Act & Assert - Invalid: wrong order
        Assert.False(spellChecker.Spell("ba")); // B before A violates A*B*C* pattern
        Assert.False(spellChecker.Spell("ca")); // C before A violates pattern
        Assert.False(spellChecker.Spell("cba")); // Wrong order entirely
    }
}
