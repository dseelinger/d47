using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A chapter's ask carries the chapter before it in full and the ones before that as a name and a
/// premise, and the draft records which adventure it follows.
/// </summary>
public sealed class ANextChapterReadsTheOneBeforeItTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = """
        {"name": "The Second Column", "premise": "The ledger has a sequel.", "want": "To find who signed.",
         "stake": "Whether a name is a debt.", "turn": "The name was hers.", "ending": "It is paid."}
        """;

    private const string Beats = """
        {"opening": "Somebody is paying again.", "reply": "Here it is.", "beats": [
          {"title": "The Lantern", "function": "setup", "kind": "arrive", "system": "Ossen's Lantern", "line": "Scoop here."},
          {"title": "The Anchorage", "function": "turn", "kind": "dock", "system": "Dyson's Hollow", "station": "Maren Anchorage", "line": "To one name."},
          {"title": "The Column", "function": "resolution", "kind": "rank", "career": "Trader", "rank": 8, "line": "It balances."}
        ]}
        """;

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "d47-next-chapter", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static Adventure First() => LanternRoute(Accepted) with
    {
        Key = "first",
        Name = "The First Chapter",
        Spine = new AdventureSpine { Premise = "First premise.", Want = "First want.", Turn = "First turn." },
    };

    private static Adventure Second() => LanternRoute(Accepted) with
    {
        Key = "second",
        Name = "The Second Chapter",
        Follows = "first",
        Spine = new AdventureSpine { Premise = "Second premise.", Want = "Second want.", Turn = "Second turn." },
    };

    private static Adventure Third() => LanternRoute(Accepted) with
    {
        Key = "third",
        Name = "The Third Chapter",
        Follows = "second",
        Spine = new AdventureSpine
        {
            Premise = "Third premise.",
            Want = "Third want.",
            Stake = "Third stake.",
            Turn = "Third turn.",
            Ending = "Third ending.",
        },
        Told =
        [
            new AdventureTold { Kind = AdventureToldKind.Beat, Text = "The beacon still runs, said aloud.", At = Accepted },
            new AdventureTold { Kind = AdventureToldKind.Aside, Text = "Nobody paid for it.", Asked = "who pays?", At = Accepted },
        ],
    };

    [Fact]
    public async Task TheChapterPromptHoldsThePreviousChapterInFullAndOnlyNamesTheOnesBefore()
    {
        var chapter = AdventureChapter.Of([First(), Second(), Third()], "third");

        Assert.NotNull(chapter);
        Assert.Equal(["first", "second"], chapter.Earlier.Select(adventure => adventure.Key));

        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = provider.Requests[0].Prompt.History[0].Text;

        // The previous chapter in full.
        Assert.Contains("Title: The Third Chapter", prompt);
        Assert.Contains("Want: Third want.", prompt);
        Assert.Contains("Stake: Third stake.", prompt);
        Assert.Contains("Turn: Third turn.", prompt);
        Assert.Contains("Ending: Third ending.", prompt);
        Assert.Contains("Beats: " + string.Join("; ", LanternRoute().Beats.Select((beat, index) => $"{index + 1}. {beat.Title}")), prompt);
        Assert.Contains("- The beacon still runs, said aloud.", prompt);
        Assert.Contains("- (the Commander asked \"who pays?\") Nobody paid for it.", prompt);
        Assert.Contains("its stake is the belief that chapter left open", prompt);

        // The ones before it by name and premise, oldest first, and nothing more.
        Assert.Contains("- The First Chapter: First premise.", prompt);
        Assert.Contains("- The Second Chapter: Second premise.", prompt);
        Assert.True(prompt.IndexOf("The First Chapter", StringComparison.Ordinal) < prompt.IndexOf("The Second Chapter", StringComparison.Ordinal));
        Assert.DoesNotContain("First want.", prompt);
        Assert.DoesNotContain("Second turn.", prompt);

        Assert.Equal("third", outcome.Draft!.Follows);
    }

    [Fact]
    public async Task APreviousChapterWithNothingRecordedOffersItsWrittenLines()
    {
        var chapter = AdventureChapter.Of([Third() with { Told = [], Follows = null }], "third");
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = provider.Requests[0].Prompt.History[0].Text;
        Assert.Contains("What was written to be said as they flew it:", prompt);
        Assert.Contains($"- {LanternRoute().Beats[0].Line}", prompt);
        Assert.DoesNotContain("The chapters before that one", prompt);
    }

    [Fact]
    public async Task AnOrdinaryAskIsNotAChapter()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.Null(outcome.Draft!.Follows);
        Assert.DoesNotContain("next chapter", provider.Requests[0].Prompt.History[0].Text);
    }

    [Fact]
    public void AChainThatLoopsStopsWhereItStarted()
    {
        var chapter = AdventureChapter.Of([First() with { Follows = "second" }, Second()], "second");

        Assert.Equal(["first"], chapter!.Earlier.Select(adventure => adventure.Key));
    }

    [Fact]
    public void FollowsSurvivesTheFileAndAFileWithoutItLoadsUnchanged()
    {
        var path = Path.Combine(_folder, "adventures.json");
        var store = new AdventureStore(path, NullLogger<AdventureStore>.Instance);

        Assert.Null(store.Save("F1", First()));
        Assert.Null(store.Save("F1", Second()));

        Assert.DoesNotContain("\"follows\": null", File.ReadAllText(path));

        var reread = new AdventureStore(path, NullLogger<AdventureStore>.Instance);
        reread.Poll();

        Assert.Empty(reread.Problems);
        Assert.Null(reread.Find("F1", "first")!.Follows);
        Assert.Equal("first", reread.Find("F1", "second")!.Follows);
    }
}
