using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Adventures;

/// <summary>Where a travelling chapter is going: a span's destination, the way home, or a place the Commander asked for.</summary>
/// <param name="Target">The system's name.</param>
/// <param name="There">The target's position.</param>
/// <param name="Home">Whether the leg is the way home to Sol or Colonia.</param>
/// <param name="Arrived">Whether the chapter starts within <see cref="Within"/> light years of the target.</param>
public sealed record ChapterLeg(string Target, StarPosition There, bool Home, bool Arrived)
{
    /// <summary>Light years from the target within which a last stop needs no more progress.</summary>
    public double Within { get; init; } = Home ? HomeLeg.Home : StoryDestinations.Arrived;

    /// <summary>Whether the leg is a changed objective's, toward a place the Commander asked for.</summary>
    public bool Asked { get; init; }

    /// <summary>Whether the leg is a place the Commander asked for in an earlier chapter, still not reached.</summary>
    public bool Chosen { get; init; }
}

/// <summary>The one rule for a chapter that travels toward a target: how far it must go and where its places are searched.</summary>
public static class ChapterPull
{
    /// <summary>The share of the reach a last stop must be nearer the target than the start.</summary>
    public const double Share = 0.75;

    /// <summary>How far along the line from the start to the target, as a share of the reach, places are searched for.</summary>
    public const double WaypointAt = 0.85;

    /// <summary>The share of the reach around the waypoint within which places are searched for.</summary>
    public const double PlacesWithin = 0.1;

    /// <summary>The most stations, and the most landable bodies, searched for around the waypoint.</summary>
    public const int PlacesPerKind = 20;

    /// <summary>Whether a chapter starting at <paramref name="start"/> in a span that names <paramref name="toward"/> is pulled toward it.</summary>
    public static bool Pulls(string? toward, StarPosition? start) =>
        toward is not null && start is { } at && StoryDestinations.PositionOf(toward) is { } there && at.DistanceTo(there) > StoryDestinations.Arrived;

    /// <summary>The leg <paramref name="ask"/> travels from <paramref name="start"/>, or null where it travels none.</summary>
    public static ChapterLeg? Of(AdventureAsk ask, StarPosition? start)
    {
        ArgumentNullException.ThrowIfNull(ask);

        if (ask.Rewrite?.Heading is { } heading)
        {
            return start is { } from ? Toward(heading, from) : null;
        }

        if (ask is not { Story: { Beacon: null, FinaleChapter: null } story, Rewrite: null, Length: not AdventureLength.Short } || start is not { } at)
        {
            return null;
        }

        if (story.Chosen is { } chosen)
        {
            return Toward(chosen, at) with { Asked = false, Chosen = true };
        }

        if (HomeLeg.Of(ask) is { } home && HomeLeg.PositionOf(home) is { } homeAt)
        {
            return new ChapterLeg(home, homeAt, true, at.DistanceTo(homeAt) <= HomeLeg.Home);
        }

        return story is { InTheBlack: true, Toward: { } toward } && StoryDestinations.PositionOf(toward) is { } there
            ? new ChapterLeg(toward, there, false, !Pulls(toward, at))
            : null;
    }

    /// <summary>The leg from <paramref name="start"/> toward a changed objective's heading: arrived within 1,000 light years of Sol or Colonia on the way home, otherwise only in the system itself.</summary>
    public static ChapterLeg Toward(AdventureHeading heading, StarPosition start)
    {
        ArgumentNullException.ThrowIfNull(heading);

        var within = heading.Home ? HomeLeg.Home : 0;

        return new ChapterLeg(heading.System, heading.Position, heading.Home, start.DistanceTo(heading.Position) <= within) { Within = within, Asked = true };
    }

    /// <summary>Whether a changed objective in <paramref name="story"/> may travel toward a place the Commander names: not in a beacon or finale chapter, nor in a span in the black with its own destination.</summary>
    public static bool MayHead(AdventureStory? story) =>
        story is null or { Beacon: null, FinaleChapter: null } and not { InTheBlack: true, Toward: not null };

    /// <summary>The point <see cref="WaypointAt"/> of <paramref name="reach"/> from <paramref name="start"/> along the line to <paramref name="target"/>, or the target when that is nearer.</summary>
    public static StarPosition Waypoint(StarPosition start, StarPosition target, double reach)
    {
        var distance = start.DistanceTo(target);
        var along = WaypointAt * reach;

        if (distance <= along)
        {
            return target;
        }

        var scale = along / distance;

        return new StarPosition(start.X + ((target.X - start.X) * scale), start.Y + ((target.Y - start.Y) * scale), start.Z + ((target.Z - start.Z) * scale));
    }

    /// <summary>Whether a last stop <paramref name="end"/> light years from the target meets the rule for a chapter starting <paramref name="start"/> light years from it.</summary>
    public static bool Meets(double start, double end, double reach, double within) => end <= within || end <= start - (Share * reach);
}
