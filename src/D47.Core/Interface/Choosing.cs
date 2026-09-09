namespace D47.Core.Interface;

/// <summary>Where a chooser is drawn (Phase 25, "Choosing takes the panel").</summary>
public enum ChoiceSurface
{
    /// <summary>
    /// The chooser replaces the panel until it is dismissed, which makes it a level of the drill stack
    /// rather than a popup.
    /// </summary>
    Page,

    /// <summary>Drawn over the page it was opened from, in the same tree.</summary>
    Layer,
}

/// <summary>One thing that can be picked.</summary>
/// <param name="Key">What it is, to the code.</param>
/// <param name="Label">What it is, to the Commander.</param>
/// <param name="Detail">
/// A second line, where the label alone does not decide it — a module's mass beside its name.
/// </param>
public sealed record ChoiceOption(string Key, string Label, string? Detail = null)
{
    /// <summary>The heading this option sits under, where the list is grouped.</summary>
    public string? Group { get; init; }
}

/// <summary>
/// A question the panel is being asked to put to the Commander (Phase 25, "Choosing takes the panel").
/// </summary>
/// <param name="Key">The crumb key this becomes, when it is a page.</param>
/// <param name="Word">The crumb word.</param>
/// <param name="Title">What is being chosen. "Weapon 3".</param>
/// <param name="Context">
/// What it is being chosen for: the slot's size, and what is fitted now.
/// </param>
/// <param name="Options">What can be picked, in the order they are shown.</param>
/// <param name="Current">Which option is fitted now, by key, or null where nothing is.</param>
/// <param name="Surface">Page or layer.</param>
public sealed record ChoiceRequest(
    string Key,
    string Word,
    string Title,
    string? Context,
    IReadOnlyList<ChoiceOption> Options,
    string? Current,
    ChoiceSurface Surface)
{
    /// <summary>What the option the Commander is already on is called, in words.</summary>
    public string CurrentWord { get; init; } = "fitted now";

    /// <summary>Whether the chooser narrows itself as the Commander types (remediation.md 12, item 5).</summary>
    public bool Searchable { get; init; }

    /// <summary>
    /// The help page this chooser's own question mark opens, or null to inherit whatever the level
    /// underneath it declares (asked for 2026-08-23).
    /// </summary>
    public string? Help { get; init; }

    /// <summary>Which option a spoken label names, or null when none of them does.</summary>
    public ChoiceOption? Match(string spoken) =>
        Options.FirstOrDefault(option =>
            string.Equals(option.Label, spoken.Trim(), StringComparison.OrdinalIgnoreCase));
}
