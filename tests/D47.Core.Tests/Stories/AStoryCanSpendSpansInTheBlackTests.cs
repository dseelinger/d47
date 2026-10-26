using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Stories;
using D47.Core.Tests.Adventures;
using D47.Core.Tests.Conversation;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A story's hidden entry may mark spans of its beat sheet as in the black; a chapter whose stage hands out a beat in a span
/// is held to neither the Sol band nor the home leg (#1737).
/// </summary>
public sealed class AStoryCanSpendSpansInTheBlackTests
{
    private static readonly StarPosition FarOut = new(3000, 0, 0);

    private static readonly AdventureStory Chapter = new("s", "S", "public", "hidden", 1, 10, 3);

    private static StorySecret Spanning(params StoryBlackSpan[] spans) => Secret with { Black = spans };

    [Fact]
    public void AStageHandingOutABeatInsideASpanIsCovered()
    {
        var secret = Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint"));

        Assert.True(StoryBlack.Covers(secret, ["breakIntoTwo", "bStory"]));
        Assert.True(StoryBlack.Covers(secret, ["bStory", "funAndGames"]));
        Assert.False(StoryBlack.Covers(secret, ["midpoint"]));
        Assert.False(StoryBlack.Covers(secret, ["openingImage", "themeStated", "setUp", "catalyst", "debate"]));
    }

    [Fact]
    public void AWholeStorySpanCoversEveryStage() =>
        Assert.All(StoryBeats.Keys, beat => Assert.True(StoryBlack.Covers(Spanning(new StoryBlackSpan(null, null)), [beat])));

    [Fact]
    public void AnEntryWithNoSpansCoversNothing()
    {
        Assert.False(StoryBlack.Covers(Secret, StoryBeats.Keys));
        Assert.False(StoryBlack.Covers(Spanning(), StoryBeats.Keys));
    }

    [Fact]
    public void AChapterInTheBlackHasNoBandAndOneOutsideKeepsIt()
    {
        Assert.Null(ChapterBand.Of(new StarPosition(588, 0, 0), new AdventureAsk(Story: Chapter with { InTheBlack = true })));
        Assert.Equal(new ChapterBand.Range(288, 488), ChapterBand.Of(new StarPosition(588, 0, 0), new AdventureAsk(Story: Chapter)));
    }

    [Fact]
    public void AChapterInTheBlackReservesNoPlaceForTheBandStop()
    {
        Assert.False(ChapterBand.ReservesAPlace(FarOut, inTheBlack: true));
        Assert.True(ChapterBand.ReservesAPlace(FarOut, inTheBlack: false));
        Assert.False(ChapterBand.ReservesAPlace(new StarPosition(100, 0, 0), inTheBlack: false));
    }

    [Fact]
    public async Task AChapterInTheBlackSetsNoHomeTargetAndClearsAStoredOne()
    {
        using var fixtures = FarOutWithAStoredTarget(Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint")));

        Assert.Null(await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None));

        var asked = Assert.Single(fixtures.Asks).Story!;
        Assert.True(asked.InTheBlack);
        Assert.Null(asked.Home);
        Assert.Null(fixtures.Stories.Current("F1")!.HomeTarget);
    }

    [Fact]
    public async Task AChapterOutsideEverySpanIsWrittenAsBefore()
    {
        using var fixtures = FarOutWithAStoredTarget(Spanning(new StoryBlackSpan("midpoint", null)));

        await fixtures.Director.WriteNextAsync("F1", Now, CancellationToken.None);

        var asked = fixtures.Asks[0].Story!;
        Assert.False(asked.InTheBlack);
        Assert.Equal(HomeLeg.Sol, asked.Home);
    }

    [Fact]
    public async Task TheObjectivesPromptSaysTheChapterIsInTheBlackInPlaceOfTheBand()
    {
        var provider = new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(Spine), RoundScriptedLlmProvider.Saying(BeatsElsewhere));

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy())
            .GenerateAsync(new AdventureAsk(Story: Chapter with { InTheBlack = true }), Now, CancellationToken.None);

        var brief = provider.Requests[1].Prompt.History[0].Text;
        Assert.Contains("This chapter of the story takes place in the black.", brief, StringComparison.Ordinal);
        Assert.DoesNotContain("outside the bubble", brief, StringComparison.Ordinal);
    }

    [Fact]
    public void ASealedEntryWithoutTheFieldLoadsAsBefore()
    {
        var loaded = Assert.Single(StoryCatalog.Unseal(Sealed(Secret)));

        Assert.Null(loaded.Black);
        Assert.False(StoryBlack.Covers(loaded, StoryBeats.Keys));
        Assert.Empty(StoryCatalog.Faults(Card, loaded));
    }

    [Fact]
    public void ASealedEntryWithSpansReadsThemBack() =>
        Assert.Equal(
            [new StoryBlackSpan(null, "catalyst"), new StoryBlackSpan("midpoint", null)],
            Assert.Single(StoryCatalog.Unseal(Sealed(Spanning(new StoryBlackSpan(null, "catalyst"), new StoryBlackSpan("midpoint", null))))).Black);

    [Theory]
    [InlineData("climax", "midpoint", "black[0].from is climax, which is not a beat of a 1 year story.")]
    [InlineData("midpoint", "catalyst", "black[0] ends at or before the beat it starts at.")]
    [InlineData("midpoint", "midpoint", "black[0] ends at or before the beat it starts at.")]
    public void ASpanOffTheSheetOrOutOfOrderIsAFault(string from, string to, string fault) =>
        Assert.Contains($"{Id}: {fault}", StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan(from, to))));

    [Fact]
    public void SpansThatOverlapAreAFaultAndSpansThatMeetAreNot()
    {
        Assert.Contains($"{Id}: black[1] overlaps black[0].", StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan("catalyst", "midpoint"), new StoryBlackSpan("bStory", "allIsLost"))));
        Assert.Contains($"{Id}: black[1] overlaps black[0].", StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan(null, null), new StoryBlackSpan("midpoint", "allIsLost"))));
        Assert.Empty(StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan("catalyst", "midpoint"), new StoryBlackSpan("midpoint", null))));
    }

    private static StoryFixtures FarOutWithAStoredTarget(StorySecret secret)
    {
        var fixtures = new StoryFixtures(new RoundScriptedLlmProvider(RoundScriptedLlmProvider.Saying(NextSpine), RoundScriptedLlmProvider.Saying(NextBeats)), secret);
        var game = Loaded(1_000_000);

        game.Apply(AdventureFixtures.Event(
            $$"""{ "timestamp":"{{AdventureFixtures.Stamp(Now)}}", "event":"Location", "StarSystem":"Far Out", "StarPos":[3000,0,0], "SystemAddress":1, "Docked":false }"""));
        fixtures.Director.Game = () => game;
        fixtures.Stories.Save("F1", new Story
        {
            Id = Id,
            Title = Card.Title,
            PublicLayer = Card.Describe(),
            PickedAt = Now.AddDays(-30),
            BeaconScanAt = Now.AddDays(-20),
            HomeTarget = HomeLeg.Sol,
        });

        return fixtures;
    }

    private static string Sealed(StorySecret secret)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };

        using var packed = new MemoryStream();

        using (var deflate = new System.IO.Compression.DeflateStream(packed, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(System.Text.Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new[] { secret }, options)));
        }

        return Convert.ToBase64String(packed.ToArray());
    }
}
