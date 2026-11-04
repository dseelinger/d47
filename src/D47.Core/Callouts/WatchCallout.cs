using D47.Core.Watches;

namespace D47.Core.Callouts;

/// <summary>Says each line a background watch made ready in the last poll.</summary>
public sealed class WatchCallout : ICallout
{
    public string Id => "watches";

    /// <summary>The announcement key of one watch's line.</summary>
    public static string Key(Guid watchId) => $"watch.{watchId}";

    /// <summary>The lines made ready in the last poll; empty while Galaxy search or BGS tick time is off.</summary>
    public Func<IReadOnlyList<WatchNotice>> Ready { get; set; } = () => [];

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming)
        {
            yield break;
        }

        foreach (var notice in Ready())
        {
            yield return new Announcement(Key(notice.WatchId), notice.Text) { Cooldown = TimeSpan.Zero };
        }
    }
}
