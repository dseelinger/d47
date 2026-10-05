using System.Collections.Frozen;

namespace D47.Core.Journal;

/// <summary>Tonnes of one material refined in a run.</summary>
public sealed record RefinedMaterial(string Symbol, string Name, int Tonnes);

/// <summary>One stretch of mining, from its first mining event to the docking or death that ended it.</summary>
public sealed record MiningRun(DateTimeOffset OpenedAt)
{
    public IReadOnlyDictionary<string, RefinedMaterial> Refined { get; init; } = FrozenDictionary<string, RefinedMaterial>.Empty;

    public int ProspectorsLaunched { get; init; }

    public int CollectorsLaunched { get; init; }

    public int RocksProspected { get; init; }

    /// <summary>Rocks prospected that held a core.</summary>
    public int CoresFound { get; init; }

    public DateTimeOffset? LastRefinedAt { get; init; }

    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary><c>Docked</c> or <c>Died</c> once the run is closed.</summary>
    public string? ClosedBy { get; init; }

    public int TonnesRefined => Refined.Values.Sum(material => material.Tonnes);

    internal MiningRun Count(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "LaunchDrone":
                return journalEvent.String("Type") switch
                {
                    "Prospector" => this with { ProspectorsLaunched = ProspectorsLaunched + 1 },
                    "Collection" => this with { CollectorsLaunched = CollectorsLaunched + 1 },
                    _ => this,
                };

            case "ProspectedAsteroid":
                return this with
                {
                    RocksProspected = RocksProspected + 1,
                    CoresFound = CoresFound + (journalEvent.String("MotherlodeMaterial") is { Length: > 0 } ? 1 : 0),
                };

            case "MiningRefined" when JournalJson.Symbol(journalEvent.String("Type")) is { } symbol:
                var name = JournalJson.Spoken(journalEvent.Named("Type")) ?? symbol;
                var tonnes = Refined.TryGetValue(symbol, out var so) ? so.Tonnes : 0;
                var refined = Refined.ToDictionary(pair => pair.Key, pair => pair.Value);
                refined[symbol] = new RefinedMaterial(symbol, name, tonnes + 1);
                return this with { Refined = refined.ToFrozenDictionary(), LastRefinedAt = journalEvent.Timestamp };

            default:
                return this;
        }
    }
}

/// <summary>The run in progress and the last one to finish.</summary>
public sealed record MiningRuns
{
    public static readonly MiningRuns None = new();

    public MiningRun? Open { get; init; }

    public MiningRun? Last { get; init; }

    public MiningRuns Apply(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Docked" or "Died":
                return Open is null
                    ? this
                    : new MiningRuns { Last = Open with { ClosedAt = journalEvent.Timestamp, ClosedBy = journalEvent.Kind }, Open = null };

            case "ProspectedAsteroid" or "MiningRefined":
                return With((Open ?? new MiningRun(journalEvent.Timestamp)).Count(journalEvent));

            case "LaunchDrone" when journalEvent.String("Type") is "Prospector" or "Collection":
                return With((Open ?? new MiningRun(journalEvent.Timestamp)).Count(journalEvent));

            default:
                return this;
        }
    }

    private MiningRuns With(MiningRun open) => this with { Open = open };
}
