# Tools.Munch (port)

This repository contains a pragmatic C# port of Hunspell's `munch` utility — a tool used when building compact root-word dictionaries from full word lists and affix (`.aff`) files.

Notes:
- This port provides a minimal, practical implementation of the core munch functionality (parsing PFX/SFX headers and entries; detecting derived words and mapping them to root words). It does not yet replicate every corner case of the original C++ implementation (condition evaluation, advanced cross-product handling, etc.).
- License: Hunspell's original code is MPL/GPL/LGPL. The port in this repository preserves appropriate copyright notices in source files where possible.

Usage
-----
Build:

```pwsh
dotnet build src\Tools.Munch\Tools.Munch.csproj -c Release
```

Run:

```pwsh
dotnet run --project src\Tools.Munch\Tools.Munch.csproj -- <wordlist-file> <aff-file>
```

Output
------
The tool prints a count of kept root entries followed by lines (one per kept word). If a kept word had affixes recorded, the line is `word/flags` (flag characters are the same letters used in the `.aff` file).

Examples, tests, and further enhancements are tracked in the repository issues. If you want me to improve fidelity vs the canonical munch implementation (conditions, cross-products, COMPOUNDRULE handling) I can continue iterating.
