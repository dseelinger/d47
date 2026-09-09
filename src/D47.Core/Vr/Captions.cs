using System.Text.Json;
using System.Text.Json.Serialization;

namespace D47.Core.Vr;

/// <summary>How big the caption text is drawn.</summary>
public enum CaptionSize
{
    Small,
    Medium,
    Large,
}

/// <summary>What Configure the captions configures (Phase 9).</summary>
public sealed record CaptionSettings
{
    /// <inheritdoc cref="Configuration.D47Settings.Extra"/>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }

    public bool Enabled { get; init; } = true;

    public CaptionSize Size { get; init; } = CaptionSize.Medium;

    /// <summary>Whether the band rides the view or sits in the cockpit (#204).</summary>
    public string Lock { get; init; } = "head";

    /// <summary><see cref="Lock"/> read as the lock it names.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public SurfaceLock Locking => string.Equals(Lock, "world", StringComparison.OrdinalIgnoreCase)
        ? SurfaceLock.WorldLocked
        : SurfaceLock.HeadLocked;

    /// <summary>How opaque the box behind the text is.</summary>
    public double BackgroundOpacity { get; init; } = 0.78;

    /// <summary>
    /// Reading speed in characters per second, which is what decides how long a caption stays up after
    /// the speech ends.
    /// </summary>
    public double CharactersPerSecond { get; init; } = Caption.AdultReadingSpeed;

    public CaptionSettings Sane() => this with
    {
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, Caption.MinimumBackgroundOpacity, 1.0),
        CharactersPerSecond = Math.Clamp(CharactersPerSecond, 8, 30),
    };
}

/// <summary>
/// The caption standard's numbers, in one place, so that "follows the CC standard" is a thing the code
/// says rather than a thing the documentation claims.
/// </summary>
public static class Caption
{
    /// <summary>Maximum characters on one line.</summary>
    public const int CharactersPerLine = 42;

    /// <summary>Maximum lines one caption event may occupy.</summary>
    public const int LinesPerEvent = 2;

    /// <summary>How many lines the layer holds at once.</summary>
    public const int WindowLines = 2;

    /// <summary>How see-through the box behind the text may be made (#201).</summary>
    public const double MinimumBackgroundOpacity = 0.6;

    public const double AdultReadingSpeed = 20.0;

    public const double ChildrensReadingSpeed = 17.0;

    /// <summary>Nothing stays up for less than five sixths of a second.</summary>
    public static readonly TimeSpan MinimumDwell = TimeSpan.FromSeconds(5.0 / 6.0);

    /// <summary>And nothing for more than seven.</summary>
    public static readonly TimeSpan MaximumDwell = TimeSpan.FromSeconds(7);

    /// <summary>
    /// Words that a line should break before rather than after, so a line ending does not separate a
    /// phrase from the thing it attaches to.
    /// </summary>
    private static readonly HashSet<string> BreakBefore = new(StringComparer.OrdinalIgnoreCase)
    {
        // Conjunctions.
        "and", "but", "or", "nor", "so", "yet", "because", "although", "though", "while",
        "unless", "until", "whereas", "if", "when",

        // Prepositions.
        "at", "by", "for", "from", "in", "into", "of", "on", "onto", "over", "to", "toward",
        "towards", "under", "with", "within", "without", "after", "before", "through",
    };

    /// <summary>How long a caption stays up once the speech has stopped.</summary>
    public static TimeSpan DwellFor(string text, double charactersPerSecond)
    {
        if (charactersPerSecond <= 0)
        {
            return MaximumDwell;
        }

        var seconds = text.Length / charactersPerSecond;

        return TimeSpan.FromSeconds(Math.Clamp(
            seconds,
            MinimumDwell.TotalSeconds,
            MaximumDwell.TotalSeconds));
    }

    /// <summary>Wraps one utterance to the standard's line length.</summary>
    public static IReadOnlyList<string> Wrap(string text, int charactersPerLine = CharactersPerLine)
    {
        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        if (collapsed.Length == 0)
        {
            return [];
        }

        if (collapsed.Length <= charactersPerLine)
        {
            return [collapsed];
        }

        var words = collapsed.Split(' ');
        var lines = new List<string>();
        var line = new List<string>();

        foreach (var word in words)
        {
            var wouldBe = line.Count == 0 ? word.Length : Length(line) + 1 + word.Length;

            if (wouldBe > charactersPerLine && line.Count > 0)
            {
                lines.Add(string.Join(' ', line));
                line.Clear();
            }

            line.Add(word);
        }

        if (line.Count > 0)
        {
            lines.Add(string.Join(' ', line));
        }

        return Balance(lines, charactersPerLine);
    }

    private static int Length(List<string> words) => words.Sum(word => word.Length) + words.Count - 1;

    /// <summary>Moves the break to a better word and leaves the last line the longer one.</summary>
    private static IReadOnlyList<string> Balance(List<string> lines, int charactersPerLine)
    {
        if (lines.Count != 2)
        {
            return lines;
        }

        var words = $"{lines[0]} {lines[1]}".Split(' ');
        var best = lines;
        var bestScore = int.MinValue;

        for (var split = 1; split < words.Length; split++)
        {
            var top = string.Join(' ', words[..split]);
            var bottom = string.Join(' ', words[split..]);

            if (top.Length > charactersPerLine || bottom.Length > charactersPerLine)
            {
                continue;
            }

            var score = 0;

            // After punctuation is the best place to break.
            if (top.Length > 0 && ".,;:!?".Contains(top[^1], StringComparison.Ordinal))
            {
                score += 6;
            }

            // Before a conjunction or a preposition is the next best.
            if (BreakBefore.Contains(words[split]))
            {
                score += 4;
            }

            // Bottom-heavy, and never two words alone on top.
            if (bottom.Length >= top.Length)
            {
                score += 2;
            }

            if (split <= 2)
            {
                score -= 5;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = [top, bottom];
            }
        }

        return best;
    }
}
