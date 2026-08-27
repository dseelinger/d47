using System.Text.Json;

namespace D47.Core.Journal;

/// <summary>One event as the Journal page shows it: a line to read, and the fields behind it.</summary>
/// <param name="Timestamp">When Elite wrote it.</param>
/// <param name="Kind">The event name, kept so the page can filter noise without re-parsing.</param>
/// <param name="Said">The sentence, or the kind spaced into words where there is no sentence.</param>
/// <param name="Raw">The event's own JSON, pretty-printed. The half that cannot be wrong.</param>
public sealed record JournalEntry(DateTimeOffset Timestamp, string Kind, string Said, string Raw)
{
    /// <summary>Whether this is one of the kinds the page hides by default.</summary>
    public bool IsNoise => JournalSentence.Noise.Contains(Kind);

    /// <summary>The line as the list draws it: the time, then what happened.</summary>
    public string Line => $"{Timestamp.ToLocalTime():HH:mm:ss}  {Said}";
}

/// <summary>
/// The last few thousand journal events, kept so the Commander can read them
/// (https://github.com/dseelinger/d47/issues/51).
/// <para>
/// <b>Fed from the spine's own poll, and never by opening the file again.</b> Elite holds the
/// current journal open, so a second reader needs <c>FileShare.ReadWrite | FileShare.Delete</c> or
/// it throws — and a page that opens its own stream is a second thing to get that right. The tick
/// loop already has every event in hand; this keeps the ones worth showing.
/// </para>
/// <para>
/// <b>Bounded, because the file is not.</b> A day's journal runs to megabytes and 48% of the corpus
/// by volume is inventory chatter nobody reads. Keeping everything would put a session's worth of
/// <c>ShipLocker</c> in memory to draw four lines from it.
/// </para>
/// <para>
/// <b>Noise is kept and marked rather than dropped.</b> The page offers a toggle, and a filter
/// applied here could not be switched off without re-reading the file — which is the thing that
/// cannot be done. Same rule <see cref="JournalSentence.Noise"/> states for itself: a display
/// filter and never a read filter.
/// </para>
/// <para>
/// Owns no thread and reads no clock, like everything else in Core. It is handed events and it
/// keeps them.
/// </para>
/// </summary>
public sealed class JournalLog(int keep = 4000)
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private readonly Queue<JournalEntry> _entries = new();
    private readonly int _keep = keep > 0 ? keep : 1;

    /// <summary>What is held, oldest first.</summary>
    public IReadOnlyList<JournalEntry> Entries => [.. _entries];

    /// <summary>How many are held.</summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Takes a poll's worth of events. Called with exactly what the spine returned, including the
    /// priming replay — a Commander opening the page wants the session they have been flying, not
    /// only what has happened since they opened it.
    /// </summary>
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
    /// The page's view of what is held, newest first — a Commander opening the journal is looking
    /// for what just happened, not for what happened when they logged in.
    /// </summary>
    /// <param name="noise">Whether the kinds the page calls noise are included.</param>
    public IReadOnlyList<JournalEntry> Read(bool noise = false)
    {
        var held = _entries.Where(entry => noise || !entry.IsNoise).ToList();

        held.Reverse();
        return held;
    }

    private static JournalEntry Entry(JournalEvent journalEvent) =>
        new(journalEvent.Timestamp,
            journalEvent.Kind,
            JournalSentence.For(journalEvent),
            Raw(journalEvent.Raw));

    /// <summary>
    /// The event's own JSON, indented. <b>Pretty-printed rather than reformatted</b>: the point of
    /// the detail pane is that it is the fields exactly as Elite wrote them, so a bug report made
    /// from it is worth something. The summary line is prose and can be wrong; this cannot.
    /// </summary>
    private static string Raw(JsonElement raw)
    {
        try
        {
            return JsonSerializer.Serialize(raw, Pretty);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // A detail pane that cannot draw must not take the page down with it. The line above it
            // still reads, which is most of what the Commander came for.
            return raw.ToString();
        }
    }
}
