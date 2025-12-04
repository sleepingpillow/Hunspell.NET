# Upstream Test Parity Status

This document tracks the status of Hunspell.NET's compatibility with the upstream Hunspell test suite.

## Summary

| Category | Active | Commented | Total | Pass Rate |
|----------|--------|-----------|-------|-----------|
| GoodWords (Root Level) | 73 | 41 | 114 | 64% |
| GoodWords (Nested) | 20 | 0 | 20 | 100% |
| WrongWords (Root Level) | 76 | 21 | 97 | 78% |
| WrongWords (Nested) | 20 | 0 | 20 | 100% |
| Suggestions | 6 | 0 | 6 | 100% |
| **Total** | **195** | **62** | **257** | **76%** |

*All 195 active tests are passing. The 62 commented-out tests represent features not yet implemented.*

## Test Categories

### Passing Tests

The following upstream test categories are fully passing:

#### Core Features
- ✅ Basic spell checking (`base`, `base_utf`)
- ✅ Affix handling (`alias`, `alias2`, `fullstrip`, `zeroaffix`)
- ✅ Case handling (`allcaps`, `allcaps2`, `allcaps3`, `allcaps_utf`, `keepcase`)
- ✅ Compound words (`compoundflag`, `compoundforbid`, `compoundaffix`, `compoundaffix2`, `compoundaffix3`)
- ✅ COMPOUNDRULE (`compoundrule` through `compoundrule8`)
- ✅ Break patterns (`break`, `breakdefault`, `breakoff`)
- ✅ Forbidden words (`forbiddenword`, `nosuggest`)
- ✅ NEEDAFFIX basic (`needaffix`)
- ✅ ONLYINCOMPOUND (`onlyincompound`)
- ✅ Force uppercase (`forceucase`)
- ✅ UTF-8 support (`utf8`, `utf8_bom`, `utf8_bom2`, `utf8_nonbmp`)
- ✅ Ignore characters (`ignore`, `ignoreutf`)
- ✅ Suggestions (`sug`, `sug2`, `sugutf`, `rep`, `reputf`, `ph`, `ph2`, `phone`)
- ✅ Word pairs (`wordpair`)
- ✅ Map replacements (`map`, `maputf`)
- ✅ Output conversion (`oconv`)
- ✅ Arabic and RTL basics (`arabic`)
- ✅ Korean (`korean`)
- ✅ Dotless I (`dotless_i`)
- ✅ Warnings (`warn`)
- ✅ Time limits (`timelimit`)

#### Compound Checking
- ✅ CHECKCOMPOUNDCASE (`checkcompoundcase`, `checkcompoundcaseutf`)
- ✅ CHECKCOMPOUNDDUP (`checkcompounddup`)
- ✅ CHECKCOMPOUNDTRIPLE (`checkcompoundtriple`)
- ✅ CHECKCOMPOUNDREP basic (`checkcompoundrep2`)
- ✅ CHECKCOMPOUNDPATTERN replacements (`checkcompoundpattern3`, `checkcompoundpattern4`)
- ✅ Compound typo guard (`limit-multiple-compounding`)

#### Advanced Affix Features
- ✅ CIRCUMFIX superlatives (`circumfix`)

#### Swedish/Scandinavian
- ✅ Swedish directives (`swedish/directives`)
- ✅ Swedish Finland (`swedish/sv_FI`)
- ✅ Swedish Sweden (`swedish/sv_SE`)

#### Dutch (OpenTaal)
- ✅ Forbidden words (`opentaal_forbiddenword1`, `opentaal_forbiddenword2`)

### Failing Tests (Commented Out)

The following test categories are not yet fully implemented:

#### CHECKSHARPS (German ß Handling)
- ❌ `checksharps` - CHECKSHARPS directive not implemented
- ❌ `checksharpsutf` - CHECKSHARPS UTF-8 not implemented

#### CHECKCOMPOUNDPATTERN
- ❌ `checkcompoundpattern` - Pattern matching at compound boundaries
- ❌ `checkcompoundpattern2` - Advanced pattern matching

#### COMPLEXPREFIXES (Right-to-Left Languages)
- ❌ `complexprefixes` - RTL prefix handling
- ❌ `complexprefixes2` - RTL prefix combinations
- ❌ `complexprefixesutf` - RTL UTF-8 handling

#### FLAG Types
- ❌ `flag` - FLAG directive type handling
- ❌ `flaglong` - FLAG long type (two-character flags)
- ❌ `flagnum` - FLAG num type (numeric flags)
- ❌ `flagutf8` - FLAG UTF-8 type

#### ICONV/OCONV (Character Conversion)
- ❌ `iconv` - Input character conversion
- ❌ `iconv2` - Advanced input conversion
- ❌ `oconv2` - Output conversion edge cases

#### German Compounding
- ❌ `germancompounding` - German compound word rules
- ❌ `germancompoundingold` - Legacy German compound rules

#### Hungarian
- ❌ `hu` - Hungarian language-specific features

#### Advanced Features
- ❌ `affixes` - Advanced affix condition handling
- ❌ `alias3` - Advanced alias handling
- ❌ `condition` - Advanced condition matching
- ❌ `conditionalprefix` - Conditional prefix application
- ❌ `encoding` - Non-UTF8 encoding handling
- ❌ `fogemorpheme` - FOGEMORPHEME directive
- ❌ `ignoresug` - IGNORESUG directive
- ❌ `morph` - Morphological analysis
- ❌ `simplifiedtriple` - SIMPLIFIEDTRIPLE directive

#### NEEDAFFIX Advanced
- ❌ `needaffix2` - Advanced NEEDAFFIX handling
- ❌ `needaffix4` - Advanced NEEDAFFIX handling
- ❌ `needaffix5` - Advanced NEEDAFFIX handling

#### Other Language-Specific
- ❌ `nepali` - Nepali language features
- ❌ `right_to_left_mark` - RTL mark handling

#### Bug Tracker Tests
- ❌ `1592880` - Specific bug fix
- ❌ `1706659` - Specific bug fix
- ❌ `1975530` - Specific bug fix
- ❌ `2970242` - Specific bug fix
- ❌ `2999225` - Specific bug fix
- ❌ `i53643` - Bug tracker issue
- ❌ `i54633` - Bug tracker issue
- ❌ `i54980` - Bug tracker issue
- ❌ `i58202` - Bug tracker issue (partial)
- ❌ `i68568` - Bug tracker issue
- ❌ `i68568utf` - Bug tracker issue

#### OpenTaal Advanced
- ✅ `opentaal_cpdpat` - OpenTaal compound pattern
- ❌ `opentaal_cpdpat2` - OpenTaal compound pattern
- ✅ `opentaal_keepcase` - OpenTaal keepcase handling

## Priority Areas for Implementation

Based on the failing tests, the following areas should be prioritized for implementation:

### High Priority
1. **FLAG Types** - Enabling `FLAG long`, `FLAG num`, and `FLAG UTF-8` would unlock many dictionaries
2. **CHECKSHARPS** - Required for proper German language support
3. **CHECKCOMPOUNDPATTERN** - Needed for advanced compound word validation
4. **ICONV/OCONV** - Character conversion for non-UTF8 dictionaries

### Medium Priority
1. **German Compounding** - Full German compound word support
2. **COMPLEXPREFIXES** - Right-to-left language support
3. **Advanced NEEDAFFIX** - Complex NEEDAFFIX scenarios

### Lower Priority
1. **Morphological Analysis** - Word form analysis
2. **FOGEMORPHEME** - Morpheme boundaries
3. **SIMPLIFIEDTRIPLE** - Triple letter simplification
4. **Language-specific fixes** - Hungarian, Nepali, etc.

## Test File Location

Upstream test files are located in:
```
tests/Hunspell.Tests/dictionaries/
```

Each test directory contains:
- `*.aff` - Affix file
- `*.dic` - Dictionary file
- `*.good` - Words that should be accepted
- `*.wrong` - Words that should be rejected
- `*.sug` - Expected suggestions (optional)

## Running Tests

```bash
# Run all upstream tests
dotnet test tests/Hunspell.Tests/Hunspell.Tests.csproj --filter "FullyQualifiedName~Upstream"

# Run only GoodWords tests
dotnet test tests/Hunspell.Tests/Hunspell.Tests.csproj --filter "FullyQualifiedName~GoodWords"

# Run only WrongWords tests
dotnet test tests/Hunspell.Tests/Hunspell.Tests.csproj --filter "FullyQualifiedName~WrongWords"
```

## Contributing

When implementing a new feature:
1. Uncomment the related tests in `UpstreamAffixAndCompoundTests.cs`
2. Run tests to verify the new implementation
3. Update this status document
4. Submit a pull request

## Last Updated

2025-12-04 - Added compoundaffix3 WrongWords coverage and updated counts
2024-11-29 - Initial upstream test evaluation
