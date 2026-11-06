using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

/// <summary>Each story prompt is sent as the whole text held in the test project's <c>Prompts</c> resources.</summary>
public sealed class TheStoryPromptsAreSentAsPinnedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Plain = """{"objections": []}""";

    private const string Objection = """{"objections": [{"objective": 2, "quote": "of the ledger", "kind": "history", "reason": "it says the Commander has read the ledger before."}]}""";

    private static readonly AdventureStory Stock = PlannedChapterWorld.Story with { Chapter = 2, DaysSinceBeacon = null };

    private static AdventureAsk Short => new(AdventureReach.Session, AdventureLength.Short);

    private static Adventure Earlier() => LanternRoute(Accepted) with { Key = "first", Name = "The First Chapter" };

    private static Adventure Failed() => LanternRoute(Accepted) with
    {
        Key = "second",
        Name = "The Second Chapter",
        Follows = "first",
        Ending = new AdventureEnding(AdventureEnding.Failed, Accepted.AddMinutes(4), "At \"The Lantern\" the Commander did what ruled it out: collect one bounty."),
    };

    [Fact]
    public async Task TheLinesOfANewChapterOfAStockStory() =>
        Pinned("lines-new-chapter", await Sent(new AdventureAsk(AdventureReach.Session, Story: Stock), "story_lines", Saying(PlannedChapterWorld.NewWords())));

    [Fact]
    public async Task TheLinesOfTheNextChapterAfterAFailedOne()
    {
        var all = new[] { Earlier(), Failed() };
        var ask = new AdventureAsk(AdventureReach.Session, Story: Stock) with { Chapter = AdventureChapter.Of(all, "first"), Failed = AdventureChapter.Of(all, "second") };

        Pinned("lines-next-chapter-after-failed", await Sent(ask, "story_lines", Saying(PlannedChapterWorld.NewWords())));
    }

    [Fact]
    public async Task TheLinesOfAShortStory() =>
        Pinned("lines-short-story", await Sent(Short, "story_lines", Saying(PlannedChapterWorld.ShortLines())));

    [Fact]
    public async Task TheLinesOfARewriteOfARefusedObjective()
    {
        var ask = Short with { Rewrite = new AdventureRewrite(LanternRoute(Accepted), 2) };

        Pinned("lines-rewrite-refused", await Sent(ask, "story_line_repairs", Saying(PlannedChapterWorld.NewWords(speakers: false))));
    }

    [Fact]
    public async Task TheLinesOfARevisionFromTheCommandersRemark()
    {
        var exchange = new[] { new AdventureRemark("Make it shorter.", "It is three objectives now.") };

        Pinned("lines-revise-remark", await Revised(LanternRoute(Accepted), Short, exchange, "Fewer docks, please."));
    }

    [Fact]
    public async Task TheRepairTurnAfterARefusedLine() =>
        Pinned("line-repairs", await Sent(Short, "story_line_repairs", Saying(PlannedChapterWorld.ShortLines()), Saying(Objection), Saying(PlannedChapterWorld.Repair(2))));

    [Fact]
    public async Task TheFidelityCheck() =>
        Pinned("fidelity", await Sent(Short, "story_fidelity", Saying(PlannedChapterWorld.ShortLines()), Saying(Plain)));

    [Fact]
    public async Task ThePlainWordsCheck() =>
        Pinned("plain", await Sent(Short, "story_plain", Saying(PlannedChapterWorld.ShortLines()), Saying(Plain), Saying(Plain)));

    private static async Task<string> Sent(AdventureAsk ask, string schema, params IReadOnlyList<LlmStreamEvent>[] rounds)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var provider = new RoundScriptedLlmProvider([.. rounds, .. Enumerable.Repeat(Saying("{}"), 8)]) { ScriptsPlainChecks = true };

        await PlannedChapterWorld.Generator(provider, PlannedChapterWorld.Roomy()).GenerateAsync(ask, Now, CancellationToken.None);

        return provider.Requests.First(request => request.ResponseSchema?.Name == schema).Prompt.History[0].Text;
    }

    private static async Task<string> Revised(Adventure draft, AdventureAsk ask, IReadOnlyList<AdventureRemark> exchange, string remark)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var provider = new RoundScriptedLlmProvider([.. Enumerable.Repeat(Saying("{}"), 8)]);

        await PlannedChapterWorld.Generator(provider, PlannedChapterWorld.Roomy()).ReviseAsync(draft, ask, exchange, remark, Now, CancellationToken.None);

        return provider.Requests.First(request => request.ResponseSchema?.Name == "story_lines").Prompt.History[0].Text;
    }

    private static void Pinned(string name, string sent)
    {
        using var stream = typeof(TheStoryPromptsAreSentAsPinnedTests).Assembly.GetManifestResourceStream($"Prompts.{name}.txt")
            ?? throw new InvalidOperationException($"Prompts.{name}.txt is not embedded.");
        using var reader = new StreamReader(stream);

        Assert.Equal(Unix(reader.ReadToEnd()), Unix(sent));
    }

    private static string Unix(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}
