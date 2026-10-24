using System.Globalization;
using D47.Core.Conversation;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Turns spoken through either push-to-talk, and the comms selections they are spoken to.</summary>
public static class OnComms
{
    public static CommsSelection Captain { get; } = new(CommsChannel.Carrier, new CommsSubChannel(CommsChannels.CaptainKey, "Captain"));

    public static CommsSelection Tower { get; } = new(CommsChannel.Carrier, new CommsSubChannel(CommsChannels.TowerKey, "Tower"));

    public static CommsSelection Narrator { get; } = new(CommsChannel.Story, new CommsSubChannel(CommsChannels.NarratorKey, "Narrator"));

    public static CommsSelection Target(string name) => new(CommsChannel.Target, new CommsSubChannel(CommsChannels.TargetKey, name));

    public static CommsSelection System(string name) => new(CommsChannel.System, new CommsSubChannel(name, name));

    public static CommsSelection Cast(string castId, string name) => new(CommsChannel.Story, new CommsSubChannel(castId, name));

    public static CommsSelection Crew(long crewId, string name) =>
        new(CommsChannel.Crew, new CommsSubChannel(crewId.ToString(CultureInfo.InvariantCulture), name));

    public static CommsSelection Character(string id, string name) => new(CommsChannel.Characters, new CommsSubChannel(id, name));

    public static CommsSelection NoOne(CommsChannel channel) => new(channel, SubChannel: null);

    /// <summary>A turn spoken through the comms push-to-talk.</summary>
    public static Task<List<TurnEvent>> SayAsync(TurnLoop loop, string input) => RunAsync(loop, input, PttTarget.Comms);

    /// <summary>A turn spoken through the primary push-to-talk.</summary>
    public static Task<List<TurnEvent>> AskAsync(TurnLoop loop, string input) => RunAsync(loop, input, PttTarget.Core);

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input, PttTarget target)
    {
        ArgumentNullException.ThrowIfNull(loop);

        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, InputSource.Spoken, target, TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }
}
