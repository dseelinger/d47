using D47.Core.Adventures;
using D47.Core.Journal;
using Xunit;

namespace D47.Scenarios.Tests;

/// <summary>Does a live model object to wording that stands an image or an invented feature in for a game thing?</summary>
[Trait("Category", "Integration")]
public class PlainWordsLiveTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AModelObjectsToABookStandingForARankAndToAClosingLineThatCountsBooks()
    {
        Assert.SkipUnless(LiveScenarioTests.Enabled, "set D47_SCENARIOS_LIVE=1 to ask a live model");

        var provider = LiveScenarioTests.Compose();
        var state = new GameStateStore();
        var generator = new AdventureGenerator(() => provider, () => LiveScenarioTests.Model, () => "You are the ship.", () => "core", () => null, () => state.Active, () => null, () => null, null, null, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance);

        var objections = await generator.PlainObjectionsAsync(
            [
                ("objective 1", "Title: The Climb. Lines: You are now a line in the Power's book. Reason: The Commander reaches Powerplay rank 6. Task: rank: Power 6"),
                ("objective 2", "Title: The Last Page. Lines: Three books hold your name. Reason: The Commander docks at Maren Anchorage. Task: dock: Maren Anchorage in Dyson's Hollow"),
            ],
            Token);

        Assert.Contains(objections, found => found.Part == "objective 1");
        Assert.Contains(objections, found => found.Part == "objective 2");
    }
}
