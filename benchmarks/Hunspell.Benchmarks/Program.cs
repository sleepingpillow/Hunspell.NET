using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Hunspell;
using System.IO;

BenchmarkRunner.Run<SpellCheckBenchmarks>();

[MemoryDiagnoser]
public class SpellCheckBenchmarks
{
    private HunspellSpellChecker _hunspellChecker;

    private readonly string[] _testWords = new[]
    {
        "hund", "katt", "bil", "hus", "mat", "vatten", "sol", "måne", "stjärna", "blomma",
        "trä", "sten", "jord", "eld", "luft", "vind", "regn", "snö", "is", "värme",
        "kyla", "ljus", "mörker", "tid", "plats", "person", "djur", "växt", "maskin", "verktyg",
        "matematik", "fysik", "kemi", "biologi", "historia", "geografi", "språk", "litteratur", "konst", "musik",
        "sport", "spel", "arbete", "ledighet", "resa", "hem", "skola", "universitet", "bok", "tidning"
    };

    [GlobalSetup]
    public void Setup()
    {
        // Setup Hunspell.NET with Swedish dictionary
        string baseDir = AppContext.BaseDirectory;
        string affixPath = Path.Combine(baseDir, "dictionaries", "sv_FI.aff");
        string dictionaryPath = Path.Combine(baseDir, "dictionaries", "sv_FI.dic");
        _hunspellChecker = new HunspellSpellChecker(affixPath, dictionaryPath);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _hunspellChecker?.Dispose();
    }

    [Benchmark]
    public void SpellCheck_CorrectWords()
    {
        foreach (var word in _testWords)
        {
            _hunspellChecker.Spell(word);
        }
    }

    [Benchmark]
    public void SpellCheck_IncorrectWords()
    {
        foreach (var word in _testWords)
        {
            var misspelled = word + "x"; // Make it misspelled
            _hunspellChecker.Spell(misspelled);
        }
    }

    [Benchmark]
    public void Suggest_Corrections()
    {
        foreach (var word in _testWords)
        {
            var misspelled = word + "x"; // Make it misspelled
            _hunspellChecker.Suggest(misspelled);
        }
    }

    [Benchmark]
    public void LoadDictionary()
    {
        // This benchmark measures dictionary loading time
        var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var affPath = Path.Combine(projectRoot, "benchmarks", "Hunspell.Benchmarks", "dictionaries", "sv_FI.aff");
        var dicPath = Path.Combine(projectRoot, "benchmarks", "Hunspell.Benchmarks", "dictionaries", "sv_FI.dic");
        using var checker = new HunspellSpellChecker(affPath, dicPath);
        checker.Spell("test");
    }
}
