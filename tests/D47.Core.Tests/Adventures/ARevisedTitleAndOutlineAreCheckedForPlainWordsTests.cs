using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>A revision that renames the story or rewrites its outline has them checked for plain wording; one that keeps them does not.</summary>
public sealed class ARevisedTitleAndOutlineAreCheckedForPlainWordsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string TitleObjected = """{"objections": [{"part": "title", "quote": "Three Books", "kind": "figurative", "reason": "books stand for the Commander's ranks."}]}""";

    private const string PlainSpine = """
        {"name": "The Lost Cargo", "premise": "Freight went missing from Ossen's Lantern.", "want": "To find the missing freight.",
         "stake": "Whether a debt can be owed to nobody.", "turn": "The freight was never loaded.", "ending": "The freight is accounted for."}
        """;

    private static readonly Adventure Draft = new()
    {
        Key = "the-lost-freight",
        Name = "The Lost Freight",
        Source = AdventureSource.Generated,
        Written = Now,
        Spine = new AdventureSpine { Premise = "Freight went missing.", Want = "To find it.", Stake = "Whether it matters.", Turn = "It was never loaded.", Ending = "It is found." },
    };

    private static string Renamed(string name) =>
        AdventureGeneratorTests.GoodBeats.Replace(
            "{\"opening\"",
            $"{{\"name\": \"{name}\", \"premise\": \"Freight went missing.\", \"want\": \"To find it.\", \"stake\": \"Whether it matters.\", \"turn\": \"It was never loaded.\", \"ending\": \"It is found.\", \"opening\"",
            StringComparison.Ordinal);

    private static Task<AdventureOutcome> Revise(RoundScriptedLlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .ReviseAsync(Draft, new AdventureAsk(Length: AdventureLength.Short), [], "Rename it.", Now, CancellationToken.None);

    [Fact]
    public async Task ARevisedTitleThatIsNotPlainIsWrittenAgainBeforeTheDraftIsShown()
    {
        var provider = new RoundScriptedLlmProvider(
            [.. new[] { Renamed("Three Books"), TitleObjected, PlainSpine, Plain, Plain, Plain }.Select(reply => RoundScriptedLlmProvider.Saying(reply))])
        {
            ScriptsPlainChecks = true,
        };

        var outcome = await Revise(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal("The Lost Cargo", outcome.Draft!.Name);
        Assert.Equal(6, provider.CallCount);
        Assert.Contains("[title] Three Books", provider.Requests[1].Prompt.History[0].Text);
        Assert.Contains("books stand for the Commander's ranks.", provider.Requests[2].Prompt.History[0].Text);
    }

    [Fact]
    public async Task ARevisedTitleStillNotPlainAfterItsRoundsFailsTheRevision()
    {
        var provider = new RoundScriptedLlmProvider(
            [.. new[] { Renamed("Three Books"), TitleObjected, Renamed("Three Books"), TitleObjected, Renamed("Three Books"), TitleObjected }.Select(reply => RoundScriptedLlmProvider.Saying(reply))])
        {
            ScriptsPlainChecks = true,
        };

        var outcome = await Revise(provider);

        Assert.False(outcome.Succeeded);
        Assert.Equal(6, provider.CallCount);
        Assert.StartsWith("The story is not written plainly: ", outcome.Refusal);
    }

    [Fact]
    public async Task ARevisionThatKeepsTheTitleAndOutlineChecksOnlyTheObjectives()
    {
        var provider = new RoundScriptedLlmProvider(
            [.. new[] { AdventureGeneratorTests.GoodBeats, Plain, Plain }.Select(reply => RoundScriptedLlmProvider.Saying(reply))])
        {
            ScriptsPlainChecks = true,
        };

        var outcome = await Revise(provider);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Equal(3, provider.CallCount);
        Assert.DoesNotContain(provider.Requests, request => request.Prompt.History[0].Text.Contains("[title]", StringComparison.Ordinal));
    }
}
