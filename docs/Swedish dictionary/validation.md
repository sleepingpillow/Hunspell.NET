# Validering och språkliga nyanser i svensk Hunspell

Hunspell använder affix-regler för att validera stavning och hantera svenska språkets nyanser. Här beskrivs hur reglerna används och vilka ordtyper de syftar till att validera.

## 1. Grundläggande stavningsvalidering
- Varje ord i .dic testas mot affix-regler i .aff
- Exempel: "hund" valideras mot genitiv ("hunds"), plural ("hundar")

## 2. Sammansättningar
- COMPOUNDRULE och ONLYINCOMPOUND styr vilka ord som får kombineras
- Exempel: "sjö" + "bod" → "sjöbod" (valideras om båda ord har rätt flaggor)
- MAXCPDSUGS begränsar antalet sammansättningar (sv_FI: max 2, sv_SE: ingen begränsning)

## 3. Förkortningar och specialfall
- BREAK hanterar avgränsare och förkortningar
- Exempel: "bl.a.", "t.ex.", "e-post"

## 4. Ersättningsregler (REP)
- Förbättrar förslagsgenerering vid stavfel
- Exempel: "å" ↔ "a", "ä" ↔ "e"

## 5. Undantag och förbjudna former
- FORBIDFLAG och POSITION kan förhindra vissa sammansättningar eller böjningar
- Exempel: "förbjudna" sammansättningar, ord som bara får stå först/sist

## 6. Nyanser och specialhantering
- Svenska har många undantag: t.ex. "barn" (ingen plural-suffix), "bl.a." (förkortning)
- Affix-regler kan behöva anpassas för dialekter och regionala skillnader

## 7. Exempel på validering
| Ordtyp           | Exempel         | Regel/Flagga         |
|------------------|----------------|---------------------|
| Grundord         | hund           | A, P                |
| Sammansättning   | sjöbod         | COMPOUNDRULE        |
| Förkortning      | bl.a.          | BREAK               |
| Plural           | katter         | P                   |
| Genitiv          | hunds          | A                   |
| Förbjuden form   | ...            | FORBIDFLAG, POSITION|

## 8. Testning
- Använd testfall för att säkerställa att reglerna fungerar som avsett
- Se till att specialfall och undantag hanteras korrekt

Se [examples.md](./examples.md) för fler praktiska exempel.
