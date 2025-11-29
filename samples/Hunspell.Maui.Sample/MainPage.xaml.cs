using System.Text;
using System.Text.RegularExpressions;

namespace Hunspell.Maui.Sample;

public partial class MainPage : ContentPage
{
    private HunspellSpellChecker? _spellChecker;

    public MainPage()
    {
        InitializeComponent();
        _ = InitializeSpellCheckerAsync();
    }

    private async Task InitializeSpellCheckerAsync()
    {
        try
        {
            // Copy assets to app data directory so Hunspell can read them as regular files
            var cacheDir = FileSystem.CacheDirectory;
            var affixPath = Path.Combine(cacheDir, "test.aff");
            var dictionaryPath = Path.Combine(cacheDir, "test.dic");

            // Copy the affix file
            using (var affStream = await FileSystem.OpenAppPackageFileAsync("test.aff"))
            using (var affWriter = File.Create(affixPath))
            {
                await affStream.CopyToAsync(affWriter);
            }

            // Copy the dictionary file
            using (var dicStream = await FileSystem.OpenAppPackageFileAsync("test.dic"))
            using (var dicWriter = File.Create(dictionaryPath))
            {
                await dicStream.CopyToAsync(dicWriter);
            }

            _spellChecker = new HunspellSpellChecker(affixPath, dictionaryPath);
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ResultsLabel.Text = $"Spell checker initialized successfully.\nDictionary encoding: {_spellChecker.DictionaryEncoding}\n\nStart typing and click 'Check Spelling' to check your text.";
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ResultsLabel.Text = $"Failed to initialize spell checker: {ex.Message}\n\nStack trace: {ex.StackTrace}";
            });
        }
    }

    private void OnCheckSpellingClicked(object? sender, EventArgs e)
    {
        if (_spellChecker == null)
        {
            ResultsLabel.Text = "Spell checker not initialized. Please wait for initialization to complete.";
            return;
        }

        var text = TextEditor.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            ResultsLabel.Text = "Please enter some text to check.";
            return;
        }

        var words = ExtractWords(text);
        var results = new StringBuilder();
        var misspelledCount = 0;
        var correctCount = 0;

        results.AppendLine("📝 Spell Check Results:");
        results.AppendLine("─────────────────────────");

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
                continue;

            var isCorrect = _spellChecker.Spell(word);
            if (isCorrect)
            {
                correctCount++;
            }
            else
            {
                misspelledCount++;
                var suggestions = _spellChecker.Suggest(word);
                results.AppendLine($"\n❌ \"{word}\" - Misspelled");
                if (suggestions.Count > 0)
                {
                    results.AppendLine($"   Suggestions: {string.Join(", ", suggestions.Take(5))}");
                }
                else
                {
                    results.AppendLine("   No suggestions available");
                }
            }
        }

        results.AppendLine("\n─────────────────────────");
        results.AppendLine($"📊 Summary: {correctCount} correct, {misspelledCount} misspelled out of {words.Count} words");

        if (misspelledCount == 0)
        {
            results.AppendLine("\n✅ All words are spelled correctly!");
        }

        ResultsLabel.Text = results.ToString();
        SemanticScreenReader.Announce($"Spell check complete. {misspelledCount} misspelled words found.");
    }

    private void OnClearClicked(object? sender, EventArgs e)
    {
        TextEditor.Text = string.Empty;
        ResultsLabel.Text = "Text cleared. Start typing and click 'Check Spelling' to check your text.";
        SemanticScreenReader.Announce("Text cleared");
    }

    private static List<string> ExtractWords(string text)
    {
        var words = new List<string>();
        var regex = WordExtractorRegex();
        var matches = regex.Matches(text);
        
        foreach (Match match in matches)
        {
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                words.Add(match.Value);
            }
        }

        return words;
    }

    [GeneratedRegex(@"\b[a-zA-Z]+\b")]
    private static partial Regex WordExtractorRegex();
}
