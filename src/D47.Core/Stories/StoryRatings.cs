using System.Text.Json;

namespace D47.Core.Stories;

/// <summary>A story's community rating: the mean of its votes and how many there are.</summary>
public sealed record StoryRating(double Average, int Count)
{
    /// <summary>The average rounded to the nearest half star, for display and filtering.</summary>
    public double Stars => Math.Round(Average * 2, MidpointRounding.AwayFromZero) / 2;
}

/// <summary>Every rated story's <see cref="StoryRating"/>, by story id.</summary>
public sealed class StoryRatings
{
    /// <summary>The only <c>format</c> this build reads.</summary>
    public const int Format = 1;

    public static StoryRatings Empty { get; } = new(new Dictionary<string, StoryRating>(StringComparer.OrdinalIgnoreCase));

    private readonly IReadOnlyDictionary<string, StoryRating> _byStory;

    private StoryRatings(IReadOnlyDictionary<string, StoryRating> byStory) => _byStory = byStory;

    public int Count => _byStory.Count;

    /// <summary>The story's rating, or null when it has no votes.</summary>
    public StoryRating? Get(string id) => _byStory.TryGetValue(id, out var rating) ? rating : null;

    /// <summary>A copy with the story's rating replaced.</summary>
    public StoryRatings With(string id, StoryRating rating) =>
        new(new Dictionary<string, StoryRating>(_byStory, StringComparer.OrdinalIgnoreCase) { [id] = rating });

    /// <summary>Reads the body of <c>GET /ratings</c>; null when it is not JSON or its <c>format</c> is not <see cref="Format"/>. A story without a usable rating is left out.</summary>
    public static StoryRatings? Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("format", out var format)
                || format.ValueKind != JsonValueKind.Number
                || !format.TryGetInt32(out var version)
                || version != Format
                || !root.TryGetProperty("stories", out var stories)
                || stories.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var byStory = new Dictionary<string, StoryRating>(StringComparer.OrdinalIgnoreCase);

            foreach (var story in stories.EnumerateObject())
            {
                if (story.Value.ValueKind == JsonValueKind.Object
                    && story.Value.TryGetProperty("average", out var average)
                    && average.ValueKind == JsonValueKind.Number
                    && average.TryGetDouble(out var mean)
                    && mean is >= 1 and <= 5
                    && story.Value.TryGetProperty("count", out var count)
                    && count.ValueKind == JsonValueKind.Number
                    && count.TryGetInt32(out var votes)
                    && votes >= 1)
                {
                    byStory[story.Name] = new StoryRating(mean, votes);
                }
            }

            return new StoryRatings(byStory);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
