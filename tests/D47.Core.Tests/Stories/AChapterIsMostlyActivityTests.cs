using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>Any story chapter may travel as much as it needs; a refused chapter tells the Commander only that it could not be written.</summary>
public sealed class AChapterIsMostlyActivityTests
{
    [Fact]
    public async Task ARefusedChapterTellsTheCommanderOnlyThatItCouldNotBeWritten()
    {
        var stranger = PlannedChapterWorld.Lines(ChapterPlanner.MostObjectives, (number, objective) => objective["lines"] = new System.Text.Json.Nodes.JsonArray(new System.Text.Json.Nodes.JsonObject { ["text"] = "Scoop here.", ["speaker"] = "stranger" }));
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(stranger), RoundScriptedLlmProvider.Saying(Rewritten(1, "Scoop here.", "stranger")), RoundScriptedLlmProvider.Saying(Rewritten(1, "Scoop here.", "stranger")));

        var outcome = await Generate(provider);

        Assert.Equal(AdventureOutcome.NotWritten, outcome.Message);
        Assert.DoesNotContain("cannot stand", outcome.Message);
        Assert.DoesNotContain("Objective 1", outcome.Message);
        Assert.DoesNotContain("credits at the last load", outcome.Message);
        Assert.DoesNotContain("Try again in a moment", outcome.Message);
        Assert.Contains("stranger", outcome.Refusal);
        Assert.Contains("stranger", provider.Requests[1].Prompt.History[0].Text);
    }

    [Fact]
    public async Task AnyChapterMayTravel()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Lines()), RoundScriptedLlmProvider.Saying(NothingObjected));

        var outcome = await Generate(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.DoesNotContain("no more than two", provider.Requests[0].Prompt.History[0].Text, StringComparison.Ordinal);
    }

    private static Task<AdventureOutcome> Generate(RoundScriptedLlmProvider provider) =>
        PlannedChapterWorld.Generator(provider).GenerateAsync(PlannedChapterWorld.Ask(), Now, CancellationToken.None);
}
