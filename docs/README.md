# Hunspell.NET Documentation

Welcome to the Hunspell.NET documentation. This directory contains detailed documentation for various features of the library.

## Available Documentation

### [Affix File Directives](affix-directives.md)
Complete reference for all supported Hunspell affix file directives, including:
- All 27 directives used in Swedish dictionaries (100% support)
- Word attribute flags (NOSUGGEST, FORBIDDENWORD, NEEDAFFIX, FORCEUCASE)
- Suggestion options (MAXCPDSUGS, MAXDIFF, ONLYMAXDIFF, NOSPLITSUGS, FULLSTRIP)
- Compound word configuration and checking
- Character mapping and replacement patterns
- Affix rules (PFX/SFX)
- Usage examples and implementation notes

### [Compound Words](compound-words.md)
Comprehensive guide to compound word support in Hunspell.NET, including:
- Compound word flags (COMPOUNDFLAG, COMPOUNDBEGIN, COMPOUNDMIDDLE, COMPOUNDEND)
- Compound word options (COMPOUNDMIN, COMPOUNDWORDMAX)
- Compound checking rules (CHECKCOMPOUNDDUP, CHECKCOMPOUNDCASE, CHECKCOMPOUNDTRIPLE)
- Special flags (ONLYINCOMPOUND, COMPOUNDFORBIDFLAG, COMPOUNDPERMITFLAG)
- Usage examples and implementation notes

### [Remaining Compound Features Plan](compound-words-remaining.md)
Detailed implementation plan for remaining compound word features:
- COMPOUNDRULE (regex-like patterns) - Priority 1
- CHECKCOMPOUNDPATTERN (boundary patterns) - Priority 1
- COMPOUNDSYLLABLE (syllable limits) - Priority 2
- BREAK (word breaking) - Priority 2
- Enhanced affix support - Priority 3-4
- Recommended implementation order and effort estimates

## Quick Links

- [Main README](../README.md) - Project overview and quick start guide
- [Affix Directives Documentation](affix-directives.md) - Complete directive support reference
- [Compound Words Documentation](compound-words.md) - Detailed compound word feature documentation
- [Tests](../tests/Hunspell.Tests/) - Test suite with usage examples

## Language Support

Hunspell.NET provides comprehensive support for:
- ✅ **Swedish (sv_SE)** - All 27 affix directives fully supported
- ✅ **Finnish (fi_FI)** - Full directive support
- ✅ **German (de_DE)** - Full compound word support
- ✅ **Hungarian (hu_HU)** - Full directive support
- ✅ **Dutch (nl_NL)** - Full compound word support
- ✅ **English (en_US/en_GB)** - Full support
- ✅ Most other Hunspell-compatible dictionaries

## Contributing

When adding new features, please update or create documentation in this directory to help users understand how to use them.
