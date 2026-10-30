using System.Globalization;
using D47.Core.Adventures;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;
using static D47.Core.Tests.Conversation.RoundScriptedLlmProvider;

namespace D47.Core.Tests.Adventures;

/// <summary>Each story prompt is sent as the whole text held in the test project's <c>Prompts</c> resources.</summary>
public sealed class TheStoryPromptsAreSentAsPinnedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private const string LostHop = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Lost Hop", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Nowhere Reached", "line": "To one name."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private const string Sightseeing = """
        {"opening": "Somebody is paying.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Tour", "function": "turn", "kind": "mission", "count": 1, "mission": "Mission_Sightseeing", "line": "Take the tourists round."},
          {"title": "The Column Will Not Balance", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private const string StillLost = """
        {"replacements": [
          {"objective": 2, "title": "The Lost Hop", "function": "turn", "kind": "arrive", "reason": "Someone there knows about the burst.", "system": "Nowhere Reached", "line": "To one name."}
        ]}
        """;

    private static readonly AdventureStory Stock = new(
        "s", "The Unrecoverable Column", "A ledger will not balance.", "The freight was never loaded.",
        Chapter: 2, DaysRunning: 9, DaysSinceBeacon: null,
        Cast: [new AdventureSpeaker("tess", "Tess", "A pilot who keeps the books.")]);

    private static AdventureAsk Short => new(Length: AdventureLength.Short);

    private static Adventure Earlier() => LanternRoute(Accepted) with { Key = "first", Name = "The First Chapter" };

    private static Adventure Failed() => LanternRoute(Accepted) with
    {
        Key = "second",
        Name = "The Second Chapter",
        Follows = "first",
        Ending = new AdventureEnding(AdventureEnding.Failed, Accepted.AddMinutes(4), "At \"The Lantern\" the Commander did what ruled it out: collect one bounty."),
    };

    [Fact]
    public async Task TheSpine() =>
        Pinned("spine", await Sent(Short, 0, Saying(AdventureGeneratorTests.Spine)));

    [Fact]
    public async Task TheBeatsOfANewChapterOfAStockStory() =>
        Pinned("beats-new-chapter", await Sent(new AdventureAsk(Story: Stock), 1, Saying(AdventureGeneratorTests.Spine), Saying(AdventureGeneratorTests.GoodBeats)));

    [Fact]
    public async Task TheBeatsOfTheNextChapterAfterAFailedOne()
    {
        var all = new[] { Earlier(), Failed() };
        var ask = Short with { Chapter = AdventureChapter.Of(all, "first"), Failed = AdventureChapter.Of(all, "second") };

        Pinned("beats-next-chapter-after-failed", await Sent(ask, 1, Saying(AdventureGeneratorTests.Spine), Saying(AdventureGeneratorTests.GoodBeats)));
    }

    [Fact]
    public async Task TheBeatsOfAShortStory() =>
        Pinned("beats-short-story", await Sent(Short, 1, Saying(AdventureGeneratorTests.Spine), Saying(AdventureGeneratorTests.GoodBeats)));

    [Fact]
    public async Task TheBeatsOfARewriteOfARefusedObjective()
    {
        var ask = Short with { Rewrite = new AdventureRewrite(LanternRoute(Accepted), 2) };

        Pinned("beats-rewrite-refused", await Sent(ask, 0, Saying(AdventureGeneratorTests.GoodBeats)));
    }

    [Fact]
    public async Task TheBeatsOfARevisionFromTheCommandersRemark()
    {
        var exchange = new[] { new AdventureRemark("Make it shorter.", "It is three objectives now.") };

        Pinned("beats-revise-remark", await Revised(LanternRoute(Accepted), Short, exchange, "Fewer docks, please."));
    }

    [Fact]
    public async Task TheRepairTurnAfterARefusedDraft() =>
        Pinned("repair-refused-draft", await Sent(Short, 2, Saying(AdventureGeneratorTests.Spine), Saying(LostHop), Saying(StillLost)));

    [Fact]
    public async Task TheFidelityCheck() =>
        Pinned("fidelity", await Sent(Short, 2, Saying(AdventureGeneratorTests.Spine), Saying(Sightseeing), Saying("""{"objections": []}""")));

    private static AdventureGenerator Generator(RoundScriptedLlmProvider provider)
    {
        var state = AdventureGeneratorTests.State(1, []);
        var galaxy = new AdventureGeneratorTests.Galaxy();
        var catalogue = new Catalogue();

        return new AdventureGenerator(() => provider, () => null, () => "You are the ship.", () => "core", () => null, () => state, () => galaxy, () => catalogue, null, null, NullLogger.Instance);
    }

    private sealed class Catalogue : INotablePlacesService
    {
        public Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NotablePlace>>(
            [
                new("The Quiet Field", "Geological site", "Quiet Field", 1, new StarPosition(3.6875, -44.625, 161)) { Summary = "A basin of glass spires." },
                new("Veyl Cairn", "Ruin", "Cairn of Veyl", 2, new StarPosition(43.6875, -44.625, 131)),
            ]);
    }

    private static async Task<string> Sent(AdventureAsk ask, int request, params IReadOnlyList<LlmStreamEvent>[] rounds)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var provider = new RoundScriptedLlmProvider([.. rounds, .. Enumerable.Repeat(Saying("{}"), 8)]);

        await Generator(provider).GenerateAsync(ask, Now, CancellationToken.None);

        return provider.Requests[request].Prompt.History[0].Text;
    }

    private static async Task<string> Revised(Adventure draft, AdventureAsk ask, IReadOnlyList<AdventureRemark> exchange, string remark)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var provider = new RoundScriptedLlmProvider([.. Enumerable.Repeat(Saying("{}"), 8)]);

        await Generator(provider).ReviseAsync(draft, ask, exchange, remark, Now, CancellationToken.None);

        return provider.Requests[0].Prompt.History[0].Text;
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
