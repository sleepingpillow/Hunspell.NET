using Hunspell;

var aff = @"c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET\tests\Hunspell.Tests\dictionaries\condition\condition.aff";
var dic = @"c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET\tests\Hunspell.Tests\dictionaries\condition\condition.dic";

using var checker = new HunspellSpellChecker(aff, dic);

var word = "pre1ofo";
Console.WriteLine($"Testing word: {word}");
Console.WriteLine($"Result: {checker.Spell(word)}");

// Try prefixes with different conditions
var words = new[] {
    "pre1ofo",  // no condition
    "pre2ofo",  // condition: o
    "pre3ofo",  // condition: [aeou]
    "pre4ofo",  // condition: [^o]
    "pre5ofo",  // condition: [^aeou]
    "pre6ofo",  // condition: of
};
Console.WriteLine("\nTesting various prefixes:");
foreach (var w in words)
{
    Console.WriteLine($"{w}: {checker.Spell(w)}");
}

