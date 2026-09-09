namespace D47.Core.Diagnostics.Donation;

/// <summary>How far back a corpus donation reaches (#174).</summary>
/// <param name="Name">What the chooser shows.</param>
/// <param name="Back">How far back from now, or null for the whole history on disk.</param>
public sealed record CorpusScope(string Name, TimeSpan? Back)
{
    /// <summary>The offer, narrowest first (#241).</summary>
    public static readonly IReadOnlyList<CorpusScope> All =
    [
        new("The last 30 days", TimeSpan.FromDays(30)),
        new("The last 3 months", TimeSpan.FromDays(90)),
        new("The last 12 months", TimeSpan.FromDays(365)),
        new("Everything", null),
    ];

    /// <summary>What the window opens on: the gentlest scope, not the biggest (#241).</summary>
    public static CorpusScope Default => All[0];

    /// <summary>The instant this scope starts at, given when the Commander asked.</summary>
    public DateTimeOffset From(DateTimeOffset now) =>
        Back is { } back ? now - back : DateTimeOffset.MinValue;

    public override string ToString() => Name;
}
