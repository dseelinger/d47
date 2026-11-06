using D47.Core.Storage;
using D47.Core.Adventures;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A chapter's ask carries what the chapter before it asked of the Commander and how it ended, the
/// ones before that as a count, and the draft records which adventure it follows.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ANextChapterReadsTheOneBeforeItTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private const string Spine = SecondColumnSpine;

    private const string Beats = SecondColumnBeats;

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
    public async Task TheChapterPromptHoldsWhatTheCommanderDidAndNoEarlierWording()
    {
        var chapter = AdventureChapter.Of([First(), Second(), Third() with { Ending = new AdventureEnding("Gone to ground", Accepted.AddMinutes(4), "It ended there.") }], "third");

        Assert.NotNull(chapter);
        Assert.Equal(["first", "second"], chapter.Earlier.Select(adventure => adventure.Key));

        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = provider.Requests[0].Prompt.History[0].Text;

        // What the Commander did in the previous chapter, and nothing of its wording.
        Assert.Contains("Objectives: " + string.Join("; ", LanternRoute().Beats.Select((beat, index) => $"{index + 1}. {beat.Title}")), prompt);
        Assert.Contains("What those objectives asked for:", prompt);
        Assert.Contains("- The Commander asked \"who pays?\": Nobody paid for it.", prompt);
        Assert.Contains("It ended in the ending \"Gone to ground\", one of its own: It ended there.", prompt);
        Assert.Contains("It follows from what the Commander did in the last chapter", prompt);

        Assert.DoesNotContain("The Third Chapter", prompt);
        Assert.DoesNotContain("Third premise.", prompt);
        Assert.DoesNotContain("Third want.", prompt);
        Assert.DoesNotContain("Third stake.", prompt);
        Assert.DoesNotContain("Third turn.", prompt);
        Assert.Contains("How its spine said it ends: Third ending.", prompt);
        Assert.DoesNotContain("The beacon still runs, said aloud.", prompt);
        Assert.DoesNotContain("the belief that chapter left open", prompt);

        // The chapters before it are a count.
        Assert.Contains("2 chapters came before it.", prompt);
        Assert.DoesNotContain("The First Chapter", prompt);
        Assert.DoesNotContain("The Second Chapter", prompt);
        Assert.DoesNotContain("First premise.", prompt);
        Assert.DoesNotContain("Second premise.", prompt);

        Assert.Equal("third", outcome.Draft!.Follows);
    }

    [Fact]
    public async Task TheChapterPromptsNameWhatTheCommanderDidAndOfferEveryActivity()
    {
        var bounties = new AdventureTrigger { Kind = TriggerKind.Bounty, Count = 3 };
        var third = Third() with { Beats = [.. Third().Beats, Beat("The Ledger", "finale", bounties, "Three names.")] };
        var chapter = AdventureChapter.Of([First(), Second(), third], "third");
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var spine = provider.Requests[0].Prompt.History[0].Text;
        Assert.DoesNotContain("The only things they can do", spine);
        Assert.Contains("the Commander does the game's activities", spine);
        Assert.Contains($"What those objectives asked for: 1. {LanternRoute().Beats[0].Trigger.Describe()}", spine);
        Assert.Contains($"6. {bounties.Describe()}", spine);
        Assert.Contains("Vary what the Commander does. The last chapters asked them to collect bounties.", spine);

        var beats = provider.Requests[2].Prompt.History[0].Text;
        Assert.Contains("The counted kinds, in no order of preference:", beats);
        Assert.Contains("Vary what the Commander does. The last chapters asked them to collect bounties.", beats);
        Assert.Contains("Mission_Smuggle", beats);
        Assert.Contains("Mission_PassengerVIP", beats);
    }

    [Fact]
    public async Task APreviousChapterWithNothingRecordedIsNotGivenItsWrittenLines()
    {
        var chapter = AdventureChapter.Of([Third() with { Told = [], Follows = null }], "third");
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = provider.Requests[0].Prompt.History[0].Text;
        Assert.DoesNotContain("What was written to be said as they flew it", prompt);
        Assert.DoesNotContain($"- {LanternRoute().Beats[0].Line}", prompt);
        Assert.DoesNotContain("chapters came before it", prompt);
    }

    [Fact]
    public async Task AStockStoryChapterIsToldToFollowTheStoryLayersAndStageBeats()
    {
        var chapter = AdventureChapter.Of([Third()], "third");
        var story = new AdventureStory("s", "S", "public layer text", "hidden layer text", 2, 10, 3);
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(Beats));

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter, Story: story), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);

        var prompt = provider.Requests[0].Prompt.History[0].Text;
        Assert.Contains("from the story's public and hidden layers and the beat-sheet lines for its stage", prompt);
        Assert.Contains("public layer text", prompt);
        Assert.Contains("hidden layer text", prompt);
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
        var store = new AdventureStore(path, new DiskFileSystem(), NullLogger<AdventureStore>.Instance);

        Assert.Null(store.Save("F1", First()));
        Assert.Null(store.Save("F1", Second()));

        Assert.DoesNotContain("\"follows\": null", File.ReadAllText(path));

        var reread = new AdventureStore(path, new DiskFileSystem(), NullLogger<AdventureStore>.Instance);
        reread.Poll();

        Assert.Empty(reread.Problems);
        Assert.Null(reread.Find("F1", "first")!.Follows);
        Assert.Equal("first", reread.Find("F1", "second")!.Follows);
    }
}
