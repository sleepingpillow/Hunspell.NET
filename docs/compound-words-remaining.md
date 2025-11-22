# Hunspell.NET Compound Word Features - Remaining Implementation Plan

This document outlines the remaining compound word features to be ported from the original Hunspell library, along with a recommended implementation order.

## Already Implemented ✅

The following features are already implemented in the current PR:

### Core Flags
- ✅ COMPOUNDFLAG - Words that can be in any position of compounds
- ✅ COMPOUNDBEGIN - Words that can be first elements
- ✅ COMPOUNDMIDDLE - Words that can be middle elements  
- ✅ COMPOUNDLAST/COMPOUNDEND - Words that can be last elements
- ✅ ONLYINCOMPOUND - Elements only valid inside compounds (fuge-elements)
- ✅ COMPOUNDFORBIDFLAG - Forbid specific words in compounds
- ✅ COMPOUNDROOT - Mark compounds in dictionary (parsing only)
- ✅ COMPOUNDPERMITFLAG - Permit affixes inside compounds (parsing only)

### Options
- ✅ COMPOUNDMIN - Minimum word length for compounding (default: 3)
- ✅ COMPOUNDWORDMAX - Maximum word count in compound

### Checking Rules
- ✅ CHECKCOMPOUNDDUP - Forbid consecutive duplicate words
- ✅ CHECKCOMPOUNDCASE - Forbid lowercase→uppercase at boundaries
- ✅ CHECKCOMPOUNDTRIPLE - Forbid triple repeating letters
- ✅ SIMPLIFIEDTRIPLE - Allow 2-letter simplified forms

## Remaining Features to Implement

### Priority 1: High Impact Features (Recommended First)

#### 1. COMPOUNDRULE (Regex-like Compound Patterns)
**Complexity:** High  
**Impact:** Very High  
**Estimated Effort:** 3-4 days  
**Status:** ✅ **IMPLEMENTED**

Define custom compound patterns with a regex-like syntax for precise control over compound formation.

**Syntax:**
```
COMPOUNDRULE number_of_definitions
COMPOUNDRULE pattern
```

**Example:**
```
COMPOUNDRULE 2
COMPOUNDRULE (A?)B(C|D)*
COMPOUNDRULE N*M
```

**Implementation details:**
- ✅ Parser for compound rule patterns
- ✅ Pattern matching engine supporting `*` (0 or more), `?` (0 or 1)
- ✅ Basic flag group handling with parentheses
- ✅ Integration with compound checking algorithm
- ⚠️ Advanced features like `|` (alternation) in groups not yet implemented
- ⚠️ Performance optimization needed for complex patterns (list copying)

**Known limitations:**
- Group expressions like `(AB)*` treat the group as a single flag, not individual flags
- Pattern matching creates list copies on each recursion (optimization opportunity)

**Test coverage:** 5 tests covering basic patterns, star quantifier, and validation

**Why Priority 1:** Used extensively in real-world dictionaries (e.g., en_US for ordinal numbers like "1st", "2nd"). Provides fine-grained control over compound patterns.

---

#### 2. CHECKCOMPOUNDPATTERN (Boundary Pattern Checking)
**Complexity:** Medium  
**Impact:** High  
**Estimated Effort:** 2-3 days  
**Status:** ✅ **IMPLEMENTED**

Forbid specific character patterns at compound boundaries, with optional replacement.

**Syntax:**
```
CHECKCOMPOUNDPATTERN number_of_definitions
CHECKCOMPOUNDPATTERN endchars[/flag] beginchars[/flag] [replacement]
```

**Example:**
```
CHECKCOMPOUNDPATTERN 3
CHECKCOMPOUNDPATTERN o e
CHECKCOMPOUNDPATTERN aa/X bb/Y replacement
```

**Implementation details:**
- ✅ Pattern storage and parsing
- ✅ Boundary pattern matching at compound split points
- ✅ Flag checking for patterns
- ⚠️ Replacement handling parsed but not yet fully implemented

**Known limitations:**
- Replacement parameter is parsed but not applied during compound checking

**Test coverage:** 5 tests covering forbidden patterns and flag validation

**Why Priority 1:** Essential for languages with specific phonetic/orthographic rules at compound boundaries (German, Hungarian).

---

### Priority 2: Language-Specific Features

#### 3. COMPOUNDSYLLABLE (Syllable-based Compound Limits)
**Complexity:** Medium-High  
**Impact:** Medium (language-specific)  
**Estimated Effort:** 2-3 days  
**Status:** ✅ **IMPLEMENTED**

Limit compound words by maximum syllable count (primarily for Hungarian).

**Syntax:**
```
COMPOUNDSYLLABLE max_syllable vowels
```

**Example:**
```
COMPOUNDSYLLABLE 15 áéíóöőúüű
```

**Implementation details:**
- ✅ Syllable counting algorithm based on vowel patterns
- ✅ Integration with compound validation
- ✅ Per-language vowel definitions
- ✅ Exception handling: allows more words than COMPOUNDWORDMAX if syllable count is within limit

**Test coverage:** 4 tests covering syllable limits, word count exceptions, and validation

**Why Priority 2:** Highly specific to Hungarian and similar languages. Essential for correct Hungarian compound word processing.

**Test files in Hunspell:** ~4 test files

**Why Priority 2:** Highly specific to Hungarian and similar languages. Lower priority unless targeting these languages.

---

#### 4. BREAK (Word Breaking for Compound Analysis)
**Complexity:** Medium  
**Impact:** Medium  
**Estimated Effort:** 2-3 days  
**Status:** ✅ **IMPLEMENTED**

Define break points for splitting words into parts for independent checking.

**Syntax:**
```
BREAK number_of_definitions
BREAK character_or_character_sequence
```

**Example:**
```
BREAK 3
BREAK -
BREAK --
BREAK n-
```

**Implementation details:**
- ✅ Break point definitions storage
- ✅ Recursive word splitting logic at break points
- ✅ Independent validation of split parts
- ⚠️ Support for `^` (start) and `$` (end) markers not yet implemented

**Test coverage:** 5 tests covering breaking, recursive breaking, and validation

**Why Priority 2:** Useful for hyphenated compounds and dashes, though COMPOUNDRULE often provides better control for structured compounds.

---

### Priority 3: Enhancement Features

#### 5. COMPOUNDMORESUFFIXES (Full Implementation)
**Complexity:** Medium  
**Impact:** Low-Medium  
**Estimated Effort:** 1-2 days

Allow twofold suffixes within compounds (currently parsed but not implemented).

**Implementation needs:**
- Track suffix application count per compound part
- Allow multiple suffixes on compound parts
- Integration with existing affix system

**Test files in Hunspell:** Tested as part of other compound tests

**Why Priority 3:** Enhancement for complex morphology. Already parsed, just needs logic implementation.

---

#### 6. CHECKCOMPOUNDREP (Full Implementation)
**Complexity:** Medium  
**Impact:** Low-Medium  
**Estimated Effort:** 1-2 days  
**Status:** ✅ **IMPLEMENTED** (basic form)

Forbid compounds that might be non-compounds with REP (replacement) faults (currently parsed but not implemented).

**Syntax:**
```
CHECKCOMPOUNDREP
```

**Implementation details:**
- ✅ REP table parsing from affix file
- ✅ Check if compound matches dictionary word via REP substitution
- ⚠️ Full recursive checking (compounds containing forbidden parts) not yet implemented

**Known limitations:**
- Only checks the compound word itself, not recursively checking if compound parts are forbidden
- Example: "szervíz" (matches "szerviz" via REP) is forbidden, but "szervízkocsi" (containing "szervíz") is not yet caught

**Test coverage:** 3 tests covering basic REP matching and dictionary word validation

**Why Priority 3:** Specific to languages with "compound-friendly" orthography. Lower priority than core features.

---

### Priority 4: Advanced Features

#### 7. Affix Support in Compounds (COMPOUNDPERMITFLAG Enhancement)
**Complexity:** High  
**Impact:** Medium  
**Estimated Effort:** 3-4 days

Full implementation of affixed words in compounds, beyond parsing.

**Implementation needs:**
- Generate affixed forms during compound checking
- Track which affixes are allowed via COMPOUNDPERMITFLAG
- Respect COMPOUNDFORBIDFLAG on affixed forms
- Handle prefix/suffix positioning rules

**Why Priority 4:** Complex interaction with existing affix system. Requires careful integration.

---

## Recommended Implementation Order

### Phase 1: Core Pattern Support (6-7 days)
1. **COMPOUNDRULE** - Most impactful, widely used
2. **CHECKCOMPOUNDPATTERN** - Essential for boundary rules

**Outcome:** Covers ~80% of real-world compound word scenarios

### Phase 2: Language-Specific Features (4-6 days)  
3. **COMPOUNDSYLLABLE** - For Hungarian support
4. **BREAK** - For hyphenated compounds

**Outcome:** Full support for Hungarian, German, and hyphen-based compounding

### Phase 3: Enhancements (2-4 days)
5. **COMPOUNDMORESUFFIXES** - Complete existing feature
6. **CHECKCOMPOUNDREP** - Complete existing feature

**Outcome:** All compound checking rules fully implemented

### Phase 4: Advanced Integration (3-4 days)
7. **Affix Support Enhancement** - Full affix+compound integration

**Outcome:** Complete feature parity with original Hunspell

---

## Total Estimated Effort

- **Phase 1 (Essential):** 6-7 days
- **Phase 2 (Language-specific):** 4-6 days  
- **Phase 3 (Enhancements):** 2-4 days
- **Phase 4 (Advanced):** 3-4 days

**Total:** 15-21 days for complete implementation

---

## Testing Strategy

For each feature:
1. Port relevant test files from Hunspell test suite (~133 compound-related test files available)
2. Create minimal reproduction tests
3. Add edge case tests
4. Validate against original Hunspell behavior

---

## Documentation Updates

As each feature is implemented:
1. Update `docs/compound-words.md` with feature status
2. Add usage examples
3. Document any .NET-specific implementation details
4. Update README.md if feature is significant

---

## Notes

- **Backward Compatibility:** All changes maintain backward compatibility with existing code
- **Performance:** Consider caching compiled COMPOUNDRULE patterns for performance
- **Testing:** Original Hunspell has 133 compound-related test files - excellent test coverage available
- **Real-world Usage:** COMPOUNDRULE and CHECKCOMPOUNDPATTERN are used in major dictionaries (en_US, de_DE, hu_HU)

---

## Success Criteria

**Minimum Viable (Phase 1):**
- COMPOUNDRULE working with real dictionaries
- CHECKCOMPOUNDPATTERN handling boundary cases
- Tests passing for both features

**Full Feature Parity (All Phases):**
- All compound-related affix directives supported
- Test coverage matching original Hunspell
- Documentation complete
- Performance benchmarked and acceptable
