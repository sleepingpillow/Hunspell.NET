using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Hunspell.Tests;

public class Debug_GermanCompounding
{
    [Fact]
    public void Debug_Computerarbeit()
    {
        var affFile = Path.Combine("dictionaries", "germancompounding", "germancompounding.aff");
        var dicFile = Path.Combine("dictionaries", "germancompounding", "germancompounding.dic");

        var hunspell = new HunspellSpellChecker(affFile, dicFile);

        bool valid = hunspell.Spell("Computerarbeit");
        Console.WriteLine($"Spell('Computerarbeit') = {valid}");
        Assert.True(valid);
    }
}
