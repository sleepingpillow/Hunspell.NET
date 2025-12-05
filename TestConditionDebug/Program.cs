using Hunspell;

var dictionaryPath = @"tests\Hunspell.Tests\dictionaries\condition\condition.dic";
var affixPath = @"tests\Hunspell.Tests\dictionaries\condition\condition.aff";

try
{
    var spellChecker = new HunspellSpellChecker(affixPath, dictionaryPath);
    
    // Test specific words
    var words = new[] 
    { 
        "pre1ofo",      // Should PASS - prefix pre1 with condition . on ofo
        "pre2ofo",      // Should PASS - prefix pre2 with condition o on ofo
        "ofo",          // Should PASS - in dictionary with flags SP
    };
    
    foreach (var word in words)
    {
        var result = spellChecker.Spell(word);
        Console.WriteLine($"{word}: {result}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack: {ex.StackTrace}");
}
