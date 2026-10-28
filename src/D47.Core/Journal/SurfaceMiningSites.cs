using System.Collections.Immutable;

namespace D47.Core.Journal;

/// <summary>Refining in an SRV on one body, from the first refine to leaving the SRV.</summary>
/// <param name="At">
/// Where a <c>Touchdown</c> or a <c>Location</c> placed the Commander on this body since arriving at it, with
/// radius 0; null when neither did.
/// </param>
public sealed record SurfaceMiningRun(DateTimeOffset OpenedAt, SurfaceFix? At)
{
    public ImmutableDictionary<string, RefinedMaterial> Refined { get; init; } = ImmutableDictionary<string, RefinedMaterial>.Empty;

    /// <summary>When the Commander left the SRV or the game shut down, or null while the run is open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    public int TonnesRefined => Refined.Values.Sum(material => material.Tonnes);

    internal SurfaceMiningRun Refine(string symbol, string name)
    {
        var tonnes = Refined.TryGetValue(symbol, out var so) ? so.Tonnes : 0;

        return this with { Refined = Refined.SetItem(symbol, new RefinedMaterial(symbol, name, tonnes + 1)) };
    }
}

/// <summary>A body the Commander has refined on from an SRV, and its runs, oldest first.</summary>
public sealed record SurfaceMiningSite(
    long SystemAddress,
    int BodyId,
    string? StarSystem,
    string? Body,
    IReadOnlyList<SurfaceMiningRun> Runs);

/// <summary>Every body the journals show the Commander refining on from an SRV.</summary>
public sealed record SurfaceMiningSites
{
    public static readonly SurfaceMiningSites None = new();

    private readonly record struct Place((long SystemAddress, int BodyId) Key, string? StarSystem, string? Body, SurfaceFix? Fix);

    /// <summary>The sites, by when each was first mined.</summary>
    public IReadOnlyList<SurfaceMiningSite> Sites => [.. Held.Values.OrderBy(site => site.Runs[0].OpenedAt)];

    /// <summary>True once the walk over earlier journals has been merged in.</summary>
    public bool IsWhole { get; init; }

    public bool IsKnown => !Held.IsEmpty;

    private ImmutableDictionary<(long SystemAddress, int BodyId), SurfaceMiningSite> Held { get; init; } =
        ImmutableDictionary<(long SystemAddress, int BodyId), SurfaceMiningSite>.Empty;

    private bool Folded { get; init; }

    private bool InSrv { get; init; }

    private Place? Here { get; init; }

    /// <summary>The site whose last run is open.</summary>
    private (long SystemAddress, int BodyId)? Open { get; init; }

    /// <summary>The site on this body, or null when the Commander has not refined there.</summary>
    public SurfaceMiningSite? At(long systemAddress, int bodyId) => Held.GetValueOrDefault((systemAddress, bodyId));

    internal bool SameSites(SurfaceMiningSites other) => ReferenceEquals(Held, other.Held);

    public SurfaceMiningSites Apply(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        if (journalEvent.Kind == "NewCommander")
        {
            return None with { IsWhole = IsWhole };
        }

        var state = this with { Folded = true, InSrv = journalEvent.InSrvAfter ?? InSrv };

        if (!state.InSrv || journalEvent.Kind == "Shutdown")
        {
            state = state.Closed(journalEvent.Timestamp);
        }

        return journalEvent.Kind switch
        {
            "ApproachBody" => state.Arrived(journalEvent, null),
            "Touchdown" => state.Arrived(journalEvent, journalEvent.Bool("Taxi") || journalEvent.Bool("Multicrew") ? null : FixOf(journalEvent)),
            "Location" when journalEvent.Int("BodyID") is not null => state.Arrived(journalEvent, FixOf(journalEvent)),
            "LeaveBody" or "FSDJump" or "CarrierJump" => state with { Here = null },
            "MiningRefined" when state.InSrv => state.Refined(journalEvent),
            _ => state,
        };
    }

    /// <summary>
    /// This, recovered from older journals, merged with what the live journal has folded, marked whole. A run in
    /// both is counted once, as the live journal has it.
    /// </summary>
    public SurfaceMiningSites With(SurfaceMiningSites live)
    {
        ArgumentNullException.ThrowIfNull(live);

        var sites = Held;

        foreach (var (key, site) in live.Held)
        {
            if (!sites.TryGetValue(key, out var held))
            {
                sites = sites.SetItem(key, site);
                continue;
            }

            var opened = site.Runs.Select(run => run.OpenedAt).ToHashSet();

            sites = sites.SetItem(key, site with
            {
                StarSystem = site.StarSystem ?? held.StarSystem,
                Body = site.Body ?? held.Body,
                Runs = [.. held.Runs.Where(run => !opened.Contains(run.OpenedAt)).Concat(site.Runs).OrderBy(run => run.OpenedAt)],
            });
        }

        var tracking = live.Folded ? live : this;

        return tracking with { Held = sites, IsWhole = true };
    }

    private SurfaceMiningSites Arrived(JournalEvent journalEvent, SurfaceFix? fix)
    {
        if (journalEvent.Long("SystemAddress") is not { } address || journalEvent.Int("BodyID") is not { } bodyId)
        {
            return this;
        }

        var key = (address, bodyId);
        var kept = Here is { } here && here.Key == key ? here.Fix : null;

        return this with { Here = new Place(key, journalEvent.String("StarSystem"), journalEvent.String("Body"), fix ?? kept) };
    }

    private SurfaceMiningSites Refined(JournalEvent journalEvent)
    {
        if (Here is not { } here || JournalJson.Symbol(journalEvent.String("Type")) is not { } symbol)
        {
            return this;
        }

        var name = JournalJson.Spoken(journalEvent.Named("Type")) ?? symbol;
        var state = Open is { } open && open != here.Key ? Closed(journalEvent.Timestamp) : this;
        var site = state.Held.TryGetValue(here.Key, out var held)
            ? held with { StarSystem = here.StarSystem ?? held.StarSystem, Body = here.Body ?? held.Body }
            : new SurfaceMiningSite(here.Key.SystemAddress, here.Key.BodyId, here.StarSystem, here.Body, []);

        IReadOnlyList<SurfaceMiningRun> runs = state.Open is null
            ? [.. site.Runs, new SurfaceMiningRun(journalEvent.Timestamp, here.Fix).Refine(symbol, name)]
            : [.. site.Runs.SkipLast(1), site.Runs[^1].Refine(symbol, name)];

        return state with { Held = state.Held.SetItem(here.Key, site with { Runs = runs }), Open = here.Key };
    }

    private SurfaceMiningSites Closed(DateTimeOffset at)
    {
        if (Open is not { } open)
        {
            return this;
        }

        var site = Held[open];

        return this with
        {
            Held = Held.SetItem(open, site with { Runs = [.. site.Runs.SkipLast(1), site.Runs[^1] with { ClosedAt = at }] }),
            Open = null,
        };
    }

    private static SurfaceFix? FixOf(JournalEvent journalEvent) =>
        journalEvent.Double("Latitude") is { } latitude && journalEvent.Double("Longitude") is { } longitude
            ? new SurfaceFix(latitude, longitude, 0)
            : null;
}
