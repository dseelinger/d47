namespace D47.Core.Journal;

/// <summary>One module in storage, and where it is.</summary>
public sealed record StoredModule(string Name, string StarSystem)
{
    /// <summary>The station holding it, where the snapshot can say.</summary>
    public string? StationName { get; init; }

    /// <summary>What moving it to where the Commander is would cost.</summary>
    public long? TransferCost { get; init; }

    /// <summary>How long that move takes, in seconds.</summary>
    public int? TransferTime { get; init; }

    /// <summary>Being moved right now, so it is neither here nor there yet.</summary>
    public bool InTransit { get; init; }

    /// <summary>Stolen.</summary>
    public bool Hot { get; init; }

    /// <summary>The blueprint applied to it, if any — the journal's symbol, tidied.</summary>
    public string? Engineering { get; init; }

    /// <summary>The grade that blueprint is at, 1 to 5.</summary>
    public int? EngineeringGrade { get; init; }

    public bool IsEngineered => Engineering is not null;

    public string Describe()
    {
        var described = Name;

        if (Engineering is not null)
        {
            described += EngineeringGrade is { } grade
                ? $" ({Engineering}, grade {grade})"
                : $" ({Engineering})";
        }

        return Hot ? described + ", hot" : described;
    }
}

/// <summary>
/// Every module the Commander has in storage (Phase 14, "Find Nearest" — the stored modules half),
/// built from the <c>StoredModules</c> event.
/// </summary>
public sealed record ModuleStore
{
    public static readonly ModuleStore Empty = new();

    /// <summary>Where the snapshot was taken, which is what "here" means for the modules in it.</summary>
    public string? SnapshotSystem { get; init; }

    public string? SnapshotStation { get; init; }

    /// <summary>When the snapshot was taken.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public IReadOnlyList<StoredModule> Modules { get; init; } = [];

    /// <summary>Whether a snapshot has been seen at all.</summary>
    public bool IsKnown => TakenAt is not null;

    public IReadOnlyList<StoredModule> Here =>
        [.. Modules.Where(module =>
            !module.InTransit
            && SnapshotSystem is not null
            && string.Equals(module.StarSystem, SnapshotSystem, StringComparison.OrdinalIgnoreCase))];

    public IReadOnlyList<StoredModule> Elsewhere =>
        [.. Modules.Where(module =>
            module.InTransit
            || SnapshotSystem is null
            || !string.Equals(module.StarSystem, SnapshotSystem, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Systems holding at least one stored module, each named once.</summary>
    public IReadOnlyList<string> Systems =>
        [.. Modules
            .Where(module => !module.InTransit)
            .Select(module => module.StarSystem)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Stored modules whose name contains what was said, case-insensitively.</summary>
    public IReadOnlyList<StoredModule> Matching(string fragment)
    {
        var wanted = fragment.Trim();

        return wanted.Length == 0
            ? []
            : [.. Modules.Where(module => module.Name.Contains(wanted, StringComparison.OrdinalIgnoreCase))];
    }

    public ModuleStore Apply(JournalEvent journalEvent)
    {
        if (journalEvent.Kind != "StoredModules")
        {
            return this;
        }

        var system = journalEvent.String("StarSystem");
        var station = journalEvent.String("StationName");

        var modules = journalEvent.Items("Items").Select(element =>
        {
            var where = element.String("StarSystem") ?? system ?? "unknown";

            return new StoredModule(ModuleNames.Readable(element.Named("Name")), where)
            {
                // Only the station of the snapshot is knowable.
                StationName = system is not null
                              && string.Equals(where, system, StringComparison.OrdinalIgnoreCase)
                    ? station
                    : null,
                TransferCost = element.Long("TransferCost"),
                TransferTime = element.Int("TransferTime"),
                InTransit = element.Bool("InTransit"),
                Hot = element.Bool("Hot"),
                Engineering = ModuleNames.ReadableOrNull(element.String("EngineerModifications")),
                EngineeringGrade = element.Int("Level"),
            };
        });

        return new ModuleStore
        {
            SnapshotSystem = system,
            SnapshotStation = station,
            TakenAt = journalEvent.Timestamp,
            Modules = [.. modules],
        };
    }
}

/// <summary>Turning the journal's module symbols into something worth saying out loud.</summary>
internal static class ModuleNames
{
    public static string Readable(string? symbol) => ReadableOrNull(symbol) ?? "an unnamed module";

    public static string? ReadableOrNull(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var text = symbol.Trim();

        // A localised name has none of this decoration, so it falls straight through.
        if (text.StartsWith('$'))
        {
            text = text[1..];
        }

        if (text.EndsWith(';'))
        {
            text = text[..^1];
        }

        if (text.EndsWith("_name", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^"_name".Length];
        }

        return text.Replace('_', ' ').Trim();
    }
}
