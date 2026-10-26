using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter drawn with sights is built around one of them, and a draft with no arrive, land or scan objective there is refused.</summary>
public sealed class ASightseeingChapterNamesItsSightTests
{
    private static readonly NotablePlace Lantern = new("The Lantern Spire", "Landmark", "Ossen's Lantern", AdventureFixtures.Lantern, new StarPosition(0, 0, 0));

    private static readonly NotablePlace Hollow = new("The Hollow Arch", "Landmark", "Dyson's Hollow", AdventureFixtures.Hollow, new StarPosition(1, 0, 0));

    private const string ArriveAtTheLantern = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Spire", "function": "setup", "kind": "arrive", "reason": "The Spire is worth the trip.", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "Any Mission", "function": "turn", "kind": "mission", "count": 2, "mission": null, "line": "Work."},
          {"title": "The Bounties", "function": "resolution", "kind": "bounty", "count": 3, "line": "Paid."}
        ]}
        """;

    private const string OnlyDockAtTheHollow = """
        {"opening": "Again.", "reply": "Here.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Back."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "reason": "A berth by the arch.", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "Moored."},
          {"title": "The Bounties", "function": "resolution", "kind": "bounty", "count": 3, "line": "Paid."}
        ]}
        """;

    private static AdventureGenerator Generator(RoundScriptedLlmProvider provider, INotablePlacesService places)
    {
        var state = AdventureGeneratorTests.State(1, []);
        var galaxy = new AdventureGeneratorTests.Galaxy();

        return new AdventureGenerator(
            () => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => places,
            null, null, NullLogger.Instance);
    }

    private static AdventureAsk Ask(params NotablePlace[]? sights) =>
        new(AdventureReach.Session, AdventureLength.Short, Story: new AdventureStory(Id, Card.Title, Card.Describe(), Secret.Secret, 3, 10, 5, Sights: sights));

    [Fact]
    public async Task ADraftArrivingAtASightIsAcceptedAndRecordsIt()
    {
        var places = new Places();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(ArriveAtTheLantern), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await Generator(provider, places).GenerateAsync(Ask(Hollow, Lantern), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Ossen's Lantern", outcome.Draft!.Sight);
        Assert.Equal(0, places.Calls);
        Assert.Contains("sightseeing chapter", provider.Requests[0].Prompt.History[0].Text);
        Assert.Contains("The Lantern Spire", provider.Requests[1].Prompt.History[0].Text);
        Assert.Contains("must contain an arrive, land or scan objective", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ADraftWhoseOnlyObjectiveAtTheSightIsADockIsRefusedListingTheSights()
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine),
            RoundScriptedLlmProvider.Saying(OnlyDockAtTheHollow),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(OnlyDockAtTheHollow, 2)),
            RoundScriptedLlmProvider.Saying(Repairs.Replacing(OnlyDockAtTheHollow, 2)));

        var outcome = await Generator(provider, new Places()).GenerateAsync(Ask(Hollow), Now, CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Contains(
            "This is a sightseeing chapter and has no arrive, land or scan objective in the system of one of its sights: The Hollow Arch (Dyson's Hollow).",
            outcome.Refusal);
    }

    [Fact]
    public async Task ASightWithoutAnAddressMatchesItsSystemNameIgnoringCase()
    {
        var byName = new NotablePlace("The Lantern Spire", "Landmark", "OSSEN'S LANTERN", null, new StarPosition(0, 0, 0));
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(ArriveAtTheLantern), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await Generator(provider, new Places()).GenerateAsync(Ask(byName), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Ossen's Lantern", outcome.Draft!.Sight);
    }

    [Fact]
    public async Task AnAskWithoutSightsIsUnchangedAndLeavesSightNull()
    {
        var places = new Places();
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(ArriveAtTheLantern), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await Generator(provider, places).GenerateAsync(Ask(), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Draft!.Sight);
        Assert.DoesNotContain("sightseeing chapter", provider.Requests[0].Prompt.History[0].Text);
    }

    private sealed class Places : INotablePlacesService
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult<IReadOnlyList<NotablePlace>>([Lantern]);
        }
    }
}
