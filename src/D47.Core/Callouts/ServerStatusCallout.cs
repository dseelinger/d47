using D47.Core.Knowledge;

namespace D47.Core.Callouts;

/// <summary>Says when Elite's servers go down for maintenance, go offline or come back, once per change.</summary>
public sealed class ServerStatusCallout : ICallout
{
    public string Id => "server-status";

    public const string Key = "servers.state";

    /// <summary>The state to announce after the last poll, or null; null while the setting is off.</summary>
    public Func<GameServerState?> Announce { get; set; } = () => null;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || Announce() is not { } state)
        {
            yield break;
        }

        var text = state switch
        {
            GameServerState.Maintenance => "Elite's servers are down for maintenance.",
            GameServerState.Offline => "Elite's servers are offline.",
            GameServerState.Online => "Elite's servers are back up.",
            _ => null,
        };

        if (text is not null)
        {
            yield return new Announcement(Key, text) { Cooldown = TimeSpan.Zero };
        }
    }
}
