namespace D47.Core.Capabilities;

/// <summary>How a capability presents itself on the panel.</summary>
public sealed record CapabilityDisplay
{
    public string? PanelTitle { get; init; }

    public int Order { get; init; } = 100;

    public bool ShowOnPanel { get; init; } = true;

    /// <summary>
    /// Whether the settings card starts collapsed on a panel that has never been touched. For
    /// a card the Commander opens once a year — nine log levels — starting closed is the
    /// difference between a panel about d47 and a panel about logging. Their own collapse
    /// choice, once made, outranks this.
    /// </summary>
    public bool StartCollapsed { get; init; }
}

/// <summary>
/// One capability, declared once at startup and never mutated (architecture.md §5 D5). Tool
/// schemas, settings rows, spoken help, the keyword router's vocabulary and the docs CI gate
/// are all projections of this — not parallel structures.
/// </summary>
public sealed record CapabilityDescriptor
{
    /// <summary>
    /// Stable kebab-case slug. Doubles as the documentation page filename, so it is a
    /// filename and a URL as well as an identity.
    /// </summary>
    public required string Id { get; init; }

    public required string Group { get; init; }

    public required string Name { get; init; }

    /// <summary>One line. This is what spoken help reads out.</summary>
    public required string Summary { get; init; }

    public IReadOnlyList<string> Examples { get; init; } = [];

    /// <summary>Vocabulary for the model-free keyword router.</summary>
    public IReadOnlyList<string> Keywords { get; init; } = [];

    /// <summary>
    /// Phrases that only mean anything when they were spoken.
    /// <para>
    /// "Can you hear me" is the case. Said into a microphone it is a question about the
    /// microphone and the report is the right answer; typed into the ask box it is a
    /// conversational opener, and answering it with a hardware summary is answering about the
    /// channel the Commander did not use. The words are identical and the intent is not, so the
    /// only thing that can tell them apart is how they arrived.
    /// </para>
    /// <para>
    /// Kept apart from <see cref="Keywords"/> rather than flagged inside it, for the same reason
    /// <see cref="InterruptKeywords"/> is: a list whose entries mean different things depending
    /// on a flag is a list that gets read wrong.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> SpokenKeywords { get; init; } = [];

    /// <summary>
    /// Phrases that only mean anything while there is something to interrupt.
    /// <para>
    /// Kept apart from <see cref="Keywords"/> so that a word too broad to be a general command
    /// can still be the fastest possible interrupt. "Stop" is the case: on its own it is a bare
    /// common verb, and the general vocabulary refuses those for the reason
    /// <see cref="Builtin.JournalCapability"/> documents — a bare word hijacks any sentence
    /// containing it. The concrete future claimant is Macros (Phase 10), whose names
    /// the Commander authors and whose vocabulary the checklist says outright cannot be closed
    /// in advance. But while d47 is mid-sentence, "stop" has one plausible meaning, and it is
    /// the shortest thing a Commander can say. Context is what disambiguates it, so context is what
    /// gates it.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> InterruptKeywords { get; init; } = [];

    public IReadOnlyList<ToolDefinition> Tools { get; init; } = [];

    public IReadOnlyList<SettingRow> Settings { get; init; } = [];

    public CapabilityDisplay Display { get; init; } = new();
}
