namespace D47.Core.Journal;

/// <summary>
/// Which of the three material lockers something belongs to. Elite's own words, because the
/// Commander sees these labels in the game and would say them back.
/// </summary>
public enum MaterialCategory
{
    Unknown,
    Raw,
    Manufactured,
    Encoded,
}

/// <summary>One material and how many are held.</summary>
public sealed record MaterialHolding(string Name, MaterialCategory Category, int Count)
{
    /// <summary>
    /// The player-facing name where the journal supplied one. Elite writes the internal symbol
    /// on some events and the localised name on others, so both are kept: the symbol is the
    /// identity, and this is what gets spoken.
    /// </summary>
    public string? DisplayName { get; init; }

    public string Speak() => DisplayName ?? Name;
}

/// <summary>
/// What the Commander is carrying (Phase 7, "Know what materials you are carrying"),
/// answerable with no external lookup.
/// <para>
/// The Materials event is a complete snapshot written at session start; everything after it is
/// a delta. Both are folded here, and the snapshot replaces rather than merges — a material
/// absent from a fresh snapshot is a material at zero, and keeping the old figure would have
/// d47 confidently reporting a stock that was spent last session.
/// </para>
/// <para>
/// Deltas are clamped at both ends rather than allowed to run past what the game permits. A
/// journal that starts mid-session — d47 launched after Elite, which is the common case — will
/// report materials being spent that were never seen being collected, and collected that were
/// never seen being spent. A negative holding is a worse answer than an understated one, and
/// <b>"109 of 100" is a worse answer than an overstated one</b>: it is a number the game cannot
/// produce, so a Commander reading it knows only that d47 is wrong
/// (remediation.md, "Adaptive Encryptors Capture is full. 109 of 100").
/// </para>
/// <para>
/// The ceiling comes from <see cref="MaterialGrades"/>, which is where capacity already lives,
/// and a material it does not recognise has no ceiling rather than a guessed one — the same
/// answer the milestone callout gives for the same reason.
/// </para>
/// </summary>
public sealed record MaterialsInventory
{
    public static readonly MaterialsInventory Empty = new();

    /// <summary>
    /// An init-only property rather than a readonly field so that <c>with</c> can replace it —
    /// which is the only way this record stays immutable while still folding deltas.
    /// </summary>
    private IReadOnlyDictionary<string, MaterialHolding> Holdings { get; init; } =
        new Dictionary<string, MaterialHolding>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a full snapshot has been seen. Until then this is deltas only.</summary>
    public bool SnapshotSeen { get; init; }

    public IReadOnlyCollection<MaterialHolding> All => [.. Holdings.Values];

    public int DistinctCount => Holdings.Count;

    public int TotalCount => Holdings.Values.Sum(holding => holding.Count);

    public IReadOnlyList<MaterialHolding> InCategory(MaterialCategory category) =>
        [.. Holdings.Values
            .Where(holding => holding.Category == category)
            .OrderByDescending(holding => holding.Count)
            .ThenBy(holding => holding.Speak(), StringComparer.OrdinalIgnoreCase)];

    public int CountOf(string name) =>
        Holdings.TryGetValue(name, out var holding) ? holding.Count : 0;

    public MaterialHolding? Find(string name) =>
        Holdings.TryGetValue(name, out var holding) ? holding : null;

    public MaterialsInventory Apply(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Materials":
            {
                var snapshot = new Dictionary<string, MaterialHolding>(StringComparer.OrdinalIgnoreCase);

                foreach (var (property, category) in (ReadOnlySpan<(string, MaterialCategory)>)
                         [("Raw", MaterialCategory.Raw),
                          ("Manufactured", MaterialCategory.Manufactured),
                          ("Encoded", MaterialCategory.Encoded)])
                {
                    foreach (var element in journalEvent.Items(property))
                    {
                        if (element.String("Name") is not { } name)
                        {
                            continue;
                        }

                        snapshot[name] = new MaterialHolding(name, category, element.Int("Count") ?? 0)
                        {
                            DisplayName = element.String("Name_Localised"),
                        };
                    }
                }

                return new MaterialsInventory { Holdings = snapshot, SnapshotSeen = true };
            }

            case "MaterialCollected":
                return Add(
                    journalEvent.String("Name"),
                    CategoryOf(journalEvent.String("Category")),
                    journalEvent.Int("Count") ?? 1,
                    journalEvent.String("Name_Localised"));

            case "MaterialDiscarded":
                return Add(
                    journalEvent.String("Name"),
                    CategoryOf(journalEvent.String("Category")),
                    -(journalEvent.Int("Count") ?? 1),
                    journalEvent.String("Name_Localised"));

            // A trade is both directions at once, and the two materials are usually different
            // grades of the same line. Applying only one side would drift the inventory every
            // time the Commander visits a trader.
            case "MaterialTrade":
            {
                var traded = this;

                if (journalEvent.Object("Paid") is { } paid)
                {
                    traded = traded.Add(
                        paid.String("Material"),
                        CategoryOf(paid.String("Category")),
                        -(paid.Int("Quantity") ?? 0),
                        paid.String("Material_Localised"));
                }

                if (journalEvent.Object("Received") is { } received)
                {
                    traded = traded.Add(
                        received.String("Material"),
                        CategoryOf(received.String("Category")),
                        received.Int("Quantity") ?? 0,
                        received.String("Material_Localised"));
                }

                return traded;
            }

            // Everything that consumes materials, under the property each event happens to use
            // for its ingredient list. The shapes differ; the effect does not.
            case "EngineerCraft":
                return Spend(journalEvent, "Ingredients");

            case "Synthesis":
            case "TechnologyBroker":
                return Spend(journalEvent, "Materials");

            case "ScientificResearch":
                return Add(
                    journalEvent.String("Name"),
                    CategoryOf(journalEvent.String("Category")),
                    -(journalEvent.Int("Count") ?? 0),
                    journalEvent.String("Name_Localised"));

            case "MissionCompleted":
            {
                var rewarded = this;

                foreach (var element in journalEvent.Items("MaterialsReward"))
                {
                    rewarded = rewarded.Add(
                        element.String("Name"),
                        CategoryOf(element.String("Category")),
                        element.Int("Count") ?? 0,
                        element.String("Name_Localised"));
                }

                return rewarded;
            }

            default:
                return this;
        }
    }

    private MaterialsInventory Spend(JournalEvent journalEvent, string property)
    {
        var spent = this;

        foreach (var element in journalEvent.Items(property))
        {
            spent = spent.Add(
                element.String("Name"),
                CategoryOf(element.String("Category")),
                -(element.Int("Count") ?? 0),
                element.String("Name_Localised"));
        }

        return spent;
    }

    private MaterialsInventory Add(string? name, MaterialCategory category, int delta, string? displayName)
    {
        if (name is null || delta == 0)
        {
            return this;
        }

        var updated = new Dictionary<string, MaterialHolding>(Holdings, StringComparer.OrdinalIgnoreCase);
        var existing = Holdings.GetValueOrDefault(name);

        // Clamped at both ends, per the note on this type. A snapshot is authoritative and is
        // not put through here; only deltas are, and only a delta can drift.
        var count = Math.Max(0, (existing?.Count ?? 0) + delta);

        if (MaterialGrades.CapacityOf(name) is { } capacity && capacity > 0)
        {
            count = Math.Min(count, capacity);
        }

        updated[name] = new MaterialHolding(
            name,
            // A delta event whose category is missing must not overwrite a known one with
            // Unknown; the snapshot is where categories are reliable.
            category == MaterialCategory.Unknown ? existing?.Category ?? category : category,
            count)
        {
            DisplayName = displayName ?? existing?.DisplayName,
        };

        return this with { Holdings = updated };
    }

    private static MaterialCategory CategoryOf(string? value) => value?.ToLowerInvariant() switch
    {
        "raw" => MaterialCategory.Raw,
        "manufactured" => MaterialCategory.Manufactured,
        "encoded" => MaterialCategory.Encoded,
        _ => MaterialCategory.Unknown,
    };
}
