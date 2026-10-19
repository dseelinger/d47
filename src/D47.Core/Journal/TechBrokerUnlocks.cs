using D47.Core.Knowledge;

namespace D47.Core.Journal;

/// <summary>
/// The tech broker rows a Commander is known to have unlocked. Elite lists no unlocks at login, so a row
/// absent here is not known to be unlocked, which is not the same as locked.
/// </summary>
public sealed record TechBrokerUnlocks
{
    public static readonly TechBrokerUnlocks Empty = new();

    private IReadOnlySet<string> Names { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    public bool IsKnown => Names.Count > 0;

    /// <summary>Whether a symbol of this row was seen in a broker unlock, a fitted module or a stored one.</summary>
    public bool IsKnownUnlocked(TechBrokerRow row) => Names.Contains(row.Name);

    /// <summary>The rows for one broker type (<c>guardian</c> or <c>human</c>) not known to be unlocked.</summary>
    public IReadOnlyList<TechBrokerRow> NotKnownToBeUnlocked(string brokerType) =>
    [
        .. TechBrokerCatalogue.Joined.Where(row =>
            string.Equals(row.BrokerType, brokerType, StringComparison.OrdinalIgnoreCase) && !IsKnownUnlocked(row)),
    ];

    public TechBrokerUnlocks Apply(JournalEvent journalEvent)
    {
        IEnumerable<string?> seen = journalEvent.Kind switch
        {
            "TechnologyBroker" => journalEvent.Items("ItemsUnlocked").Select(item => item.String("Name")),
            "Loadout" => journalEvent.Items("Modules").Select(module => module.String("Item")),
            "StoredModules" => journalEvent.Items("Items").Select(item => item.String("Name")),
            _ => [],
        };

        return With(seen.Select(TechBrokerCatalogue.ForSymbol).OfType<TechBrokerRow>().Select(row => row.Name));
    }

    /// <summary>This record plus everything <paramref name="earlier"/> knew.</summary>
    public TechBrokerUnlocks With(TechBrokerUnlocks earlier) => With(earlier.Names);

    private TechBrokerUnlocks With(IEnumerable<string> names)
    {
        var added = names.Where(name => !Names.Contains(name)).ToArray();

        return added.Length == 0
            ? this
            : this with { Names = new HashSet<string>(Names.Concat(added), StringComparer.Ordinal) };
    }
}
