namespace D47.Core.Diagnostics.Donation;

/// <summary>How far back a donation reaches, in the units a Commander thinks in (#173).</summary>
/// <param name="Name">What the chooser shows.</param>
/// <param name="Before">How far back from the mark.</param>
/// <param name="After">
/// How far past it — nothing for the wider spans, which already run to the mark, and a minute for the
/// tightest, because a Commander says "note that" while a thing is going wrong at least as often as
/// afterwards.
/// </param>
public sealed record ExcerptSpan(string Name, TimeSpan Before, TimeSpan After)
{
    /// <summary>The offer, tightest first.</summary>
    public static readonly IReadOnlyList<ExcerptSpan> All =
    [
        new("The last 10 minutes", TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(1)),
        new("The last hour", TimeSpan.FromHours(1), TimeSpan.FromMinutes(1)),
        new("The last 6 hours", TimeSpan.FromHours(6), TimeSpan.Zero),
        new("The last 12 hours", TimeSpan.FromHours(12), TimeSpan.Zero),
    ];

    /// <summary>What a window opens on.</summary>
    public static ExcerptSpan Default => All[0];

    /// <summary>The window this span puts around a mark.</summary>
    public ExcerptRequest Around(DateTimeOffset markedAt, bool includeMySpeech) =>
        new(markedAt, Before, After, includeMySpeech);

    public override string ToString() => Name;
}
