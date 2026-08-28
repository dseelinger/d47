namespace D47.App.Controls;

/// <summary>
/// Whether what a Commander typed matches a choice in the picker
/// (<a href="https://github.com/dseelinger/d47/issues/146">#146</a>).
/// <para>
/// <b>It used to be <c>Contains</c>, and <c>"female".Contains("male")</c> is true.</b> So typing
/// <em>male</em> in the voice picker listed every female voice, and there was no way to type your
/// way out of it: <em>female</em> worked and <em>male</em> could not.
/// </para>
/// <para>
/// <b>This repository has already ruled on exactly this failure, one surface over.</b> Phase 52, on
/// the spoken router: <em>"a keyword that short hijacks every sentence containing it… so Engage is a
/// whole phrase and never a keyword"</em>. <c>engage</c> inside <em>engage supercruise</em> is
/// <c>male</c> inside <em>female</em>. The ruling there was whole-phrase matching; the equivalent
/// for a search box is matching at <b>word starts</b>.
/// </para>
/// <para>
/// <b>Word starts rather than whole words, because whole words would break the searching that
/// works.</b> <em>eng</em> must still find <em>Engineering</em> and <em>kra</em> must still find
/// <em>Krait</em>, and whole-word matching finds neither. The choice between the two is not
/// cosmetic and this is the one that fixes the bug without taking anything away.
/// </para>
/// </summary>
public static class ChoiceMatch
{
    /// <summary>
    /// Whether <paramref name="filter"/> begins a word in <paramref name="text"/>. An empty filter
    /// matches everything, which is the picker's resting state.
    /// </summary>
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

    /// <summary>
    /// Whether this position begins a word. Three ways it can, and the third is what keeps the
    /// search useful on the ids these lists are full of.
    /// <list type="number">
    /// <item>The start of the string.</item>
    /// <item>After anything that is not a letter or a digit — a space, a hyphen, a bracket, a dot.
    /// That covers <em>en-US</em>, <c>{0.0.1.00000000}</c> and every ordinary sentence.</item>
    /// <item><b>A camel hump</b>: a capital following a lower-case letter or a digit. Without it,
    /// <em>Multilingual</em> would stop finding <c>en-US-AndrewMultilingualNeural</c>, which is what
    /// most of the Edge catalogue is named like — a regression the obvious rule would have shipped
    /// silently.</item>
    /// </list>
    /// <para>
    /// A hump requires the <em>previous</em> character to be lower case or a digit, so a run of
    /// capitals is one word: <c>FEMALE</c> does not start a word at its <c>M</c>, and the bug stays
    /// fixed however a provider capitalises its labels.
    /// </para>
    /// </summary>
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
