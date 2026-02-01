using System;
using Hunspell;

var aff = "../../../dictionaries/morph/morph.aff";
var dic = "../../../dictionaries/morph/morph.dic";

using var sp = new HunspellSpellChecker(aff, dic);

Console.WriteLine("Testing morphological analysis and stemming:");
Console.WriteLine();

var testWords = new[] { "drink", "drinks", "drinkable", "drinkables", "drank", "drunk" };

foreach (var word in testWords)
{
    Console.WriteLine($"> {word}");
    
    var stems = sp.Stem(word);
    Console.WriteLine($"  Stems: {string.Join(", ", stems)}");
    
    var analyses = sp.Analyze(word);
    foreach (var analysis in analyses)
    {
        Console.WriteLine($"  Analysis: {analysis}");
    }
    
    Console.WriteLine();
}
