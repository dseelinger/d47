using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>One reminder per session when it has run for the chosen number of hours (#641).</summary>
public sealed class SessionLengthCallout : ICallout
{
    public string Id => "session-length";

    public const string Key = "session.length";

    /// <summary>The session length that triggers the reminder, in hours.</summary>
    public Func<int> Hours { get; set; } = () => 4;

    private DateTimeOffset? _start;

    private bool _spoken;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "LoadGame":
                    _start = journalEvent.Timestamp;
                    _spoken = false;
                    break;

                case "Shutdown":
                    _start = null;
                    break;
            }
        }

        if (context.IsPriming || _spoken || _start is not { } start)
        {
            yield break;
        }

        var hours = Hours();
        if (context.Now - start < TimeSpan.FromHours(hours))
        {
            yield break;
        }

        _spoken = true;
        yield return new Announcement(Key, $"You have been flying {hours} hours, Commander.")
        {
            Urgency = CalloutUrgency.Routine,
            Cooldown = TimeSpan.Zero,
        };
    }
}
