using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A chapter drawn with sights is built around one of them, and a draft with no arrive, land or scan objective there is refused.</summary>
public sealed class ASightseeingChapterNamesItsSightTests
{
    private static readonly NotablePlace Beta = new("The Beta Spire", "Landmark", "Fixture Beta", 3, new StarPosition(120, 0, 0));

    private static readonly NotablePlace Alpha = new("The Alpha Arch", "Landmark", "Fixture Alpha", 2, new StarPosition(110, 0, 0));

    private static AdventureGenerator Generator(RoundScriptedLlmProvider provider, INotablePlacesService places)
    {
        var state = PlannedChapterWorld.State();
        var galaxy = new PlannedChapterWorld.Galaxy();

        return new AdventureGenerator(
            () => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => places,
            null, null, NullLogger.Instance);
    }

    private static AdventureAsk Ask(params NotablePlace[]? sights) =>
        new(AdventureReach.Session, AdventureLength.Short, Story: PlannedChapterWorld.Story with { Sights = sights });

    [Fact]
    public async Task ADraftArrivingAtASightIsAcceptedAndRecordsIt()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await Generator(provider, new Places()).GenerateAsync(Ask(Beta), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Fixture Beta", outcome.Draft!.Sight);
        Assert.Contains("The Beta Spire", provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ASightWithoutAnAddressMatchesItsSystemNameIgnoringCase()
    {
        var byName = new NotablePlace("The Beta Spire", "Landmark", "FIXTURE BETA", null, new StarPosition(120, 0, 0));
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await Generator(provider, new Places()).GenerateAsync(Ask(byName), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Fixture Beta", outcome.Draft!.Sight, ignoreCase: true);
    }

    [Fact]
    public async Task AnAskWithoutSightsIsUnchangedAndLeavesSightNull()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await Generator(provider, new Places()).GenerateAsync(Ask(), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Draft!.Sight);
        Assert.DoesNotContain("one of the chapter's sights", provider.Requests[0].Prompt.History[0].Text);
    }

    private sealed class Places : INotablePlacesService
    {
        public Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotablePlace>>([Alpha]);
    }
}
