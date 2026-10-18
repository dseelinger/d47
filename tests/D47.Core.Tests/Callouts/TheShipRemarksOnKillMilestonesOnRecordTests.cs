using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>A kill that is a round count, a first of its type or a record award is remarked on, once (#1278).</summary>
public class TheShipRemarksOnKillMilestonesOnRecordTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Bounty(DateTimeOffset at, string target, long reward) =>
        "{\"timestamp\":\"" + at.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",\"event\":\"Bounty\",\"Target\":\""
        + target + "\",\"TotalReward\":" + reward + ",\"VictimFaction\":\"Somebody\"}";

    private static string Bond(DateTimeOffset at, long reward) =>
        "{\"timestamp\":\"" + at.ToString("yyyy-MM-ddTHH:mm:ssZ") + "\",\"event\":\"FactionKillBond\",\"Reward\":"
        + reward + ",\"AwardingFaction\":\"A\",\"VictimFaction\":\"B\"}";

    /// <summary>
    /// <paramref name="earlier"/> kills on record, primed, then one tick of <paramref name="tick"/>. Earlier kills
    /// are 10 minutes apart so no session reason applies to them.
    /// </summary>
    private static List<Announcement> Tick(int earlier, Func<int, string> earlierKill, params string[] tick)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var callout = new KillCallout();

        for (var i = 0; i < earlier; i++)
        {
            var kill = Event(earlierKill(i).Replace("{T}", Start.AddMinutes(i * 10).ToString("yyyy-MM-ddTHH:mm:ssZ")));
            state.Apply(kill);
            _ = callout.Examine(Context(state, true, kill)).ToList();
        }

        var events = tick.Select(Event).ToList();

        foreach (var journalEvent in events)
        {
            state.Apply(journalEvent);
        }

        return [.. callout.Examine(Context(state, false, [.. events]))];
    }

    private static CalloutContext Context(CommanderGameState state, bool priming, params JournalEvent[] events)
    {
        state.Combat = state.Combat with { IsWhole = true };
        return new(events[0].Timestamp, priming, state, GameStatus.Unknown, NavRoute.None, events);
    }

    private static string Eagle(int i) => Bounty(Start, "eagle", 1000).Replace(Start.ToString("yyyy-MM-ddTHH:mm:ssZ"), "{T}");

    private static DateTimeOffset After(int earlier) => Start.AddMinutes(earlier * 10);

    [Fact]
    public void TheHundredthKillOnRecordSaysSo()
    {
        var said = Assert.Single(Tick(99, Eagle, Bounty(After(99), "eagle", 1000)));

        Assert.Equal(KillCallout.RecordKey, said.Key);
        Assert.StartsWith("Kill number 100 on record. ", said.Text);
    }

    [Fact]
    public void TheHundredAndFirstKillDoesNotSpeakRecord()
    {
        var said = Tick(100, Eagle, Bounty(After(100), "eagle", 1000));

        Assert.DoesNotContain(said, announcement => announcement.Key == KillCallout.RecordKey);
    }

    [Fact]
    public void OnlyTheHundredthOfTwoKillsInOneTickGetsTheCountMilestone()
    {
        var said = Tick(99, Eagle, Bounty(After(99), "eagle", 1000), Bounty(After(99).AddSeconds(1), "eagle", 1000));

        Assert.Equal(KillCallout.RecordKey, said[0].Key);
        Assert.StartsWith("Kill number 100 on record. ", said[0].Text);
        Assert.DoesNotContain(said.Skip(1), announcement => announcement.Key == KillCallout.RecordKey);
    }

    [Fact]
    public void TwoKillsInTheSameSecondAreMatchedInOrder()
    {
        var said = Tick(98, Eagle, Bounty(After(98), "eagle", 1000), Bounty(After(98), "eagle", 1000));

        var record = Assert.Single(said, announcement => announcement.Key == KillCallout.RecordKey);

        Assert.StartsWith("Kill number 100 on record. ", record.Text);
    }

    [Fact]
    public void TheFirstOfATypeOnRecordSaysSo()
    {
        var said = Assert.Single(Tick(3, Eagle, Bounty(After(3), "anaconda", 1000)));

        Assert.Equal(KillCallout.RecordKey, said.Key);
        Assert.StartsWith("First Anaconda on record. ", said.Text);
    }

    [Fact]
    public void TheSecondOfATypeDoesNot()
    {
        var said = Tick(3, i => i == 2 ? Bounty(Start, "anaconda", 1000).Replace(Start.ToString("yyyy-MM-ddTHH:mm:ssZ"), "{T}") : Eagle(i),
            Bounty(After(3), "anaconda", 1000));

        Assert.DoesNotContain(said, announcement => announcement.Key == KillCallout.RecordKey);
    }

    [Fact]
    public void ABondIsNeverTheFirstOfItsType()
    {
        var said = Tick(3, Eagle, Bond(After(3), 1000));

        Assert.DoesNotContain(said, announcement => announcement.Key == KillCallout.RecordKey);
    }

    [Fact]
    public void ARewardAboveTenEarlierKillsIsTheBiggestOnRecord()
    {
        var said = Assert.Single(Tick(10, Eagle, Bounty(After(10), "eagle", 5000)));

        Assert.Equal(KillCallout.RecordKey, said.Key);
        Assert.StartsWith("Biggest reward on record. ", said.Text);
    }

    [Fact]
    public void ARewardAboveOnlyFiveEarlierKillsIsNotARecord()
    {
        var said = Tick(5, Eagle, Bounty(After(5), "eagle", 5000));

        Assert.DoesNotContain(said, announcement => announcement.Key == KillCallout.RecordKey);
    }

    [Fact]
    public void TheCountMilestoneWinsOverAFirstOfItsType()
    {
        var said = Assert.Single(Tick(99, Eagle, Bounty(After(99), "anaconda", 5000)));

        Assert.StartsWith("Kill number 100 on record. ", said.Text);
    }

    [Fact]
    public void WithoutAWholeRecordTheMilestonesAreNotSpoken()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var kill = Event(Bounty(Start, "anaconda", 1000));
        state.Apply(kill);

        var said = new KillCallout().Examine(
            new CalloutContext(Start, false, state, GameStatus.Unknown, NavRoute.None, [kill])).ToList();

        Assert.Equal(KillCallout.FirstKey, Assert.Single(said).Key);
    }

    [Fact]
    public void NothingSpeaksWhilePriming()
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Fixture"));
        var kill = Event(Bounty(Start, "anaconda", 1000));
        state.Apply(kill);

        Assert.Empty(new KillCallout().Examine(Context(state, true, kill)));
    }
}
