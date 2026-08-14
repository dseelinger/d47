namespace D47.Core.Journal;

/// <summary>One module in storage, and where it is.</summary>
public sealed record StoredModule(string Name, string StarSystem)
{
    /// <summary>The station holding it, where the snapshot can say. Absent for remote modules.</summary>
    public string? StationName { get; init; }

    /// <summary>What moving it to where the Commander is would cost. Zero for a module already here.</summary>
    public long? TransferCost { get; init; }

    /// <summary>How long that move takes, in seconds.</summary>
    public int? TransferTime { get; init; }

    /// <summary>Being moved right now, so it is neither here nor there yet.</summary>
    public bool InTransit { get; init; }

    /// <summary>
    /// Stolen. Worth carrying because it is the reason a module cannot be fitted at some
    /// stations, which is otherwise a mystery at the outfitting screen rather than at the
    /// point of asking.
    /// </summary>
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
/// Every module the Commander has in storage (list.md Phase 14, "Find Nearest" — the stored
/// modules half), built from the <c>StoredModules</c> event.
/// <para>
/// The same snapshot rule as <see cref="FleetRegistry"/>, and for the same reason: Elite writes
/// <c>StoredModules</c> as a complete list on docking at an outfitting station, so the store is
/// replaced wholesale rather than merged. A list still holding a module that has since been sold
/// or fitted is worse than a list that is merely old, because the failure it causes is a
/// Commander flying somewhere to collect something that is not there.
/// </para>
/// <para>
/// <b>Not the same question as the outfitting search.</b> "Where can I buy a 5A Frame Shift
/// Drive" reaches spansh.co.uk; "do I already own one" is answerable from disk and should never
/// leave the machine. The two live apart deliberately — this one works with the galaxy search
/// switched off, which is the state a fresh install is in.
/// </para>
/// </summary>
public sealed record ModuleStore
{
    public static readonly ModuleStore Empty = new();

    /// <summary>Where the snapshot was taken, which is what "here" means for the modules in it.</summary>
    public string? SnapshotSystem { get; init; }

    public string? SnapshotStation { get; init; }

    /// <summary>When the snapshot was taken. Null means no outfitting station has been visited yet.</summary>
    public DateTimeOffset? TakenAt { get; init; }

    public IReadOnlyList<StoredModule> Modules { get; init; } = [];

    /// <summary>
    /// Whether a snapshot has been seen at all. The difference between "no modules stored" and
    /// "nothing has told me yet" — and only one of those is an answer.
    /// </summary>
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

    /// <summary>
    /// Stored modules whose name contains what was said, case-insensitively. A fragment rather
    /// than an exact name, because "have I got a shield generator anywhere" is the question this
    /// is asked, and the catalogue matching that <see cref="Knowledge.OutfittingCatalogue"/> does
    /// for the outfitting search would be wrong here: this list contains whatever Elite called
    /// the thing, engineered names and all, not catalogue entries.
    /// </summary>
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
                // Only the station of the snapshot is knowable. A remote module's event carries a
                // MarketID and no station name, and turning an id into a name would mean a lookup
                // this deliberately does not make.
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

/// <summary>
/// Turning the journal's module symbols into something worth saying out loud.
/// <para>
/// Elite writes <c>Name_Localised</c> for most stored modules and, for the ones it does not,
/// leaves a symbol like <c>$int_powerplant_size6_class5_name;</c>. Speaking that verbatim is
/// worse than useless, and inventing a friendly name for it would be inventing game data — so
/// this only strips the decoration Frontier puts around the symbol and leaves the words alone.
/// The result is "int powerplant size6 class5", which is ugly and true.
/// </para>
/// </summary>
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
