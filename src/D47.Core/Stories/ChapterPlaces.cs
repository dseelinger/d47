using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Stories;

/// <summary>The tags a place may carry, which a chapter's tag set and its activities limit the place draw to.</summary>
public static class ChapterTags
{
    /// <summary>A system with an Anarchy government.</summary>
    public const string Anarchy = "anarchy";

    /// <summary>A system at war or in civil war.</summary>
    public const string War = "war";

    /// <summary>A station with Frontline Solutions in a system at war.</summary>
    public const string Frontline = "frontline";
}

/// <summary>A system a chapter may place a step in, with what the place rules read about it.</summary>
public sealed record ChapterSystem(string Name, long? Address, StarPosition? Position)
{
    public string? Government { get; init; }

    public bool AtWar { get; init; }

    /// <summary>Bodies with a surface to map; null where not gathered.</summary>
    public int? Mappable { get; init; }

    /// <summary>Bodies with a signal or a ring hotspot; null where not gathered.</summary>
    public int? Signals { get; init; }

    /// <summary>Species with biology on record; null where not gathered.</summary>
    public int? Species { get; init; }
}

/// <summary>A Guardian site, tourist beacon or generation ship within reach.</summary>
public sealed record ChapterSite(string Filter, string System, long SystemAddress, StarPosition? Position, string Name);

/// <summary>The base a chapter ends near: a station when <see cref="MarketId"/> is known, otherwise the system.</summary>
public sealed record ChapterBase(string System, long? SystemAddress, string? Station, long? MarketId, StarPosition? Position);

/// <summary>Where a mission held is to be delivered: a station when <see cref="MarketId"/> is known, otherwise the system.</summary>
public sealed record ChapterMissionStop(long MissionId, string System, long? SystemAddress, StarPosition? Position, string? Station, long? MarketId);

/// <summary>Whether places were searched for an area, and what came back.</summary>
public enum ChapterAreaState
{
    /// <summary>No rule asked for the area.</summary>
    NotSearched,
    Listed,
    NoneFound,

    /// <summary>The galaxy search failed, so the rule still holds with nothing listed.</summary>
    Unavailable,
}

/// <summary>The stations and landable bodies of an area a chapter's last stop is held to.</summary>
public sealed record ChapterArea(ChapterAreaState State, IReadOnlyList<StationSummary> Stations, IReadOnlyList<BodySummary> Bodies)
{
    public static readonly ChapterArea NotSearched = new(ChapterAreaState.NotSearched, [], []);
}

/// <summary>Every fact the chapter planner's place rules read, gathered before it runs. Lists keep the order the planner draws in.</summary>
public sealed record ChapterPlaces
{
    public static readonly ChapterPlaces None = new();

    public IReadOnlyList<ChapterSystem> Systems { get; init; } = [];

    /// <summary>Stations, with their pads and services.</summary>
    public IReadOnlyList<StationSummary> Stations { get; init; } = [];

    /// <summary>Landable bodies.</summary>
    public IReadOnlyList<BodySummary> Bodies { get; init; } = [];

    /// <summary>Markets whose docks are closed.</summary>
    public IReadOnlyList<long> ClosedMarkets { get; init; } = [];

    public IReadOnlyList<ChapterSite> Sites { get; init; } = [];

    public IReadOnlyList<NotablePlace> Notable { get; init; } = [];

    /// <summary>The places a sightseeing chapter goes to see one of.</summary>
    public IReadOnlyList<NotablePlace> Sights { get; init; } = [];

    /// <summary>The places inside the chapter's Sol band.</summary>
    public ChapterArea InBand { get; init; } = ChapterArea.NotSearched;

    /// <summary>The places around the waypoint of a travelling chapter, or of a finale chapter on the way to its destination.</summary>
    public ChapterArea Pull { get; init; } = ChapterArea.NotSearched;

    /// <summary>Where the finale's destination is; null outside the finale or where unknown.</summary>
    public StarPosition? DestinationAt { get; init; }

    /// <summary>For finale chapter 1 with no destination, the landable bodies found around points between half the limit and the limit from the Commander.</summary>
    public IReadOnlyList<BodySummary> Destinations { get; init; } = [];

    public ChapterBase? Base { get; init; }

    public IReadOnlyList<ChapterMissionStop> Missions { get; init; } = [];

    /// <summary>Systems the Commander has visited, by name.</summary>
    public IReadOnlyList<string> Visited { get; init; } = [];

    /// <summary>Systems earlier chapters of the story placed a step in, by name.</summary>
    public IReadOnlyList<string> Used { get; init; } = [];

    /// <summary>The facts gathered for <paramref name="name"/>, or null.</summary>
    public ChapterSystem? System(string? name) =>
        name is null ? null : Systems.FirstOrDefault(system => string.Equals(system.Name, name, StringComparison.OrdinalIgnoreCase));

    public bool WasVisited(string? name) => name is not null && Visited.Contains(name, StringComparer.OrdinalIgnoreCase);

    public bool WasUsed(string? name) => name is not null && Used.Contains(name, StringComparer.OrdinalIgnoreCase);
}
