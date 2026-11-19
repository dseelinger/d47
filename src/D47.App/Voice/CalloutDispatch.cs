using D47.Core.Callouts;
using Microsoft.Extensions.Logging;

namespace D47.App.Voice;

/// <summary>
/// Writes and speaks each tick's drained callouts. Quick lines are spoken in the order drained; urgent lines take
/// no place in the order, and a composed line or a named NPC's first line takes its place only once it is ready.
/// A region first is urgent and also holds a place ahead of the batch's ordered lines until it is spoken.
/// </summary>
internal sealed class CalloutDispatch : IDisposable
{
    private readonly CalloutOrder _order = new();
    private readonly Func<Announcement, Task<IReadOnlyList<Announcement>>> _write;
    private readonly Func<IReadOnlyList<Announcement>, Task> _speak;
    private readonly Func<Announcement, Task?> _nameAnswer;
    private readonly ILogger _logger;
    private int _urgentUnspoken;

    /// <param name="write">The lines one callout is spoken as.</param>
    /// <param name="speak">Takes the speaking turn and says the lines; never called while holding it.</param>
    /// <param name="nameAnswer">What a first line from a named NPC waits for before it is cast, or null.</param>
    public CalloutDispatch(
        Func<Announcement, Task<IReadOnlyList<Announcement>>> write,
        Func<IReadOnlyList<Announcement>, Task> speak,
        Func<Announcement, Task?> nameAnswer,
        ILogger logger)
    {
        _write = write;
        _speak = speak;
        _nameAnswer = nameAnswer;
        _logger = logger;
    }

    /// <summary>Whether an urgent line has been drained and not yet spoken.</summary>
    public bool UrgentUnspoken => Volatile.Read(ref _urgentUnspoken) > 0;

    /// <summary>The urgent lines, the composed lines, and the rest in the order drained.</summary>
    internal static (List<Announcement> Urgent, List<Announcement> Composed, List<Announcement> Ordered) Split(
        IEnumerable<Announcement> batch)
    {
        var urgent = new List<Announcement>();
        var composed = new List<Announcement>();
        var ordered = new List<Announcement>();

        foreach (var announcement in batch)
        {
            if (announcement.Urgency == CalloutUrgency.Urgent)
            {
                urgent.Add(announcement);
            }
            else if (LineWriter.IsComposed(announcement))
            {
                composed.Add(announcement);
            }
            else
            {
                ordered.Add(announcement);
            }
        }

        return (urgent, composed, ordered);
    }

    /// <summary>Starts writing and speaking one drained batch. Called on the tick thread; does not block.</summary>
    public Task Dispatch(IReadOnlyList<Announcement> pending)
    {
        var started = new List<Task>();
        var ready = new List<Announcement>();

        foreach (var announcement in pending)
        {
            if (_nameAnswer(announcement) is { } answer)
            {
                var urgent = announcement.Urgency == CalloutUrgency.Urgent ? 1 : 0;
                Interlocked.Add(ref _urgentUnspoken, urgent);
                started.Add(Task.Run(() => CountedAsync(() => HeldAsync(announcement, answer), urgent)));
            }
            else
            {
                ready.Add(announcement);
            }
        }

        var (urgentLines, composed, ordered) = Split(ready);

        var leading = urgentLines.Where(IsLeading).ToList();

        if (leading.Count > 0)
        {
            urgentLines = [.. urgentLines.Except(leading)];

            var ahead = _order.Take();
            Interlocked.Add(ref _urgentUnspoken, leading.Count);
            started.Add(Task.Run(() => CountedAsync(() => LeadingAsync(leading, ahead), leading.Count)));
        }

        if (ordered.Count > 0)
        {
            var place = _order.Take();
            started.Add(Task.Run(() => OrderedAsync(ordered, place)));
        }

        if (urgentLines.Count > 0)
        {
            Interlocked.Add(ref _urgentUnspoken, urgentLines.Count);
            started.Add(Task.Run(() => CountedAsync(() => UnorderedAsync(urgentLines), urgentLines.Count)));
        }

        foreach (var line in composed)
        {
            started.Add(Task.Run(() => ComposedAsync(line)));
        }

        return Task.WhenAll(started);
    }

    public void Dispose() => _order.Dispose();

    private async Task CountedAsync(Func<Task> speak, int urgent)
    {
        try
        {
            await speak().ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Add(ref _urgentUnspoken, -urgent);
        }
    }

    private async Task HeldAsync(Announcement line, Task answer)
    {
        await SpeakerAccent.WaitForNameAsync(answer).ConfigureAwait(false);

        if (line.Urgency == CalloutUrgency.Urgent)
        {
            await UnorderedAsync([line]).ConfigureAwait(false);
        }
        else if (LineWriter.IsComposed(line))
        {
            await ComposedAsync(line).ConfigureAwait(false);
        }
        else
        {
            await OrderedAsync([line], _order.Take()).ConfigureAwait(false);
        }
    }

    private async Task UnorderedAsync(IReadOnlyList<Announcement> pending)
    {
        var lines = await WriteAllAsync(pending).ConfigureAwait(false);

        if (lines.Count > 0)
        {
            await _speak(lines).ConfigureAwait(false);
        }
    }

    private static bool IsLeading(Announcement announcement) =>
        announcement.Key.StartsWith(RegionFirstCallout.KeyPrefix, StringComparison.Ordinal);

    /// <summary>Speaks urgent lines at once, and holds a place so the ordered lines taken after it wait for them.</summary>
    private async Task LeadingAsync(IReadOnlyList<Announcement> pending, CalloutOrder.Place place)
    {
        using (place)
        {
            await UnorderedAsync(pending).ConfigureAwait(false);
        }
    }

    private async Task OrderedAsync(IReadOnlyList<Announcement> pending, CalloutOrder.Place place)
    {
        using (place)
        {
            await SpeakInPlaceAsync(await WriteAllAsync(pending).ConfigureAwait(false), place).ConfigureAwait(false);
        }
    }

    private async Task ComposedAsync(Announcement line)
    {
        var lines = await WriteAllAsync([line]).ConfigureAwait(false);

        if (lines.Count == 0)
        {
            return;
        }

        using var place = _order.Take();
        await SpeakInPlaceAsync(lines, place).ConfigureAwait(false);
    }

    /// <summary>Waits for every earlier place, then takes the turn. The place is awaited outside the turn, never in it.</summary>
    private async Task SpeakInPlaceAsync(List<Announcement> lines, CalloutOrder.Place place)
    {
        if (lines.Count == 0)
        {
            return;
        }

        try
        {
            await place.Ready.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await _speak(lines).ConfigureAwait(false);
    }

    /// <summary>Writes the lines one after another: the writer keeps per-speaker state.</summary>
    private async Task<List<Announcement>> WriteAllAsync(IReadOnlyList<Announcement> pending)
    {
        var lines = new List<Announcement>(pending.Count);

        foreach (var announcement in pending)
        {
            try
            {
                lines.AddRange(await _write(announcement).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The callout {Key} could not be written", announcement.Key);
            }
        }

        return lines;
    }
}
