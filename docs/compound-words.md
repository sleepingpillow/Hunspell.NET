# Compound Word Support in Hunspell.NET

This document describes the compound word support features in Hunspell.NET, ported from the original Hunspell library.

## Overview

Compound words are words formed by combining two or more words together. Many languages use compound words extensively (German, Dutch, Swedish, Finnish, Hungarian, etc.). Hunspell.NET supports checking and suggesting compound words through various affix file directives.

## Status

✅ **Phase 1 Complete:** Core compound word support is implemented and tested (19 tests).

See [compound-words-remaining.md](compound-words-remaining.md) for a detailed plan of remaining features to implement.

## Compound Word Flags

### COMPOUNDFLAG flag
Words signed with COMPOUNDFLAG may be in compound words (except when word shorter than COMPOUNDMIN). Affixes with COMPOUNDFLAG also permits compounding of affixed words.

**Status:** ✅ Implemented

### COMPOUNDBEGIN flag
Words signed with COMPOUNDBEGIN (or with a signed affix) may be first elements in compound words.

**Status:** ✅ Implemented

### COMPOUNDMIDDLE flag
Words signed with COMPOUNDMIDDLE (or with a signed affix) may be middle elements in compound words.

**Status:** ✅ Implemented

### COMPOUNDLAST / COMPOUNDEND flag
Words signed with COMPOUNDLAST (or with a signed affix) may be last elements in compound words.

**Status:** ✅ Implemented

### ONLYINCOMPOUND flag
Suffixes signed with ONLYINCOMPOUND flag may be only inside of compounds (Fuge-elements in German, fogemorphemes in Swedish). ONLYINCOMPOUND flag works also with words.

**Status:** ✅ Implemented

### COMPOUNDPERMITFLAG flag
Prefixes are allowed at the beginning of compounds, suffixes are allowed at the end of compounds by default. Affixes with COMPOUNDPERMITFLAG may be inside of compounds.

**Status:** ✅ Implemented (parsing only, logic for affix handling to be enhanced)

### COMPOUNDFORBIDFLAG flag
Suffixes with this flag forbid compounding of the affixed word. Dictionary words with this flag are removed from the beginning and middle of compound words, overriding the effect of COMPOUNDPERMITFLAG.

**Status:** ✅ Implemented

### COMPOUNDROOT flag
COMPOUNDROOT flag signs the compounds in the dictionary.

**Status:** ✅ Implemented (parsing only)

## Compound Word Options

### COMPOUNDMIN num
Minimum length of words used for compounding. Default value is 3 letters.

**Status:** ✅ Implemented

### COMPOUNDWORDMAX number
Set maximum word count in a compound word. Default is unlimited.

**Status:** ✅ Implemented

### COMPOUNDMORESUFFIXES
Allow twofold suffixes within compounds.

**Status:** ⚠️ Parsed but not yet fully implemented

## Compound Checking Options

### CHECKCOMPOUNDDUP
Forbid word duplication in compounds (e.g. foofoo).

**Status:** ✅ Implemented

### CHECKCOMPOUNDCASE
Forbid upper case characters at word boundaries in compounds.

**Status:** ✅ Implemented

### CHECKCOMPOUNDTRIPLE
Forbid compounding, if compound word contains triple repeating letters (e.g. foo|ox or xo|oof).

**Status:** ✅ Implemented

### SIMPLIFIEDTRIPLE
Allow simplified 2-letter forms of the compounds forbidden by CHECKCOMPOUNDTRIPLE. Useful for Swedish and Norwegian.

**Status:** ✅ Implemented

### CHECKCOMPOUNDREP
Forbid compounding, if the compound word may be a non-compound word with a REP fault. Useful for languages with 'compound friendly' orthography.

**Status:** ⚠️ Parsed but not yet fully implemented

### CHECKCOMPOUNDPATTERN number_of_definitions
### CHECKCOMPOUNDPATTERN endchars[/flag] beginchars[/flag] [replacement]
Forbid compounding, if the first word in the compound ends with endchars, and the second word begins with beginchars, and optionally use replacement instead.

**Status:** ✅ Implemented (forbids patterns at boundaries, flag checking supported)

### COMPOUNDSYLLABLE max_syllable vowels
Limit compound words by maximum syllable count. Primarily used for Hungarian.

**Status:** ✅ Implemented (allows exceeding COMPOUNDWORDMAX if syllable count is within limit)

## Advanced Features

### COMPOUNDRULE number_of_definitions
### COMPOUNDRULE compound_pattern
Define custom compound patterns with a regex-like syntax. Compound patterns consist of compound flags, parentheses, star and question mark meta characters.

**Status:** ✅ Implemented (supports `*` and `?` quantifiers, basic parentheses)

### BREAK number_of_definitions
### BREAK character_or_character_sequence
Define break points for breaking words and checking word parts separately. Useful for compounding with joining characters (hyphens, etc.).

**Status:** ✅ Implemented (recursive breaking, independent part validation)

## Examples

### Basic Compound Words

**Dictionary file (basic.dic):**
```
3
foo/A
bar/A
test/A
```

**Affix file (basic.aff):**
```
SET UTF-8
COMPOUNDFLAG A
COMPOUNDMIN 3
```

**Usage:**
```csharp
using var spellChecker = new HunspellSpellChecker("basic.aff", "basic.dic");

// Valid compounds
spellChecker.Spell("foobar");     // true - foo + bar
spellChecker.Spell("bartest");    // true - bar + test
spellChecker.Spell("footest");    // true - foo + test
spellChecker.Spell("foobartest"); // true - foo + bar + test

// Still valid as single words
spellChecker.Spell("foo");        // true
spellChecker.Spell("bar");        // true

// Invalid - contains non-dictionary word
spellChecker.Spell("foobaz");     // false
```

### Preventing Duplicate Words

**Affix file (dup.aff):**
```
SET UTF-8
CHECKCOMPOUNDDUP
COMPOUNDFLAG A
```

**Usage:**
```csharp
using var spellChecker = new HunspellSpellChecker("dup.aff", "dup.dic");

spellChecker.Spell("foobar"); // true - different words
spellChecker.Spell("foofoo"); // false - duplicate forbidden
spellChecker.Spell("barbar"); // false - duplicate forbidden
```

### Case-Sensitive Boundaries

**Affix file (case.aff):**
```
SET UTF-8
CHECKCOMPOUNDCASE
COMPOUNDFLAG A
```

**Dictionary file (case.dic):**
```
4
foo/A
Bar/A
BAZ/A
-/A
```

**Usage:**
```csharp
using var spellChecker = new HunspellSpellChecker("case.aff", "case.dic");

// Valid - no lowercase followed by uppercase at boundary
spellChecker.Spell("Barfoo");  // true - uppercase first, then lowercase
spellChecker.Spell("BAZfoo");  // true - uppercase followed by lowercase

// Invalid - lowercase followed by uppercase at boundary
spellChecker.Spell("fooBar");  // false - violates case rule
spellChecker.Spell("fooBAZ");  // false - violates case rule
```

### Position-Specific Flags

**Dictionary file (position.dic):**
```
4
start/B
mid/M
end/E
any/A
```

**Affix file (position.aff):**
```
SET UTF-8
COMPOUNDBEGIN B
COMPOUNDMIDDLE M
COMPOUNDEND E
COMPOUNDFLAG A
```

**Usage:**
```csharp
using var spellChecker = new HunspellSpellChecker("position.aff", "position.dic");

// Valid - respects position constraints
spellChecker.Spell("startend");       // true - start first, end last
spellChecker.Spell("startmidend");    // true - start, mid, end
spellChecker.Spell("anyany");         // true - COMPOUNDFLAG works anywhere

// Invalid - wrong positions
spellChecker.Spell("endstart");       // false - end can't be first
spellChecker.Spell("midend");         // false - mid can't be first
```

### COMPOUNDRULE Patterns

**Affix file (compoundrule.aff):**
```
COMPOUNDMIN 1
COMPOUNDRULE 1
COMPOUNDRULE ABC
```

**Dictionary file (compoundrule.dic):**
```
3
a/A
b/B
c/BC
```

**Usage:**
```csharp
using var spellChecker = new HunspellSpellChecker("compoundrule.aff", "compoundrule.dic");

// Valid - matches pattern ABC (flag A, then B, then C)
spellChecker.Spell("abc");  // true - a/A + b/B + c/BC
spellChecker.Spell("acc");  // true - a/A + c/BC + c/BC (c has both B and C)

// Invalid - doesn't match pattern
spellChecker.Spell("ab");   // false - only 2 parts, need 3
spellChecker.Spell("ba");   // false - wrong order
```

**Star quantifier example (A*B*C*):**
```csharp
// Pattern A*B*C* means: zero or more A, then zero or more B, then zero or more C
using var spellChecker = new HunspellSpellChecker("star.aff", "star.dic");

spellChecker.Spell("aa");     // true - A* (2 A's)
spellChecker.Spell("abc");    // true - A*B*C*
spellChecker.Spell("aabbcc"); // true - multiple of each
spellChecker.Spell("ba");     // false - wrong order
```

### CHECKCOMPOUNDPATTERN (Boundary Patterns)

**Affix file (checkcompoundpattern.aff):**
```
COMPOUNDFLAG A
CHECKCOMPOUNDPATTERN 2
CHECKCOMPOUNDPATTERN oo e
CHECKCOMPOUNDPATTERN ss s
```

**Dictionary file (checkcompoundpattern.dic):**
```
5
foo/A
bar/A
boss/A
set/A
eat/A
```

**Usage:**
```csharp
using var spellChecker = new HunspellSpellChecker("checkcompoundpattern.aff", "checkcompoundpattern.dic");

// Valid - doesn't match forbidden patterns
spellChecker.Spell("foobar");  // true - foo + bar (no forbidden pattern)
spellChecker.Spell("barfoo");  // true - bar + foo (no forbidden pattern)

// Invalid - matches forbidden pattern "oo e"
spellChecker.Spell("fooeat");  // false - foo (ends with "oo") + eat (starts with "e")

// Invalid - matches forbidden pattern "ss s"
spellChecker.Spell("bossset"); // false - boss (ends with "ss") + set (starts with "s")
```

## Implementation Notes

- Compound word support is implemented in the `AffixManager` and `Hunspell` classes
- Compound checking is performed during spell checking
- Compound-related flags are parsed from the affix file
- Tests are located in `tests/Hunspell.Tests/CompoundTests.cs`

## References

- [Original Hunspell Manual](https://github.com/hunspell/hunspell)
- Hunspell man page (hunspell.5)
