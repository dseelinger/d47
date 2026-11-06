using System.Text.Json.Nodes;
using D47.Core.Adventures;
using D47.Core.Stories;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>A revision's title and outline are checked for plain wording.</summary>
public sealed class ARevisedTitleAndOutlineAreCheckedForPlainWordsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string NoBrief = """{"home": false, "system": null, "activities": [], "ship": null, "changes": false}""";

    private const string TitleObjected = """{"objections": [{"part": "title", "quote": "Three Books", "kind": "figurative", "reason": "books stand for the Commander's ranks."}]}""";

    private static string Spine(string name) => $$"""
        {"objectives": [], "name": "{{name}}", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The freight is accounted for."}
        """;

    private static string Renamed(string name) =>
        PlannedChapterWorld.Lines(
            ChapterPlanner.MostObjectives,
            (number, objective) => objective["lines"] = new JsonArray(JsonValue.Create($"Page {number} of the ledger is open.")),
            answer =>
            {
                answer["name"] = name;
                answer["opening"] = "A ledger sits in the hold.";
            });

    /// <summary>A first draft written plainly, then the revision's replies: the brief read, the lines, and the checks.</summary>
    private static async Task<(AdventureOutcome Outcome, RoundScriptedLlmProvider Provider)> Revise(params string[] revision)
    {
        var first = Renamed("The Broker's Ledger");
        var provider = new RoundScriptedLlmProvider([.. new[] { first, Plain, Plain }.Concat(revision).Select(reply => RoundScriptedLlmProvider.Saying(reply))])
        {
            ScriptsPlainChecks = true,
        };
        var generator = PlannedChapterWorld.Generator(provider, PlannedChapterWorld.Roomy());
        var ask = new AdventureAsk(AdventureReach.Session, AdventureLength.Evening);
        var draft = (await generator.GenerateAsync(ask, Now, TestContext.Current.CancellationToken)).Draft!;
        var outcome = await generator.ReviseAsync(draft, ask, [], "Rename it.", Now, TestContext.Current.CancellationToken);

        return (outcome, provider);
    }

    [Fact]
    public async Task ARevisedTitleThatIsNotPlainIsWrittenAgainBeforeTheDraftIsShown()
    {
        var (outcome, provider) = await Revise(NoBrief, Renamed("Three Books"), Plain, TitleObjected, Spine("The Lost Cargo"), Plain, Plain);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("The Lost Cargo", outcome.Draft!.Name);
        Assert.Contains(provider.Requests, request => request.Prompt.History[0].Text.Contains("[title] Three Books", StringComparison.Ordinal));
        Assert.Contains(provider.Requests, request => request.ResponseSchema?.Name == "story_line_repairs" && request.Prompt.History[0].Text.Contains("books stand for the Commander's ranks.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ARevisedTitleStillNotPlainAfterItsRoundsFailsTheRevision()
    {
        var (outcome, _) = await Revise(NoBrief, Renamed("Three Books"), Plain, TitleObjected, Spine("Three Books"), Plain, TitleObjected, Spine("Three Books"), Plain, TitleObjected);

        Assert.False(outcome.Succeeded);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

}
