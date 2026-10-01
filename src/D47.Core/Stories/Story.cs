using System.Text.Json.Serialization;
using D47.Core.Persona;

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

    /// <summary>Its last finale chapter is done.</summary>
    Finished,
}

/// <summary>A stretch during which the Commander had the story switched off; <see cref="To"/> is null while it is still off.</summary>
public sealed record StoryOffSpan(DateTimeOffset From, DateTimeOffset? To);

/// <summary>A stock story the Commander picked: a chain of chapters, each an adventure written as they play.</summary>
public sealed record Story
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>The card's public layer as it read when the story was picked.</summary>
    public required string PublicLayer { get; init; }

    /// <summary>The card's length key when the story was picked; a story saved without one is a year.</summary>
    public string Length { get; init; } = StoryPacing.OneYear.Key;

    /// <summary>How the story is paced: by its <see cref="Length"/>, or as a year for a key this build does not know.</summary>
    [JsonIgnore]
    public StoryPacing Pacing => StoryPacing.Find(Length) ?? StoryPacing.OneYear;

    /// <summary>The chapters' adventure keys, oldest first.</summary>
    public IReadOnlyList<string> Chapters { get; init; } = [];

    public StoryState State { get; init; } = StoryState.Running;

    public DateTimeOffset PickedAt { get; init; }

    /// <summary>When the Commander first scanned a Guardian beacon while this story ran.</summary>
    public DateTimeOffset? BeaconScanAt { get; init; }

    /// <summary>The beacon systems the Commander scanned while this story was current, by <c>SystemAddress</c>, in order.</summary>
    public IReadOnlyList<long> BeaconSystems { get; init; } = [];

    /// <summary>When it was abandoned, ended or finished.</summary>
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

    /// <summary>The count of <see cref="Chapters"/> when the last clue was spoken.</summary>
    public int? ClueChapter { get; init; }

    /// <summary>When the ending message was posted, once it has been.</summary>
    public DateTimeOffset? EndingPostedAt { get; init; }

    /// <summary>The id of the ending option the Commander chose, once they have.</summary>
    public string? EndingChoice { get; init; }

    /// <summary>The number of the first finale chapter, once the finale has begun.</summary>
    public int? FinaleFrom { get; init; }

    /// <summary>Which finale chapter, counted from 1, is the current one, or null outside the finale.</summary>
    [JsonIgnore]
    public int? FinaleChapter => FinaleFrom is { } from && Chapters.Count >= from ? Chapters.Count - from + 1 : null;

    /// <summary>The stretches the Commander had the story switched off, oldest first. The last is open while it is off.</summary>
    public IReadOnlyList<StoryOffSpan> OffSpans { get; init; } = [];

    /// <summary>The stretches the game ran without Odyssey, oldest first. The last is open until a LoadGame says it is back.</summary>
    public IReadOnlyList<StoryOffSpan> WithoutOdyssey { get; init; } = [];

    /// <summary>Whether the last LoadGame since the story was picked ran without Odyssey.</summary>
    [JsonIgnore]
    public bool IsWithoutOdyssey => WithoutOdyssey.Count > 0 && WithoutOdyssey[^1].To is null;

    /// <summary>Whether the Commander has the story switched off.</summary>
    [JsonIgnore]
    public bool IsOff => OffSpans.Count > 0 && OffSpans[^1].To is null;

    /// <summary>Running or paused: the Commander's current story.</summary>
    [JsonIgnore]
    public bool IsCurrent => State is StoryState.Running or StoryState.Paused;

    /// <summary>The beacon systems scanned, counting a scan stamped in <see cref="BeaconScanAt"/> before the systems were recorded.</summary>
    [JsonIgnore]
    public int BeaconsScanned => Math.Max(BeaconSystems.Count, BeaconScanAt is null ? 0 : 1);

    /// <summary>
    /// The Guardian cores this story holds back: all of them until its first beacon scan, the Heretic until a
    /// second system's, and none while it is paused, switched off, without Odyssey or stopped.
    /// </summary>
    [JsonIgnore]
    public HeldCores HeldCores => State != StoryState.Running || IsOff || IsWithoutOdyssey
        ? HeldCores.None
        : BeaconsScanned switch
        {
            0 => HeldCores.All,
            1 => HeldCores.Heretic,
            _ => HeldCores.None,
        };

    /// <summary><see cref="HeldCores"/> with this story's title.</summary>
    [JsonIgnore]
    public CoreHold CoreHold => HeldCores == HeldCores.None ? CoreHold.None : new CoreHold(HeldCores, Title);

    [JsonIgnore]
    public string? CurrentChapter => Chapters.Count > 0 ? Chapters[^1] : null;

    /// <summary>Real time since the beacon scan with paused, switched-off and without-Odyssey time taken out, or null before the scan.</summary>
    public TimeSpan? SinceBeacon(DateTimeOffset now)
    {
        if (BeaconScanAt is not { } scan)
        {
            return null;
        }

        var paused = PausedFor + (State == StoryState.Paused ? PausedSince(scan, now) : TimeSpan.Zero) + OffSince(scan, now);
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

    /// <summary>Whether the story was switched off at this moment.</summary>
    public bool WasOffAt(DateTimeOffset at) => OffSpans.Any(span => at >= span.From && (span.To is not { } end || at < end));

    public Story SwitchedOff(DateTimeOffset now) => IsOff ? this : this with { OffSpans = [.. OffSpans, new StoryOffSpan(now, null)] };

    public Story SwitchedOn(DateTimeOffset now) =>
        IsOff ? this with { OffSpans = [.. OffSpans.SkipLast(1), OffSpans[^1] with { To = now }] } : this;

    /// <summary>Records a LoadGame: opens a without-Odyssey stretch when it ran without, and closes one when it ran with.</summary>
    public Story Loaded(bool odyssey, DateTimeOffset at) => (odyssey, IsWithoutOdyssey) switch
    {
        (false, false) => this with { WithoutOdyssey = [.. WithoutOdyssey, new StoryOffSpan(at, null)] },
        (true, true) when at >= WithoutOdyssey[^1].From => this with { WithoutOdyssey = [.. WithoutOdyssey.SkipLast(1), WithoutOdyssey[^1] with { To = at }] },
        _ => this,
    };

    /// <summary>The time the story was switched off or without Odyssey between the beacon scan and <paramref name="now"/>, overlaps counted once.</summary>
    private TimeSpan OffSince(DateTimeOffset scan, DateTimeOffset now)
    {
        var total = TimeSpan.Zero;
        var reached = scan;

        foreach (var span in OffSpans.Concat(WithoutOdyssey).OrderBy(span => span.From))
        {
            var from = span.From > reached ? span.From : reached;
            var to = span.To is { } end && end < now ? end : now;

            if (to > from)
            {
                total += to - from;
                reached = to;
            }
        }

        return total;
    }

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
