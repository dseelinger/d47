using D47.Core.Journal;
using D47.Core.Stories;

namespace D47.Core.Adventures;

/// <summary>Where a travelling chapter is going: a span's destination or the way home.</summary>
/// <param name="Target">The system's name.</param>
/// <param name="There">The target's position.</param>
/// <param name="Home">Whether the leg is the way home rather than a span's destination.</param>
/// <param name="Arrived">Whether the chapter starts within <see cref="Within"/> light years of the target.</param>
public sealed record ChapterLeg(string Target, StarPosition There, bool Home, bool Arrived)
{
    /// <summary>Light years from the target within which a last stop needs no more progress.</summary>
    public double Within => Home ? HomeLeg.Home : StoryDestinations.Arrived;
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

        if (ask is not { Story: { Beacon: null, FinaleChapter: null } story, Rewrite: null, Length: not AdventureLength.Short } || start is not { } at)
        {
            return null;
        }

        if (HomeLeg.Of(ask) is { } home && HomeLeg.PositionOf(home) is { } homeAt)
        {
            return new ChapterLeg(home, homeAt, true, at.DistanceTo(homeAt) <= HomeLeg.Home);
        }

        return story is { InTheBlack: true, Toward: { } toward } && StoryDestinations.PositionOf(toward) is { } there
            ? new ChapterLeg(toward, there, false, !Pulls(toward, at))
            : null;
    }

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
