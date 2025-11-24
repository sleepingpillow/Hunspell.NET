# Affix-regler och ordtyper i svensk Hunspell

Den svenska Hunspell-ordboken använder ett antal affix-regler för att hantera böjningar, sammansättningar och specialfall. Nedan beskrivs de viktigaste reglerna, deras syfte och exempel på ord de påverkar.

## 1. SET UTF-8
- **Syfte:** Hantera svenska tecken korrekt.
- **Exempel:** "å", "ä", "ö"

## 2. Suffixregler (t.ex. genitiv, adjektiv, plural)
- **Flaggor:** A, D, S, P, ...
- **Exempel:**
  - "hund" → "hunds" (genitiv, flagga A)
  - "stor" → "större" (komparativ, flagga D)
  - "katt" → "katter" (plural, flagga P)

## 3. Prefixregler
- **Syfte:** Hantera t.ex. "o-" för negation ("osäker")

## 4. Sammansättningsregler
- **COMPOUNDRULE:** Definierar hur ord kan kombineras.
- **ONLYINCOMPOUND:** Ord som bara får användas i sammansättningar.
- **Exempel:**
  - "sjö" + "bod" → "sjöbod"
  - "för" + "fattare" → "författare"

## 5. MAXCPDSUGS
- **Syfte:** Begränsar antal sammansättningsförslag.
- **Exempel:**
  - sv_FI: MAXCPDSUGS 2 (max två sammansatta ord)
  - sv_SE: MAXCPDSUGS 0 (ingen begränsning)

## 6. REP (ersättningsregler)
- **Syfte:** Förbättra förslagsgenerering vid stavfel.
- **Exempel:**
  - "å" ↔ "a"
  - "ä" ↔ "e"

## 7. BREAK
- **Syfte:** Hantera avgränsare i sammansatta ord och förkortningar.
- **Exempel:**
  - "bl.a." (förkortning)
  - "e-post"

## 8. Specialflaggor och undantag
- **Exempel:**
  - "förbjudna" sammansättningar (FORBIDFLAG)
  - "position"-regler (POSITION)

## 9. Exempel på ordtyper
- Grundord: "hund", "katt", "bil"
- Sammansatta ord: "sjöbod", "e-postadress"
- Förkortningar: "bl.a.", "t.ex."
- Pluralformer: "hundar", "katter"
- Genitiv: "hunds", "katts"

Se [validation.md](./validation.md) för hur dessa regler används för stavningsvalidering.