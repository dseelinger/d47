using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class ServerStateChangesAreSaidTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class Source : IServerStatusSource
    {
        public GameServerState Next { get; set; }

        public Task<GameServerState> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Next);
    }

    /// <summary>The lines said as the watch is fed these answers one minute apart.</summary>
    private static List<string> Said(params GameServerState[] answers)
    {
        var source = new Source();
        var watch = new ServerStatusWatch(source, () => true, NullLogger<ServerStatusWatch>.Instance)
        {
            Dispatch = work => work().GetAwaiter().GetResult(),
        };
        var callout = new ServerStatusCallout { Announce = () => watch.Announce };
        var said = new List<string>();
        var now = Start;

        foreach (var answer in answers)
        {
            source.Next = answer;
            watch.Poll(now);
            watch.Poll(now + TimeSpan.FromSeconds(1));
            said.AddRange(callout.Examine(Context(now + TimeSpan.FromSeconds(1))).Select(a => a.Text));
            now += TimeSpan.FromMinutes(11);
        }

        return said;
    }

    private static CalloutContext Context(DateTimeOffset now) =>
        new(now, false, null, GameStatus.Unknown, NavRoute.None, []);

    [Fact]
    public void MaintenanceAfterOnlineIsSaid() =>
        Assert.Equal(
            ["Elite's servers are down for maintenance."],
            Said(GameServerState.Online, GameServerState.Maintenance));

    [Fact]
    public void MaintenanceAsTheFirstAnswerIsSaid() =>
        Assert.Equal(["Elite's servers are down for maintenance."], Said(GameServerState.Maintenance));

    [Fact]
    public void OfflineAfterOnlineOrAsTheFirstAnswerIsSaid()
    {
        Assert.Equal(["Elite's servers are offline."], Said(GameServerState.Online, GameServerState.Offline));
        Assert.Equal(["Elite's servers are offline."], Said(GameServerState.Offline));
    }

    [Fact]
    public void BackUpIsSaidOnlyAfterMaintenanceOrOffline()
    {
        Assert.Equal(
            ["Elite's servers are down for maintenance.", "Elite's servers are back up."],
            Said(GameServerState.Maintenance, GameServerState.Online));
        Assert.Equal(
            ["Elite's servers are offline.", "Elite's servers are back up."],
            Said(GameServerState.Offline, GameServerState.Online));
        Assert.Empty(Said(GameServerState.Online));
    }

    [Fact]
    public void UnknownIsNeverSaidAndResetsNothing()
    {
        Assert.Empty(Said(GameServerState.Unknown, GameServerState.Unknown));
        Assert.Equal(
            ["Elite's servers are down for maintenance."],
            Said(GameServerState.Online, GameServerState.Unknown, GameServerState.Maintenance));
        Assert.Equal(
            ["Elite's servers are offline."],
            Said(GameServerState.Offline, GameServerState.Unknown, GameServerState.Offline));
    }

    [Fact]
    public void ARepeatedStateIsSaidOnce() =>
        Assert.Equal(
            ["Elite's servers are down for maintenance."],
            Said(GameServerState.Online, GameServerState.Maintenance, GameServerState.Maintenance));

    [Fact]
    public void AMoveBetweenMaintenanceAndOfflineIsNotSaid()
    {
        Assert.Equal(
            ["Elite's servers are down for maintenance."],
            Said(GameServerState.Maintenance, GameServerState.Offline));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        var callout = new ServerStatusCallout { Announce = () => GameServerState.Maintenance };

        Assert.Empty(callout.Examine(Context(Start) with { IsPriming = true }));
    }

    [Fact]
    public void TheRowIsOnByDefaultAndTheFetchOffByDefault()
    {
        Assert.True(new CalloutSettings().ServerStatus);
        Assert.False(new KnowledgeSettings().ServerStatus);
    }
}
