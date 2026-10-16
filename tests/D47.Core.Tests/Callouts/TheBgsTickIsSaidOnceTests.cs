using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class TheBgsTickIsSaidOnceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-16T12:00:00Z");

    private static CalloutContext Context(DateTimeOffset now, bool priming = false) =>
        new(now, priming, null, GameStatus.Unknown, NavRoute.None, []);

    private static (BgsTickCallout Callout, Func<string?> Saved) Callout(DateTimeOffset? tick, string? stored = null)
    {
        var saved = stored;
        var callout = new BgsTickCallout
        {
            LastTick = () => tick,
            LastSaidTick = () => saved,
            RememberSaidTick = value => saved = value,
        };

        return (callout, () => saved);
    }

    private static string[] Said(BgsTickCallout callout, DateTimeOffset? now = null) =>
        callout.Examine(Context(now ?? Now)).Select(a => a.Text).ToArray();

    [Fact]
    public void ATickThreeMinutesOldIsSaidOnceAndNotAgain()
    {
        var (callout, _) = Callout(Now.AddMinutes(-3));

        Assert.Equal(["The BGS has ticked."], Said(callout));
        Assert.Empty(Said(callout, Now.AddSeconds(1)));
    }

    [Fact]
    public void ATickIsNotSaidAgainAfterARestart()
    {
        var (first, saved) = Callout(Now.AddMinutes(-3));
        Said(first);

        var (restarted, _) = Callout(Now.AddMinutes(-3), saved());

        Assert.Empty(Said(restarted));
    }

    [Fact]
    public void ATickThirtyEightMinutesOldSaysHowLongAgo()
    {
        var (callout, _) = Callout(Now.AddMinutes(-38));

        Assert.Equal(["The BGS ticked forty minutes ago."], Said(callout));
    }

    [Fact]
    public void ATickThreeHoursOldIsRecordedAndNotSaid()
    {
        var (callout, saved) = Callout(Now.AddHours(-3));

        Assert.Empty(Said(callout));
        Assert.NotNull(saved());
    }

    [Fact]
    public void ATickTwentyMinutesAfterTheOneSaidIsTheSameTick()
    {
        var (callout, _) = Callout(Now.AddMinutes(-3));
        Said(callout);

        var (revised, _) = Callout(Now.AddMinutes(-3).AddMinutes(20), callout.LastSaidTick!());

        Assert.Empty(Said(revised));
    }

    [Fact]
    public void ATickMoreThanAnHourAfterTheOneSaidIsSaid()
    {
        var earlier = Now.AddHours(-30);
        var (callout, _) = Callout(Now.AddMinutes(-3), earlier.UtcDateTime.ToString("O"));

        Assert.Equal(["The BGS has ticked."], Said(callout));
    }

    [Fact]
    public void NothingIsSaidWhilePrimingOrWithoutATick()
    {
        var (callout, _) = Callout(Now.AddMinutes(-3));
        Assert.Empty(callout.Examine(Context(Now, priming: true)));

        var (none, _) = Callout(null);
        Assert.Empty(Said(none));
    }

    [Fact]
    public void WithTheRowOffNothingIsSaid()
    {
        var (callout, _) = Callout(Now.AddMinutes(-3));
        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance);
        engine.Add(callout);
        engine.SetEnabled("bgs-tick", false);

        engine.Tick(Context(Now));

        Assert.Empty(engine.Drain());
    }

    [Fact]
    public void TheRowIsOnByDefault() => Assert.True(new CalloutSettings().BgsTick);
}
