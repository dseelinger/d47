using D47.Core.Adventures;
using D47.Core.Checklists;
using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

public enum DrawnSpanKind
{
    Exploration,
    Sight,
    Engineers,
}

public enum DrawnSpanPhase
{
    Out,
    There,
    Back,
}

/// <summary>
/// A span in the black opened by the draw rather than the story's author. <see cref="Within"/> is how near
/// <see cref="TargetAt"/> counts as arrival, <see cref="ThereLeft"/> the chapters still to play at the target, and
/// <see cref="Opened"/> the number of the chapter that opened it.
/// </summary>
public sealed record DrawnSpan(
    DrawnSpanKind Kind, string? Target, long? TargetAddress, StarPosition TargetAt, double Within,
    StarPosition StartAt, DrawnSpanPhase Phase, int ThereLeft, int Opened);

/// <summary>What must hold for a chapter to open a drawn span; every default lets it open.</summary>
public sealed record DrawnSpanGates
{
    public bool NewChapter { get; init; } = true;

    public bool BeaconScanned { get; init; } = true;

    public bool Finale { get; init; }

    public bool AuthoredSpan { get; init; }

    public bool SpanOpen { get; init; }

    /// <summary>Whether a home target or a chosen heading is set.</summary>
    public bool Heading { get; init; }

    public double ExplorationWeight { get; init; } = CareerWeights.Default;

    public SpanReach? Reach { get; init; }

    public StarPosition? At { get; init; }

    /// <summary>Whether every gate holds: a new, non-finale chapter after the scan, with no span or heading, Exploration above 0, a reach and a position.</summary>
    public bool Hold => NewChapter && BeaconScanned && !Finale && !AuthoredSpan && !SpanOpen && !Heading && ExplorationWeight > 0 && Reach is not null && At is not null;
}

/// <summary>The seeded rules that open, shape and close a <see cref="DrawnSpan"/>.</summary>
public static class DrawnSpans
{
    /// <summary>The chance at an even Exploration weight.</summary>
    public const double Share = 0.25;

    public const double MostChance = 0.5;

    /// <summary>The share of the reach each chapter out covers.</summary>
    public const double OutShare = 0.75;

    /// <summary>The share of the reach within which the Commander has arrived.</summary>
    public const double ArrivalShare = 0.25;

    /// <summary>The most the heading is turned either way, in degrees.</summary>
    public const double MostTurn = 60;

    /// <summary>The chance a chapter opens a span: scaled by the Exploration weight, capped, and reduced when the previous chapter closed one.</summary>
    public static double Chance(double explorationWeight, bool previousClosed)
    {
        var chance = Math.Min(MostChance, Share * Math.Max(0, explorationWeight) / CareerWeights.Default);

        return previousClosed ? chance * ChapterDraw.RecentFactor : chance;
    }

    /// <summary>Whether a drawn activity is an Exploration or Exobiology row.</summary>
    public static bool Explores(IReadOnlyList<DrawnActivity>? slots) =>
        slots?.Any(slot => ChapterActivities.Find(slot.Kind, slot.Family, slot.Organic) is { Career: Career.Exploration or Career.Exobiology }) == true;

    /// <summary>Whether a row may take the first slot of a chapter after a span's first.</summary>
    public static bool FirstSlot(DrawableActivity row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return row.Career is Career.Exploration or Career.Exobiology;
    }

    /// <summary>Chapters out, from a roll in [0, 1).</summary>
    public static int Out(StoryPacing pacing, double roll)
    {
        ArgumentNullException.ThrowIfNull(pacing);

        return Band(pacing) switch
        {
            0 => 1,
            1 => roll < 0.7 ? 1 : 2,
            _ => roll < 0.5 ? 1 : roll < 0.85 ? 2 : 3,
        };
    }

    /// <summary>Chapters at the target, from a roll in [0, 1).</summary>
    public static int There(StoryPacing pacing, double roll)
    {
        ArgumentNullException.ThrowIfNull(pacing);

        return Band(pacing) == 0 ? 0 : roll < 0.5 ? 0 : roll < 0.85 ? 1 : 2;
    }

    /// <summary>Light years from a sight's position within which a sight span's Commander is in its system, allowing for rounded coordinates.</summary>
    public const double SightArrival = 0.1;

    /// <summary>The most chapters out a span of this length takes.</summary>
    public static int MostOut(StoryPacing pacing)
    {
        ArgumentNullException.ThrowIfNull(pacing);

        return Band(pacing) + 1;
    }

    /// <summary>How far a sight may lie for a span of this length to reach it: the most chapters out times the share of the reach each covers.</summary>
    public static double SightRadius(StoryPacing pacing, double reach) => MostOut(pacing) * OutShare * reach;

    /// <summary>Chapters out to cover <paramref name="distance"/> at <see cref="OutShare"/> of <paramref name="reach"/> each, rounded up.</summary>
    public static int ChaptersOut(double distance, double reach) => Math.Max(1, (int)Math.Ceiling(distance / (OutShare * reach)));

    /// <summary>
    /// The sight span chapter <paramref name="chapter"/> opens toward <paramref name="sight"/> from <paramref name="at"/>, arriving in the sight's system,
    /// with the chapters at the target from the seeded roll for the story's length.
    /// </summary>
    public static DrawnSpan OpenSight(string storyId, int chapter, StoryPacing pacing, NotablePlace sight, StarPosition at)
    {
        ArgumentNullException.ThrowIfNull(storyId);
        ArgumentNullException.ThrowIfNull(sight);

        var there = There(pacing, new Random32(ChapterDraw.BlackSeed(storyId, chapter)).NextDouble());

        return new DrawnSpan(DrawnSpanKind.Sight, sight.System, sight.SystemAddress, sight.Position, SightArrival, at, DrawnSpanPhase.Out, there, chapter);
    }

    /// <summary>A sight span turned toward <paramref name="sight"/>, as when the Commander changes the sight objective; any other span unchanged.</summary>
    public static DrawnSpan MoveSight(DrawnSpan span, NotablePlace sight)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(sight);

        return span.Kind == DrawnSpanKind.Sight ? span with { Target = sight.System, TargetAddress = sight.SystemAddress, TargetAt = sight.Position } : span;
    }

    /// <summary>
    /// The point <paramref name="distance"/> light years from <paramref name="at"/> at its height, heading away from the nearer of
    /// Sol and Colonia turned by <paramref name="turn"/> degrees in the galactic plane. Where that heading is undefined, within
    /// 1 light year of the nearer in the plane, the heading is <paramref name="turn"/> from the +Z axis.
    /// </summary>
    public static StarPosition Target(StarPosition at, double distance, double turn)
    {
        var nearer = at.DistanceTo(StarPosition.Origin) <= at.DistanceTo(ExpeditionKit.Colonia) ? StarPosition.Origin : ExpeditionKit.Colonia;
        var dx = at.X - nearer.X;
        var dz = at.Z - nearer.Z;
        var away = Math.Sqrt((dx * dx) + (dz * dz)) < 1 ? 0 : Math.Atan2(dx, dz);
        var heading = away + (turn * Math.PI / 180);

        return new StarPosition(at.X + (distance * Math.Sin(heading)), at.Y, at.Z + (distance * Math.Cos(heading)));
    }

    /// <summary>
    /// The span chapter <paramref name="chapter"/> opens when its roll, seeded by <see cref="ChapterDraw.BlackSeed"/>, falls under
    /// <see cref="Chance"/>; null when it does not or the gates do not hold.
    /// </summary>
    public static DrawnSpan? Open(string storyId, int chapter, StoryPacing pacing, DrawnSpanGates gates, bool previousClosed)
    {
        ArgumentNullException.ThrowIfNull(gates);

        if (!gates.Hold || gates is not { Reach: { } reach, At: { } at })
        {
            return null;
        }

        var random = new Random32(ChapterDraw.BlackSeed(storyId, chapter));

        if (random.NextDouble() >= Chance(gates.ExplorationWeight, previousClosed))
        {
            return null;
        }

        var outward = Out(pacing, random.NextDouble());
        var there = There(pacing, random.NextDouble());
        var turn = (random.NextDouble() * 2 * MostTurn) - MostTurn;

        return new DrawnSpan(
            DrawnSpanKind.Exploration, null, null, Target(at, outward * OutShare * reach.LightYears, turn), ArrivalShare * reach.LightYears,
            at, DrawnSpanPhase.Out, there, chapter);
    }

    /// <summary>
    /// The span's state when the next chapter is written, settled from <paramref name="at"/>; null once it closes. Out or There turns back after
    /// two changed chapters in a row, or for an Exploration span at an Exploration weight of 0. Out reaches the target within
    /// <see cref="DrawnSpan.Within"/>, There counts down a chapter at a time, and Back closes within <see cref="HomeLeg.Home"/> of
    /// the start, in the same call that turns it back.
    /// </summary>
    public static DrawnSpan? Next(DrawnSpan span, StarPosition? at, IReadOnlyList<string> chapters, IReadOnlyList<string> changed, double explorationWeight)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(chapters);
        ArgumentNullException.ThrowIfNull(changed);

        var turnedDown = chapters.Count >= HomeLeg.ChangedInARow && chapters.TakeLast(HomeLeg.ChangedInARow).All(changed.Contains);

        if (span.Phase != DrawnSpanPhase.Back && (turnedDown || (span.Kind == DrawnSpanKind.Exploration && explorationWeight <= 0)))
        {
            span = span with { Phase = DrawnSpanPhase.Back };
        }
        else if (span.Phase == DrawnSpanPhase.Out && at is { } here && here.DistanceTo(span.TargetAt) <= span.Within)
        {
            span = span with { Phase = span.ThereLeft > 0 ? DrawnSpanPhase.There : DrawnSpanPhase.Back };
        }
        else if (span.Phase == DrawnSpanPhase.There)
        {
            var left = span.ThereLeft - 1;

            span = span with { ThereLeft = Math.Max(0, left), Phase = left > 0 ? DrawnSpanPhase.There : DrawnSpanPhase.Back };
        }

        return span.Phase == DrawnSpanPhase.Back && at is { } now && now.DistanceTo(span.StartAt) <= HomeLeg.Home ? null : span;
    }

    /// <summary>The span turned back because no place lies on the way out; null when <paramref name="at"/> is already within <see cref="HomeLeg.Home"/> of where it opened.</summary>
    public static DrawnSpan? NothingOut(DrawnSpan span, StarPosition? at)
    {
        ArgumentNullException.ThrowIfNull(span);

        return at is { } now && now.DistanceTo(span.StartAt) <= HomeLeg.Home ? null : span with { Phase = DrawnSpanPhase.Back };
    }

    private static int Band(StoryPacing pacing) =>
        new[] { StoryPacing.ThreeDays, StoryPacing.OneWeek, StoryPacing.TwoWeeks }.Any(brief => brief.Key == pacing.Key) ? 0
        : new[] { StoryPacing.OneMonth, StoryPacing.ThreeMonths }.Any(middle => middle.Key == pacing.Key) ? 1
        : 2;
}
