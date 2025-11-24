# Skapande av svensk Hunspell-ordbok

Att skapa en svensk Hunspell-ordbok kräver noggrannhet och förståelse för svenska språkets böjningsmönster och sammansättningar.

## Steg-för-steg-guide

### 1. Samla grundord
- Lista vanliga substantiv, verb, adjektiv, förkortningar och specialord.
- Exempel: "hund", "katt", "bil", "bl.a.", "e-post"

### 2. Skapa dictionary-fil (.dic)
- Varje rad: grundord + eventuella affixflaggor
- Exempel:
  ```
  hund/A
  katt/P
  stor/D
  bl.a.
  e-post
  ```

### 3. Skapa affix-fil (.aff)
- Ange kodning: `SET UTF-8`
- Definiera suffix och prefix (böjningar, plural, genitiv, negation)
- Lägg till sammansättningsregler (COMPOUNDRULE, ONLYINCOMPOUND)
- Lägg till ersättningsregler (REP)
- Hantera avgränsare (BREAK)
- Exempel på affix-regel:
  ```
  SFX A Y 1
  SFX A 0 s .
  ```

### 4. Testa ordboken
- Använd Hunspell eller testkod för att kontrollera att böjningar och sammansättningar fungerar.
- Testa specialfall och undantag.

### 5. Vanliga fallgropar
- Felaktig kodning (måste vara UTF-8)
- Saknade flaggor eller regler
- Sammansättningar som inte fungerar som förväntat
- Förkortningar och specialtecken

## Tips
- Utgå från befintliga svenska Hunspell-ordlistor och anpassa efter behov.
- Dokumentera varje regel och flagga i affix-filen.

Se [affix-rules.md](./affix-rules.md) för detaljer om regler.
