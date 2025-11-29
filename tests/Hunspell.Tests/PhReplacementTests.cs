using System.Linq;
using Xunit;

namespace Hunspell.Tests
{
    public class PhReplacementTests
    {
        private static string D(string baseDir, string f) => Path.Combine("..", "..", "..", "dictionaries", baseDir, f);

        [Theory]
        [InlineData("prity", "pretty")]
        [InlineData("pritiest", "prettiest")]
        [InlineData("hepy", "happy")]
        [InlineData("hepiest", "happiest")]
        [InlineData("fubarö", "foobarö")]
        [InlineData("fubarőt", "foobarőt")]
        public void PhReplacements_ProduceExpectedSuggestion(string wrong, string expected)
        {
            var aff = D("ph2", "ph2.aff");
            var dic = D("ph2", "ph2.dic");

            using var sp = new HunspellSpellChecker(aff, dic);

            var suggestions = sp.Suggest(wrong).Select(s => s.ToLowerInvariant()).ToList();

            Assert.True(suggestions.Count > 0, $"Expected suggestions for '{wrong}' but found none.");
            Assert.Contains(expected.ToLowerInvariant(), suggestions);
        }
    }
}
