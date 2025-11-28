using Hunspell;

Console.WriteLine("╔═══════════════════════════════════════════════╗");
Console.WriteLine("║      Hunspell.NET Sample Application          ║");
Console.WriteLine("║      .NET 10 Spell Checker Demo               ║");
Console.WriteLine("╚═══════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize the spell checker
const string affixPath = "dictionaries/test.aff";
const string dictionaryPath = "dictionaries/test.dic";

using var spellChecker = new HunspellSpellChecker(affixPath, dictionaryPath);

Console.WriteLine($"Dictionary encoding: {spellChecker.DictionaryEncoding}");
Console.WriteLine();

// Test words
var testWords = new[]
{
    "hello",
    "world",
    "helo",
    "wrld",
    "test",
    "tset",
    "example",
    "exampel"
};

Console.WriteLine("Spell Checking Results:");
Console.WriteLine("─────────────────────────────────────────────────");

foreach (var word in testWords)
{
    var isCorrect = spellChecker.Spell(word);
    var status = isCorrect ? "✓ CORRECT" : "✗ INCORRECT";
    Console.Write($"{word,-15} {status}");

    if (!isCorrect)
    {
        var suggestions = spellChecker.Suggest(word);
        if (suggestions.Count > 0)
        {
            Console.Write($" → Suggestions: {string.Join(", ", suggestions.Take(3))}");
        }
    }
    Console.WriteLine();
}

// (end of sample output)

Console.WriteLine();
Console.WriteLine("─────────────────────────────────────────────────");

// Demonstrate runtime dictionary management
Console.WriteLine();
Console.WriteLine("Runtime Dictionary Management:");
Console.WriteLine("─────────────────────────────────────────────────");

const string customWord = "dotnet";
Console.WriteLine($"Adding '{customWord}' to runtime dictionary...");
spellChecker.Add(customWord);

Console.WriteLine($"Checking '{customWord}': {(spellChecker.Spell(customWord) ? "✓ FOUND" : "✗ NOT FOUND")}");

Console.WriteLine($"Removing '{customWord}' from runtime dictionary...");
spellChecker.Remove(customWord);

Console.WriteLine($"Checking '{customWord}': {(spellChecker.Spell(customWord) ? "✓ FOUND" : "✗ NOT FOUND")}");

Console.WriteLine();
Console.WriteLine("─────────────────────────────────────────────────");
Console.WriteLine("Sample completed successfully!");

