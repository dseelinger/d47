using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// The rewrites the last plain check gives are put in place once the writer's rounds are used up, unchecked; an objection with no
/// rewrite, or a rewrite that repeats the quote, still refuses the story. Between writer rounds a part takes one rewrite of the check's.
/// </summary>
public sealed class TheLastPlainCheckRewriteIsPutInPlaceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string PlainerSpine = """
        {"objectives": [], "name": "The Lost Cargo", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The freight is accounted for."}
        """;

    private static string Objected(string part, string quote, string? plain) =>
        $$"""{"objections": [{"part": "{{part}}", "quote": "{{quote}}", "kind": "figurative", "reason": "it is an image."{{(plain is null ? string.Empty : $", \"plain\": \"{plain}\"")}}}]}""";

    private static async Task<AdventureOutcome> Write(params string[] replies)
    {
        var ask = PlannedChapterWorld.Ask();
        var plan = await PlannedChapterWorld.PlanAsync(ask);
        var provider = new RoundScriptedLlmProvider([RoundScriptedLlmProvider.Saying(PlannedChapterWorld.Lines(plan.Objectives.Count)), .. replies.Select(reply => RoundScriptedLlmProvider.Saying(reply))])
        {
            ScriptsPlainChecks = true,
        };

        return await PlannedChapterWorld.Generator(provider).GenerateAsync(ask, Now, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task APremiseWithAThirdFigurativePhraseTakesTheLastRewrite()
    {
        var outcome = await Write(
            Plain, Objected("premise", "ledger turns up", "ledger is found"),
            Plain, Objected("premise", "ledger is found", "ledger is open"),
            PlainerSpine,
            Plain, Objected("premise", "Freight went missing", "Cargo was lost"),
            Plain, Objected("premise", "Cargo was lost", "Cargo was gone"),
            PlainerSpine,
            Plain, Objected("premise", "Freight went missing", "Cargo was lost"));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("Cargo was lost from Ossen's Lantern.", outcome.Draft!.Spine!.Premise);
    }

    [Fact]
    public async Task APremiseWhoseLastObjectionHasNoRewriteIsStillRefused()
    {
        var outcome = await Write(
            Plain, Objected("premise", "ledger turns up", "ledger is found"),
            Plain, Objected("premise", "ledger is found", "ledger is open"),
            PlainerSpine,
            Plain, Objected("premise", "Freight went missing", "Cargo was lost"),
            Plain, Objected("premise", "Cargo was lost", "Cargo was gone"),
            PlainerSpine,
            Plain, Objected("premise", "Freight went missing", null));

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

    [Fact]
    public async Task AnObjectiveWithAThirdFigurativePhraseTakesTheLastRewrite()
    {
        var outcome = await Write(
            Plain, Objected("objective 2", "another page", "a further record"),
            Plain, Objected("objective 2", "a further record", "a clear record"),
            PlannedChapterWorld.Repair(2),
            Plain, Objected("objective 2", "legible now", "readable"),
            Plain, Objected("objective 2", "readable", "plain"),
            PlannedChapterWorld.Repair(2),
            Plain, Objected("objective 2", "legible now", "clear now"));

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("The ledger's two page is clear now.", outcome.Draft!.Beats[1].Lines[0].Text);
    }

    [Fact]
    public async Task AnObjectiveWhoseLastObjectionHasNoRewriteIsStillRefused()
    {
        var outcome = await Write(
            Plain, Objected("objective 2", "another page", "a further record"),
            Plain, Objected("objective 2", "a further record", "a clear record"),
            PlannedChapterWorld.Repair(2),
            Plain, Objected("objective 2", "legible now", "readable"),
            Plain, Objected("objective 2", "readable", "plain"),
            PlannedChapterWorld.Repair(2),
            Plain, Objected("objective 2", "legible now", null));

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }
}
