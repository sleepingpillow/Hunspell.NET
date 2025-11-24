
# Affix-regler och flaggor i svensk Hunspell

## Innehållsförteckning
- SET
- TRY
- WORDCHARS
- BREAK
- NOSUGGEST
- FORBIDDENWORD / FORBIDFLAG
- NEEDAFFIX
- COMPOUNDMIN, COMPOUNDRULE, COMPOUNDPERMITFLAG, COMPOUNDBEGIN, COMPOUNDMIDDLE, COMPOUNDEND, ONLYINCOMPOUND
- MAXCPDSUGS, MAXDIFF, ONLYMAXDIFF, NOSPLITSUGS
- FULLSTRIP, FORCEUCASE
- MAP
- REP
- Suffixflaggor (A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, T, e, d, z, g, y, h, x, w, v, f, u, i, t, s, r, b, 1, 7)
- Specialflaggor och pragma-direktiv

---

## Exempel på flaggsektion (mall):

### [FLAGNAME]
- **Teknisk funktion:** [Beskrivning av hur Hunspell tolkar och använder flaggan]
- **Språkligt syfte:** [Hur flaggan används för svenska böjningar/sammansättningar]
- **Exempel:** [Konkreta ord och former]
- **Edge cases / specialfall:** [Eventuella undantag, begränsningar, eller typiska fel]

---


---

## SET
- **Teknisk funktion:** Anger teckenkodning för ordboken. För svenska används alltid `SET UTF-8` för att stödja tecken som å, ä, ö.
- **Språkligt syfte:** Möjliggör korrekt hantering av svenska bokstäver och specialtecken.
- **Exempel:**
	- SET UTF-8
- **Edge cases:** Om fel teckenkodning används kan svenska ord tolkas felaktigt.

## TRY
- **Teknisk funktion:** Lista av tecken Hunspell ska prioritera vid förslagsgenerering (stavningskorrigering).
- **Språkligt syfte:** Anpassas för svenska vanliga bokstäver och typiska felskrivningar.
- **Exempel:**
	- TRY abcdefghijklmnopqrstuvwxyzåäö
- **Edge cases:** En dåligt vald TRY-lista kan ge sämre förslag vid stavfel.

## WORDCHARS
- **Teknisk funktion:** Definierar vilka tecken som får ingå i ord (utöver bokstäver). Används för t.ex. bindestreck, apostrof, förkortningar.
- **Språkligt syfte:** Möjliggör svenska ord med t.ex. "e-post", "bl.a.".
- **Exempel:**
	- WORDCHARS -.'
- **Edge cases:** Om tecken saknas här kan vissa ord inte hanteras korrekt.

## BREAK
- **Teknisk funktion:** Anger avgränsare och regler för att dela upp sammansatta ord och förkortningar.
- **Språkligt syfte:** Hanterar svenska förkortningar och sammansättningar med punkt eller bindestreck.
- **Exempel:**
	- BREAK .
	- BREAK -
- **Edge cases:** Felaktiga BREAK-regler kan ge felaktiga sammansättningsförslag.

## NOSUGGEST
- **Teknisk funktion:** Markerar ord som inte ska föreslås vid stavningskorrigering.
- **Språkligt syfte:** Används för t.ex. känsliga, olämpliga eller föråldrade ord.
- **Exempel:**
	- NOSUGGEST
- **Edge cases:** Om för många ord markeras kan förslagslistan bli för snäv.

## FORBIDDENWORD / FORBIDFLAG
- **Teknisk funktion:** Anger ord som är förbjudna, t.ex. felaktiga sammansättningar eller ord som inte ska godkännas.
- **Språkligt syfte:** Förhindrar felaktiga eller oönskade ordformer.
- **Exempel:**
	- FORBIDDENWORD
	- FORBIDFLAG X
- **Edge cases:** Om för många ord förbjuds kan giltiga former uteslutas.

## NEEDAFFIX
- **Teknisk funktion:** Markerar ord som måste ha ett affix (prefix/suffix) för att vara giltiga.
- **Språkligt syfte:** Används för t.ex. rotmorfem som bara får förekomma i sammansättningar.
- **Exempel:**
	- NEEDAFFIX
- **Edge cases:** Kan leda till att vissa ord inte känns igen om affix saknas.

## COMPOUNDMIN, COMPOUNDRULE, COMPOUNDPERMITFLAG, COMPOUNDBEGIN, COMPOUNDMIDDLE, COMPOUNDEND, ONLYINCOMPOUND
- **Teknisk funktion:** Styr hur sammansatta ord bildas och vilka delar som får användas i början, mitten eller slutet av en sammansättning.
- **Språkligt syfte:** Möjliggör svenska sammansättningar med rätt morfologisk struktur.
- **Exempel:**
	- COMPOUNDMIN 3 (minsta antal tecken per del)
	- COMPOUNDRULE 12 (regel för sammansättning)
	- COMPOUNDPERMITFLAG Y
	- COMPOUNDBEGIN A
	- COMPOUNDMIDDLE B
	- COMPOUNDEND C
	- ONLYINCOMPOUND
- **Edge cases:** Felaktiga regler kan ge grammatiskt felaktiga sammansättningar.

---

Se [validation.md](./validation.md) för hur dessa regler används för stavningsvalidering.
