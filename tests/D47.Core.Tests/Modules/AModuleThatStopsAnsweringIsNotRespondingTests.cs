using D47.Core.Modules;
using Xunit;

namespace D47.Core.Tests.Modules;

public class AModuleThatStopsAnsweringIsNotRespondingTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AModuleThatHasJustStartedIsRunning()
    {
        var health = new ModuleResponsiveness(Start);

        Assert.Equal(ModuleStatus.Running, health.Status(Start));
        Assert.Equal(Start, health.LastAnswer);
    }

    [Fact]
    public void APingIsDueEveryFifteenSeconds()
    {
        var health = new ModuleResponsiveness(Start);

        Assert.False(health.PingDue(Start.AddSeconds(14)));
        Assert.True(health.PingDue(Start.AddSeconds(15)));

        health.PingSent(Start.AddSeconds(15));
        health.Answered(Start.AddSeconds(15.2));

        Assert.False(health.PingDue(Start.AddSeconds(29)));
        Assert.True(health.PingDue(Start.AddSeconds(30)));
    }

    [Fact]
    public void APingUnansweredForFiveSecondsMakesItNotResponding()
    {
        var health = new ModuleResponsiveness(Start);
        var sent = Start.AddSeconds(15);

        health.PingSent(sent);

        Assert.Equal(ModuleStatus.Running, health.Status(sent.AddSeconds(5)));
        Assert.Equal(ModuleStatus.NotResponding, health.Status(sent.AddSeconds(5.1)));
        Assert.Equal(Start, health.LastAnswer);
    }

    [Fact]
    public void ALaterAnswerMakesItRunningAgain()
    {
        var health = new ModuleResponsiveness(Start);

        health.PingSent(Start.AddSeconds(15));
        health.PingSent(Start.AddSeconds(30));

        Assert.Equal(ModuleStatus.NotResponding, health.Status(Start.AddSeconds(31)));

        health.Answered(Start.AddSeconds(31));

        Assert.Equal(ModuleStatus.Running, health.Status(Start.AddSeconds(31)));
        Assert.Equal(Start.AddSeconds(31), health.LastAnswer);
    }

    [Fact]
    public void ACallOutstandingForMoreThanTenSecondsMakesItNotResponding()
    {
        var health = new ModuleResponsiveness(Start);
        var call = health.CallStarted(Start.AddSeconds(1));

        Assert.Equal(ModuleStatus.Running, health.Status(Start.AddSeconds(11)));
        Assert.Equal(ModuleStatus.NotResponding, health.Status(Start.AddSeconds(11.1)));

        health.CallEnded(call);

        Assert.Equal(ModuleStatus.Running, health.Status(Start.AddSeconds(12)));
    }

    [Fact]
    public void AnAnswerDoesNotClearASlowCallStillOutstanding()
    {
        var health = new ModuleResponsiveness(Start);

        health.CallStarted(Start);
        health.Answered(Start.AddSeconds(20));

        Assert.Equal(ModuleStatus.NotResponding, health.Status(Start.AddSeconds(20)));
        Assert.Equal(Start.AddSeconds(20), health.LastAnswer);
    }
}
