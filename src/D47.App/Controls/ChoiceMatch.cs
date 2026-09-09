namespace D47.App.Controls;

/// <summary>Whether what a Commander typed matches a choice in the picker (#146).</summary>
public static class ChoiceMatch
{
    /// <summary>Whether <paramref name="filter"/> begins a word in <paramref name="text"/>.</summary>
    public static bool Matches(string? text, string filter)
    {
        if (filter.Length == 0)
        {
            return true;
        }

        if (string.IsNullOrEmpty(text) || filter.Length > text.Length)
        {
            return false;
        }

        for (var i = 0; i + filter.Length <= text.Length; i++)
        {
            if (StartsAWord(text, i)
                && text.AsSpan(i, filter.Length).Equals(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether this position begins a word.</summary>
    private static bool StartsAWord(string text, int index)
    {
        if (index == 0)
        {
            return true;
        }

        var previous = text[index - 1];

        if (!char.IsLetterOrDigit(previous))
        {
            return true;
        }

        return char.IsUpper(text[index]) && (char.IsLower(previous) || char.IsDigit(previous));
    }
}
