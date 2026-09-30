using System.Text.Json.Serialization;

namespace D47.Core.Stories;

/// <summary>Where a picked story stands.</summary>
public enum StoryState
{
    /// <summary>A chapter is under way or being written.</summary>
    Running,

    /// <summary>The current chapter was abandoned on the Adventures page; Resume begins it again.</summary>
    Paused,

    /// <summary>Stopped by Switch, for another story.</summary>
    Abandoned,

    /// <summary>Stopped by Abandon.</summary>
    Ended,
}

/// <summary>A stock story the Commander picked: a chain of chapters, each an adventure written as they play.</summary>
public sealed record Story
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>The card's public layer as it read when the story was picked.</summary>
    public required string PublicLayer { get; init; }

    /// <summary>The chapters' adventure keys, oldest first.</summary>
    public IReadOnlyList<string> Chapters { get; init; } = [];

    public StoryState State { get; init; } = StoryState.Running;

    public DateTimeOffset PickedAt { get; init; }

    /// <summary>When the Commander first scanned a Guardian beacon while this story ran.</summary>
    public DateTimeOffset? BeaconScanAt { get; init; }

    /// <summary>When it was abandoned or ended.</summary>
    public DateTimeOffset? StoppedAt { get; init; }

    /// <summary>When the current pause began, while <see cref="State"/> is paused.</summary>
    public DateTimeOffset? PausedAt { get; init; }

    /// <summary>Time spent paused since <see cref="BeaconScanAt"/>, not counting the current pause.</summary>
    public TimeSpan PausedFor { get; init; }

    /// <summary>Play sessions, counted by <c>LoadGame</c>, since the story was picked.</summary>
    public int Sessions { get; init; }

    /// <summary>The timestamp of the last <c>LoadGame</c> counted in <see cref="Sessions"/>, so a replayed one is not counted again.</summary>
    public DateTimeOffset? LastSessionAt { get; init; }

    /// <summary>How many of the hidden layer's clues have been spoken.</summary>
    public int CluesGiven { get; init; }

    /// <summary>The value of <see cref="Sessions"/> when the last clue was spoken.</summary>
    public int? ClueSession { get; init; }

    /// <summary>Running or paused: the Commander's current story.</summary>
    [JsonIgnore]
    public bool IsCurrent => State is StoryState.Running or StoryState.Paused;

    [JsonIgnore]
    public string? CurrentChapter => Chapters.Count > 0 ? Chapters[^1] : null;

    /// <summary>Real time since the beacon scan with paused time taken out, or null before the scan.</summary>
    public TimeSpan? SinceBeacon(DateTimeOffset now)
    {
        if (BeaconScanAt is not { } scan)
        {
            return null;
        }

        var paused = PausedFor + (State == StoryState.Paused ? PausedSince(scan, now) : TimeSpan.Zero);
        var since = now - scan - paused;

        return since > TimeSpan.Zero ? since : TimeSpan.Zero;
    }

    public Story Paused(DateTimeOffset now) => this with { State = StoryState.Paused, PausedAt = now };

    public Story Resumed(DateTimeOffset now) => this with
    {
        State = StoryState.Running,
        PausedAt = null,
        PausedFor = PausedFor + (BeaconScanAt is { } scan ? PausedSince(scan, now) : TimeSpan.Zero),
    };

    /// <summary>The part of the current pause that falls after the beacon scan.</summary>
    private TimeSpan PausedSince(DateTimeOffset scan, DateTimeOffset now)
    {
        if (PausedAt is not { } since)
        {
            return TimeSpan.Zero;
        }

        var from = since > scan ? since : scan;

        return now > from ? now - from : TimeSpan.Zero;
    }
}
