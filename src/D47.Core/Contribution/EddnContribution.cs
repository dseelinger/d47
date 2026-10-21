using D47.Core.Journal;

namespace D47.Core.Contribution;

/// <summary>Folds journal events into the composer and posts the live messages they call for.</summary>
public sealed class EddnContribution(EddnComposer composer, IEddnSink sink, Func<bool> enabled)
{
    /// <summary>The oldest an event may be, against the tick's clock, and still be sent.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    /// <summary>Every event is folded; a message is built only for a live event, with the setting on, while the shown Commander flies.</summary>
    public void Apply(IReadOnlyList<JournalEvent> events, DateTimeOffset now, bool isFirst, bool offDuty)
    {
        var send = !isFirst && !offDuty && enabled();

        foreach (var journalEvent in events)
        {
            if (send && now - journalEvent.Timestamp <= MaxAge)
            {
                if (composer.Compose(journalEvent) is { } envelope)
                {
                    sink.Post(envelope);
                }
            }
            else
            {
                composer.Observe(journalEvent);
            }
        }
    }
}
