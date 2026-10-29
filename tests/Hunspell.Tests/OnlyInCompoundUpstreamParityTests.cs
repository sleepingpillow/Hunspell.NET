using System.Text;

namespace Hunspell.Tests;

public class OnlyInCompoundUpstreamParityTests
{
    [Theory]
    [InlineData("fogemorpheme", ".good", true)]
    [InlineData("fogemorpheme", ".wrong", false)]
    [InlineData("onlyincompound", ".good", true)]
    [InlineData("onlyincompound", ".wrong", false)]
    [InlineData("onlyincompound2", ".good", true)]
    [InlineData("onlyincompound2", ".wrong", false)]
    public void UpstreamFixture_EveryWordHasExpectedResult(string fixture, string extension, bool expected)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "dictionaries", fixture);
        var affPath = Path.Combine(directory, fixture + ".aff");
        var dicPath = Path.Combine(directory, fixture + ".dic");
        var wordsPath = Path.Combine(directory, fixture + extension);

        Assert.True(File.Exists(affPath), $"Missing fixture: {affPath}");
        Assert.True(File.Exists(dicPath), $"Missing fixture: {dicPath}");
        Assert.True(File.Exists(wordsPath), $"Missing fixture: {wordsPath}");
        var words = File.ReadAllLines(wordsPath, Encoding.UTF8)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();
        Assert.NotEmpty(words);

        using var spellChecker = new HunspellSpellChecker(affPath, dicPath);
        var mismatches = words
            .Where(word => spellChecker.Spell(word) != expected)
            .ToArray();
        Assert.True(mismatches.Length == 0,
            $"{fixture}{extension}: expected {expected}, mismatches: {string.Join(", ", mismatches)}");
    }

    [Fact]
    public void OnlyInCompoundFlagAloneDoesNotPermitCompounding()
    {
        Check(
            "SET UTF-8\nCOMPOUNDMIN 1\nCOMPOUNDFLAG A\nONLYINCOMPOUND O\n",
            "2\nfoo/A\npseudo/O\n",
            ("foo", true), ("pseudo", false), ("pseudofoo", false), ("foopseudo", false));
    }

    [Fact]
    public void OriginalOnlyInCompoundFixtureUsesSingleLineDirective()
    {
        Check(
            "# words only in compounds (see also fogemorpheme example)\n" +
            "ONLYINCOMPOUND O\nCOMPOUNDFLAG A\nSFX B Y 1\nSFX B 0 s .\n",
            "2\nfoo/A\npseudo/OAB\n",
            ("foo", true), ("pseudofoo", true), ("foopseudo", true),
            ("foopseudos", true), ("pseudo", false), ("pseudos", false));
    }

    [Fact]
    public void OnlyInCompoundAffixDoesNotPermitCompoundingWithoutPositionFlag()
    {
        Check(
            "SET UTF-8\nCOMPOUNDMIN 1\nCOMPOUNDFLAG A\nONLYINCOMPOUND O\n" +
            "COMPOUNDPERMITFLAG P\nSFX B Y 1\nSFX B 0 s/OP .\n",
            "2\nfoo/A\npseudo/B\n",
            ("pseudo", true), ("pseudos", false), ("pseudosfoo", false), ("foopseudos", false));
    }

    [Fact]
    public void OtherAffixAnalysisCanPermitStandaloneWord()
    {
        Check(
            "SET UTF-8\nONLYINCOMPOUND O\nSFX B Y 2\nSFX B 0 s/O .\nSFX B 0 s .\n",
            "1\nfoo/B\n",
            ("foo", true), ("foos", true));
    }

    [Fact]
    public void AffixPermissionAndRestrictionMustBelongToSameHomonym()
    {
        Check(
            "SET UTF-8\nONLYINCOMPOUND O\nSFX B Y 1\nSFX B 0 s .\n",
            "2\nfoo/OB\nfoo/C\n",
            ("foo", true), ("foos", false));
    }

    [Fact]
    public void RestrictedDictionarySurfaceDoesNotHideValidAffixAnalysis()
    {
        Check(
            "SET UTF-8\nONLYINCOMPOUND O\nSFX B Y 1\nSFX B 0 o .\n",
            "2\nfoo/O\nfo/B\n",
            ("foo", true));
    }

    [Fact]
    public void FlagComparisonIsCaseSensitive()
    {
        Check(
            "SET UTF-8\nCOMPOUNDMIN 1\nCOMPOUNDFLAG A\nONLYINCOMPOUND O\n",
            "2\nfoo/A\npseudo/o\n",
            ("pseudo", true), ("pseudofoo", false));
    }

    [Fact]
    public void NumericAndLongFlagsAreWholeTokens()
    {
        Check("SET UTF-8\nFLAG num\nONLYINCOMPOUND 12\n", "2\nfoo/312\nbar/12\n",
            ("foo", true), ("bar", false));
        Check("SET UTF-8\nFLAG long\nONLYINCOMPOUND AB\n", "2\nfoo/ZABA\nbar/AB\n",
            ("foo", true), ("bar", false));
    }

    [Fact]
    public void ZeroSuffixCountsAsUnmodifiedForCompoundPattern()
    {
        Check(
            "SET UTF-8\nCOMPOUNDMIN 1\nCOMPOUNDFLAG A\nCOMPOUNDPERMITFLAG P\n" +
            "NEEDAFFIX N\nSFX C Y 1\nSFX C 0 0/BP .\n" +
            "CHECKCOMPOUNDPATTERN 1\nCHECKCOMPOUNDPATTERN 0/B /A\n",
            "2\nfoo/A\npseudo/ACN\n",
            ("pseudo", true), ("pseudofoo", false), ("foopseudo", true));
    }

    private static void Check(string aff, string dic, params (string Word, bool Expected)[] examples)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hunspell-parity-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var affPath = Path.Combine(directory, "test.aff");
            var dicPath = Path.Combine(directory, "test.dic");
            File.WriteAllText(affPath, aff, Encoding.UTF8);
            File.WriteAllText(dicPath, dic, Encoding.UTF8);
            using var spellChecker = new HunspellSpellChecker(affPath, dicPath);
            foreach (var (word, expected) in examples)
            {
                var actual = spellChecker.Spell(word);
                Assert.True(actual == expected, $"{word}: expected {expected}, got {actual}");
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
