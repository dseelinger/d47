namespace D47.Core.Conversation;

/// <summary>
/// A phrase pattern: plain words and <c>[a|b|c]</c> groups of alternatives, where an empty alternative
/// makes the group optional. Groups do not nest.
/// </summary>
public static class PhrasePattern
{
    public const int MaxWordings = 100;

    /// <summary>The wordings a pattern stands for, normalised and distinct, or the reason it is refused.</summary>
    public static bool TryExpand(string pattern, out IReadOnlyList<string> wordings, out string error)
    {
        wordings = [];
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(pattern))
        {
            error = "The pattern is empty.";
            return false;
        }

        var parts = new List<List<string>>();
        var literal = new System.Text.StringBuilder();
        List<string>? alternatives = null;

        foreach (var ch in pattern)
        {
            switch (ch)
            {
                case '[' when alternatives is not null:
                    error = "Groups cannot be nested: found '[' inside a group.";
                    return false;

                case '[':
                    parts.Add([literal.ToString()]);
                    literal.Clear();
                    alternatives = [];
                    break;

                case '|' when alternatives is not null:
                    alternatives.Add(literal.ToString());
                    literal.Clear();
                    break;

                case '|':
                    error = "A '|' belongs inside a [group].";
                    return false;

                case ']' when alternatives is null:
                    error = "Found ']' with no '[' before it.";
                    return false;

                case ']':
                    alternatives!.Add(literal.ToString());
                    literal.Clear();
                    parts.Add(alternatives);
                    alternatives = null;
                    break;

                default:
                    literal.Append(ch);
                    break;
            }
        }

        if (alternatives is not null)
        {
            error = "A '[' is never closed.";
            return false;
        }

        parts.Add([literal.ToString()]);

        var count = 1L;

        foreach (var part in parts)
        {
            count *= part.Count;

            if (count > MaxWordings)
            {
                error = $"The pattern makes more than {MaxWordings} wordings.";
                return false;
            }
        }

        IEnumerable<string> built = [string.Empty];

        foreach (var part in parts)
        {
            built = built.SelectMany(prefix => part.Select(alternative => prefix + alternative)).ToList();
        }

        var distinct = built
            .Select(KeywordRouter.Utterance)
            .Where(wording => wording.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
        {
            error = "The pattern has no words in it.";
            return false;
        }

        wordings = distinct;
        return true;
    }

    /// <summary>The utterance with the pattern characters removed, so it can only be a literal wording.</summary>
    public static string Literal(string utterance) =>
        string.Concat(utterance.Where(ch => ch is not ('[' or ']' or '|')));
}
