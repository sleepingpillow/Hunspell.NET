# Morphological Analysis and Stemming

Hunspell.NET supports morphological analysis and stemming, allowing you to extract linguistic information from words.

## Stemming

Stemming reduces words to their base or root form by removing affixes.

### Basic Usage

```csharp
using Hunspell;

var aff = "path/to/dictionary.aff";
var dic = "path/to/dictionary.dic";

using var hunspell = new HunspellSpellChecker(aff, dic);

// Get stems for a word
var stems = hunspell.Stem("running");
// Result: ["run"]

var stems2 = hunspell.Stem("better");
// Result: ["good", "well"] (depending on dictionary)
```

### Multi-Level Affixation

The stemming function handles words with multiple affixes applied:

```csharp
var stems = hunspell.Stem("drinkables");
// Result: ["drinkable"]
// "drinkables" = "drink" + "able" suffix + "s" suffix
// Stem returns "drinkable" (one level of affix removal)
```

### Irregular Forms

For irregular forms specified in the dictionary with the `st:` (stem) field:

```csharp
var stems = hunspell.Stem("drank");
// Result: ["drink"]

var stems2 = hunspell.Stem("mice");
// Result: ["mouse"]
```

## Morphological Analysis

Morphological analysis returns detailed linguistic information about a word, including:
- Part of speech
- Tense and aspect
- Inflectional and derivational morphology
- Stem information
- Allomorphs (alternate forms)

### Basic Usage

```csharp
var analyses = hunspell.Analyze("drinks");
// Result: ["po:noun is:plur", "po:verb al:drank al:drunk ts:present is:sg_3"]
```

### Morphological Tags

Common morphological tags found in analyses:

- `st:` - Explicit stem (e.g., `st:drink`)
- `po:` - Part of speech (e.g., `po:noun`, `po:verb`, `po:adj`)
- `al:` - Allomorph/alternate form (e.g., `al:drank`, `al:drunk`)
- `ts:` - Tense/aspect (e.g., `ts:present`, `ts:past`)
- `is:` - Inflectional suffix (e.g., `is:plur`, `is:sg_3`, `is:past_1`)
- `ds:` - Derivational suffix (e.g., `ds:der_able`)
- `dp:` - Derivational prefix (e.g., `dp:pfx_un`)
- `sp:` - Surface prefix (the actual prefix text, e.g., `sp:un`)

### Example with Multiple Affixes

```csharp
var analyses = hunspell.Analyze("undrinkables");
// Result: ["dp:pfx_un sp:un st:drink po:verb al:drank al:drunk ts:present ds:der_able is:plur"]
```

Breaking this down:
- `dp:pfx_un` - derivational prefix "un"
- `sp:un` - surface form of prefix is "un"
- `st:drink` - stem is "drink"
- `po:verb` - part of speech is verb
- `al:drank al:drunk` - alternate forms (allomorphs)
- `ts:present` - tense is present
- `ds:der_able` - derivational suffix "able"
- `is:plur` - inflectional suffix marking plural

## Dictionary Format

To enable morphological analysis, your dictionary files should include morphological descriptions:

### .dic file example:
```
drink/S	po:noun
drink/RQ	po:verb	al:drank	al:drunk	ts:present
drank	po:verb	st:drink	is:past_1
drunk	po:verb	st:drink	is:past_2
```

### .aff file example:
```
SFX S Y 1
SFX S   0 s . is:plur

SFX R Y 1
SFX R   0 able/PS . ds:der_able

PFX P Y 1
PFX P  0 un . dp:pfx_un
```

## Practical Applications

### Text Analysis
```csharp
var words = new[] { "cats", "running", "better", "unbelievable" };
foreach (var word in words)
{
    var stems = hunspell.Stem(word);
    var analyses = hunspell.Analyze(word);
    
    Console.WriteLine($"{word}:");
    Console.WriteLine($"  Stems: {string.Join(", ", stems)}");
    Console.WriteLine($"  Analyses:");
    foreach (var analysis in analyses)
    {
        Console.WriteLine($"    {analysis}");
    }
}
```

### Search Engine Indexing
```csharp
// Index both the original word and its stems
var document = "The cats were running quickly";
var terms = new HashSet<string>();

foreach (var word in document.Split(' '))
{
    terms.Add(word.ToLower());
    
    var stems = hunspell.Stem(word.ToLower());
    foreach (var stem in stems)
    {
        terms.Add(stem);
    }
}
// Now index using 'terms' for better recall
```

### Grammar Analysis
```csharp
var analyses = hunspell.Analyze("runs");
foreach (var analysis in analyses)
{
    if (analysis.Contains("po:verb") && analysis.Contains("is:sg_3"))
    {
        Console.WriteLine("Third person singular verb form");
    }
}
```

## Performance Considerations

- Morphological analysis is more expensive than simple spell checking
- Results are not cached - consider caching frequently-analyzed words
- For large-scale text processing, consider batching operations

## Compatibility

The morphological analysis implementation follows the Hunspell standard and is compatible with standard Hunspell dictionaries that include morphological descriptions.
