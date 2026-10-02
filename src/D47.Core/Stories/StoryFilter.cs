namespace D47.Core.Stories;

/// <summary>How a card's length is compared with <see cref="StoryFilter.Length"/>.</summary>
public enum StoryLengthCompare
{
    Any,
    AtLeast,
    Exactly,
    AtMost,
}

/// <summary>Which stock stories the Stories page lists: a level, and a length compared with one of <see cref="StoryPacing.All"/>.</summary>
public sealed record StoryFilter(string? Level = null, StoryLengthCompare Compare = StoryLengthCompare.Any, string? Length = null)
{
    /// <summary>Whether the filter keeps every card.</summary>
    public bool IsDefault => Level is null && LengthIndex is null;

    /// <summary>Whether the card has the level, if one is set, and a length that satisfies the comparison; an unknown <see cref="Length"/> compares as Any.</summary>
    public bool Matches(StoryCard card)
    {
        if (Level is not null && !string.Equals(card.Level, Level, StringComparison.Ordinal))
        {
            return false;
        }

        if (LengthIndex is not { } wanted)
        {
            return true;
        }

        var have = IndexOf(card.Pacing);

        return Compare switch
        {
            StoryLengthCompare.AtLeast => have >= wanted,
            StoryLengthCompare.Exactly => have == wanted,
            _ => have <= wanted,
        };
    }

    private int? LengthIndex => Compare != StoryLengthCompare.Any && StoryPacing.Find(Length) is { } pacing ? IndexOf(pacing) : null;

    private static int IndexOf(StoryPacing pacing)
    {
        for (var i = 0; i < StoryPacing.All.Count; i++)
        {
            if (StoryPacing.All[i].Key == pacing.Key)
            {
                return i;
            }
        }

        return StoryPacing.All.Count - 1;
    }
}
