using System.Text.Json;
using System.Text.Json.Serialization;

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
/// Which stock stories the Stories page lists and in what order: any of the levels, a length compared with one of
/// <see cref="StoryPacing.All"/>, a least star rating, a sort, and optionally only stories in the black.
/// </summary>
public sealed record StoryFilter(
    [property: JsonPropertyName("level"), JsonConverter(typeof(StoryLevelsConverter))] IReadOnlyList<string>? Levels = null,
    StoryLengthCompare Compare = StoryLengthCompare.Any,
    string? Length = null,
    int? MinStars = null,
    StorySort Sort = StorySort.Catalogue,
    bool InTheBlack = false)
{
    /// <summary>Whether no level narrows the list: none is set, or every level is.</summary>
    public bool AnyLevel => Levels is not { Count: > 0 } || StoryCard.Levels.All(level => Levels.Contains(level, StringComparer.Ordinal));

    /// <summary>Whether the filter keeps every card in catalogue order.</summary>
    public bool IsDefault => AnyLevel && LengthIndex is null && MinStars is null && Sort == StorySort.Catalogue && !InTheBlack;

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

        if (InTheBlack && card.InTheBlack == StoryBlackAmount.None)
        {
            return false;
        }

        if (!AnyLevel && !Levels!.Contains(card.Level, StringComparer.Ordinal))
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

    public bool Equals(StoryFilter? other) =>
        other is not null
        && Compare == other.Compare
        && Length == other.Length
        && MinStars == other.MinStars
        && Sort == other.Sort
        && InTheBlack == other.InTheBlack
        && (Levels ?? []).ToHashSet(StringComparer.Ordinal).SetEquals(other.Levels ?? []);

    public override int GetHashCode() =>
        HashCode.Combine(Compare, Length, MinStars, Sort, InTheBlack, (Levels ?? []).Distinct(StringComparer.Ordinal).Aggregate(0, (hash, level) => hash ^ StringComparer.Ordinal.GetHashCode(level)));

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

/// <summary>Reads a saved <c>"level"</c> as a single key (the older form) or an array of keys, and writes an array.</summary>
internal sealed class StoryLevelsConverter : JsonConverter<IReadOnlyList<string>>
{
    public override IReadOnlyList<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() is { Length: > 0 } one ? [one] : [];

            case JsonTokenType.StartArray:
                var keys = new List<string>();

                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String && reader.GetString() is { Length: > 0 } key)
                    {
                        keys.Add(key);
                    }
                }

                return keys;

            default:
                reader.Skip();
                return [];
        }
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<string> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var level in value)
        {
            writer.WriteStringValue(level);
        }

        writer.WriteEndArray();
    }
}
