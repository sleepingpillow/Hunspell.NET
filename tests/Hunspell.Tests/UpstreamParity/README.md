# Pinned Hunspell parity checks

The reference is [Hunspell v1.7.6](https://github.com/hunspell/hunspell/releases/tag/v1.7.6),
commit `15faca15532b34db3106d50f162e5edc2503d6d6`. The files in `v1.7.6/`
are unmodified copies from its `tests/` directory; `UpstreamParityTests` checks
their SHA-256 hashes before using them. They are distributed under Hunspell's
MPL-1.1/GPL-2.0-or-later/LGPL-2.1-or-later tri-license. See the upstream
repository for its copyright and license files.

This first corpus contains `breakdefault` and `slash`, together with isolated
cases in `synthetic-cases.json`. The upstream `.good` and `.wrong` files define
the expected spelling results. The synthetic results were checked against the
native C API at the same pinned commit; suggestion lists are compared exactly,
including order and capitalization. All cases contain ASCII or UTF-8 data.
The historical fixtures elsewhere in `dictionaries/` remain separate because
some of them differ from upstream.

Run the managed checks with:

```sh
dotnet test tests/Hunspell.Tests/Hunspell.Tests.csproj \
  --filter 'FullyQualifiedName~Hunspell.Tests.UpstreamParityTests' \
  --logger 'console;verbosity=detailed'
```

`known-differences.json` records the **exact current .NET output** for each
known difference, keyed by case ID. The checks fail if an unlisted difference
appears or an existing difference changes to a different incorrect result.
An output that matches the reference passes and is reported as resolved; remove
that entry in the same change when practical. Entries without a corresponding
case fail. Missing or empty required word lists fail rather than passing as
zero assertions. The detailed test output includes checked and known counts.

Do not update the known-differences file merely to make CI green: investigate
new results against the pinned reference and add or change entries only when a
reviewed, deliberate baseline change is needed. The baseline covers the cases
listed here; it is not a percentage of Hunspell compatibility.

The `verify-reference` CI job builds the pinned upstream source and checks the
reference expectations with its C API. To repeat that locally on Linux, obtain
the pinned upstream checkout and run:

```sh
git clone https://github.com/hunspell/hunspell.git /tmp/hunspell-reference
git -C /tmp/hunspell-reference checkout 15faca15532b34db3106d50f162e5edc2503d6d6
g++ -std=c++14 -O2 -fPIC -DBUILDING_LIBHUNSPELL -shared \
  /tmp/hunspell-reference/src/hunspell/*.cxx -o /tmp/libhunspell-reference.so
python3 tests/Hunspell.Tests/UpstreamParity/verify_reference.py \
  --library /tmp/libhunspell-reference.so
```

The script rejects libraries that do not report version 1.7.6. Add future cases
with stable IDs and verify their expectations against the reference before
adding any corresponding known .NET differences.
