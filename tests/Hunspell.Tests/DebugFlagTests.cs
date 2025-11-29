using System;
using Xunit;

namespace Hunspell.Tests
{
    public class DebugFlagTests
    {
        private static string D(string baseDir, string f) => Path.Combine("..","..","..","dictionaries", baseDir, f);

        [Fact]
        public void DumpFlagAcceptance()
        {
            var names = new[] { "flag", "flaglong", "flagnum", "flagutf8" };
            foreach (var n in names)
            {
                var aff = D(n, n + ".aff");
                var dic = D(n, n + ".dic");
                using var sp = new HunspellSpellChecker(aff, dic);
                var w = "unfoosbar";
                var ok = sp.Spell(w);
                var sug = sp.Suggest(w);
                Console.WriteLine($"{n}: Spell('{w}') -> {ok}; Suggestions: [{string.Join(", ", sug)}]");
                Assert.True(ok, $"Expected '{w}' accepted for {n} (diagnostic)");
            }
        }
    }
}
