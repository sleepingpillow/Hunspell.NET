// Simple console app demonstrating morphological analysis
// Build and run: dotnet run

using System;
using System.IO;
using System.Linq;
using Hunspell;

// Path to test dictionary
var baseDir = Path.Combine("..", "..", "tests", "Hunspell.Tests", "dictionaries", "morph");
var affPath = Path.Combine(baseDir, "morph.aff");
var dicPath = Path.Combine(baseDir, "morph.dic");

if (!File.Exists(affPath) || !File.Exists(dicPath))
{
    Console.WriteLine("Error: Dictionary files not found.");
    Console.WriteLine($"Looking for: {affPath}");
    return;
}

using var spellChecker = new HunspellSpellChecker(affPath, dicPath);

Console.WriteLine("=== Hunspell.NET Morphological Analysis Demo ===\n");

// Test words demonstrating different morphological features
var testWords = new[]
{
    ("drink", "Simple dictionary word"),
    ("drinks", "Inflected form (plural/3rd person)"),
    ("drinkable", "Derived form (suffix -able)"),
    ("drinkables", "Multiple suffixes (-able + -s)"),
    ("undrinkable", "Prefix + derived form"),
    ("undrinkables", "Prefix + multiple suffixes"),
    ("drank", "Irregular past tense"),
    ("drunk", "Irregular past participle"),
    ("phenomenon", "Greek-origin word"),
    ("phenomena", "Irregular plural")
};

foreach (var (word, description) in testWords)
{
    Console.WriteLine($"Word: {word}");
    Console.WriteLine($"Description: {description}");
    
    // Check if word is valid
    bool isValid = spellChecker.Spell(word);
    Console.WriteLine($"Valid: {isValid}");
    
    // Get stems
    var stems = spellChecker.Stem(word);
    if (stems.Any())
    {
        Console.WriteLine($"Stem(s): {string.Join(", ", stems)}");
    }
    else
    {
        Console.WriteLine("Stem(s): (none found)");
    }
    
    // Get morphological analyses
    var analyses = spellChecker.Analyze(word);
    if (analyses.Any())
    {
        Console.WriteLine("Analysis:");
        foreach (var analysis in analyses)
        {
            Console.WriteLine($"  {analysis}");
            
            // Parse and explain the tags
            var tags = ParseMorphTags(analysis);
            if (tags.Any())
            {
                Console.WriteLine("  Explanation:");
                foreach (var (tag, value) in tags)
                {
                    var explanation = ExplainTag(tag);
                    Console.WriteLine($"    {tag}: {value} ({explanation})");
                }
            }
        }
    }
    else
    {
        Console.WriteLine("Analysis: (none found)");
    }
    
    Console.WriteLine();
}

Console.WriteLine("=== Demo Complete ===");

// Helper to parse morphological tags
static (string tag, string value)[] ParseMorphTags(string analysis)
{
    var parts = analysis.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries);
    return parts
        .Where(p => p.Contains(':'))
        .Select(p =>
        {
            var colonIndex = p.IndexOf(':');
            return (p.Substring(0, colonIndex), p.Substring(colonIndex + 1));
        })
        .ToArray();
}

// Helper to explain tag meanings
static string ExplainTag(string tag) => tag switch
{
    "st" => "stem/root form",
    "po" => "part of speech",
    "al" => "allomorph (alternate form)",
    "ts" => "tense/aspect",
    "is" => "inflectional suffix",
    "ds" => "derivational suffix",
    "dp" => "derivational prefix",
    "sp" => "surface prefix",
    _ => "unknown"
};
