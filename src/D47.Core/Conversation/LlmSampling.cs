namespace D47.Core.Conversation;

/// <summary>How adventurous the sampler may be on one call (#98).</summary>
public sealed record LlmSampling
{
    /// <summary>
    /// In character. 0.9 rather than a guess: reported guidance for character writing puts the useful
    /// band at 0.8 to 1.0 — flat below about 0.7, incoherent above about 1.2 — and most of what d47
    /// says is meant to be in character rather than correct.
    /// </summary>
    public const double Warm = 0.9;

    /// <summary>
    /// No warmth at all, for the calls that are questions about d47's own configuration or that are
    /// checked against the world afterwards.
    /// </summary>
    public const double Cold = 0.0;

    private LlmSampling(double? temperature) => Temperature = temperature;

    /// <summary>
    /// What to ask for, or null to send no sampling field and take whatever the endpoint does by
    /// default.
    /// </summary>
    public double? Temperature { get; }

    /// <summary>Say nothing, deliberately.</summary>
    // Cast because a record's copy constructor is also a one-argument constructor, and a bare null cannot
    // tell the two apart.
    public static readonly LlmSampling Unstated = new((double?)null);

    /// <summary>A turn the Commander asked for.</summary>
    public static readonly LlmSampling Conversation = new(Warm);

    /// <summary>
    /// An ambient remark, an opening brief, a gap reaction, a re-voiced callout, a carrier's tower,
    /// invented NPC chatter.
    /// </summary>
    public static readonly LlmSampling InCharacter = new(Warm);

    /// <summary>A lore lookup: what a web search turned up about a system, reported as a search result.</summary>
    public static readonly LlmSampling Lore = new(Cold);

    /// <summary>Adventure generation.</summary>
    public static readonly LlmSampling Adventure = new(Cold);

    /// <summary>The Commander's log.</summary>
    public static readonly LlmSampling Log = new(Cold);

    /// <summary>Voice casting.</summary>
    public static readonly LlmSampling VoiceCasting = new(Cold);
}
