using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A counted objective says its running total as it rises; tons and credits only at each quarter of the count.</summary>
public sealed class ACountedObjectiveSaysItsRunningTotalTests
{
    private const string Key = "the-lantern-route";

    private static readonly string AdventuresFile = Path.Combine(MemoryInstall.FakeRoot, "running-total", "adventures.json");

    private static Adventure Story(AdventureTrigger counted) => LanternRoute(Accepted) with
    {
        Beats =
        [
            Beat("The Count", "setup", counted, "Done."),
            Beat("The Lantern", "finale", new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" }, "Scoop here."),
        ],
    };

    private static AdventureCallout Wired(AdventureTrigger counted)
    {
        var store = new AdventureStore(AdventuresFile, new MemoryFileSystem(), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        Assert.Null(book.Write("F1", Story(counted)));
        book.CatchUp([]);

        return new AdventureCallout(book);
    }

    private static CalloutContext At(DateTimeOffset now, bool priming = false, params JournalEvent[] events) =>
        new(now, priming, new CommanderGameState(new CommanderIdentity("F1", "Tester")), GameStatus.Unknown, NavRoute.None, events);

    private static DateTimeOffset Minute(int minutes) => Accepted.AddMinutes(minutes);

    private static JournalEvent Bounty(int minutes) =>
        Event($$"""{ "timestamp":"{{Stamp(Minute(minutes))}}", "event":"Bounty", "Rewards":[{"Faction":"Federation","Reward":500}], "Target":"sidewinder", "TotalReward":500, "VictimFaction":"Pirates" }""");

    private static JournalEvent Refined(int minutes) =>
        Event($$"""{ "timestamp":"{{Stamp(Minute(minutes))}}", "event":"MiningRefined", "Type":"$platinum_name;", "Type_Localised":"Platinum" }""");

    private static List<Announcement> Progress(AdventureCallout callout, DateTimeOffset now, params JournalEvent[] events) =>
        [.. callout.Examine(At(now, false, events)).Where(line => line.Key.StartsWith(AdventureCallout.ProgressPrefix, StringComparison.Ordinal))];

    [Fact]
    public void TheThirdOfFiveBountiesIsSaidWhenItLands()
    {
        var callout = Wired(new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 5 });

        Assert.Equal("1 of 5 bounties collected.", Assert.Single(Progress(callout, Minute(1), Bounty(1))).Text);
        Assert.Equal("2 of 5 bounties collected.", Assert.Single(Progress(callout, Minute(2), Bounty(2))).Text);

        var third = Assert.Single(Progress(callout, Minute(3), Bounty(3)));

        Assert.Equal("3 of 5 bounties collected.", third.Text);
        Assert.Equal($"{AdventureCallout.ProgressPrefix}{Key}.3", third.Key);
        Assert.Equal(D47.Core.Audio.VoiceRole.ShipAi, third.Voice);
    }

    [Fact]
    public void TheBountyThatCompletesTheObjectiveSaysNoTotal()
    {
        var callout = Wired(new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 });

        Assert.Single(Progress(callout, Minute(1), Bounty(1)));
        Assert.Empty(Progress(callout, Minute(2), Bounty(2)));
        Assert.Empty(Progress(callout, Minute(3)));
    }

    [Fact]
    public void SeveralBountiesInOneTickAreSaidOnceWithTheNewestTotal()
    {
        var callout = Wired(new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 5 });

        Assert.Equal("3 of 5 bounties collected.", Assert.Single(Progress(callout, Minute(3), Bounty(1), Bounty(2), Bounty(3))).Text);
    }

    [Fact]
    public void TonsAreSaidAtEachQuarterOfTheCountOnly()
    {
        var callout = Wired(new AdventureTrigger { Kind = TriggerKind.Mine, Count = 8, Commodity = "platinum" });

        var said = Enumerable.Range(1, 7).Select(minute => Progress(callout, Minute(minute), Refined(minute)).SingleOrDefault()?.Text).ToList();

        Assert.Equal(
            [null, "2 of 8 t of platinum refined.", null, "4 of 8 t of platinum refined.", null, "6 of 8 t of platinum refined.", null],
            said);
    }

    [Fact]
    public void NothingIsSaidFromThePrimingBacklog()
    {
        var callout = Wired(new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 5 });

        Assert.Empty(callout.Examine(At(Minute(2), priming: true, Bounty(1), Bounty(2))));
        Assert.Empty(Progress(callout, Minute(3)));
    }

    [Theory]
    [InlineData(TriggerKind.Bounty, 5, 2, 3, true)]
    [InlineData(TriggerKind.Bounty, 5, 3, 3, false)]
    [InlineData(TriggerKind.Mine, 200, 49, 50, true)]
    [InlineData(TriggerKind.Mine, 200, 50, 51, false)]
    [InlineData(TriggerKind.Sell, 200, 149, 150, true)]
    [InlineData(TriggerKind.Contribute, 400, 190, 410, true)]
    public void OnlyAnIncreaseOrANewQuarterIsSpoken(TriggerKind kind, int count, int before, int after, bool spoken)
    {
        Assert.Equal(spoken, new AdventureTrigger { Kind = kind, Count = count }.SpeaksAt(before, after));
    }
}
