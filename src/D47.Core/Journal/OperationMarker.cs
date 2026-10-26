namespace D47.Core.Journal;

/// <summary>
/// Sets <see cref="JournalEvent.InOperation"/> on the lines Elite writes for an Operation, in order of arrival. A
/// <c>LoadGame</c> outside an Operation is held until the line after it, which says whether it entered one.
/// </summary>
public sealed class OperationMarker
{
    private bool _open;

    private JournalEvent? _held;

    /// <summary>The next lines of a file being tailed, marked; a <c>LoadGame</c> ending them is held for the next call.</summary>
    public IReadOnlyList<JournalEvent> Next(IReadOnlyList<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var marked = new List<JournalEvent>(events.Count + 1);

        foreach (var journalEvent in events)
        {
            Feed(journalEvent, marked);
        }

        return marked;
    }

    /// <summary>The held <c>LoadGame</c>, unmarked, or null when none is held.</summary>
    public JournalEvent? Release()
    {
        var held = _held;
        _held = null;
        return held;
    }

    /// <summary>One whole file, marked. A reader that filters lines must pass <c>LoadGame</c>, <c>GameModeChange</c>, <c>Fileheader</c> and <c>Shutdown</c>.</summary>
    public static IEnumerable<JournalEvent> Mark(IEnumerable<JournalEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var marker = new OperationMarker();
        var marked = new List<JournalEvent>(2);

        foreach (var journalEvent in events)
        {
            marker.Feed(journalEvent, marked);

            foreach (var line in marked)
            {
                yield return line;
            }

            marked.Clear();
        }

        if (marker.Release() is { } held)
        {
            yield return held;
        }
    }

    private void Feed(JournalEvent journalEvent, List<JournalEvent> marked)
    {
        var entersOperation = journalEvent.Kind == "GameModeChange" && journalEvent.String("GameMode") == "Operation";

        if (Release() is { } held)
        {
            marked.Add(entersOperation ? held with { InOperation = true } : held);
        }

        switch (journalEvent.Kind)
        {
            case "Fileheader" or "Shutdown":
                _open = false;
                marked.Add(journalEvent);
                break;

            case "LoadGame" when !_open:
                _held = journalEvent;
                break;

            case "GameModeChange" when entersOperation:
                _open = true;
                marked.Add(journalEvent with { InOperation = true });
                break;

            case "GameModeChange" when _open:
                _open = false;
                marked.Add(journalEvent with { InOperation = true });
                break;

            default:
                marked.Add(_open ? journalEvent with { InOperation = true } : journalEvent);
                break;
        }
    }
}
