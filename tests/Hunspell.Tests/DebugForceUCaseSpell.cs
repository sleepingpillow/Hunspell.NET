using Xunit;

namespace Hunspell.Tests;

public class DebugForceUCaseSpell
{
    [Fact]
    public void Spell_FooBaz_Debug()
    {
        using var sp = new HunspellSpellChecker("dictionaries/forceucase/forceucase.aff", "dictionaries/forceucase/forceucase.dic");
        var w = "foobaz";
        var ok = sp.Spell(w);
        System.Console.WriteLine($"Spell('{w}') -> {ok}");
        Assert.False(ok);
    }
}
