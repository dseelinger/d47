using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>One event as the Journal page shows it: a line to read, and the fields behind it.</summary>
/// <param name="Timestamp">When Elite wrote it.</param>
/// <param name="Kind">The event name, kept so the page can filter noise without re-parsing.</param>
/// <param name="Said">The sentence, or the kind spaced into words where there is no sentence.</param>
/// <param name="Raw">The event's own JSON, pretty-printed.</param>
/// <param name="Compact">The same JSON on one line, as the file holds it.</param>
public sealed record JournalEntry(
    DateTimeOffset Timestamp, string Kind, string Said, string Raw, string Compact)
{
    /// <summary>Whether this is one of the kinds the page hides by default.</summary>
    public bool IsNoise => JournalSentence.Noise.Contains(Kind);

    /// <summary>The line as the list draws it: the time, then what happened.</summary>
    public string Line => $"{Timestamp.ToLocalTime():HH:mm:ss}  {Said}";

    /// <summary>The event's own <c>StarSystem</c> field, or null when it has none.</summary>
    public string? StarSystem { get; init; }
}

/// <summary>
/// The last few thousand journal events, kept so the Commander can read them
/// (https://github.com/dseelinger/d47/issues/51).
/// </summary>
public sealed class JournalLog(int keep = 4000)
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private static readonly JsonSerializerOptions Flat = new() { WriteIndented = false };

    private readonly Queue<JournalEntry> _entries = new();
    private readonly int _keep = keep > 0 ? keep : 1;

    /// <summary>What is held, oldest first.</summary>
    public IReadOnlyList<JournalEntry> Entries => [.. _entries];

    /// <summary>How many are held.</summary>
    public int Count => _entries.Count;

    /// <summary>Takes a poll's worth of events.</summary>
    public void Add(IReadOnlyList<JournalEvent> events)
    {
        foreach (var journalEvent in events)
        {
            _entries.Enqueue(Entry(journalEvent));

            while (_entries.Count > _keep)
            {
                _entries.Dequeue();
            }
        }
    }

    /// <summary>
    /// The page's view of what is held, newest first — a Commander opening the journal is looking for
    /// what just happened, not for what happened when they logged in.
    /// </summary>
    /// <param name="noise">Whether the kinds the page calls noise are included.</param>
    public IReadOnlyList<JournalEntry> Read(bool noise = false)
    {
        var held = _entries.Where(entry => noise || !entry.IsNoise).ToList();

        held.Reverse();
        return held;
    }

    /// <summary>
    /// The events as the file holds them, newest first, one per line — what the Raw Journal reading
    /// shows.
    /// </summary>
    public string Document(bool noise = false) =>
        string.Join(Environment.NewLine, Read(noise).Select(entry => entry.Compact));

    private static JournalEntry Entry(JournalEvent journalEvent) =>
        new(journalEvent.Timestamp,
            journalEvent.Kind,
            JournalSentence.For(journalEvent),
            Raw(journalEvent.Raw, indented: true),
            Raw(journalEvent.Raw, indented: false))
        {
            StarSystem = journalEvent.Raw.ValueKind == JsonValueKind.Object
                && journalEvent.Raw.TryGetProperty("StarSystem", out var system)
                && system.ValueKind == JsonValueKind.String
                    ? system.GetString()
                    : null,
        };

    /// <summary>The event's own JSON, indented.</summary>
    private static string Raw(JsonElement raw, bool indented)
    {
        try
        {
            return JsonSerializer.Serialize(raw, indented ? Pretty : Flat);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A detail pane that cannot draw must not take the page down with it.
            return raw.ToString();
        }
    }
}
