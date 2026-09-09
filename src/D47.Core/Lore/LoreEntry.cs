namespace D47.Core.Lore;

/// <summary>
/// How much weight a note carries, and therefore which sentence reads it back (Phase 23, "Commander's
/// Lore").
/// </summary>
public enum LoreTier
{
    /// <summary>A row in the shipped table.</summary>
    Shipped,

    /// <summary>An addition a web lookup appeared to support when it was made.</summary>
    Corroborated,

    /// <summary>The Commander's word, and nothing else.</summary>
    Commander,
}

/// <summary>How an entry got into the book.</summary>
public enum LoreArrival
{
    /// <summary>The Commander's own hands, on the panel.</summary>
    Panel,

    /// <summary>The model's tool call.</summary>
    Model,
}

/// <summary>One thing worth saying about one system.</summary>
/// <param name="SystemAddress">
/// The key, and it is the address rather than the name because names move and addresses do not —
/// Ceeckia ZQ-L c24-0 is Beagle Point now and is the same 81973396946 it always was.
/// </param>
/// <param name="Name">The system as it was spelled when the entry was made.</param>
/// <param name="Note">The fact itself, as one or two sentences.</param>
public sealed record LoreEntry(long SystemAddress, string Name, string Note)
{
    public required LoreTier Tier { get; init; }

    /// <summary>How it arrived.</summary>
    public LoreArrival Arrival { get; init; } = LoreArrival.Panel;

    /// <summary>
    /// The Frontier id of the Commander who was aboard when it was added, or null for a shipped row and
    /// for an entry hand-written into the file.
    /// </summary>
    public string? FrontierId { get; init; }

    /// <summary>When it was added.</summary>
    public DateTimeOffset? AddedAt { get; init; }

    /// <summary>
    /// The sentence this entry is read back in, which is the whole point of <see cref="LoreTier"/>
    /// being a field rather than a note the persona is trusted to add.
    /// </summary>
    public string Spoken() => (Tier, Arrival) switch
    {
        (LoreTier.Shipped, _) => Note,
        (LoreTier.Corroborated, _) => $"You added this one, and the search agreed at the time: {Note}",
        (_, LoreArrival.Model) => $"I wrote this one down myself, and nothing has checked it: {Note}",
        _ => $"You told me: {Note}",
    };
}
