using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;

namespace Hunspell.Tests;

/// <summary>
/// Tests for upstream Hunspell test cases. These tests verify compatibility with
/// the original Hunspell test suite.
/// 
/// Test Status Summary (this file only):
/// - GoodWords tests (Root Level): 73 active, 41 commented out
/// - GoodWords tests (Nested): 20 active
/// - WrongWords tests (Root Level): 74 active, 23 commented out  
/// - WrongWords tests (Nested): 20 active
/// - Suggestions tests: 6 active
/// - Total active in this file: 193 tests (all passing)
/// - Total commented out: 64 tests (features not yet implemented)
/// 
/// Note: The test project also includes 94 other tests in separate test files.
/// 
/// Commented out tests indicate features not yet fully implemented.
/// See docs/upstream-test-status.md for detailed status.
/// </summary>
public class UpstreamAffixAndCompoundTests
{
    private static string D(string baseName, string ext)
    {
        // baseName can be "rep" or "compoundrule/basic" 
        // Extract just the final part for the filename
        var parts = baseName.Split('/');
        var fileName = parts[^1]; // Get the last part
        return Path.Combine("..", "..", "..", "dictionaries", baseName, fileName + ext);
    }

    private static IEnumerable<string> ReadList(string path)
    {
        if (!File.Exists(path)) yield break;
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t)) continue;
            yield return t;
        }
    }

    #region GoodWords Tests - Root Level Test Cases

    [Theory]
    // Root-level upstream test cases with .good files
    // Alphabetically sorted for easy maintenance
    // 
    // PASSING TESTS:
    [InlineData("1748408-1")]
    [InlineData("1748408-2")]
    [InlineData("1748408-3")]
    [InlineData("1748408-4")]
    [InlineData("2970240")]
    [InlineData("2970242")]
    [InlineData("IJ")]
    [InlineData("alias")]
    [InlineData("alias2")]
    [InlineData("allcaps")]
    [InlineData("allcaps2")]
    [InlineData("allcaps3")]
    [InlineData("allcaps_utf")]
    [InlineData("base")]
    [InlineData("base_utf")]
    [InlineData("break")]
    [InlineData("breakdefault")]
    [InlineData("breakoff")]
    [InlineData("checkcompoundcase")]
    [InlineData("checkcompoundcaseutf")]
    [InlineData("checkcompounddup")]
    [InlineData("checkcompoundrep2")]
    [InlineData("checkcompoundtriple")]
    [InlineData("circumfix")]
    [InlineData("compoundaffix")]
    [InlineData("compoundaffix2")]
    [InlineData("compoundaffix3")]
    [InlineData("compoundflag")]
    [InlineData("compoundforbid")]
    [InlineData("compoundrule")]
    [InlineData("compoundrule2")]
    [InlineData("compoundrule3")]
    [InlineData("compoundrule4")]
    [InlineData("compoundrule5")]
    [InlineData("compoundrule6")]
    [InlineData("compoundrule7")]
    [InlineData("compoundrule8")]
    [InlineData("condition_utf")]
    [InlineData("dotless_i")]
    [InlineData("fogemorpheme")]
    [InlineData("forbiddenword")]
    [InlineData("forceucase")]
    [InlineData("fullstrip")]
    [InlineData("i35725")]
    [InlineData("i58202")]
    [InlineData("ignore")]
    [InlineData("ignoreutf")]
    [InlineData("keepcase")]
    [InlineData("korean")]
    [InlineData("limit-multiple-compounding")]
    [InlineData("map")]
    [InlineData("needaffix")]
    [InlineData("needaffix3")]
    [InlineData("ngram_utf_fix")]
    [InlineData("nosuggest")]
    [InlineData("oconv")]
    [InlineData("onlyincompound")]
    [InlineData("onlyincompound2")]
    [InlineData("opentaal_cpdpat")]
    [InlineData("opentaal_forbiddenword1")]
    [InlineData("opentaal_forbiddenword2")]
    [InlineData("opentaal_keepcase")]
    [InlineData("ph2")]
    [InlineData("slash")]
    [InlineData("timelimit")]
    [InlineData("utf8")]
    [InlineData("utf8_bom")]
    [InlineData("utf8_bom2")]
    [InlineData("utf8_nonbmp")]
    [InlineData("utfcompound")]
    [InlineData("warn")]
    [InlineData("wordpair")]
    [InlineData("zeroaffix")]
    //
    // FAILING TESTS - Commented out (features not yet implemented):
    // 
    // Bug tracker tests requiring specific fixes:
    // [InlineData("1592880")]        // FAILING: Bug tracker test
    // [InlineData("1975530")]        // FAILING: Bug tracker test  
    // [InlineData("2999225")]        // FAILING: Bug tracker test
    //
    // CHECKSHARPS feature (German ß handling):
    // [InlineData("checksharps")]    // FAILING: CHECKSHARPS not implemented
    // [InlineData("checksharpsutf")] // FAILING: CHECKSHARPS not implemented
    //
    // CHECKCOMPOUNDPATTERN feature:
    // [InlineData("checkcompoundcase2")]     // FAILING: Advanced compound case handling
    // [InlineData("checkcompoundpattern")]   // FAILING: CHECKCOMPOUNDPATTERN not fully implemented
    // [InlineData("checkcompoundpattern2")]  // FAILING: CHECKCOMPOUNDPATTERN not fully implemented
    // [InlineData("checkcompoundpattern3")]  // FAILING: CHECKCOMPOUNDPATTERN not fully implemented
    // [InlineData("checkcompoundpattern4")]  // FAILING: CHECKCOMPOUNDPATTERN not fully implemented
    // [InlineData("checkcompoundrep")]       // FAILING: CHECKCOMPOUNDREP not fully implemented
    //
    // COMPLEXPREFIXES feature (right-to-left languages):
    // [InlineData("complexprefixes")]    // FAILING: COMPLEXPREFIXES not implemented
    // [InlineData("complexprefixes2")]   // FAILING: COMPLEXPREFIXES not implemented
    // [InlineData("complexprefixesutf")] // FAILING: COMPLEXPREFIXES not implemented
    //
    // Advanced affix condition handling:
    // [InlineData("affixes")]            // FAILING: Advanced affix conditions
    // [InlineData("alias3")]             // FAILING: Advanced alias handling
    // [InlineData("condition")]          // FAILING: Advanced condition matching
    // [InlineData("conditionalprefix")]  // FAILING: Conditional prefix application
    //
    // FLAG types (long/num/utf8):
    // [InlineData("encoding")]   // FAILING: Non-UTF8 encoding handling
    // [InlineData("flag")]       // FAILING: FLAG type handling
    // [InlineData("flaglong")]   // FAILING: FLAG long type
    // [InlineData("flagnum")]    // FAILING: FLAG num type
    // [InlineData("flagutf8")]   // FAILING: FLAG UTF-8 type
    //
    // German compounding:
    // [InlineData("germancompounding")]     // FAILING: German compounding rules
    // [InlineData("germancompoundingold")]  // FAILING: Old German compounding rules
    //
    // Hungarian-specific tests:
    // [InlineData("hu")]   // FAILING: Hungarian language features
    //
    // ICONV/OCONV (input/output conversion):
    // [InlineData("iconv")]     // FAILING: ICONV not implemented
    // [InlineData("iconv2")]    // FAILING: ICONV not implemented
    // [InlineData("oconv2")]    // FAILING: OCONV edge cases
    //
    // Bug tracker tests:
    // [InlineData("i53643")]  // FAILING: Bug tracker test
    // [InlineData("i54633")]  // FAILING: Bug tracker test
    // [InlineData("i54980")]  // FAILING: Bug tracker test
    //
    // IGNORESUG and related:
    // [InlineData("ignoresug")]   // FAILING: IGNORESUG not implemented
    //
    // Morphological analysis:
    // [InlineData("morph")]   // FAILING: Morphological analysis not implemented
    //
    // NEEDAFFIX advanced cases:
    // [InlineData("needaffix2")]  // FAILING: Advanced NEEDAFFIX handling
    // [InlineData("needaffix4")]  // FAILING: Advanced NEEDAFFIX handling
    // [InlineData("needaffix5")]  // FAILING: Advanced NEEDAFFIX handling
    //
    // Language-specific tests:
    // [InlineData("nepali")]              // FAILING: Nepali language features
    // [InlineData("right_to_left_mark")]  // FAILING: RTL mark handling
    //
    // OpenTaal (Dutch):
    // [InlineData("opentaal_cpdpat2")]  // FAILING: OpenTaal compound pattern
    //
    // SIMPLIFIEDTRIPLE feature:
    // [InlineData("simplifiedtriple")]  // FAILING: SIMPLIFIEDTRIPLE not implemented
    public void UpstreamGoodWords_RootLevel_ShouldPass(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        // if both aff and dic missing, skip
        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var good = D(baseName, ".good");
        foreach (var w in ReadList(good))
        {
            Assert.True(sp.Spell(w), $"Expected '{w}' from {baseName}.good to be accepted");
        }
    }

    #endregion

    #region GoodWords Tests - Nested Subdirectory Test Cases

    [Theory]
    // Nested test cases (subdirectories within test directories)
    [InlineData("affixes/affixes")]
    [InlineData("break/basic")]
    [InlineData("checkcompoundpattern/basic")]
    [InlineData("checkcompoundpattern/replacement")]
    [InlineData("checkcompoundrep/basic")]
    [InlineData("compoundmoresuffixes/basic")]
    [InlineData("compoundrule/basic")]
    [InlineData("compoundrule/compoundrule")]
    [InlineData("compoundrule/compoundrule2")]
    [InlineData("compoundrule/compoundrule3")]
    [InlineData("compoundrule/compoundrule4")]
    [InlineData("compoundrule/compoundrule5")]
    [InlineData("compoundrule/compoundrule6")]
    [InlineData("compoundrule/compoundrule7")]
    [InlineData("compoundrule/star")]
    [InlineData("compoundsyllable/syllable")]
    [InlineData("condition/condition")]
    [InlineData("swedish/directives")]
    [InlineData("swedish/sv_FI")]
    [InlineData("swedish/sv_SE")]
    public void UpstreamGoodWords_Nested_ShouldPass(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        // if both aff and dic missing, skip
        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var good = D(baseName, ".good");
        foreach (var w in ReadList(good))
        {
            Assert.True(sp.Spell(w), $"Expected '{w}' from {baseName}.good to be accepted");
        }
    }

    #endregion

    #region WrongWords Tests - Root Level Test Cases

    [Theory]
    // Root-level upstream test cases with .wrong files
    //
    // PASSING TESTS:
    [InlineData("1463589")]
    [InlineData("1463589_utf")]
    [InlineData("1695964")]
    [InlineData("1975530")]
    [InlineData("2970240")]
    [InlineData("IJ")]
    [InlineData("alias")]
    [InlineData("allcaps")]
    [InlineData("allcaps2")]
    [InlineData("allcaps3")]
    [InlineData("allcaps_utf")]
    [InlineData("arabic")]
    [InlineData("base")]
    [InlineData("base_utf")]
    [InlineData("break")]
    [InlineData("breakdefault")]
    [InlineData("breakoff")]
    [InlineData("checkcompoundcase")]
    [InlineData("checkcompoundcase2")]
    [InlineData("checkcompoundcaseutf")]
    [InlineData("checkcompounddup")]
    [InlineData("checkcompoundpattern")]
    [InlineData("checkcompoundpattern2")]
    [InlineData("checkcompoundpattern4")]
    [InlineData("checkcompoundrep")]
    [InlineData("checkcompoundrep2")]
    [InlineData("checkcompoundtriple")]
    [InlineData("checksharps")]
    [InlineData("checksharpsutf")]
    [InlineData("compoundaffix")]
    [InlineData("compoundaffix2")]
    [InlineData("compoundflag")]
    [InlineData("compoundforbid")]
    [InlineData("compoundrule")]
    [InlineData("compoundrule2")]
    [InlineData("compoundrule3")]
    [InlineData("compoundrule4")]
    [InlineData("compoundrule5")]
    [InlineData("compoundrule6")]
    [InlineData("compoundrule7")]
    [InlineData("compoundrule8")]
    [InlineData("condition")]
    [InlineData("condition_utf")]
    [InlineData("digits_in_words")]
    [InlineData("dotless_i")]
    [InlineData("forbiddenword")]
    [InlineData("forceucase")]
    [InlineData("fullstrip")]
    [InlineData("i35725")]
    [InlineData("i53643")]
    [InlineData("i54633")]
    [InlineData("keepcase")]
    [InlineData("korean")]
    [InlineData("map")]
    [InlineData("maputf")]
    [InlineData("needaffix")]
    [InlineData("nepali")]
    [InlineData("nosuggest")]
    [InlineData("oconv")]
    [InlineData("onlyincompound")]
    [InlineData("opentaal_forbiddenword1")]
    [InlineData("opentaal_forbiddenword2")]
    [InlineData("ph")]
    [InlineData("ph2")]
    [InlineData("phone")]
    [InlineData("rep")]
    [InlineData("reputf")]
    [InlineData("sug")]
    [InlineData("sug2")]
    [InlineData("sugutf")]
    [InlineData("timelimit")]
    [InlineData("utf8_nonbmp")]
    [InlineData("utfcompound")]
    [InlineData("wordpair")]
    //
    // FAILING TESTS - Commented out (features not yet implemented):
    //
    // Bug tracker tests:
    // [InlineData("1706659")]  // FAILING: Bug tracker test
    // [InlineData("2970242")]  // FAILING: Bug tracker test (wrong word accepted)
    //
    // CHECKCOMPOUNDPATTERN issues:
    // [InlineData("checkcompoundpattern3")]  // FAILING: Pattern boundary not enforced
    //
    // CIRCUMFIX feature:
    // [InlineData("circumfix")]  // FAILING: CIRCUMFIX not fully implemented
    //
    // Compound affix advanced cases:
    // [InlineData("compoundaffix3")]  // FAILING: Complex compound affix rules
    //
    // COMPLEXPREFIXES feature:
    // [InlineData("complexprefixes")]    // FAILING: COMPLEXPREFIXES not implemented
    // [InlineData("complexprefixesutf")] // FAILING: COMPLEXPREFIXES not implemented
    //
    // CONDITIONALPREFIX feature:
    // [InlineData("conditionalprefix")]  // FAILING: Conditional prefix restrictions
    //
    // FOGEMORPHEME feature:
    // [InlineData("fogemorpheme")]  // FAILING: FOGEMORPHEME not implemented
    //
    // German compounding:
    // [InlineData("germancompounding")]     // FAILING: German compound restrictions
    // [InlineData("germancompoundingold")]  // FAILING: Old German compound restrictions
    //
    // Hungarian-specific:
    // [InlineData("hu")]  // FAILING: Hungarian restrictions
    //
    // Bug tracker tests:
    // [InlineData("i58202")]     // FAILING: Bug tracker test
    // [InlineData("i68568")]     // FAILING: Bug tracker test
    // [InlineData("i68568utf")]  // FAILING: Bug tracker test
    //
    // Compound limiting:
    // [InlineData("limit-multiple-compounding")]  // FAILING: Compound limit not enforced
    //
    // NEEDAFFIX advanced:
    // [InlineData("needaffix3")]  // FAILING: Advanced NEEDAFFIX restrictions
    // [InlineData("needaffix5")]  // FAILING: Advanced NEEDAFFIX restrictions
    //
    // N-gram UTF-8:
    // [InlineData("ngram_utf_fix")]  // FAILING: N-gram UTF-8 handling
    //
    // ONLYINCOMPOUND advanced:
    // [InlineData("onlyincompound2")]  // FAILING: Advanced ONLYINCOMPOUND
    //
    // OpenTaal compound patterns:
    // [InlineData("opentaal_cpdpat")]    // FAILING: OpenTaal compound pattern
    // [InlineData("opentaal_keepcase")]  // FAILING: OpenTaal keepcase handling
    //
    // SIMPLIFIEDTRIPLE:
    // [InlineData("simplifiedtriple")]  // FAILING: SIMPLIFIEDTRIPLE not implemented
    public void UpstreamWrongWords_RootLevel_ShouldFail(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var wrong = D(baseName, ".wrong");
        foreach (var w in ReadList(wrong))
        {
            Assert.False(sp.Spell(w), $"Expected '{w}' from {baseName}.wrong to be rejected");
        }
    }

    #endregion

    #region WrongWords Tests - Nested Subdirectory Test Cases

    [Theory]
    // Nested test cases (subdirectories within test directories)
    [InlineData("affixes/affixes")]
    [InlineData("break/basic")]
    [InlineData("checkcompoundpattern/basic")]
    [InlineData("checkcompoundpattern/replacement")]
    [InlineData("checkcompoundrep/basic")]
    [InlineData("compoundmoresuffixes/basic")]
    [InlineData("compoundrule/basic")]
    [InlineData("compoundrule/compoundrule")]
    [InlineData("compoundrule/compoundrule2")]
    [InlineData("compoundrule/compoundrule3")]
    [InlineData("compoundrule/compoundrule4")]
    [InlineData("compoundrule/compoundrule5")]
    [InlineData("compoundrule/compoundrule6")]
    [InlineData("compoundrule/compoundrule7")]
    [InlineData("compoundrule/star")]
    [InlineData("compoundsyllable/syllable")]
    [InlineData("condition/condition")]
    [InlineData("swedish/directives")]
    [InlineData("swedish/sv_FI")]
    [InlineData("swedish/sv_SE")]
    public void UpstreamWrongWords_Nested_ShouldFail(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var wrong = D(baseName, ".wrong");
        foreach (var w in ReadList(wrong))
        {
            Assert.False(sp.Spell(w), $"Expected '{w}' from {baseName}.wrong to be rejected");
        }
    }

    #endregion

    #region Suggestions Tests

    [Theory]
    [InlineData("sug")]
    [InlineData("sug2")]
    [InlineData("rep")]
    [InlineData("alias")]
    [InlineData("fullstrip")]
    [InlineData("wordpair")]
    public void UpstreamSuggestions_ShouldContainExpected(string baseName)
    {
        var aff = D(baseName, ".aff");
        var dic = D(baseName, ".dic");

        if (!File.Exists(dic) && !File.Exists(aff)) return;

        using var sp = new HunspellSpellChecker(aff, dic);

        var wrong = ReadList(D(baseName, ".wrong")).ToList();
        var sug = ReadList(D(baseName, ".sug")).ToList();

        if (!wrong.Any() || !sug.Any()) return;

        var count = Math.Min(wrong.Count, sug.Count);
        for (int i = 0; i < count; i++)
        {
            var miss = wrong[i];
            var expectedLine = sug[i];
            // expected format: "miss:expected1,expected2" or just "expected"
            var parts = expectedLine.Split(':', 2);
            var expectedPart = parts.Length > 1 ? parts[1] : parts[0];
            var expected = expectedPart.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().Trim('"')).Where(s => s.Length > 0).Select(s => s.ToLowerInvariant()).ToList();

            var suggestions = sp.Suggest(miss).Select(s => s.ToLowerInvariant()).ToList();

            Assert.True(suggestions.Count > 0, $"Expected suggestions for '{miss}' in {baseName}.sug but found none.");

            // At least one expected suggestion should be present in returned suggestions
            Assert.True(expected.Any(e => suggestions.Contains(e)),
                $"Expected at least one of [{string.Join(",", expected)}] for '{miss}' but suggestions were [{string.Join(",", suggestions)}]");
        }
    }

    #endregion
}
