namespace Hunspell.Tests;

public class HunspellSpellCheckerTests
{
    private const string TestDictionaryPath = "../../../dictionaries/test.dic";
    private const string TestAffixPath = "../../../dictionaries/test.aff";

    [Fact]
    public void Constructor_WithValidPaths_CreatesInstance()
    {
        // Arrange & Act
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Assert
        Assert.NotNull(spellChecker);
    }

    [Fact]
    public void Constructor_WithNullAffixPath_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HunspellSpellChecker(null!, TestDictionaryPath));
    }

    [Fact]
    public void Constructor_WithNullDictionaryPath_ThrowsArgumentException()
    {
        // Arrange, Act & Assert
        Assert.Throws<ArgumentNullException>(() => new HunspellSpellChecker(TestAffixPath, null!));
    }

    [Fact]
    public void Constructor_WithNonExistentDictionaryPath_ThrowsFileNotFoundException()
    {
        // Arrange, Act & Assert
        Assert.Throws<FileNotFoundException>(() => new HunspellSpellChecker(TestAffixPath, "nonexistent.dic"));
    }

    [Fact]
    public void Spell_WithCorrectWord_ReturnsTrue()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Act
        var result = spellChecker.Spell("hello");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Spell_WithIncorrectWord_ReturnsFalse()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Act
        var result = spellChecker.Spell("helo");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void Spell_IsCaseInsensitive()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Act & Assert
        Assert.True(spellChecker.Spell("HELLO"));
        Assert.True(spellChecker.Spell("Hello"));
        Assert.True(spellChecker.Spell("hello"));
    }

    [Fact]
    public void Suggest_WithMisspelledWord_ReturnsSuggestions()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Act
        var suggestions = spellChecker.Suggest("helo");

        // Assert
        Assert.NotNull(suggestions);
        Assert.Contains("hello", suggestions);
    }

    [Fact]
    public void Suggest_WithCorrectWord_ReturnsEmptyList()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Act
        var suggestions = spellChecker.Suggest("hello");

        // Assert
        Assert.NotNull(suggestions);
        Assert.Empty(suggestions);
    }

    [Fact]
    public void Add_WithNewWord_AddsToRuntimeDictionary()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);
        const string newWord = "customword";

        // Act
        var addResult = spellChecker.Add(newWord);
        var spellResult = spellChecker.Spell(newWord);

        // Assert
        Assert.True(addResult);
        Assert.True(spellResult);
    }

    [Fact]
    public void Remove_WithExistingWord_RemovesFromRuntimeDictionary()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);
        const string word = "customword";
        spellChecker.Add(word);

        // Act
        var removeResult = spellChecker.Remove(word);
        var spellResult = spellChecker.Spell(word);

        // Assert
        Assert.True(removeResult);
        Assert.False(spellResult);
    }

    [Fact]
    public void DictionaryEncoding_ReturnsUTF8()
    {
        // Arrange
        using var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Act
        var encoding = spellChecker.DictionaryEncoding;

        // Assert
        Assert.Equal("UTF-8", encoding);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);

        // Act & Assert
        spellChecker.Dispose();
        spellChecker.Dispose(); // Should not throw
    }

    [Fact]
    public void Spell_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var spellChecker = new HunspellSpellChecker(TestAffixPath, TestDictionaryPath);
        spellChecker.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => spellChecker.Spell("hello"));
    }
}
