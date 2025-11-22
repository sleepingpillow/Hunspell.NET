# Hunspell.NET Documentation

Welcome to the Hunspell.NET documentation. This directory contains detailed documentation for various features of the library.

## Available Documentation

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
- [Compound Words Documentation](compound-words.md) - Detailed compound word feature documentation
- [Tests](../tests/Hunspell.Tests/) - Test suite with usage examples

## Contributing

When adding new features, please update or create documentation in this directory to help users understand how to use them.
