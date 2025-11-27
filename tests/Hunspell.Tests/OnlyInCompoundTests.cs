using Xunit;

namespace Hunspell.Tests;

public class OnlyInCompoundTests
{
    [Fact]
    public void AffixDerived_OnlyInCompound_ShouldBeRejectedStandalone()
    {
        using var sp = new HunspellSpellChecker("dictionaries/onlyincompound/onlyincompound.aff", "dictionaries/onlyincompound/onlyincompound.dic");

        // base 'pseudo' is marked ONLYINCOMPOUND; derived 'pseudos' must be rejected as standalone
        Assert.False(sp.Spell("pseudos"), "Expected 'pseudos' to be rejected as a standalone word because 'pseudo' is ONLYINCOMPOUND");

        // but compound form should be accepted
        Assert.True(sp.Spell("foopseudos"), "Expected 'foopseudos' to be accepted as it is a compound where 'pseudo' is only-in-compound");
    }
}
