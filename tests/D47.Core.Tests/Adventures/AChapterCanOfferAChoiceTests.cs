using D47.Core.Adventures;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A chapter offers a choice; the Commander says it and must then do it, and doing anything else of the options fails the chapter.</summary>
public sealed class AChapterCanOfferAChoiceTests
{
    private const string Key = "the-lantern-route";

    private const long MissionId = 1066795391;

    private const string Offer = "Say choose option one to fight, or choose option two to flee.";

    private static readonly string Folder = Path.Combine(MemoryInstall.FakeRoot, "choices");
    private static readonly string AdventuresFile = Path.Combine(Folder, "adventures.json");

    private static readonly AdventureTrigger Taken = new() { Kind = TriggerKind.MissionTaken, Handle = "parcel", MissionFamily = "Mission_Courier" };

    private readonly MemoryFileSystem _files = new();
    private AdventureStore _store;
    private AdventureBook _book;

    public AChapterCanOfferAChoiceTests()
    {
        (_store, _book) = Open();
    }

    private (AdventureStore, AdventureBook) Open()
    {
        var store = new AdventureStore(AdventuresFile, _files, NullLogger<AdventureStore>.Instance);
        store.Poll();
        return (store, new AdventureBook(store, NullLogger<AdventureBook>.Instance));
    }

    private static DateTimeOffset At(int minutes) => Accepted.AddMinutes(minutes);

    private static BeatStep Step(TriggerKind kind) => new() { Trigger = new AdventureTrigger { Kind = kind, Handle = "parcel" } };

    private static BeatStep Choice(BeatOption fight, BeatOption flee) =>
        new() { Trigger = new AdventureTrigger { Kind = TriggerKind.Choice }, Options = [fight, flee] };

    private static BeatOption Fight(params BeatStep[] steps) => new() { Label = "fight", Steps = steps.Length > 0 ? steps : [Step(TriggerKind.MissionComplete)] };

    private static BeatOption Flee(string? ending = null) => new()
    {
        Label = "flee",
        Steps = [Step(TriggerKind.MissionAbandon)],
        Ending = ending,
        EndingLine = ending is null ? null : "The parcel drifts behind you, and nobody comes after it.",
    };

    /// <summary>Take the parcel; choose to fight for it or flee from it; then fly home.</summary>
    internal static Adventure Chapter(BeatOption? fight = null, BeatOption? flee = null)
    {
        var route = LanternRoute(Accepted);

        return route with
        {
            Beats =
            [
                route.Beats[0] with { Steps = [new BeatStep { Trigger = Taken }], Line = "They are waiting at the jump-in. Fight them, or drop the parcel and run." },
                route.Beats[1] with { Steps = [Choice(fight ?? Fight(), flee ?? Flee())], Line = "Done, one way or the other." },
                route.Beats[4],
            ],
        };
    }

    private AdventureStanding Begun(Adventure? adventure = null)
    {
        Assert.Null(_book.Write("F1", adventure ?? Chapter()));
        _book.CatchUp([]);
        return Standing();
    }

    private AdventureStanding Standing() => _book.Standing("F1", Key)!;

    internal static JournalEvent Accept(int minutes) =>
        Event($$"""{ "timestamp":"{{Stamp(At(minutes))}}", "event":"MissionAccepted", "Faction":"League of HR 6649 Front", "Name":"Mission_Courier", "DestinationSystem":"Kweleutahe", "DestinationStation":"Merchiston Dock", "MissionID":{{MissionId}} }""");

    private static JournalEvent Mission(string kind, int minutes) =>
        Event($$"""{ "timestamp":"{{Stamp(At(minutes))}}", "event":"{{kind}}", "Name":"Mission_Courier", "MissionID":{{MissionId}} }""");

    /// <summary>The parcel taken, and the offer said at <paramref name="said"/> minutes.</summary>
    private AdventureStanding Offered(int said = 2)
    {
        Begun();
        _book.Observe(Accept(1), "F1");
        _book.Offered("F1", Key, 1, At(said));
        return Standing();
    }

    private static CalloutContext Context(DateTimeOffset now, GameStatus? status = null, params JournalEvent[] events) =>
        new(now, false, new CommanderGameState(new CommanderIdentity("F1", "Tester")), status ?? GameStatus.Unknown, NavRoute.None, events);

    private static GameStatus Interdicted(DateTimeOffset at) => GameStatus.Unknown with
    {
        Flags = StatusFlags.InMainShip | StatusFlags.BeingInterdicted,
        ReadAt = at,
    };

    private string Journal(params JournalEvent[] events)
    {
        var path = Path.Combine(Folder, "Journal.2026-08-22T190000.01.log");
        _files.WriteText(path, string.Join('\n', events.Select(e => e.Raw.GetRawText())) + "\n");
        return path;
    }

    [Fact]
    public void ChoosingOneAndDoingItCarriesTheChapterOn()
    {
        Assert.NotNull(Offered().OpenChoice);

        var chosen = _book.Choose("F1", Key, 0, At(3));

        Assert.Null(chosen.Refusal);
        Assert.Equal("You chose to fight. Next: complete the parcel mission.", chosen.Reply);

        _book.Observe(Mission("MissionCompleted", 4), "F1");

        var standing = Standing();
        Assert.False(standing.IsFailed);
        Assert.Equal(2, standing.Current);
        Assert.Equal([AdventureChoice.EventName, "MissionCompleted"], standing.StepFires.Where(fire => fire.Beat == 1).Select(fire => fire.By));
    }

    [Fact]
    public void ChoosingOneAndDoingAnotherFailsTheChapter()
    {
        Offered();
        Assert.Null(_book.Choose("F1", Key, 0, At(3)).Refusal);

        _book.Observe(Mission("MissionAbandoned", 4), "F1");

        Assert.True(Standing().IsFailed);
        Assert.Contains("abandon the parcel mission", Standing().Failure!.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void DoingAnotherOptionsOutcomeOnTheWayFailsTheChapter()
    {
        Assert.Null(_book.Write("F1", Chapter(fight: Fight(Step(TriggerKind.MissionDestination), Step(TriggerKind.MissionComplete)))));
        _book.CatchUp([]);
        _book.Observe(Accept(1), "F1");
        _book.Offered("F1", Key, 1, At(2));
        Assert.Null(_book.Choose("F1", Key, 0, At(3)).Refusal);

        _book.Observe(Mission("MissionAbandoned", 4), "F1");

        Assert.True(Standing().IsFailed);
    }

    [Theory]
    [InlineData("MissionAbandoned")]
    [InlineData("MissionCompleted")]
    public void AnOutcomeBeforeChoosingFailsTheChapter(string outcome)
    {
        Offered();

        _book.Observe(Mission(outcome, 3), "F1");

        Assert.True(Standing().IsFailed);
    }

    [Fact]
    public void AChoicePhraseWithNoOpenChoiceDoesNothing()
    {
        Begun();
        _book.Observe(Accept(1), "F1");

        // The beat has fired and the choice is current, but its offer has not been said.
        Assert.True(Standing().InChoice);
        Assert.Null(Standing().OpenChoice);
        Assert.Equal(AdventureChoiceOutcome.NoChoice, _book.Choose("F1", Key, 0, At(2)).Refusal);
        Assert.Empty(_store.Find("F1", Key)!.Choices);

        var router = new KeywordRouter(CapabilityRegistry.Build([AdventureCapability.Create(storyChoice: new AdventureCapability.StoryChoice { IsOpen = () => false })]));
        Assert.Null(router.MatchToolCommand("choose option one"));
    }

    [Fact]
    public void AnOfferSaidBeforeTheChoiceWasCurrentDoesNotOpenIt()
    {
        Begun();
        _book.Offered("F1", Key, 1, At(1));

        Assert.Null(_store.Find("F1", Key)!.Offered);
    }

    [Fact]
    public void AnOptionOutOfRangeIsRefusedAndNothingIsStored()
    {
        Offered();

        Assert.NotNull(_book.Choose("F1", Key, 2, At(3)).Refusal);
        Assert.Empty(_store.Find("F1", Key)!.Choices);
        Assert.NotNull(Standing().OpenChoice);
    }

    [Theory]
    [InlineData("choose option one", 1)]
    [InlineData("choose option two", 2)]
    [InlineData("choose option three", 3)]
    public void EachPhraseReachesTheToolWhileAChoiceIsOpen(string said, int option)
    {
        var router = new KeywordRouter(CapabilityRegistry.Build([AdventureCapability.Create(storyChoice: new AdventureCapability.StoryChoice { IsOpen = () => true })]));

        var match = router.MatchToolCommand(said);

        Assert.Equal(AdventureCapability.ChooseTool, match?.ToolName);
        Assert.True(match!.Arguments.TryGetInt32("option", out var number));
        Assert.Equal(option, number);
    }

    [Fact]
    public async Task TheModelCannotChooseAndTheCommanderCan()
    {
        var chosen = new List<int>();
        var registry = CapabilityRegistry.Build([AdventureCapability.Create(storyChoice: new AdventureCapability.StoryChoice
        {
            IsOpen = () => true,
            Choose = option =>
            {
                chosen.Add(option);
                return new AdventureChoiceOutcome(null, "You chose to fight.");
            },
        })]);
        var arguments = new ToolArguments(new Dictionary<string, string> { ["option"] = "1" });

        var model = await registry.InvokeAsync(AdventureCapability.ChooseTool, arguments, caller: ToolCaller.Model, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(model.IsError);
        Assert.Empty(chosen);

        var commander = await registry.InvokeAsync(AdventureCapability.ChooseTool, arguments, caller: ToolCaller.Commander, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(commander.IsError, commander.Content);
        Assert.Equal([0], chosen);
    }

    [Fact]
    public void AnOfferDueInDangerIsHeldAndThenSaidInFullWithItsPhrases()
    {
        Begun();
        var callout = new AdventureCallout(_book) { Settle = TimeSpan.FromSeconds(20) };
        var reached = At(1);

        callout.Examine(Context(reached, null, Accept(1))).ToList();

        Assert.Empty(callout.Examine(Context(reached.AddSeconds(30), Interdicted(reached))));
        Assert.Empty(callout.Examine(Context(reached.AddSeconds(40), Interdicted(reached))));

        var said = callout.Examine(Context(reached.AddMinutes(5))).ToList();

        Assert.Equal(["adventure.the-lantern-route.0", AdventureCallout.OfferKey(Key, 1)], said.Select(announcement => announcement.Key));
        Assert.Equal("They are waiting at the jump-in. Fight them, or drop the parcel and run.", said[0].Text);
        Assert.Equal(Offer, said[1].Text);
        Assert.Equal((Key, 1), AdventureCallout.Offer(said[1].Key));
    }

    [Fact]
    public void AnOfferNeverSaidIsSaidAgainAfterARestart()
    {
        Begun();
        _book.Observe(Accept(1), "F1");

        // The app closed before the line was said: a fresh book and callout over the same file.
        (_store, _book) = Open();
        _book.CatchUp([Journal(Commander("F1", Accepted.AddMinutes(-1)), Accept(1))]);
        Assert.True(Standing().InChoice);

        var callout = new AdventureCallout(_book) { Settle = TimeSpan.FromSeconds(20) };
        var said = callout.Examine(Context(At(10))).ToList();

        Assert.Equal(Offer, Assert.Single(said, announcement => announcement.Key == AdventureCallout.OfferKey(Key, 1)).Text);
        Assert.Contains(said, announcement => announcement.Key == "adventure.the-lantern-route.0");
        Assert.Empty(callout.Examine(Context(At(11))));

        _book.Offered("F1", Key, 1, At(11));
        Assert.Null(_book.Choose("F1", Key, 1, At(12)).Refusal);
    }

    [Fact]
    public void AnOfferYieldedButNeverRecordedAsSaidIsOfferedAgainAfterAWhile()
    {
        Begun();
        var callout = new AdventureCallout(_book) { Settle = TimeSpan.FromSeconds(20) };

        callout.Examine(Context(At(1), null, Accept(1))).ToList();
        Assert.Contains(callout.Examine(Context(At(1).AddSeconds(30))), announcement => announcement.Key == AdventureCallout.OfferKey(Key, 1));

        // The announcer dropped it, so nothing recorded the offer as said.
        Assert.Empty(callout.Examine(Context(At(3))));
        Assert.Contains(callout.Examine(Context(At(5))), announcement => announcement.Key == AdventureCallout.OfferKey(Key, 1));
        Assert.Empty(callout.Examine(Context(At(6))));
    }

    [Fact]
    public void AnAbandonedStoryIsNotWaitingOnAChoice()
    {
        Offered();
        Assert.Null(_book.Abandon("F1", Key, At(3)));

        Assert.False(Standing().InChoice);
        Assert.Null(Standing().OpenChoice);
        Assert.Equal(AdventureChoiceOutcome.NoChoice, _book.Choose("F1", Key, 0, At(4)).Refusal);
        Assert.Empty(new AdventureCallout(_book).Examine(Context(At(10))));
    }

    [Fact]
    public void TheNarratorMayNotOfferAChoice()
    {
        var route = Chapter();
        var narrated = route with { Beats = [route.Beats[0] with { Speaker = "narrator" }, .. route.Beats.Skip(1)] };

        Assert.Contains(AdventureValidation.Problems(narrated), problem => problem.Contains("a choice that the narrator offers", StringComparison.Ordinal));
    }

    [Fact]
    public void AnOptionWithItsOwnEndingEndsTheChapterInIt()
    {
        Begun(Chapter(flee: Flee("Gone to ground")));
        _book.Observe(Accept(1), "F1");
        _book.Offered("F1", Key, 1, At(2));
        _book.Drain();

        Assert.Equal("You chose to flee. Next: abandon the parcel mission.", _book.Choose("F1", Key, 1, At(3)).Reply);
        _book.Observe(Mission("MissionAbandoned", 4), "F1");

        var standing = Standing();
        Assert.True(standing.IsDone);
        Assert.False(standing.IsFailed);
        Assert.Equal("Gone to ground", standing.Ended!.Name);
        Assert.Equal(At(4), standing.FinishedAt);

        var moment = Assert.Single(_book.Drain());
        Assert.True(moment.IsEnding);
        Assert.Equal("The parcel drifts behind you, and nobody comes after it.", moment.Line);

        Assert.Single(_book.Unstored("F1"));
        Assert.Null(_book.StoreEnding("F1", Key));
        Assert.Empty(_book.Unstored("F1"));

        var stored = _store.Find("F1", Key)!;
        Assert.Equal("Gone to ground", stored.Ending!.Name);
        Assert.False(stored.IsFailed);
        Assert.True(Standing().IsDone);
    }

    [Fact]
    public void AChapterEndedInAnOptionsEndingStaysEndedAfterARestart()
    {
        Begun(Chapter(flee: Flee("Gone to ground")));
        _book.Observe(Accept(1), "F1");
        _book.Offered("F1", Key, 1, At(2));
        _book.Choose("F1", Key, 1, At(3));
        _book.Observe(Mission("MissionAbandoned", 4), "F1");
        Assert.Null(_book.StoreEnding("F1", Key));

        (_store, _book) = Open();
        _book.CatchUp([]);

        Assert.True(Standing().IsDone);
        Assert.Equal("Gone to ground", Standing().Ended!.Name);
        Assert.Equal(["Write the next chapter", "Remove"], Standing().ReadingButtons());
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(2, true)]
    public void AStoredChoiceIsReplayedInOrderWithTheJournal(int abandoned, bool before)
    {
        Offered(said: 1);
        Assert.Null(_book.Choose("F1", Key, 1, At(3)).Refusal);

        (_store, _book) = Open();
        _book.CatchUp([Journal(Commander("F1", Accepted.AddMinutes(-1)), Accept(1), Mission("MissionAbandoned", abandoned))]);

        // Fleeing then abandoning keeps the choice; abandoning first is an outcome before choosing.
        Assert.Equal(before, Standing().IsFailed);
        Assert.Equal(before ? 1 : 2, Standing().Current);
    }

    [Fact]
    public void ChoosingAndCompletingSurvivesAWalk()
    {
        Offered(said: 1);
        Assert.Null(_book.Choose("F1", Key, 0, At(3)).Refusal);

        (_store, _book) = Open();
        var journal = Journal(Commander("F1", Accepted.AddMinutes(-1)), Accept(1), Mission("MissionCompleted", 4));
        _book.CatchUp([journal]);
        var once = Standing();

        _book.CatchUp([journal]);

        Assert.False(once.IsFailed);
        Assert.Equal(2, once.Current);
        Assert.Equal(once.StepFires, Standing().StepFires);
    }

    [Fact]
    public void AOneStepChoiceBeatAndTheChoiceSurviveASaveAndAReload()
    {
        var adventure = Chapter(flee: Flee("Gone to ground"));
        Assert.Null(_book.Write("F1", adventure with { AcceptedAt = Accepted, Offered = new AdventureOffer(1, At(2)), Choices = [new AdventureChoice(1, 1, At(3))], Stepped = [new AdventureStep(1, At(4), Step: 1)] }));

        var (reread, _) = Open();
        var stored = reread.Find("F1", Key)!;

        Assert.Equal(adventure.Beats, stored.Beats);
        Assert.Equal(2, stored.Beats[1].Steps[0].Options.Count);
        Assert.Equal("Gone to ground", stored.Beats[1].Steps[0].Options[1].Ending);
        Assert.Equal([new AdventureChoice(1, 1, At(3))], stored.Choices);
        Assert.Equal(new AdventureOffer(1, At(2)), stored.Offered);
        Assert.Equal([new AdventureStep(1, At(4), Step: 1)], stored.Stepped);
    }

    [Fact]
    public void AChoiceCannotBeMarkedDoneOrChanged()
    {
        Offered();
        _book.Quiet("F1", Key);

        Assert.Contains("waiting on a choice", _book.Step("F1", Key, At(3), AdventureFold.CommanderSource), StringComparison.Ordinal);
        Assert.Equal(AdventureStanding.ChoiceRefusal, _book.ReplaceBeats("F1", Key, 1, [Chapter().Beats[2]], At(3)));
        Assert.False(Standing().IsShortStoryRefusable);
    }

    [Fact]
    public void AChoiceIsTheFirstStepOfItsObjectiveWithTwoOrThreeLabelledOptions()
    {
        var route = Chapter();
        var late = route with { Beats = [route.Beats[0] with { Steps = [new BeatStep { Trigger = Taken }, Choice(Fight(), Flee())] }, route.Beats[2]] };
        var one = route with { Beats = [route.Beats[0], route.Beats[1] with { Steps = [Choice(Fight(), Flee()) with { Options = [Fight()] }] }, route.Beats[2]] };
        var unlabelled = Chapter(fight: Fight() with { Label = " " });
        var travels = Chapter(fight: Fight(new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = Home } }));
        var unsaid = Chapter(flee: Flee("Gone to ground") with { EndingLine = null });
        var failed = Chapter(flee: Flee(AdventureEnding.Failed));

        Assert.Empty(AdventureValidation.Problems(Chapter()));
        Assert.Contains(AdventureValidation.Problems(late), problem => problem.Contains("a choice is the first step of its objective", StringComparison.Ordinal));
        Assert.Contains(AdventureValidation.Problems(one), problem => problem.Contains("a choice offers two or three", StringComparison.Ordinal));
        Assert.Contains(AdventureValidation.Problems(unlabelled), problem => problem.Contains("has no label", StringComparison.Ordinal));
        Assert.Contains(AdventureValidation.Problems(travels), problem => problem.Contains("not places to go", StringComparison.Ordinal));
        Assert.Contains(AdventureValidation.Problems(unsaid), problem => problem.Contains("no line to say", StringComparison.Ordinal));
        Assert.Contains(AdventureValidation.Problems(failed), problem => problem.Contains("which is the failure's", StringComparison.Ordinal));
    }

    [Fact]
    public void AnOptionsStepsAreCheckedAsAnObjectivesAre()
    {
        var rank = Chapter(fight: Fight(new BeatStep { Trigger = new AdventureTrigger { Kind = TriggerKind.Rank } }));
        var counted = Chapter(fight: Fight(Step(TriggerKind.MissionComplete) with { FailsOn = [new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 2 }] }));

        Assert.Contains(AdventureValidation.Problems(rank), problem => problem.Contains("an option's steps are missions, counted or one-event objectives", StringComparison.Ordinal));
        Assert.Contains(AdventureValidation.Problems(counted), problem => problem.Contains("has a failsOn with a count", StringComparison.Ordinal));
    }

    [Fact]
    public void AChapterEndedByAnOptionCountsTheBeatItEndedIn()
    {
        Begun(Chapter(flee: Flee("Gone to ground")));
        _book.Observe(Accept(1), "F1");
        _book.Offered("F1", Key, 1, At(2));
        _book.Choose("F1", Key, 1, At(3));
        _book.Observe(Mission("MissionAbandoned", 4), "F1");

        Assert.Equal("Step 2 of 3", Standing().Step());
        Assert.Equal("ended: Gone to ground", Standing().Place());
    }

    [Fact]
    public void AnOptionsMissionHandleMustBeTakenBeforeIt()
    {
        var route = Chapter();
        var untaken = route with { Beats = [route.Beats[1] with { Line = "First." }, route.Beats[2]] };

        Assert.Contains(AdventureValidation.Problems(untaken), problem => problem.Contains("option 1 refers to the mission \"parcel\" before any objective takes it", StringComparison.Ordinal));
    }

    [Fact]
    public void TheChoiceKindIsNotCountedOrAuthorableAndSaysItsPhrases()
    {
        Assert.False(AdventureTrigger.IsCountedKind(TriggerKind.Choice));
        Assert.False(AdventureTrigger.IsAuthorable(TriggerKind.Choice));
        Assert.Contains("choice", AdventureValidation.Kinds);
        Assert.Equal(Offer, Choice(Fight(), Flee()).HandOff());
        Assert.Equal(
            "Say choose option one to fight, choose option two to flee, or choose option three to hide.",
            BeatOption.Saying([Fight(), Flee(), new BeatOption { Label = "hide" }]));
    }
}
