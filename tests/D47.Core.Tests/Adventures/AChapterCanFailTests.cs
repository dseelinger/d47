using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A beat's failsOn trigger ends the chapter as failed: the fold finds it, the book stores it, the ship says it once.</summary>
public sealed class AChapterCanFailTests
{
    private const string Key = "the-lantern-route";

    private static readonly string Folder = Path.Combine(MemoryInstall.FakeRoot, "chapter-fails");
    private static readonly string AdventuresFile = Path.Combine(Folder, "adventures.json");

    private static readonly AdventureTrigger Bounty = new() { Kind = TriggerKind.Bounty };

    private readonly MemoryFileSystem _files = new();
    private AdventureBook _book;

    public AChapterCanFailTests()
    {
        _book = Book();
    }

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private AdventureBook Book() =>
        new(new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance), NullLogger<AdventureBook>.Instance);

    private static Adventure Failing(int beat, params AdventureTrigger[] fails)
    {
        var route = LanternRoute(Accepted);

        return route with { Beats = [.. route.Beats.Select((each, index) => index == beat ? each with { FailsOn = fails } : each)] };
    }

    private static JournalEvent BountyAt(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Bounty", "Rewards":[{"Faction":"Federation","Reward":500}], "Target":"sidewinder", "TotalReward":500, "VictimFaction":"Pirates" }""");

    private AdventureStanding Begun(Adventure adventure)
    {
        Assert.Null(_book.Write("F1", adventure));
        _book.CatchUp([]);
        return _book.Standing("F1", Key)!;
    }

    private static CalloutContext Context(DateTimeOffset now, JournalEvent journalEvent) =>
        new(now, false, new CommanderGameState(new CommanderIdentity("F1", "Tester")), GameStatus.Unknown, NavRoute.None, [journalEvent]);

    [Fact]
    public void AMatchOnTheCurrentBeatFailsTheChapter()
    {
        Begun(Failing(0, Bounty));

        _book.Observe(BountyAt(At(1)), "F1");

        var standing = _book.Standing("F1", Key)!;
        Assert.True(standing.IsFailed);
        Assert.False(standing.IsDone);
        Assert.Equal(0, standing.Failure!.Beat);
        Assert.Equal(At(1), standing.Failure.At);
        Assert.Contains("The Lantern", standing.Failure.Reason);
        Assert.Contains("collect one bounty", standing.Failure.Reason);
        Assert.Empty(_book.Active("F1"));
        Assert.Equal("failed", standing.Place());
    }

    [Fact]
    public void AMatchOnABeatThatIsNotCurrentDoesNotFailIt()
    {
        Begun(Failing(1, Bounty));

        _book.Observe(BountyAt(At(1)), "F1");

        Assert.False(_book.Standing("F1", Key)!.IsFailed);
    }

    [Fact]
    public void AMatchBeforeARewriteDoesNotFailIt()
    {
        Begun(LanternRoute(Accepted));

        var beats = LanternRoute().Beats.Select(each => each with { FailsOn = [Bounty] }).ToList();
        Assert.Null(_book.ReplaceBeats("F1", Key, 0, beats, At(10)));

        _book.Observe(BountyAt(At(5)), "F1");
        Assert.False(_book.Standing("F1", Key)!.IsFailed);

        _book.Observe(BountyAt(At(11)), "F1");
        Assert.True(_book.Standing("F1", Key)!.IsFailed);
    }

    [Fact]
    public void AnEventThatFiresTheBeatAndMatchesItsFailsOnFailsTheChapter()
    {
        Begun(Failing(0, new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Lantern }));

        _book.Observe(Jump(Lantern, At(1)), "F1");

        var standing = _book.Standing("F1", Key)!;
        Assert.True(standing.IsFailed);
        Assert.Empty(standing.Fired);
    }

    [Fact]
    public void ValidationRefusesACountAndTheConflictKind()
    {
        var counted = AdventureValidation.Problems(Failing(0, Bounty with { Count = 3 }));
        Assert.Contains(counted, problem => problem.Contains("failsOn with a count", StringComparison.Ordinal));

        var conflict = AdventureValidation.Problems(Failing(0, new AdventureTrigger { Kind = TriggerKind.Conflict }));
        Assert.Contains(conflict, problem => problem.Contains("conflict kind", StringComparison.Ordinal));

        Assert.Empty(AdventureValidation.Problems(Failing(0, Bounty)));
    }

    [Fact]
    public void TheFailedStateSurvivesASaveAndAReload()
    {
        Begun(Failing(0, Bounty));
        _book.Observe(BountyAt(At(1)), "F1");

        Assert.Null(_book.StoreEnding("F1", Key));

        _book = Book();
        _book.Store.Poll();
        _book.CatchUp([]);

        var stored = _book.Store.Find("F1", Key)!;
        Assert.Equal(AdventureEnding.Failed, stored.Ending!.Name);
        Assert.Equal(At(1), stored.Ending.At);
        Assert.False(stored.IsActive);
        Assert.Equal([Bounty], stored.Beats[0].FailsOn);

        var standing = _book.Standing("F1", Key)!;
        Assert.True(standing.IsFailed);
        Assert.Contains("failed", standing.Describe(At(2)));
        Assert.Contains(stored.Ending.Reason, standing.Describe(At(2)));
        Assert.Empty(_book.Active("F1"));
    }

    [Fact]
    public void CatchUpFailsAChapterOnceAndQueuesNoLine()
    {
        Begun(Failing(0, Bounty));
        var path = Path.Combine(Folder, "Journal.2026-08-22T190000.01.log");
        _files.WriteText(path, Commander("F1", Accepted.AddMinutes(-1)).Raw.GetRawText() + "\n" + BountyAt(At(2)).Raw.GetRawText() + "\n");

        _book.WalkAgain();
        _book.CatchUp([path]);

        Assert.Single(_book.Unstored("F1"));
        Assert.Empty(_book.Drain());

        Assert.Null(_book.StoreEnding("F1", Key));
        Assert.Empty(_book.Unstored("F1"));

        _book.WalkAgain();
        _book.CatchUp([path]);

        Assert.Empty(_book.Unstored("F1"));
        Assert.Empty(_book.Drain());
        Assert.StartsWith("The Lantern Route: failed.", _book.Standing("F1", Key)!.Describe(At(3)), StringComparison.Ordinal);
    }

    [Fact]
    public void ALiveFailureDropsTheQueuedLinesAndQueuesOneFailureLine()
    {
        Begun(Failing(1, Bounty));

        _book.Observe(Jump(Lantern, At(1)), "F1");
        Assert.True(_book.IsStirring("F1", Key));

        _book.Observe(BountyAt(At(2)), "F1");

        var moment = Assert.Single(_book.Drain());
        Assert.True(moment.IsFailure);
        Assert.Contains("The Survey", moment.Reason);
        Assert.False(_book.IsStirring("F1", Key));

        _book.Observe(BountyAt(At(3)), "F1");
        Assert.Empty(_book.Drain());
    }

    [Fact]
    public void TheShipSaysAFailureAndAStockCoresNarratorTellsItInTheThirdPerson()
    {
        Begun(Failing(0, Bounty));
        var callout = new AdventureCallout(_book);

        var said = Assert.Single(callout.Examine(Context(At(1), BountyAt(At(1)))));
        Assert.StartsWith("This chapter has failed.", said.Text, StringComparison.Ordinal);
        Assert.Equal(D47.Core.Audio.VoiceRole.ShipAi, said.Voice);
        Assert.StartsWith(AdventureCallout.FailedPrefix, said.Key, StringComparison.Ordinal);

        _book = Book();
        Begun(Failing(0, Bounty));
        callout = new AdventureCallout(_book) { StockCoreAboard = () => true };

        said = Assert.Single(callout.Examine(Context(At(1), BountyAt(At(1)))));
        Assert.StartsWith("The chapter failed.", said.Text, StringComparison.Ordinal);
        Assert.Equal(D47.Core.Audio.VoiceRole.Narrator, said.Voice);
        Assert.DoesNotContain("you", said.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AFailedShortStoryOffersTheNextChapterAndRemoval()
    {
        Begun(Failing(0, Bounty));
        _book.Observe(BountyAt(At(1)), "F1");
        Assert.Null(_book.StoreEnding("F1", Key));

        Assert.Equal(["Write the next chapter", "Remove"], _book.Standing("F1", Key)!.ReadingButtons());
    }

    [Fact]
    public void AFailedStoryChapterOffersNothing()
    {
        Begun(Failing(0, Bounty) with { StoryId = "a-story" });
        _book.Observe(BountyAt(At(1)), "F1");
        Assert.Null(_book.StoreEnding("F1", Key));

        Assert.Empty(_book.Standing("F1", Key)!.ReadingButtons());
    }
}
