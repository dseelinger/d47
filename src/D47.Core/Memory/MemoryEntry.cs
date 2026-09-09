namespace D47.Core.Memory;

/// <summary>
/// How a remembered fact arrived at being believed, and therefore which sentence reads it back (Phase
/// 31, "A memory is written down, never inferred into being").
/// </summary>
public enum MemoryTier
{
    /// <summary>The Commander said so, on the panel, in their own words.</summary>
    Stated,

    /// <summary>d47 read it out of the journal.</summary>
    Observed,

    /// <summary>d47 wrote it down on its own initiative, out of a conversation.</summary>
    Inferred,
}

/// <summary>How an entry got into the store.</summary>
public enum MemoryArrival
{
    /// <summary>The Commander's own hands, on the panel.</summary>
    Panel,

    /// <summary>The model's tool call.</summary>
    Model,

    /// <summary>d47's own reading of the journal.</summary>
    Journal,
}

/// <summary>One thing d47 remembers about the Commander.</summary>
/// <param name="Key">Identity, and what a rewrite replaces.</param>
/// <param name="Fact">The thing itself, as one sentence.</param>
public sealed record MemoryEntry(string Key, string Fact)
{
    public required MemoryTier Tier { get; init; }

    /// <summary>How it arrived.</summary>
    public MemoryArrival Arrival { get; init; } = MemoryArrival.Panel;

    /// <summary>What was going on when it was written down — the system, the hull, the activity.</summary>
    public IReadOnlyList<string> About { get; init; } = [];

    /// <summary>When it was written down.</summary>
    public DateTimeOffset? AddedAt { get; init; }

    /// <summary>The sentence this entry is read back in.</summary>
    public string Spoken() => Tier switch
    {
        MemoryTier.Stated => $"You told me: {Fact}",
        MemoryTier.Observed => $"I noticed: {Fact}",
        _ => $"I wrote this one down myself, and nothing has checked it: {Fact}",
    };
}

/// <summary>One entry that could not be read back, and why.</summary>
public sealed record MemoryProblem(string What, string Why);
