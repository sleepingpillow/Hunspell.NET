using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Hunspell.Tests;

/// <summary>
/// Compares a small, pinned reference corpus and isolated API cases with Hunspell.
/// Known differences are recorded by exact observed result; improvements pass
/// without changing the baseline, but a new or changed difference fails.
/// </summary>
public sealed class UpstreamParityTests(ITestOutputHelper output)
{
    private static readonly string ParityDirectory = Path.Combine(AppContext.BaseDirectory, "UpstreamParity");
    private static readonly string FixtureDirectory = Path.Combine(ParityDirectory, "v1.7.6");

    // SHA-256 of the original bytes from hunspell/hunspell v1.7.6,
    // commit 15faca15532b34db3106d50f162e5edc2503d6d6.
    private static readonly Dictionary<string, string> FixtureHashes = new(StringComparer.Ordinal)
    {
        ["breakdefault.aff"] = "ae508420da64bd465739f33d72fa98d908b31e82511de3ea8bb6df7330aa9f75",
        ["breakdefault.dic"] = "d2086f6e7f4e7a141d52c43a56e4c017c147f852393ed5071792ec521a8a945d",
        ["breakdefault.good"] = "679a49c784affa998aa1104383d3c456bc94619cf795ae300774fb890abdf938",
        ["breakdefault.wrong"] = "2dff49acca69c291de11e34178b1d9b52ef140e9a8351cdb224c5d5d9059ec2a",
        ["slash.aff"] = "9eaa6b9593e2b23e025706f3451165fb5e6ab100c06516b80e1aff06b7386ad9",
        ["slash.dic"] = "7bf043cdfefd3050e4221fe483edf149c41495abb61342233c8da79024e710b9",
        ["slash.good"] = "28e595567bf7a6fa1f4dd10c72bd4af4e37630d2f6cf7ce59c6a4e54c29a5b54"
    };

    [Fact]
    public void PinnedUpstreamCases_DoNotIntroduceOrChangeDifferences()
    {
        var baseline = ReadBaseline();
        var executed = new HashSet<string>(StringComparer.Ordinal);
        var unexpected = new List<string>();
        var knownCount = 0;
        var resolvedCount = 0;

        void Observe(string id, object expected, object actual)
        {
            Assert.True(executed.Add(id), $"Duplicate parity case: {id}");
            var expectedJson = JsonSerializer.Serialize(expected);
            var actualJson = JsonSerializer.Serialize(actual);

            if (expectedJson == actualJson)
            {
                if (baseline.ContainsKey(id))
                {
                    resolvedCount++;
                    output.WriteLine($"Resolved: {id}; remove it from known-differences.json.");
                }
                return;
            }

            if (baseline.TryGetValue(id, out var known) &&
                actualJson == JsonSerializer.Serialize(known))
            {
                knownCount++;
                output.WriteLine($"Known difference: {id}; expected {expectedJson}, actual {actualJson}");
                return;
            }

            unexpected.Add($"{id}: expected {expectedJson}, actual {actualJson}");
        }

        foreach (var (fileName, expectedHash) in FixtureHashes)
        {
            var path = Path.Combine(FixtureDirectory, fileName);
            var actualHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            Assert.Equal(expectedHash, actualHash);
        }

        void CheckFixture(string name, bool hasWrongWords)
        {
            using var checker = new HunspellSpellChecker(
                Path.Combine(FixtureDirectory, name + ".aff"),
                Path.Combine(FixtureDirectory, name + ".dic"));

            void CheckWords(string extension, bool expected)
            {
                var words = ReadRequiredWords(Path.Combine(FixtureDirectory, name + extension));
                for (var index = 0; index < words.Count; index++)
                {
                    var word = words[index];
                    var id = $"upstream/{name}/{extension[1..]}/{index + 1}";
                    output.WriteLine($"{id}: {JsonSerializer.Serialize(word)}");
                    Observe(id, expected, checker.Spell(word));
                }
            }

            CheckWords(".good", true);
            if (hasWrongWords) CheckWords(".wrong", false);
        }

        CheckFixture("breakdefault", hasWrongWords: true);
        CheckFixture("slash", hasWrongWords: false);

        // The same cases can be checked against the native C API with
        // verify_reference.py, which rejects any library version except 1.7.6.
        var cases = JsonSerializer.Deserialize<ReferenceCase[]>(
            File.ReadAllText(Path.Combine(ParityDirectory, "synthetic-cases.json")));
        Assert.NotNull(cases);
        Assert.NotEmpty(cases);
        foreach (var testCase in cases)
        {
            using var temporary = new TemporaryDictionary(testCase.Affix, testCase.Dictionary);
            object actual = testCase.Operation switch
            {
                "spell" => temporary.Checker.Spell(testCase.Input),
                "suggest" => temporary.Checker.Suggest(testCase.Input),
                "remove" => RemoveAndCheck(temporary.Checker, testCase.Input),
                _ => throw new InvalidDataException($"Unknown reference operation: {testCase.Operation}")
            };
            Observe(testCase.Id, testCase.Expected, actual);
        }

        var unregistered = baseline.Keys.Except(executed, StringComparer.Ordinal).Order().ToArray();
        Assert.True(unregistered.Length == 0,
            $"Baseline entries without a corresponding case: {string.Join(", ", unregistered)}");

        output.WriteLine($"Compared {executed.Count} cases: {knownCount} known differences, " +
            $"{resolvedCount} resolved, {unexpected.Count} new or changed differences.");
        Assert.True(unexpected.Count == 0, "New or changed Hunspell differences:\n" + string.Join("\n", unexpected));
    }

    [Fact]
    public void RequiredWordList_RejectsMissingAndEmptyFiles()
    {
        using var temporary = new TemporaryDictionary("SET UTF-8\n", "1\nword\n");
        var path = Path.Combine(temporary.DirectoryPath, "words.good");

        Assert.Throws<FileNotFoundException>(() => ReadRequiredWords(path));
        File.WriteAllText(path, " \n\n");
        Assert.Throws<InvalidDataException>(() => ReadRequiredWords(path));
    }

    private static IReadOnlyList<string> ReadRequiredWords(string path)
    {
        var words = File.ReadAllLines(path, Encoding.UTF8)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (words.Length == 0) throw new InvalidDataException($"No words in required fixture: {path}");
        return words;
    }

    private static bool RemoveAndCheck(HunspellSpellChecker checker, string word)
    {
        Assert.True(checker.Spell(word), $"Removal precondition failed for: {word}");
        checker.Remove(word);
        return checker.Spell(word);
    }

    private sealed record ReferenceCase(
        string Id, string Affix, string Dictionary, string Operation, string Input, JsonElement Expected);

    private static Dictionary<string, JsonElement> ReadBaseline()
    {
        var path = Path.Combine(ParityDirectory, "known-differences.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.EnumerateObject())
        {
            Assert.True(result.TryAdd(entry.Name, entry.Value.Clone()), $"Duplicate baseline entry: {entry.Name}");
        }
        return result;
    }

    private sealed class TemporaryDictionary : IDisposable
    {
        public string DirectoryPath { get; }
        public HunspellSpellChecker Checker { get; }

        public TemporaryDictionary(string affix, string dictionary)
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), "hunspell-parity-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DirectoryPath);
            var affixPath = Path.Combine(DirectoryPath, "test.aff");
            var dictionaryPath = Path.Combine(DirectoryPath, "test.dic");
            File.WriteAllText(affixPath, affix, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.WriteAllText(dictionaryPath, dictionary, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Checker = new HunspellSpellChecker(affixPath, dictionaryPath);
        }

        public void Dispose()
        {
            Checker.Dispose();
            Directory.Delete(DirectoryPath, recursive: true);
        }
    }
}
