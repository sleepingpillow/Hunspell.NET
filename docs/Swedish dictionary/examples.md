# Exempel och vanliga fallgropar

Här följer praktiska exempel på hur svenska Hunspell-regler används, samt vanliga problem och lösningar.

## Exempel på ord och regler

### 1. Grundord och böjningar
| Grundord | Plural | Genitiv | Komparativ |
|----------|--------|---------|------------|
| hund     | hundar | hunds   | -          |
| katt     | katter | katts   | -          |
| stor     | -      | -       | större     |

### 2. Sammansättningar
| Delord 1 | Delord 2 | Sammansatt | Regel         |
|----------|----------|------------|--------------|
| sjö      | bod      | sjöbod     | COMPOUNDRULE |
| e        | post     | e-post     | BREAK        |

### 3. Förkortningar
| Förkortning | Regel  |
|-------------|--------|
| bl.a.       | BREAK  |
| t.ex.       | BREAK  |

### 4. Förbjudna former
| Ord         | Regel      |
|-------------|-----------|
| ...         | FORBIDFLAG|

## Vanliga fallgropar
- **Fel kodning:** Måste vara UTF-8 för svenska tecken
- **Saknade flaggor:** Ordet böjs inte korrekt
- **Sammansättningar fungerar inte:** Kontrollera COMPOUNDRULE och flaggor
- **Förkortningar ignoreras:** Lägg till BREAK-regler
- **Regionala skillnader:** Anpassa affix-regler för dialekter

## Tips för felsökning
- Testa ordboken med typiska svenska texter
- Kontrollera att alla specialfall hanteras
- Dokumentera varje regel och undantag

Se övriga dokument för djupare förklaringar av regler och struktur.
