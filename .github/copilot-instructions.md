# GitHub Copilot Instructions for Hunspell.NET

## Project Overview

Hunspell.NET is a modern .NET 10 port of the Hunspell spell checker library, targeting C# 13. This is an **accuracy-first port** with a strict goal of 100% behavioral compatibility with upstream Hunspell. All implementation decisions must preserve upstream behavior exactly - no breaking changes are acceptable. Modern .NET idioms are used only where they don't alter behavior.

## Architecture

### Three-Layer Design
1. **HunspellSpellChecker** (`src/Hunspell/Hunspell.cs`) - Public API facade
2. **HashManager** (`src/Hunspell/HashManager.cs`) - Dictionary storage and lookup with homonym support
3. **AffixManager** (`src/Hunspell/AffixManager.cs`) - Affix rules, compound words, and suggestion generation

### Critical Initialization Pattern
```csharp
// MUST read encoding from .aff BEFORE loading .dic
var encodingHint = AffixManager.ReadDeclaredEncodingFromAffix(affixPath);
_hashManager = new HashManager(dictionaryPath, encodingHint);
_affixManager = new AffixManager(affixPath, _hashManager);
```
**Why:** Many upstream tests use legacy encodings (ISO-8859-1, ISO-8859-15, CP1250). The `SET` directive in `.aff` files declares encoding for `.dic` files. Reading this first prevents mojibake.

### Homonym Handling
HashManager stores `Dictionary<string, List<WordEntry>>` to support multiple dictionary entries for the same word with different flags (homonyms). Key methods:
- `GetWordFlags()` - Returns merged/deduplicated flags from all variants
- `GetWordFlagVariants()` - Returns per-entry flag strings (use for multi-char flag detection)

**Pattern:** For flags like ONLYINCOMPOUND/NEEDAFFIX, check if **ALL** variants have the flag (use `.All()`), not `.Any()`. A word is valid if at least one variant allows it.

## Testing Workflow

### Upstream Test Compatibility (Primary Goal)
- **Status:** 196/257 tests passing (76%)
- **Location:** `tests/Hunspell.Tests/UpstreamAffixAndCompoundTests.cs`
- **Tracking:** `docs/upstream-test-status.md`

#### Running Tests
```powershell
# Full test suite (394 tests including 196 upstream)
dotnet test

# Only upstream tests
dotnet test --filter "FullyQualifiedName~Upstream"

# Specific test by name
dotnet test --filter "DisplayName~1592880"

# Category filters
dotnet test --filter "FullyQualifiedName~GoodWords"
dotnet test --filter "FullyQualifiedName~WrongWords"
```

#### Enabling Commented Tests
Workflow for porting new upstream features:
1. Find commented `[InlineData("testname")]` in `UpstreamAffixAndCompoundTests.cs`
2. Uncomment and run test to identify missing features
3. Implement feature (check `docs/affix-directives.md` for specs)
4. Fix any `.Any()` vs `.All()` logic for flags (homonym handling)
5. Update `docs/upstream-test-status.md` with new counts and feature description
6. **Pattern:** Always run full suite after changes - compound logic is interconnected

### Test Data Structure
```
tests/Hunspell.Tests/dictionaries/<testname>/
  <testname>.aff   # Affix rules and directives
  <testname>.dic   # Word list with flags
  <testname>.good  # Words that MUST be accepted
  <testname>.wrong # Words that MUST be rejected
  <testname>.sug   # Expected suggestions (optional)
```

## Code Conventions

### Modern C# 13 Features (Required)
- File-scoped namespaces: `namespace Hunspell;` (no braces)
- Nullable reference types enabled: Use `?` for nullable, never `!` suppression
- Primary constructors for simple classes
- Collection expressions: `[item1, item2]` not `new List<T> { }`
- Pattern matching: Use `is` patterns and switch expressions
- `ObjectDisposedException.ThrowIf(_disposed, this)` - not manual checks

### Flag Handling Patterns
```csharp
// WRONG - misses multi-char flags in "long" format
var flags = _hashManager.GetWordFlags(word);
if (flags.Contains(_onlyInCompound)) { }

// RIGHT - preserves multi-char flags like "cc"
var variants = _hashManager.GetWordFlagVariants(word).ToList();
return variants.All(v => !string.IsNullOrEmpty(v) && v.Contains(_flag));
```

### Compound Word Checking
Compound validation in `AffixManager.CheckCompoundRecursive()` uses depth-first search with:
- `COMPOUNDMIN` - Minimum part length (default 3)
- `COMPOUNDWORDMAX` - Maximum word count
- Flag checks: COMPOUNDBEGIN, COMPOUNDMIDDLE, COMPOUNDEND, COMPOUNDFORBIDFLAG
- Pattern matching: CHECKCOMPOUNDPATTERN with replacement rules
- Boundary checks: CHECKCOMPOUNDDUP, CHECKCOMPOUNDCASE, CHECKCOMPOUNDTRIPLE

**Critical:** Compound checking is recursive and stateful. Changes affect multiple test categories.

## Documentation Standards

When implementing new features:
1. Update `docs/upstream-test-status.md` summary table and feature list
2. Add feature description to `docs/affix-directives.md` or `docs/compound-words.md`
3. Mark implementation status: ✅ (implemented), ❌ (not implemented), ⚠️ (partial)
4. Include code examples in docs showing `.aff` directive usage

## Common Pitfalls

1. **Encoding Issues:** Always pass `encodingHint` from affix to HashManager. Normalize ISO encodings: `ISO8859-15` → `ISO-8859-15` (with hyphens)

2. **Flag Format Confusion:** Dictionary entries can use four flag formats (SET in .aff):
   - Single: one char = one flag (`FLAG` directive absent or `FLAG single`)
   - Long: two chars = one flag (`FLAG long`)
   - Num: comma-separated numbers (`FLAG num`)
   - UTF-8: UTF-8 encoded chars (`FLAG UTF-8`)

3. **Homonym Logic:** Don't use `.Any()` for restrictive flags (ONLYINCOMPOUND, NEEDAFFIX, FORBIDDENWORD). Use `.All()` - word is invalid only if ALL variants forbid it.

4. **Case Handling:** Hunspell is case-insensitive by default but KEEPCASE flag enforces exact casing. Check `KeepCaseFlag` before accepting capitalized forms.

5. **Compound Word Debugging:** Enable debug output with `DEBUG: CheckCompound` logging to trace recursive compound validation paths.

6. **Behavioral Compatibility:** Never modify behavior to "improve" on upstream Hunspell. If upstream has quirks or inconsistencies, replicate them exactly. The goal is bit-for-bit compatibility with upstream test results.

## Priority Implementation Areas

Based on `docs/upstream-test-status.md`:
1. **FLAG types** (long/num/UTF-8) - unlocks 20+ tests
2. **CHECKSHARPS** (German ß handling) - 2 tests
3. **CHECKCOMPOUNDPATTERN** advanced - 2 tests
4. **COMPLEXPREFIXES** (RTL languages) - 3 tests

## Build and Samples

- **Build:** `dotnet build` (must have .NET 10 SDK)
- **Samples:** `samples/` directory has WinForms, MAUI, and console examples
- **Tools:** `src/Tools.Munch/` - dictionary munching tool (affix application)
