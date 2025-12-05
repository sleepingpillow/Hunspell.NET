using Hunspell;

var aff = "tests/Hunspell.Tests/dictionaries/condition/condition.aff";
var dic = "tests/Hunspell.Tests/dictionaries/condition/condition.dic";

using var checker = new HunspellSpellChecker(aff, dic);

Console.WriteLine($"\nChecking pre1ofo: {checker.Spell("pre1ofo")}");
Console.WriteLine($"Checking pre2ofo: {checker.Spell("pre2ofo")}");
Console.WriteLine($"Checking ofo: {checker.Spell("ofo")}");
