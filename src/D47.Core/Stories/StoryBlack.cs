namespace D47.Core.Stories;

/// <summary>Which chapters of a story take place in the black, by the spans its hidden entry marks.</summary>
public static class StoryBlack
{
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
}
