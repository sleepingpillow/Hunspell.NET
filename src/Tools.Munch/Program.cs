using System;
using System.IO;
using System.Threading.Tasks;

namespace Tools.Munch
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("Usage: munch <wordlist> <affix-file>");
                return 1;
            }

            var wordListPath = args[0];
            var affPath = args[1];

            if (!File.Exists(wordListPath))
            {
                Console.Error.WriteLine($"Word list not found: {wordListPath}");
                return 2;
            }
            if (!File.Exists(affPath))
            {
                Console.Error.WriteLine($"Affix file not found: {affPath}");
                return 3;
            }

            var muncher = new Muncher();
            try
            {
                var aff = AffixParser.Parse(affPath);
                var result = await muncher.RunAsync(wordListPath, aff);

                // Print count and lines (compatible-ish with original munch output)
                Console.Out.WriteLine(result.KeptCount);
                foreach (var line in result.Lines)
                    Console.Out.WriteLine(line);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error: " + ex.Message);
                return 4;
            }
        }
    }
}
