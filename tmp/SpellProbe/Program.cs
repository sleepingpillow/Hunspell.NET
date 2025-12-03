using Hunspell;

if (args.Length < 3)
{
	Console.WriteLine("Usage: SpellProbe <aff-path> <dic-path> <word1> [word2 ...]");
	return;
}

var affPath = Path.GetFullPath(args[0]);
var dicPath = Path.GetFullPath(args[1]);
var words = args.Skip(2).ToArray();

using var checker = new HunspellSpellChecker(affPath, dicPath);

var affixField = typeof(HunspellSpellChecker).GetField("_affixManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var affixManager = affixField?.GetValue(checker);
var checkAffixed = affixManager?.GetType().GetMethod("CheckAffixedWord", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
var checkCompound = affixManager?.GetType().GetMethod("CheckCompound", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
var isValidPart = affixManager?.GetType().GetMethod("IsValidCompoundPart", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
var checkRules = affixManager?.GetType().GetMethod("CheckCompoundRules", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

foreach (var word in words)
{
	bool ok = checker.Spell(word);
	bool affixed = false;
	bool compound = false;

	if (checkAffixed is not null)
	{
		affixed = (bool)checkAffixed.Invoke(affixManager, new object[] { word, false })!;
	}

	if (checkCompound is not null)
	{
		compound = (bool)checkCompound.Invoke(affixManager, new object[] { word })!;
	}

	Console.WriteLine($"{word} => Spell:{ok} Affix:{affixed} Compound:{compound}");

	if (isValidPart is not null && word.Length >= 2)
	{
		for (int split = 1; split < word.Length; split++)
		{
			var left = word.Substring(0, split);
			var right = word.Substring(split);

			var leftArgs = new object?[] { left, 0, 0, split, word, false };
			var leftValid = (bool)isValidPart.Invoke(affixManager, leftArgs)!;
			var leftForce = (bool)leftArgs[5]!;

			var rightArgs = new object?[] { right, 1, split, word.Length, word, false };
			var rightValid = (bool)isValidPart.Invoke(affixManager, rightArgs)!;
			var rightForce = (bool)rightArgs[5]!;

			bool rulesOk = true;
			if (checkRules is not null && leftValid && rightValid)
			{
				var ruleArgs = new object?[] { word, split, split + right.Length, left, right };
				rulesOk = (bool)checkRules.Invoke(affixManager, ruleArgs)!;
			}

			Console.WriteLine($"  split {split}: '{left}' valid={leftValid} force={leftForce} | '{right}' valid={rightValid} force={rightForce} | rulesOk={rulesOk}");
		}
	}
}
