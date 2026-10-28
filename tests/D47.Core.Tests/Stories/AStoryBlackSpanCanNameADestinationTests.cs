using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A span in the black may name one destination from <see cref="StoryDestinations"/> (#1849).</summary>
public sealed class AStoryBlackSpanCanNameADestinationTests
{
    private static StorySecret Spanning(params StoryBlackSpan[] spans) => Secret with { Black = spans };

    [Fact]
    public void ADestinationIsFoundByNameWhateverTheCase()
    {
        Assert.Equal(StarPosition.Origin, StoryDestinations.PositionOf("sol"));
        Assert.Equal(new StarPosition(25.21875, -20.90625, 25899.96875), StoryDestinations.PositionOf("SAGITTARIUS A*"));
        Assert.Equal(new StarPosition(-1111.5625, -134.21875, 65269.75), StoryDestinations.PositionOf("Beagle Point"));
        Assert.NotNull(StoryDestinations.PositionOf("Colonia"));
        Assert.Null(StoryDestinations.PositionOf("Mars"));
    }

    [Fact]
    public void ASpanWithNoTowardIsUnchanged()
    {
        Assert.Empty(StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint"))));
        Assert.Null(StoryBlack.Toward(Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint")), ["bStory"]));
    }

    [Fact]
    public void AKnownDestinationInTheMiddleOfAStoryPasses() =>
        Assert.Empty(StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint", "colonia"))));

    [Fact]
    public void ADestinationNotInTheListIsAFaultThatNamesTheList() =>
        Assert.Contains(
            $"{Id}: black[0].toward is Mars, which is not one of Sol, Colonia, Sagittarius A*, Beagle Point.",
            StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint", "Mars"))));

    [Theory]
    [InlineData("midpoint", null)]
    [InlineData("breakIntoThree", "finale")]
    [InlineData("darkNightOfTheSoul", "finalImage")]
    public void ADestinationOnASpanThatReachesTheFinaleIsAFault(string from, string? to) =>
        Assert.Contains(
            $"{Id}: black[0].toward is set on a span that reaches the finale, which has its own destination.",
            StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan(from, to, "Sol"))));

    [Fact]
    public void ADestinationOnASpanThatEndsBeforeTheFinaleIsNotAFault() =>
        Assert.Empty(StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan("midpoint", "breakIntoThree", "Sol"))));

    [Theory]
    [InlineData(null)]
    [InlineData("catalyst")]
    public void ADestinationOnASpanStartingInActOneOfARealBeaconStoryIsAFault(string? from) =>
        Assert.Contains(
            $"{Id}: black[0].toward is set on a span that starts before breakIntoTwo, in a 1 year story that flies to a real beacon.",
            StoryCatalog.Faults(Card, Spanning(new StoryBlackSpan(from, "midpoint", "Sol"))));

    [Fact]
    public void TheDestinationOfTheSpanCoveringTheLatestBeatWins()
    {
        var secret = Spanning(
            new StoryBlackSpan("breakIntoTwo", "bStory", "Sol"),
            new StoryBlackSpan("bStory", "midpoint", "Colonia"));

        Assert.Equal("Sol", StoryBlack.Toward(secret, ["breakIntoTwo"]));
        Assert.Equal("Colonia", StoryBlack.Toward(secret, ["breakIntoTwo", "bStory"]));
        Assert.Equal("Colonia", StoryBlack.Toward(secret, ["bStory", "breakIntoTwo"]));
        Assert.Null(StoryBlack.Toward(secret, ["midpoint"]));
    }

    [Fact]
    public void TheLatestCoveredBeatDecidesEvenWhenALaterBeatIsOutsideEverySpan() =>
        Assert.Equal("Sol", StoryBlack.Toward(Spanning(new StoryBlackSpan("breakIntoTwo", "bStory", "Sol")), ["breakIntoTwo", "bStory"]));

    [Fact]
    public void ASealedEntryReadsTowardBack() =>
        Assert.Equal("Sol", Assert.Single(StoryCatalog.Unseal(SealedWith(Spanning(new StoryBlackSpan("breakIntoTwo", "midpoint", "Sol"))))).Black![0].Toward);

    private static string SealedWith(StorySecret secret)
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
