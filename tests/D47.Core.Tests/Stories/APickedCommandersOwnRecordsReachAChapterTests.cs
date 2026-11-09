using D47.Core.Adventures;
using D47.Core.Goals;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A Commander picked in place of the one in the game is checked against their own fleet and credits.</summary>
public sealed class APickedCommandersOwnRecordsReachAChapterTests
{
    private const string Picked = "F735466";

    private static readonly FleetRegistry Fleet = new()
    {
        TakenAt = DateTimeOffset.Parse("2026-10-07T19:10:50Z"),
        SnapshotSystem = "LTT 7786",
        Ships = [new StoredShip(11, "Alliance Chieftain", "Defender", "LTT 7786")],
    };

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed), json);
        return parsed!;
    }

    private static GameStateStore PickedWhileAnotherIsInGame(Func<string, long?>? credits = null, bool fleet = true)
    {
        var store = new GameStateStore
        {
            RestoreFleet = fid => fleet && fid == Picked ? Fleet : null,
            RestoreCredits = credits,
        };

        store.Apply(Event("""{ "timestamp":"2026-10-12T09:00:00Z", "event":"Commander", "FID":"F100", "Name":"ARLO KESTREL" }"""));
        store.Apply(Event("""{ "timestamp":"2026-10-12T09:00:01Z", "event":"Location", "StarSystem":"Oppi", "SystemAddress":3382387380970, "StarPos":[3.68750,-44.62500,131.00000], "Docked":false }"""));
        store.Pick(new CommanderIdentity(Picked, "JOHN DEPARAGON"));
        store.Active!.Location = PlannedChapterWorld.State().Location;

        return store;
    }

    [Fact]
    public void APickedCommandersCreditsComeFromTheirNewestLoadGame()
    {
        var store = PickedWhileAnotherIsInGame(fid => fid == Picked ? 10_472_582_720 : null);

        Assert.Equal(10_472_582_720, store.Active!.Session.Balance);
    }

    [Fact]
    public void ALoadGameFoldedThisRunWinsOverTheRestoredCredits()
    {
        var store = PickedWhileAnotherIsInGame(fid => 10_472_582_720);

        store.Apply(Event($$"""{ "timestamp":"2026-10-12T10:00:00Z", "event":"LoadGame", "FID":"{{Picked}}", "Commander":"JOHN DEPARAGON", "Credits":5 }"""));
        store.RestoreLate();

        Assert.Equal(5, store.Active!.Session.Balance);
    }

    [Fact]
    public void ACommanderThatOwnsTheHullBoardsItWithNoLoadGameFoldedThisRun()
    {
        var outcome = Generate(PickedWhileAnotherIsInGame());

        Assert.True(outcome.Result.Succeeded, outcome.Result.Refusal);
    }

    [Fact]
    public void ACommanderWhoseCreditsAreUnknownIsNotToldHowMuchTheyNeed()
    {
        var outcome = Generate(PickedWhileAnotherIsInGame(fleet: false));

        Assert.Contains("does not know the Commander's credits", outcome.Result.Refusal);
        Assert.DoesNotContain("needs", outcome.Result.Refusal);
    }

    [Fact]
    public void ACommanderWithTheCreditsBoardsAHullTheyDoNotOwn()
    {
        var outcome = Generate(PickedWhileAnotherIsInGame(fid => 10_472_582_720, fleet: false));

        Assert.True(outcome.Result.Succeeded, outcome.Result.Refusal);
    }

    private static (AdventureOutcome Result, int Requests) Generate(GameStateStore store)
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected));
        var board = new GoalClimbStep("ship", "Fly an Alliance Chieftain")
        {
            Objective = new AdventureTrigger { Kind = TriggerKind.Board, ShipType = "typex" },
        };

        var generator = PlannedChapterWorld.Generator(provider, state: () => store.Active);

        var result = generator.GenerateAsync(
            new AdventureAsk(AdventureReach.Session, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 3, 10, 5, Goals: [board])),
            Now,
            CancellationToken.None).GetAwaiter().GetResult();

        return (result, provider.Requests.Count);
    }
}
