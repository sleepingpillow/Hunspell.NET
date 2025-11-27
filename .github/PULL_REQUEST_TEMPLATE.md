## What this PR does

This PR imports a large set of upstream Hunspell affix/compound test datasets into the repository's test harness and adjusts spell-check behavior to match upstream Hunspell semantics where required.

Key items added and validated in tests/Hunspell.Tests/dictionaries:
- rep
- checkcompoundrep2
- checkcompoundcase
- checkcompounddup
- checkcompoundtriple
- needaffix
- checkcompoundrep/basic
- checkcompoundpattern/basic and replacement
- compoundrule variants (basic, compoundrule, compoundrule2..8, star)
- compoundmoresuffixes/basic
- compoundsyllable/syllable
- condition/condition
- swedish (directives, sv_FI, sv_SE)
- affixes/affixes
- break/basic

## Why this change is needed

We want to run upstream Hunspell test cases unmodified in our xUnit harness to ensure Hunspell.NET's behavior matches the canonical Hunspell implementation. These datasets help catch semantic differences in compound/affix handling and provide a regression suite.

## Implementation notes / key fixes

- Implemented positional REP matching for compound checks (position-aware replacements) to match upstream semantics.
- Implemented robust COMPOUNDRULE parsing and matching (digits, spelled numbers, ordinals, quantifiers) to replicate upstream behavior.
- Fixed CHECKCOMPOUNDDUP, CHECKCOMPOUNDCASE, COMPOUNDFORBID and position-specific COMPOUNDFLAG behavior.
- Added support for multi-line aff directive values used by upstream test files.

## Test plan

All new tests are included in `tests/Hunspell.Tests/UpstreamAffixAndCompoundTests.cs` and were run locally: 146/146 tests pass.

## Next steps

- Continue importing additional upstream datasets if desired.
- Optionally refactor certain heuristics to increase fidelity further and remove temporary debugging helpers.
