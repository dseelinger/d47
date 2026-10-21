using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A beat holds a list of steps and fires with its last one; each step has its own trigger, failsOn and count.</summary>
public sealed class ABeatCanHoldSeveralStepsTests
{
    private const string Key = "the-lantern-route";

    private static readonly string Folder = Path.Combine(MemoryInstall.FakeRoot, "beat-steps");
    private static readonly string AdventuresFile = Path.Combine(Folder, "adventures.json");

    private static readonly AdventureTrigger Bounty = new() { Kind = TriggerKind.Bounty, Count = 1 };

    private readonly MemoryFileSystem _files = new();
    private AdventureBook _book;

    public ABeatCanHoldSeveralStepsTests()
    {
        _book = Book();
    }

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private AdventureBook Book() =>
        new(new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance), NullLogger<AdventureBook>.Instance);

    private static JournalEvent BountyAt(DateTimeOffset at) =>
        Event($$"""{ "timestamp":"{{Stamp(at)}}", "event":"Bounty", "Rewards":[{"Faction":"Federation","Reward":500}], "Target":"sidewinder", "TotalReward":500, "VictimFaction":"Pirates" }""");

    private static readonly AdventureTrigger Arrive = new() { Kind = TriggerKind.Arrive, SystemAddress = Lantern, System = "Ossen's Lantern" };

    private static readonly AdventureTrigger Dock = new() { Kind = TriggerKind.Dock, MarketId = Anchorage, System = "Dyson's Hollow", Station = "Maren Anchorage" };

    /// <summary>The Lantern Route with its first beat taking three steps: arrive, collect a bounty, dock.</summary>
    private static Adventure ThreeSteps(DateTimeOffset? acceptedAt = null)
    {
        var route = LanternRoute(acceptedAt ?? Accepted);

        return route with
        {
            Beats =
            [
                route.Beats[0] with { Steps = [new BeatStep { Trigger = Arrive }, new BeatStep { Trigger = Bounty }, new BeatStep { Trigger = Dock }] },
                .. route.Beats.Skip(1),
            ],
        };
    }

    private AdventureStanding Begun(Adventure adventure)
    {
        Assert.Null(_book.Write("F1", adventure));
        _book.CatchUp([]);
        return _book.Standing("F1", Key)!;
    }

    private static CalloutContext Context(DateTimeOffset now, params JournalEvent[] events) =>
        new(now, false, new CommanderGameState(new CommanderIdentity("F1", "Tester")), GameStatus.Unknown, NavRoute.None, events);

    [Fact]
    public void AnOldOneTriggerBeatLoadsAsOneStepCarryingTheBeatsFailsOn()
    {
        _files.WriteText(AdventuresFile, $$"""
            {"commanders":[{"frontierId":"F1","adventures":[{
              "key":"{{Key}}","name":"The Lantern Route",
              "beats":[{"title":"The Lantern","function":"setup","reason":"Someone is paying.",
                "trigger":{"kind":"arrive","systemAddress":{{Lantern}},"system":"Ossen's Lantern"},
                "failsOn":[{"kind":"bounty"}],"line":"Scoop here."}]}]}]}
            """);

        var store = new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance);
        store.Poll();

        var beat = Assert.Single(store.Find("F1", Key)!.Beats);
        var step = Assert.Single(beat.Steps);
        Assert.Equal(TriggerKind.Arrive, step.Trigger.Kind);
        Assert.Equal([TriggerKind.Bounty], step.FailsOn.Select(fail => fail.Kind));
        Assert.Equal("Someone is paying.", step.Reason);
    }

    [Fact]
    public void AOneStepBeatIsStillWrittenInTheOldShape()
    {
        Assert.Null(_book.Write("F1", LanternRoute(Accepted)));

        var text = _files.ReadText(AdventuresFile)!;
        Assert.DoesNotContain("\"steps\"", text, StringComparison.Ordinal);
        Assert.Contains("\"trigger\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ABeatOfSeveralStepsSurvivesASaveAndAReload()
    {
        var adventure = ThreeSteps();
        Assert.Null(_book.Write("F1", adventure));

        var reread = new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance);
        reread.Poll();

        var stored = reread.Find("F1", Key)!;
        Assert.Equal(adventure.Beats, stored.Beats);
        Assert.Equal(3, stored.Beats[0].Steps.Count);
    }

    [Fact]
    public void AThreeStepBeatSaysAHandOffAfterStepsOneAndTwoAndItsLinesAfterThree()
    {
        Begun(ThreeSteps());
        var callout = new AdventureCallout(_book);

        Assert.StartsWith(AdventureCallout.AckPrefix, Assert.Single(callout.Examine(Context(At(1), Jump(Lantern, At(1))))).Key, StringComparison.Ordinal);
        var first = Assert.Single(callout.Examine(Context(At(1).AddSeconds(30))));
        Assert.StartsWith(AdventureCallout.StepPrefix, first.Key, StringComparison.Ordinal);
        Assert.StartsWith("Next:", first.Text, StringComparison.Ordinal);
        Assert.Contains("bounty", first.Text, StringComparison.OrdinalIgnoreCase);

        Assert.StartsWith(AdventureCallout.AckPrefix, Assert.Single(callout.Examine(Context(At(2), BountyAt(At(2))))).Key, StringComparison.Ordinal);
        var second = Assert.Single(callout.Examine(Context(At(2).AddSeconds(30))));
        Assert.StartsWith(AdventureCallout.StepPrefix, second.Key, StringComparison.Ordinal);
        Assert.NotEqual(first.Key, second.Key);
        Assert.Contains("Maren Anchorage", second.Text, StringComparison.Ordinal);

        Assert.StartsWith(AdventureCallout.AckPrefix, Assert.Single(callout.Examine(Context(At(3), Docked(Anchorage, At(3))))).Key, StringComparison.Ordinal);
        var said = callout.Examine(Context(At(3).AddSeconds(30))).ToList();
        Assert.StartsWith(AdventureCallout.KeyPrefix, said[0].Key, StringComparison.Ordinal);
        Assert.Contains("Scoop here.", said[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(said, announcement => announcement.Key.StartsWith(AdventureCallout.StepPrefix, StringComparison.Ordinal));

        var standing = _book.Standing("F1", Key)!;
        Assert.Equal(1, standing.Current);
        Assert.Equal(0, standing.CurrentStep);
    }

    [Fact]
    public void ANewerStepReplacesAHandOffStillWaiting()
    {
        Begun(ThreeSteps());
        var callout = new AdventureCallout(_book);

        _ = callout.Examine(Context(At(1), Jump(Lantern, At(1)))).ToList();
        _ = callout.Examine(Context(At(1).AddSeconds(5), BountyAt(At(1).AddSeconds(5)))).ToList();

        var said = Assert.Single(callout.Examine(Context(At(2))));
        Assert.Contains("Maren Anchorage", said.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ABeatFiresOnlyWhenItsLastStepDoes()
    {
        Begun(ThreeSteps());

        _book.Observe(Jump(Lantern, At(1)), "F1");
        var standing = _book.Standing("F1", Key)!;
        Assert.Empty(standing.Fired);
        Assert.Equal(1, standing.CurrentStep);
        Assert.Equal(Bounty, standing.CurrentTrigger);

        _book.Observe(Docked(Anchorage, At(2)), "F1");
        Assert.Equal(1, _book.Standing("F1", Key)!.CurrentStep);

        _book.Observe(BountyAt(At(3)), "F1");
        _book.Observe(Docked(Anchorage, At(4)), "F1");

        standing = _book.Standing("F1", Key)!;
        Assert.Equal([At(4)], standing.Fired);
        Assert.Equal(["Docked"], standing.FiredBy);
        Assert.Equal([0, 1, 2], standing.StepFires.Select(fire => fire.Step));
        Assert.Equal([At(1), At(3), At(4)], standing.StepFires.Select(fire => fire.At));
        Assert.Equal(1, standing.Current);
        Assert.Equal(0, standing.CurrentStep);
    }

    [Fact]
    public void ACountedStepCountsOnlyWhileItIsTheCurrentStep()
    {
        var route = ThreeSteps();
        var counted = route with { Beats = [route.Beats[0] with { Steps = [route.Beats[0].Steps[0], new BeatStep { Trigger = Bounty with { Count = 2 } }, route.Beats[0].Steps[2]] }, .. route.Beats.Skip(1)] };
        Begun(counted);

        _book.Observe(BountyAt(At(1)), "F1");
        _book.Observe(Jump(Lantern, At(2)), "F1");
        _book.Observe(BountyAt(At(3)), "F1");

        var standing = _book.Standing("F1", Key)!;
        Assert.Equal(1, standing.CurrentStep);
        Assert.Equal(1, standing.Counted);

        _book.Observe(BountyAt(At(4)), "F1");
        standing = _book.Standing("F1", Key)!;
        Assert.Equal(2, standing.CurrentStep);
        Assert.Equal(0, standing.Counted);
    }

    [Fact]
    public void AStepsFailsOnFailsTheChapterOnlyWhileThatStepIsCurrent()
    {
        var route = ThreeSteps();
        var guarded = route with
        {
            Beats = [route.Beats[0] with { Steps = [route.Beats[0].Steps[0], route.Beats[0].Steps[1] with { FailsOn = [new AdventureTrigger { Kind = TriggerKind.Srv }] }, route.Beats[0].Steps[2]] }, .. route.Beats.Skip(1)],
        };
        Begun(guarded);

        _book.Observe(Event($$"""{ "timestamp":"{{Stamp(At(1))}}", "event":"LaunchSRV", "SRVType":"testbuggy" }"""), "F1");
        Assert.False(_book.Standing("F1", Key)!.IsFailed);

        _book.Observe(Jump(Lantern, At(2)), "F1");
        _book.Observe(Event($$"""{ "timestamp":"{{Stamp(At(3))}}", "event":"LaunchSRV", "SRVType":"testbuggy" }"""), "F1");

        var standing = _book.Standing("F1", Key)!;
        Assert.True(standing.IsFailed);
        Assert.Equal(0, standing.Failure!.Beat);
    }

    [Fact]
    public void ChangeObjectiveOnStepTwoReplacesOnlyThatStep()
    {
        Begun(ThreeSteps());
        _book.Observe(Jump(Lantern, At(1)), "F1");

        var replacement = new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Mission, Count = 2 } };
        Assert.Null(_book.ReplaceStep("F1", Key, 0, 1, replacement, At(5)));

        var stored = _book.Store.Find("F1", Key)!;
        var original = ThreeSteps();
        Assert.Equal([original.Beats[0].Steps[0], replacement, original.Beats[0].Steps[2]], stored.Beats[0].Steps);
        Assert.Equal(original.Beats.Skip(1), stored.Beats.Skip(1));
        Assert.Equal(1, _book.Standing("F1", Key)!.CurrentStep);
        Assert.Equal(TriggerKind.Mission, _book.Standing("F1", Key)!.CurrentTrigger!.Kind);

        _book.Observe(BountyAt(At(3)), "F1");
        Assert.Equal(1, _book.Standing("F1", Key)!.CurrentStep);
    }

    [Fact]
    public void ReplacingAStepTheStoryHasMovedPastIsRefused()
    {
        Begun(ThreeSteps());

        var refusal = _book.ReplaceStep("F1", Key, 0, 1, new BeatStep { Trigger = Bounty }, At(5));

        Assert.Equal("The story has moved on from that objective.", refusal);
    }

    [Fact]
    public void MarkingDoneFiresTheCurrentStepAndASecondMarkFiresTheNext()
    {
        Begun(ThreeSteps());

        Assert.Null(_book.Step("F1", Key, At(1), AdventureFold.CommanderSource));
        var standing = _book.Standing("F1", Key)!;
        Assert.Equal(1, standing.CurrentStep);
        Assert.Empty(standing.Fired);
        var first = Assert.Single(_book.Drain());
        Assert.False(_book.IsStirring("F1", Key));

        Assert.Null(_book.Step("F1", Key, At(2), AdventureFold.CommanderSource));
        Assert.Equal(2, _book.Standing("F1", Key)!.CurrentStep);
        var second = Assert.Single(_book.Drain());

        Assert.Equal([true, true], [first.IsStep, second.IsStep]);
        Assert.Equal([0, 1], [first.Step!.Value, second.Step!.Value]);
    }

    [Fact]
    public void AMarkedStepIsFiredAgainWhenTheJournalIsWalkedAgain()
    {
        Begun(ThreeSteps());
        Assert.Null(_book.Step("F1", Key, At(1), AdventureFold.CommanderSource));

        _book = Book();
        _book.Store.Poll();
        _book.CatchUp([]);

        var standing = _book.Standing("F1", Key)!;
        Assert.Equal(1, standing.CurrentStep);
        Assert.Equal([AdventureFold.CommanderSource], standing.StepFires.Select(fire => fire.By));
    }

    [Fact]
    public void AChapterOfFortyStepsLoadsFoldsAndSavesWithEveryFiredStepKept()
    {
        var route = LanternRoute(Accepted);
        var many = route with
        {
            Beats =
            [
                route.Beats[0] with { Steps = [.. Enumerable.Repeat(new BeatStep { Trigger = Bounty }, 25)] },
                route.Beats[1] with { Steps = [.. Enumerable.Repeat(new BeatStep { Trigger = Bounty }, 15)] },
            ],
        };
        Assert.Empty(AdventureValidation.Problems(many));
        Begun(many);

        for (var minute = 1; minute <= 40; minute++)
        {
            _book.Observe(BountyAt(At(minute)), "F1");
        }

        var standing = _book.Standing("F1", Key)!;
        Assert.True(standing.IsDone);
        Assert.Equal(40, standing.StepFires.Count);
        Assert.Equal([At(25), At(40)], standing.Fired);

        _book = Book();
        _book.Store.Poll();
        var stored = _book.Store.Find("F1", Key)!;
        Assert.Equal([25, 15], stored.Beats.Select(beat => beat.Steps.Count));

        var path = Path.Combine(Folder, "Journal.2026-08-22T190000.01.log");
        _files.WriteText(path, Commander("F1", Accepted.AddMinutes(-1)).Raw.GetRawText() + "\n" + string.Join("\n", Enumerable.Range(1, 40).Select(minute => BountyAt(At(minute)).Raw.GetRawText())) + "\n");
        _book.WalkAgain();
        _book.CatchUp([path]);

        standing = _book.Standing("F1", Key)!;
        Assert.True(standing.IsDone);
        Assert.Equal(40, standing.StepFires.Count);
    }
}
