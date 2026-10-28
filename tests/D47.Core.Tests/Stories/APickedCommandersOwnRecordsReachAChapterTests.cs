using D47.Core.Adventures;
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

    private const string ChieftainBeats = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Hull", "function": "setup", "kind": "board", "ship": "Alliance Chieftain", "line": "Sign for it."},
          {"title": "The Bounties", "function": "midpoint", "kind": "bounty", "count": 3, "line": "Paid."},
          {"title": "The Ore", "function": "finale", "kind": "mine", "count": 20, "line": "Refined."}
        ]}
        """;

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

    [Fact]
    public void ALocalisedExplorerInTheFleetAllowsALongHaul()
    {
        var store = PickedWhileAnotherIsInGame(fleet: false);

        store.Active!.Fleet = Fleet with { Ships = [new StoredShip(12, "Caspian Explorer", null, "LTT 7786")] };

        Assert.True(ChapterFit.IsLongHaul(StoryPacing.ThreeMonths, store.Active));
    }

    private static (AdventureOutcome Result, int Requests) Generate(GameStateStore store)
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(ChieftainBeats), RoundScriptedLlmProvider.Saying(Repairs.Replacing(ChieftainBeats, 1)), RoundScriptedLlmProvider.Saying(Repairs.Replacing(ChieftainBeats, 1)));

        var generator = new AdventureGenerator(
            () => provider, () => null, () => "You are the ship.", () => "core", () => null,
            () => store.Active, () => new AdventureGeneratorTests.Galaxy(), () => null, null, null, NullLogger.Instance);

        var result = generator.GenerateAsync(
            new AdventureAsk(AdventureReach.Session, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 1, 10, 5)),
            Now,
            CancellationToken.None).GetAwaiter().GetResult();

        return (result, provider.Requests.Count);
    }
}
