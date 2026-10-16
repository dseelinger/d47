using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class TheBgsTickIsAskedForOnlyWhileOnAndEliteRunsTests
{
    internal sealed class Source : IBgsTickSource
    {
        public int Calls { get; private set; }

        public Queue<Func<DateTimeOffset?>> Answers { get; } = new();

        public Task<DateTimeOffset?> LastTickAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Answers.TryDequeue(out var answer) ? answer() : null);
        }
    }

    internal sealed class Rig
    {
        public Source Source { get; } = new();

        public bool Enabled { get; set; } = true;

        public bool Running { get; set; } = true;

        public string? Stored { get; set; }

        public BgsTickWatch Watch()
        {
            var watch = new BgsTickWatch(
                Source,
                () => Enabled,
                () => Running,
                () => Stored,
                last => Stored = last,
                NullLogger<BgsTickWatch>.Instance);

            watch.Dispatch = work => work().GetAwaiter().GetResult();
            return watch;
        }
    }

    internal static readonly DateTimeOffset Start = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NothingIsAskedWhileTheSettingIsOff()
    {
        var rig = new Rig { Enabled = false };
        var watch = rig.Watch();

        watch.Poll(Start);
        watch.Poll(Start + TimeSpan.FromHours(1));

        Assert.Equal(0, rig.Source.Calls);
    }

    [Fact]
    public void NothingIsAskedWhileEliteIsNotRunning()
    {
        var rig = new Rig { Running = false };
        var watch = rig.Watch();

        watch.Poll(Start);
        watch.Poll(Start + TimeSpan.FromHours(1));

        Assert.Equal(0, rig.Source.Calls);
    }

    [Fact]
    public void ItAsksOnTheFirstTickAndThenOncePerInterval()
    {
        var rig = new Rig();
        var watch = rig.Watch();

        watch.Poll(Start);
        Assert.Equal(1, rig.Source.Calls);

        watch.Poll(Start + TimeSpan.FromSeconds(1));
        watch.Poll(Start + BgsTickWatch.Interval - TimeSpan.FromSeconds(1));
        Assert.Equal(1, rig.Source.Calls);

        watch.Poll(Start + BgsTickWatch.Interval);
        Assert.Equal(2, rig.Source.Calls);
    }

    [Fact]
    public void TurningTheSettingOnAsksOnTheNextTick()
    {
        var rig = new Rig();
        var watch = rig.Watch();

        watch.Poll(Start);
        rig.Enabled = false;
        watch.Poll(Start + TimeSpan.FromMinutes(1));
        rig.Enabled = true;
        watch.Poll(Start + TimeSpan.FromMinutes(2));

        Assert.Equal(2, rig.Source.Calls);
    }

    [Fact]
    public void EliteStartingLaterAsksOnItsFirstRunningTick()
    {
        var rig = new Rig { Running = false };
        var watch = rig.Watch();

        watch.Poll(Start);
        rig.Running = true;
        watch.Poll(Start + TimeSpan.FromSeconds(2));

        Assert.Equal(1, rig.Source.Calls);
    }

    [Fact]
    public void AFetchThatThrowsIsAskedAgainAtTheNextInterval()
    {
        var rig = new Rig();
        var watch = rig.Watch();
        rig.Source.Answers.Enqueue(() => throw new InvalidOperationException());

        watch.Poll(Start);
        watch.Poll(Start + TimeSpan.FromSeconds(1));
        watch.Poll(Start + BgsTickWatch.Interval);

        Assert.Equal(2, rig.Source.Calls);
    }

    [Fact]
    public void NoSecondFetchStartsWhileOneIsUnanswered()
    {
        var rig = new Rig();
        var watch = rig.Watch();
        var pending = new List<Func<Task>>();
        watch.Dispatch = pending.Add;

        watch.Poll(Start);
        watch.Poll(Start + BgsTickWatch.Interval);

        Assert.Single(pending);
    }
}

public class ABgsTickAnswerIsUsedOnlyInsideItsWindowTests
{
    private static readonly DateTimeOffset Start = TheBgsTickIsAskedForOnlyWhileOnAndEliteRunsTests.Start;

    private static DateTimeOffset? Answer(TimeSpan fromStart, string? stored = null)
    {
        var rig = new TheBgsTickIsAskedForOnlyWhileOnAndEliteRunsTests.Rig { Stored = stored };
        var watch = rig.Watch();
        rig.Source.Answers.Enqueue(() => Start + fromStart);

        watch.Poll(Start);
        watch.Poll(Start + TimeSpan.FromSeconds(1));

        return watch.Last;
    }

    [Fact]
    public void AnAnswerAnHourAgoIsHeld() =>
        Assert.Equal(Start - TimeSpan.FromHours(1), Answer(-TimeSpan.FromHours(1)));

    [Fact]
    public void AnAnswerUpToFiveMinutesAheadIsHeld() =>
        Assert.Equal(Start + TimeSpan.FromMinutes(5), Answer(TimeSpan.FromMinutes(5)));

    [Fact]
    public void AnAnswerMoreThanFiveMinutesAheadIsIgnored() =>
        Assert.Null(Answer(TimeSpan.FromMinutes(6)));

    [Fact]
    public void AnAnswerMoreThanSevenDaysAgoIsIgnored() =>
        Assert.Null(Answer(-TimeSpan.FromDays(8)));

    [Fact]
    public void AnAnswerEarlierThanTheOneHeldIsIgnored()
    {
        var held = Start - TimeSpan.FromHours(1);

        Assert.Equal(held, Answer(-TimeSpan.FromHours(2), BgsTickWatch.Format(held)));
    }

    [Fact]
    public void NoAnswerLeavesTheHeldTimeAlone()
    {
        var held = Start - TimeSpan.FromHours(1);
        var rig = new TheBgsTickIsAskedForOnlyWhileOnAndEliteRunsTests.Rig { Stored = BgsTickWatch.Format(held) };
        var watch = rig.Watch();

        watch.Poll(Start);
        watch.Poll(Start + TimeSpan.FromSeconds(1));

        Assert.Equal(held, watch.Last);
    }
}

public class TheBgsTickSurvivesARestartTests
{
    private static readonly DateTimeOffset Start = TheBgsTickIsAskedForOnlyWhileOnAndEliteRunsTests.Start;

    [Fact]
    public void AnAcceptedTimeIsStoredAndReadBackByTheNextWatch()
    {
        var rig = new TheBgsTickIsAskedForOnlyWhileOnAndEliteRunsTests.Rig();
        var tick = Start - TimeSpan.FromHours(3);
        rig.Source.Answers.Enqueue(() => tick);

        var first = rig.Watch();
        first.Poll(Start);
        first.Poll(Start + TimeSpan.FromSeconds(1));

        Assert.Equal("2026-10-16T09:00:00.0000000Z", rig.Stored);

        rig.Enabled = false;

        Assert.Equal(tick, rig.Watch().Last);
    }

    [Fact]
    public void AnUnreadableStoredTimeIsNoTime()
    {
        var rig = new TheBgsTickIsAskedForOnlyWhileOnAndEliteRunsTests.Rig { Stored = "yesterday" };

        Assert.Null(rig.Watch().Last);
    }
}
