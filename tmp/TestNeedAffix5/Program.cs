using Hunspell;

var repoRoot = args.Length > 0 ? args[0] : @"c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET";
var aff = Path.Combine(repoRoot, "tests", "Hunspell.Tests", "dictionaries", "needaffix5", "needaffix5.aff");
var dic = Path.Combine(repoRoot, "tests", "Hunspell.Tests", "dictionaries", "needaffix5", "needaffix5.dic");

using var checker = new HunspellSpellChecker(aff, dic);

Console.WriteLine("Testing GOOD words (should all be TRUE):");
var goodWords = new[] {
    "pseudoprefoopseudosufbar",
    "pseudoprefoosuf",
    "prefoopseudosuf"
};
foreach (var word in goodWords)
{
    Console.WriteLine($"  {word}: {checker.Spell(word)}");
}

Console.WriteLine("\nTesting WRONG words (should all be FALSE):");
var wrongWords = new[] {
    "pseudoprefoo",
    "foopseudosuf",
    "pseudoprefoopseudosuf"
};
foreach (var word in wrongWords)
{
    Console.WriteLine($"  {word}: {checker.Spell(word)}");
}
