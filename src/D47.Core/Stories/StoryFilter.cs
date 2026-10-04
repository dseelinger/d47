namespace D47.Core.Stories;

/// <summary>How a card's length is compared with <see cref="StoryFilter.Length"/>.</summary>
public enum StoryLengthCompare
{
    Any,
    AtLeast,
    Exactly,
    AtMost,
}

/// <summary>How the Stories page orders the cards.</summary>
public enum StorySort
{
    Catalogue,
    HighestRated,
}

/// <summary>
/// Which stock stories the Stories page lists and in what order: a level, a length compared with one of
/// <see cref="StoryPacing.All"/>, a least star rating, and a sort.
/// </summary>
public sealed record StoryFilter(
    string? Level = null,
    StoryLengthCompare Compare = StoryLengthCompare.Any,
    string? Length = null,
    int? MinStars = null,
    StorySort Sort = StorySort.Catalogue)
{
    /// <summary>Whether the filter keeps every card in catalogue order.</summary>
    public bool IsDefault => Level is null && LengthIndex is null && MinStars is null && Sort == StorySort.Catalogue;

    /// <summary>Whether the card matches ignoring ratings; <see cref="MinStars"/>, if set, then fails every card.</summary>
    public bool Matches(StoryCard card) => Matches(card, StoryRatings.Empty);

    /// <summary>
    /// Whether the card has the level, if one is set, a length that satisfies the comparison and, if <see cref="MinStars"/>
    /// is set, a rated <see cref="StoryRating.Stars"/> at least that high; an unknown <see cref="Length"/> compares as Any.
    /// </summary>
    public bool Matches(StoryCard card, StoryRatings ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        if (MinStars is { } least && !(ratings.Get(card.Id) is { } rating && rating.Stars >= least))
        {
            return false;
        }

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

    /// <summary>The cards in <see cref="Sort"/> order; unrated cards come last, in catalogue order, under <see cref="StorySort.HighestRated"/>.</summary>
    public IEnumerable<StoryCard> Order(IEnumerable<StoryCard> cards, StoryRatings ratings)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(ratings);

        if (Sort != StorySort.HighestRated)
        {
            return cards;
        }

        var all = cards.ToList();

        return all.Where(card => ratings.Get(card.Id) is not null)
            .OrderByDescending(card => ratings.Get(card.Id)!.Stars)
            .ThenByDescending(card => ratings.Get(card.Id)!.Count)
            .Concat(all.Where(card => ratings.Get(card.Id) is null));
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
