using System.Text;

namespace Hunspell;

internal sealed class ConversionTable
{
    private readonly List<Rule> _rules = [];

    internal void Add(string pattern, string replacement)
    {
        bool atStart = pattern.StartsWith('_');
        if (atStart) pattern = pattern[1..];
        bool atEnd = pattern.EndsWith('_');
        if (atEnd) pattern = pattern[..^1];
        if (pattern.Length == 0) return;

        _rules.Add(new Rule(pattern.Replace('_', ' '), replacement.Replace('_', ' '), atStart, atEnd));
    }

    internal string Apply(string word)
    {
        if (_rules.Count == 0) return word;

        StringBuilder? converted = null;
        for (int index = 0; index < word.Length;)
        {
            var remaining = word.AsSpan(index);
            Rule? best = null;
            foreach (var rule in _rules)
            {
                if ((rule.AtStart && index != 0) ||
                    (rule.AtEnd && remaining.Length != rule.Pattern.Length) ||
                    !remaining.StartsWith(rule.Pattern, StringComparison.Ordinal))
                    continue;

                if (best is not { } previous || rule.Pattern.Length > previous.Pattern.Length ||
                    (rule.Pattern.Length == previous.Pattern.Length && rule.Specificity >= previous.Specificity))
                    best = rule;
            }

            if (best is { } match)
            {
                converted ??= new StringBuilder(word.Length).Append(word.AsSpan(0, index));
                converted.Append(match.Replacement);
                index += match.Pattern.Length;
            }
            else
            {
                converted?.Append(word[index]);
                index++;
            }
        }

        return converted?.ToString() ?? word;
    }

    private readonly record struct Rule(string Pattern, string Replacement, bool AtStart, bool AtEnd)
    {
        public int Specificity => (AtStart ? 1 : 0) + (AtEnd ? 2 : 0);
    }
}
