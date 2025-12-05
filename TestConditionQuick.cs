using Hunspell;

var aff = "tests/Hunspell.Tests/dictionaries/condition/condition.aff";
var dic = "tests/Hunspell.Tests/dictionaries/condition/condition.dic";

using var checker = new HunspellSpellChecker(aff, dic);

var results = new[] { "ofo", "pre1ofo", "pre2ofo", "pre3ofo" }
    .Select(word => (word, accepted: checker.Spell(word)))
    .ToList();

foreach (var (word, accepted) in results)
{
    Console.WriteLine($"{word,-20} {(accepted ? "✓ ACCEPT" : "✗ REJECT")}");
}
