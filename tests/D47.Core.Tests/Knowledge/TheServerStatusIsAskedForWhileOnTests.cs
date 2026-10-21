using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheServerStatusIsAskedForWhileOnTests
{
    private sealed class Source : IServerStatusSource
    {
        public int Calls { get; private set; }

        public Queue<GameServerState> Answers { get; } = new();

        public Task<GameServerState> ReadAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Answers.TryDequeue(out var answer) ? answer : GameServerState.Unknown);
        }
    }

    private sealed class Rig
    {
        public Source Source { get; } = new();

        public bool Enabled { get; set; } = true;

        public ServerStatusWatch Watch { get; }

        public Rig()
        {
            Watch = new ServerStatusWatch(Source, () => Enabled, NullLogger<ServerStatusWatch>.Instance)
            {
                Dispatch = work => work().GetAwaiter().GetResult(),
            };
        }
    }

    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NothingIsAskedWhileTheSettingIsOff()
    {
        var rig = new Rig { Enabled = false };

        rig.Watch.Poll(Start);
        rig.Watch.Poll(Start + TimeSpan.FromHours(1));

        Assert.Equal(0, rig.Source.Calls);
        Assert.Null(rig.Watch.Shown);
    }

    [Fact]
    public void ItAsksOnTheFirstPollWhetherOrNotEliteRuns()
    {
        var rig = new Rig();
        rig.Source.Answers.Enqueue(GameServerState.Online);

        rig.Watch.Poll(Start);

        Assert.Equal(1, rig.Source.Calls);
    }

    [Fact]
    public void AfterOnlineItAsksEveryTenMinutes()
    {
        var rig = new Rig();
        rig.Source.Answers.Enqueue(GameServerState.Online);
        rig.Source.Answers.Enqueue(GameServerState.Online);

        rig.Watch.Poll(Start);
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(1));
        Assert.Equal(GameServerState.Online, rig.Watch.Shown);

        rig.Watch.Poll(Start + TimeSpan.FromMinutes(9));
        Assert.Equal(1, rig.Source.Calls);

        rig.Watch.Poll(Start + TimeSpan.FromMinutes(10));
        Assert.Equal(2, rig.Source.Calls);
    }

    [Theory]
    [InlineData(GameServerState.Maintenance)]
    [InlineData(GameServerState.Offline)]
    [InlineData(GameServerState.Unknown)]
    public void AfterAnythingElseItAsksEveryMinute(GameServerState first)
    {
        var rig = new Rig();
        rig.Source.Answers.Enqueue(first);

        rig.Watch.Poll(Start);
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(1));
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(59));
        Assert.Equal(1, rig.Source.Calls);

        rig.Watch.Poll(Start + TimeSpan.FromMinutes(1));
        Assert.Equal(2, rig.Source.Calls);
    }

    [Fact]
    public void ItDoesNotAskAgainWhileAFetchIsOutstanding()
    {
        var rig = new Rig();
        var held = new List<Func<Task>>();
        rig.Watch.Dispatch = held.Add;

        rig.Watch.Poll(Start);
        rig.Watch.Poll(Start + TimeSpan.FromMinutes(5));

        Assert.Single(held);
    }

    [Fact]
    public void TheReadoutFollowsTheLatestAnswerIncludingUnknown()
    {
        var rig = new Rig();
        var shown = new List<GameServerState?>();
        rig.Watch.ShownChanged += shown.Add;
        rig.Source.Answers.Enqueue(GameServerState.Online);
        rig.Source.Answers.Enqueue(GameServerState.Unknown);

        rig.Watch.Poll(Start);
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(1));
        rig.Watch.Poll(Start + TimeSpan.FromMinutes(10));
        rig.Watch.Poll(Start + TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        Assert.Equal([GameServerState.Online, GameServerState.Unknown], shown);
    }

    [Fact]
    public void TurningTheSettingOffClearsTheReadoutAndOnAsksAgain()
    {
        var rig = new Rig();
        var shown = new List<GameServerState?>();
        rig.Watch.ShownChanged += shown.Add;
        rig.Source.Answers.Enqueue(GameServerState.Online);
        rig.Source.Answers.Enqueue(GameServerState.Maintenance);

        rig.Watch.Poll(Start);
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(1));
        rig.Enabled = false;
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(2));
        rig.Enabled = true;
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(3));
        rig.Watch.Poll(Start + TimeSpan.FromSeconds(4));

        Assert.Equal(2, rig.Source.Calls);
        Assert.Equal([GameServerState.Online, null, GameServerState.Maintenance], shown);
    }
}
