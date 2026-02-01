# Hunspell.NET

A modern .NET 10 port of the popular Hunspell spell checker library. This implementation leverages the latest C# 13 features and .NET 10 capabilities to provide a high-performance, idiomatic .NET spelling and suggestion engine.

## Features

- ✅ **Spell Checking**: Fast and accurate word validation
- ✅ **Suggestion Generation**: Intelligent suggestions for misspelled words
- ✅ **Morphological Analysis**: Extract linguistic information from words (NEW)
- ✅ **Stemming**: Reduce words to their base forms (NEW)
- ✅ **Compound Word Support**: Full support for compound words with customizable rules
- ✅ **Runtime Dictionary Management**: Add/remove words at runtime
- ✅ **Dictionary File Support**: Compatible with standard Hunspell .dic and .aff files
- ✅ **Modern C# 13**: Uses latest language features including file-scoped namespaces, record types, and pattern matching
- ✅ **UTF-8 Support**: Full Unicode support for international languages
- ✅ **High Performance**: Optimized for .NET 10 runtime

## Requirements

- .NET 10.0 or later

## Installation

```bash
dotnet add package Hunspell.NET
```

## Quick Start

```csharp
using Hunspell;

// Create a spell checker with dictionary files
using var spellChecker = new HunspellSpellChecker("en_US.aff", "en_US.dic");

// Check if a word is spelled correctly
bool isCorrect = spellChecker.Spell("hello");  // true
bool isMisspelled = spellChecker.Spell("helo"); // false

// Get suggestions for a misspelled word
List<string> suggestions = spellChecker.Suggest("helo");
// Returns: ["hello", "help", "hero", ...]

// Add a word to the runtime dictionary
spellChecker.Add("dotnet");

// Remove a word from the runtime dictionary
spellChecker.Remove("dotnet");

// Get dictionary encoding
string encoding = spellChecker.DictionaryEncoding; // "UTF-8"
```

## Morphological Analysis and Stemming

Hunspell.NET now supports morphological analysis and stemming for extracting linguistic information from words.

### Stemming

Reduce words to their base forms:

```csharp
using var spellChecker = new HunspellSpellChecker("en_US.aff", "en_US.dic");

// Get the stem(s) of a word
var stems = spellChecker.Stem("running");
// Returns: ["run"]

var stems2 = spellChecker.Stem("drinkables");
// Returns: ["drinkable"]
```

### Morphological Analysis

Extract detailed linguistic information:

```csharp
var analyses = spellChecker.Analyze("drinks");
// Returns: ["po:noun is:plur", "po:verb al:drank al:drunk ts:present is:sg_3"]
// Tags: po=part of speech, is=inflectional suffix, al=allomorph, ts=tense
```

**Morphological tags supported:**
- `st:` - stem
- `po:` - part of speech (noun, verb, adj, etc.)
- `al:` - allomorph/alternate form
- `ts:` - tense/aspect
- `is:` - inflectional suffix (plur, sg_3, past, etc.)
- `ds:` - derivational suffix
- `dp:` - derivational prefix
- `sp:` - surface prefix

For complete documentation and examples, see [docs/morphological-analysis.md](docs/morphological-analysis.md).

## Dictionary Files

Hunspell.NET uses standard Hunspell dictionary formats:

- **`.dic` file**: Contains the word list
- **`.aff` file**: Contains affix rules and configuration

You can use existing Hunspell dictionaries from various sources or create custom ones.

### Example Dictionary Files

**test.dic**:
```
5
hello
world
test
example
spell
```

**test.aff**:
```
SET UTF-8
TRY esianrtolcdugmphbyfvkwzESIANRTOLCDUGMPHBYFVKWZ
```

## Advanced Usage

### Case-Insensitive Spell Checking

```csharp
using var spellChecker = new HunspellSpellChecker("en_US.aff", "en_US.dic");

// All variants are correctly recognized
bool result1 = spellChecker.Spell("HELLO"); // true
bool result2 = spellChecker.Spell("Hello"); // true
bool result3 = spellChecker.Spell("hello"); // true
```

### Custom Suggestion Handling

```csharp
using var spellChecker = new HunspellSpellChecker("en_US.aff", "en_US.dic");

string misspelledWord = "recieve";
var suggestions = spellChecker.Suggest(misspelledWord);

if (suggestions.Count > 0)
{
    Console.WriteLine($"Did you mean: {suggestions[0]}?");
    foreach (var suggestion in suggestions.Skip(1))
    {
        Console.WriteLine($"  or: {suggestion}");
    }
}
```

### Resource Management

The `HunspellSpellChecker` class implements `IDisposable` for proper resource cleanup:

```csharp
// Using statement (recommended)
using var spellChecker = new HunspellSpellChecker("en_US.aff", "en_US.dic");

// Manual disposal
var spellChecker = new HunspellSpellChecker("en_US.aff", "en_US.dic");
try
{
    // Use spell checker
}
finally
{
    spellChecker.Dispose();
}
```

### Compound Words

Hunspell.NET supports compound words - words formed by combining multiple words together. This is essential for languages like German, Dutch, Swedish, Finnish, and others.

```csharp
// Dictionary with compound support
using var spellChecker = new HunspellSpellChecker("de_DE.aff", "de_DE.dic");

// Check compound words
bool isValid = spellChecker.Spell("Donaudampfschiff"); // true (German compound)

// Compound rules can be configured in the .aff file
// See docs/compound-words.md for detailed documentation
```

**Supported compound features:**
- COMPOUNDFLAG, COMPOUNDBEGIN, COMPOUNDMIDDLE, COMPOUNDEND
- COMPOUNDMIN, COMPOUNDWORDMAX
- CHECKCOMPOUNDDUP, CHECKCOMPOUNDCASE, CHECKCOMPOUNDTRIPLE
- ONLYINCOMPOUND, COMPOUNDPERMITFLAG, COMPOUNDFORBIDFLAG
- And more...

For complete documentation, see [docs/compound-words.md](docs/compound-words.md).

## Modern .NET 10 Features Used

This port takes full advantage of .NET 10 and C# 13 features:

- **File-scoped namespaces**: Cleaner code structure
- **Record types**: Immutable data structures for affix rules and word entries
- **Pattern matching**: Enhanced switch expressions and is patterns
- **Nullable reference types**: Compile-time null safety
- **String.Create**: Zero-allocation string building for suggestions
- **Collection expressions**: Modern collection initialization
- **Primary constructors**: Simplified class definitions
- **Required members**: Enforced initialization
- **Raw string literals**: Better multiline string handling

## Architecture

The library consists of three main components:

1. **HunspellSpellChecker**: Main API for spell checking and suggestions
2. **HashManager**: Efficient dictionary storage and lookup
3. **AffixManager**: Handles affix rules and suggestion generation

## Performance

Hunspell.NET is optimized for modern .NET runtime:

- Uses `Dictionary<TKey, TValue>` with optimized hash codes
- Employs `Span<T>` for zero-allocation string operations
- Leverages `string.Create` for efficient string building
- Minimizes allocations in hot paths

## License

Licensed under MPL 1.1/GPL 2.0/LGPL 2.1 (tri-license), the same as the original Hunspell library.

## Contributing

Contributions are welcome! This is an AI-driven port aiming for accuracy and modern .NET idioms.

## Credits

Based on the original [Hunspell](https://github.com/hunspell/hunspell) library by László Németh and contributors.

## Links

- [Original Hunspell Project](https://github.com/hunspell/hunspell)
- [Hunspell Website](https://hunspell.github.io/)

