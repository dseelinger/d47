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

    /// <summary>Running or paused: the Commander's current story.</summary>
    [JsonIgnore]
    public bool IsCurrent => State is StoryState.Running or StoryState.Paused;

    [JsonIgnore]
    public string? CurrentChapter => Chapters.Count > 0 ? Chapters[^1] : null;
}
