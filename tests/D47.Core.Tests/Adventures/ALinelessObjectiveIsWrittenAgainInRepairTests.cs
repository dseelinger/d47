using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;
using static D47.Core.Tests.Stories.PlannedChapterWorld;

namespace D47.Core.Tests.Adventures;

/// <summary>An objective written with no line goes to a line repair, rather than failing the chapter; a one-objective detour may have no line.</summary>
public sealed class ALinelessObjectiveIsWrittenAgainInRepairTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AFirstDraftObjectiveWithNoLineIsAskedForAgainInALineRepair()
    {
        var ask = Ask();
        var plan = await PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider(
            Saying(Lines(plan.Objectives.Count, (number, objective) =>
            {
                if (number == 1)
                {
                    objective["lines"] = new System.Text.Json.Nodes.JsonArray();
                }
            })),
            Saying(Repair(1)),
            Saying(NoObjections));

        var outcome = await Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("story_line_repairs", provider.Requests[1].ResponseSchema?.Name);
        Assert.Contains("has no line", provider.Requests[1].Prompt.History[0].Text);
        Assert.Equal(["The ledger's one page is legible now."], [.. outcome.Draft!.Beats[0].Lines.Select(line => line.Text)]);
    }

    [Fact]
    public async Task ADetourOfOneObjectiveIsNotRefusedForHavingNoLine()
    {
        const string lineless = """
            {"objectives": [
              {"objective": 1, "title": "The Collectors", "reason": "Someone there knows about the burst.", "lines": []}
            ]}
            """;

        var detour = AdventureFixtures.LanternRoute() with { Detour = true, Beats = [AdventureFixtures.LanternRoute().Beats[0]] };
        var provider = new RoundScriptedLlmProvider(Saying(lineless), Saying(NoObjections));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy { Positioned = true }, new RecordingLogger())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Rewrite: new AdventureRewrite(detour, 0)), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Single(provider.Requests, request => request.ResponseSchema?.Name == "story_line_repairs");
        Assert.Equal("The Collectors", Assert.Single(outcome.Draft!.Beats).Title);
    }
}
