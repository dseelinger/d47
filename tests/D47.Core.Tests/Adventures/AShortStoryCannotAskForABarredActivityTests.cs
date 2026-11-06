using System.Text.Json.Nodes;
using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>An activity the Commander has changed away from five times is not asked for in a short story.</summary>
public sealed class AShortStoryCannotAskForABarredActivityTests
{
    [Fact]
    public async Task ADraftWithoutABarIsWritten()
    {
        var galaxy = new PlannedChapterWorld.Galaxy();

        for (var index = 0; index < 6; index++)
        {
            galaxy.MoreStations.Add(new StationSummary
            {
                Name = $"Roomy Port {index}", SystemName = $"Roomy {index}", SystemAddress = 900 + index, MarketId = 9000 + index,
                Position = new StarPosition(130 + (index * 10), 0, 0), Type = "Coriolis Starport", LargePads = 2,
            });
            galaxy.MoreBodies.Add(new BodySummary
            {
                Name = $"Roomy Rock {index} 1", SystemName = $"Roomy Rock {index}", SystemAddress = 950 + index, BodyId = 1,
                Position = new StarPosition(130 + (index * 10), 10, 0), IsLandable = true,
            });
        }

        var lines = PlannedChapterWorld.Lines(
            ChapterPlanner.MostObjectives,
            (number, objective) => objective["lines"] = new JsonArray(JsonValue.Create($"Page {number} of the ledger is open.")),
            answer => answer["opening"] = "A ledger sits in the hold.");
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(lines), RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));
        var generator = PlannedChapterWorld.Generator(provider, galaxy);

        var outcome = await generator.GenerateAsync(new AdventureAsk(AdventureReach.Session, AdventureLength.Evening), AdventureFixtures.Accepted, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.NotNull(outcome.Draft);
    }
}
