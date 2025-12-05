using Hunspell;

var dictionaryPath = @"..\tests\Hunspell.Tests\dictionaries\condition\condition.dic";
var affixPath = @"..\tests\Hunspell.Tests\dictionaries\condition\condition.aff";

Console.WriteLine("Creating spellchecker...");
var spellChecker = new HunspellSpellChecker(affixPath, dictionaryPath);

Console.WriteLine();
var testWords = new[] { "pre1ofo", "pre2ofo", "ofo" };

foreach (var word in testWords)
{
    var result = spellChecker.Spell(word);
    Console.WriteLine($"{word}: {result}");
}
