using System;
using System.IO;
using Hunspell;

namespace MorphTest;

class Program
{
    static void Main()
    {
        var baseDir = Path.Combine("..", "..", "tests", "Hunspell.Tests", "dictionaries", "morph");
        var aff = Path.Combine(baseDir, "morph.aff");
        var dic = Path.Combine(baseDir, "morph.dic");

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
    }
}
