using Hunspell;

Console.WriteLine($"CWD: {Directory.GetCurrentDirectory()}");
var aff = Path.GetFullPath(Path.Combine("tests","Hunspell.Tests","dictionaries","rep","rep.aff"));
var dic = Path.GetFullPath(Path.Combine("tests","Hunspell.Tests","dictionaries","rep","rep.dic"));
Console.WriteLine($"Using aff: {aff}");
Console.WriteLine($"Using dic: {dic}");

using var sp = new HunspellSpellChecker(aff, dic);

string[] words = new[] { "foo", "foobars", "barfoos", "vinteún", "autos" };

foreach (var w in words)
{
    Console.WriteLine($"Word: {w} -> Correct: {sp.Spell(w)}");
    var s = sp.Suggest(w);
    Console.WriteLine("Suggestions: " + (s.Any() ? string.Join(", ", s) : "<none>"));
}

// helper finished

