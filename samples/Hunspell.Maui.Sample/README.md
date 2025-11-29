# Hunspell.NET MAUI Text Editor Sample

This sample demonstrates how to integrate **Hunspell.NET** spell checking functionality into a .NET MAUI desktop application with a simple text editor.

## Features

- **Simple Text Editor**: A multiline text editor where users can type or paste text
- **Spell Checking**: Click "Check Spelling" to identify misspelled words
- **Suggestions**: Get spelling suggestions for misspelled words
- **Cross-Platform**: Works on Android and Windows desktop (with appropriate workloads installed)

## Screenshot

The application provides:
- A text input area for entering text
- "Check Spelling" button to analyze the text
- "Clear" button to reset the editor
- Results panel showing misspelled words and suggestions

## Prerequisites

- .NET 10.0 SDK
- MAUI workloads installed:
  ```bash
  dotnet workload install maui-android  # For Android
  dotnet workload install maui-windows  # For Windows (on Windows machines)
  ```

## Building

From the repository root:

```bash
# For Android
dotnet build samples/Hunspell.Maui.Sample/Hunspell.Maui.Sample.csproj -f net10.0-android

# For Windows (on Windows machines)
dotnet build samples/Hunspell.Maui.Sample/Hunspell.Maui.Sample.csproj -f net10.0-windows10.0.19041.0
```

## Running

### Android

```bash
# Deploy to connected Android device or emulator
dotnet run samples/Hunspell.Maui.Sample/Hunspell.Maui.Sample.csproj -f net10.0-android
```

### Windows

```bash
# Run on Windows
dotnet run samples/Hunspell.Maui.Sample/Hunspell.Maui.Sample.csproj -f net10.0-windows10.0.19041.0
```

## Dictionary Files

The sample includes a test dictionary with common English words located in `Resources/Raw/`:
- `test.aff` - Affix rules file
- `test.dic` - Dictionary word list

To use a full English dictionary, you can replace these files with complete Hunspell dictionary files (e.g., `en_US.aff` and `en_US.dic`).

## How It Works

1. **Initialization**: On startup, the app copies dictionary files from the app package to the cache directory, then initializes the `HunspellSpellChecker`.

2. **Spell Checking**: When the user clicks "Check Spelling", the app:
   - Extracts individual words from the text
   - Checks each word against the dictionary using `spellChecker.Spell(word)`
   - For misspelled words, fetches suggestions using `spellChecker.Suggest(word)`
   - Displays results with counts and suggestions

## Code Example

```csharp
// Initialize spell checker
using var spellChecker = new HunspellSpellChecker("test.aff", "test.dic");

// Check if a word is spelled correctly
bool isCorrect = spellChecker.Spell("hello");  // true
bool isMisspelled = spellChecker.Spell("helo"); // false

// Get suggestions for misspelled words
List<string> suggestions = spellChecker.Suggest("helo");
// Returns: ["hello", "help", "held", ...]
```

## Customization

To add more words to the dictionary at runtime:

```csharp
// Add a word
spellChecker.Add("customword");

// Remove a word
spellChecker.Remove("customword");
```

## Platform Support

| Platform | Status |
|----------|--------|
| Android  | ✅ Supported |
| Windows  | ✅ Supported |
| iOS      | 🔧 Can be enabled |
| macOS    | 🔧 Can be enabled |

To enable iOS or macOS support, update the `TargetFrameworks` in the `.csproj` file:

```xml
<TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
```

## License

This sample is part of the Hunspell.NET project and is licensed under MPL 1.1/GPL 2.0/LGPL 2.1.
