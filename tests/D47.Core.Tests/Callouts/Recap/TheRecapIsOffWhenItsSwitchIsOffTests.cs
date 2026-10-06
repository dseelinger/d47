using D47.Core.Callouts;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts.Recap;

/// <summary>The recap's switch silences the line and takes its egress entry with it.</summary>
public sealed class TheRecapIsOffWhenItsSwitchIsOffTests
{
    private static readonly DateTimeOffset T0 = new(3312, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static CalloutContext At(DateTimeOffset now) =>
        new(now, false, null, GameStatus.Unknown, NavRoute.None, []);

    private static IReadOnlyList<Announcement> Said(bool enabled)
    {
        var recap = new RecapCallout();
        recap.Prepare = _ => recap.Supply("Last session you finished in Deciat.");

        var engine = new CalloutEngine(NullLogger<CalloutEngine>.Instance).Add(recap);
        engine.SetEnabled(recap.Id, enabled, T0);

        engine.Tick(At(T0));
        engine.Tick(At(T0 + TimeSpan.FromSeconds(30)));

        return engine.Drain();
    }

    private static EgressEntry Entry(bool recap) =>
        EgressDisclosure.Entry(
            EgressDisclosure.Recap,
            new D47Settings { Callouts = new CalloutSettings { Recap = recap } },
            llmKeyPresent: true);

    [Fact]
    public void OnItIsSaidOnce() =>
        Assert.Equal(RecapCallout.Key, Assert.Single(Said(enabled: true)).Key);

    [Fact]
    public void OffItIsNotSaid() => Assert.Empty(Said(enabled: false));

    [Fact]
    public void TheEgressEntryIsActiveOnlyWhileTheSwitchIsOn()
    {
        Assert.True(Entry(recap: true).Active);
        Assert.False(Entry(recap: false).Active);
    }

    [Fact]
    public void TheEgressEntrySaysTheBackstoryIsNotSent() =>
        Assert.Contains("Backstory", Entry(recap: true).What, StringComparison.Ordinal);
}
