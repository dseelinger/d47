namespace D47.Core.Audio;

/// <summary>Where a Chatterbox voice's reference clip is.</summary>
public enum ChatterboxClip
{
    /// <summary>The clip ships beside the executable.</summary>
    Shipped,

    /// <summary>The clip is on this PC and matched its size and SHA-256.</summary>
    Fetched,

    /// <summary>No clip on this PC.</summary>
    NotFetched,

    /// <summary>A fetch is running.</summary>
    Fetching,

    /// <summary>The last fetch failed.</summary>
    Failed,

    /// <summary>A voice the Commander recorded or imported; always on this PC, never fetched.</summary>
    Custom,
}

/// <summary>A Chatterbox voice's clip state and its size in bytes from <c>catalog.tsv</c>, zero where no row gives one.</summary>
public sealed record ChatterboxClipState(ChatterboxClip Clip, long Bytes)
{
    /// <summary>Why the last fetch failed, as the log states it; null unless <see cref="Clip"/> is <see cref="ChatterboxClip.Failed"/>.</summary>
    public string? Reason { get; init; }
}
