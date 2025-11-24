# Filstruktur för svensk Hunspell-ordbok

En svensk Hunspell-ordbok består av två huvudfiler:

- **.aff** (affix-fil): Innehåller regler för böjningar, sammansättningar, undantag och specialhantering.
- **.dic** (dictionary-fil): Innehåller grundord och flaggor som kopplar till affix-regler.

## Exempel på filstruktur

```
SwedishDictionary/
├── sv_SE.aff
├── sv_SE.dic
├── sv_FI.aff
├── sv_FI.dic
```

## Kodning
- **SET UTF-8** används för att hantera svenska tecken korrekt.

## Placering
- Filerna kan placeras i en `dictionaries/`-mapp i projektet.

## Viktiga filavsnitt i .aff
- Prefix- och suffixregler
- Sammansättningsregler (COMPOUNDRULE, ONLYINCOMPOUND)
- Ersättningsregler (REP)
- Avgränsare (BREAK)
- Max antal sammansättningar (MAXCPDSUGS)

Se [affix-rules.md](./affix-rules.md) för detaljerad beskrivning av varje regeltyp.