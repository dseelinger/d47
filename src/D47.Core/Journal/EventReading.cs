namespace D47.Core.Journal;

/// <summary>One journal event as a headline, labelled rows and every field it carries.</summary>
public sealed record EventReading(
    string Headline,
    DateTimeOffset Timestamp,
    IReadOnlyList<ReadingRow> Rows,
    IReadOnlyList<PlumbingField> Plumbing);

/// <summary>A label and its values. An empty label is a sentence the page draws full width.</summary>
public sealed record ReadingRow(string Label, IReadOnlyList<ReadingValue> Values)
{
    /// <summary>The journal field the row came from, for its tooltip; null for a row built from several.</summary>
    public string? Field { get; init; }
}

public sealed record ReadingValue(string Text)
{
    private IReadOnlyList<ReadingRun>? _runs;

    /// <summary>Frontier's token, where Text was decoded from one.</summary>
    public string? Symbol { get; init; }

    public ReadingLink? Link { get; init; }

    /// <summary>Typed by a player: drawn exactly as written, never interpreted.</summary>
    public bool Typed { get; init; }

    public ReadingTone Tone { get; init; }

    /// <summary>Text in order, each number its own run; joined, they are Text.</summary>
    public IReadOnlyList<ReadingRun> Runs
    {
        get => _runs ?? [new ReadingRun(Text, false)];
        init => _runs = value;
    }
}

public enum ReadingTone { Value, Name, System, Warning }

public sealed record ReadingRun(string Text, bool Number);

public enum ReadingLinkKind { Engineer, Ship }

/// <summary>Where a value leads: the engineer's <c>EngineerID</c> or the ship's <c>ShipID</c>.</summary>
public sealed record ReadingLink(ReadingLinkKind Kind, string Key);

public enum PlumbingKind { Plain, Empty, Object, List }

/// <summary>A field of the event, as the file wrote it. A group carries its children in file order.</summary>
public sealed record PlumbingField(string Name, string Value)
{
    public PlumbingKind Kind { get; init; }

    public IReadOnlyList<PlumbingField> Fields { get; init; } = [];

    /// <summary>A list element's first string field, such as a module's Slot.</summary>
    public string? Tag { get; init; }

    public bool StartsOpen =>
        Kind is PlumbingKind.Object or PlumbingKind.List
        && Fields.Count <= 4
        && Fields.All(child => child.Kind is PlumbingKind.Plain or PlumbingKind.Empty);
}
