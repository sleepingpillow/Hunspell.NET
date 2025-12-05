#r "c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET\src\Hunspell\bin\Debug\net10.0\Hunspell.dll"

using Hunspell;

var aff = @"c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET\tests\Hunspell.Tests\dictionaries\needaffix5\needaffix5.aff";
var dic = @"c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET\tests\Hunspell.Tests\dictionaries\needaffix5\needaffix5.dic";

using var checker = new HunspellSpellChecker(aff, dic);

Console.WriteLine("Testing GOOD words (should all be TRUE):");
Console.WriteLine($"pseudoprefoopseudosufbar: {checker.Spell("pseudoprefoopseudosufbar")}");
Console.WriteLine($"pseudoprefoosuf: {checker.Spell("pseudoprefoosuf")}");
Console.WriteLine($"prefoopseudosuf: {checker.Spell("prefoopseudosuf")}");

Console.WriteLine("\nTesting WRONG words (should all be FALSE):");
Console.WriteLine($"pseudoprefoo: {checker.Spell("pseudoprefoo")}");
Console.WriteLine($"foopseudosuf: {checker.Spell("foopseudosuf")}");
Console.WriteLine($"pseudoprefoopseudosuf: {checker.Spell("pseudoprefoopseudosuf")}");
