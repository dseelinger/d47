namespace D47.Core.Stories;

/// <summary>How much of a story's length takes place in the black.</summary>
public enum StoryBlackAmount
{
    None,
    Part,
    Most,
}

/// <summary>Which chapters of a story take place in the black, by the spans its hidden entry marks.</summary>
public static class StoryBlack
{
    /// <summary>The card values of <see cref="StoryBlackAmount"/>.</summary>
    public static readonly IReadOnlyList<string> Values = ["none", "part", "most"];

    /// <summary>The amount a card's <c>black</c> names; a missing or unknown value is none.</summary>
    public static StoryBlackAmount Parse(string? value)
    {
        var at = Values.ToList().FindIndex(known => string.Equals(known, value, StringComparison.Ordinal));

        return at < 0 ? StoryBlackAmount.None : (StoryBlackAmount)at;
    }

    /// <summary>None when no beat of <paramref name="pacing"/>'s sheet lies in a span, most when more than half do, otherwise part.</summary>
    public static StoryBlackAmount Amount(StorySecret secret, StoryPacing pacing)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(pacing);

        var spans = Spans(secret).ToList();
        var sheet = pacing.BeatKeys.Select(StoryAboard.Order).Where(at => at >= 0).ToList();
        var covered = sheet.Count(at => spans.Any(span => span.From <= at && at < span.To));

        return covered == 0 ? StoryBlackAmount.None : covered * 2 > sheet.Count ? StoryBlackAmount.Most : StoryBlackAmount.Part;
    }

    /// <summary>Whether any of <paramref name="stageBeats"/>, the beats a chapter's stage hands out, lies in one of the entry's spans.</summary>
    public static bool Covers(StorySecret secret, IReadOnlyList<string> stageBeats)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(stageBeats);

        var spans = Spans(secret).ToList();

        return stageBeats
            .Select(StoryAboard.Order)
            .Where(at => at >= 0)
            .Any(at => spans.Any(span => span.From <= at && at < span.To));
    }

    /// <summary>The entry's spans as story-order ranges, from included and to not, leaving out any span naming a key that is not a beat.</summary>
    public static IEnumerable<(int From, int To)> Spans(StorySecret secret)
    {
        ArgumentNullException.ThrowIfNull(secret);

        foreach (var span in (secret.Black ?? []).OfType<StoryBlackSpan>())
        {
            var from = span.From is null ? 0 : StoryAboard.Order(span.From);
            var to = span.To is null ? StoryBeats.Keys.Count : StoryAboard.Order(span.To);

            if (from >= 0 && to >= 0)
            {
                yield return (from, to);
            }
        }
    }

    /// <summary>The destination of the span covering the latest of <paramref name="stageBeats"/> that any span covers, or null.</summary>
    public static string? Toward(StorySecret secret, IReadOnlyList<string> stageBeats)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(stageBeats);

        var latest = -1;
        string? toward = null;

        foreach (var span in (secret.Black ?? []).OfType<StoryBlackSpan>())
        {
            var from = span.From is null ? 0 : StoryAboard.Order(span.From);
            var to = span.To is null ? StoryBeats.Keys.Count : StoryAboard.Order(span.To);

            if (from < 0 || to < 0)
            {
                continue;
            }

            foreach (var at in stageBeats.Select(StoryAboard.Order).Where(at => at > latest && from <= at && at < to))
            {
                latest = at;
                toward = span.Toward;
            }
        }

        return toward;
    }
}
