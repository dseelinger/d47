using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A span's <c>"toward"</c> may be a name, a system with its position, or a list of either (#2253).</summary>
public sealed class AStoryBlackSpanCanNameAnySystemTests
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

    private static StorySecret Spanning(StoryToward toward) => Secret with { Black = [new StoryBlackSpan("breakIntoTwo", "midpoint", toward)] };

    private static StoryToward Stops(params StoryStop[] stops) => new(stops) { IsList = true };

    private static StoryStop At(string system, double z) => new(system, null, [0, 0, z]);

    private static StoryToward Read(string toward) => JsonSerializer.Deserialize<StoryBlackSpan>($$"""{ "from": "breakIntoTwo", "to": "midpoint", "toward": {{toward}} }""", Json)!.Toward!;

    [Fact]
    public void ANameReadsAsOneStop()
    {
        var toward = Read("\"Colonia\"");

        Assert.False(toward.IsList);
        Assert.Equal("Colonia", Assert.Single(toward.Stops).System);
        Assert.Equal(StoryDestinations.PositionOf("Colonia"), toward.Stops[0].Place()!.Position);
    }

    [Fact]
    public void AnObjectReadsWithItsAddressAndPosition()
    {
        var stop = Assert.Single(Read("""{ "system": "Hyades Sector", "address": 123, "position": [1, 2, 3] }""").Stops);

        Assert.Equal("Hyades Sector", stop.System);
        Assert.Equal(123, stop.Address);
        Assert.Equal(new StarPosition(1, 2, 3), stop.Place()!.Position);
    }

    [Fact]
    public void AnObjectNeedsNoAddress() =>
        Assert.Null(Assert.Single(Read("""{ "system": "Hyades Sector", "position": [1, 2, 3] }""").Stops).Address);

    [Fact]
    public void AListReadsNamesAndObjectsInOrder()
    {
        var toward = Read("""["Sol", { "system": "Far", "position": [0, 0, 9] }, "Beagle Point"]""");

        Assert.True(toward.IsList);
        Assert.Equal(["Sol", "Far", "Beagle Point"], toward.Stops.Select(stop => stop.System));
    }

    [Fact]
    public void AListWritesBackAsAList() =>
        Assert.Equal("""["Sol",{"system":"Far","position":[0,0,9]}]""", JsonSerializer.Serialize(Read("""["Sol", { "system": "Far", "position": [0, 0, 9] }]"""), Json));

    [Fact]
    public void ASystemWithAPositionPassesWhateverItsName() =>
        Assert.Empty(StoryCatalog.Faults(Card, Spanning(Stops(At("Nowhere Special", 100)))));

    [Fact]
    public void APositionOfTwoNumbersIsAFault() =>
        Assert.Contains(
            $"{Id}: black[0].toward[0].position has 2 numbers, not three.",
            StoryCatalog.Faults(Card, Spanning(Stops(new StoryStop("Far", null, [1, 2])))));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void AnEmptySystemNameIsAFault(string? system) =>
        Assert.Contains(
            $"{Id}: black[0].toward[1] has no system name.",
            StoryCatalog.Faults(Card, Spanning(Stops(At("A", 1), new StoryStop(system, null, [0, 0, 1])))));

    [Fact]
    public void AnObjectWithNoPositionMustBeOneOfTheFourNames() =>
        Assert.Contains(
            $"{Id}: black[0].toward is Mars, which is not one of Sol, Colonia, Sagittarius A*, Beagle Point.",
            StoryCatalog.Faults(Card, Spanning(new StoryToward([new StoryStop("Mars")]))));

    [Fact]
    public void AnEmptyListIsAFault() =>
        Assert.Contains($"{Id}: black[0].toward is an empty list.", StoryCatalog.Faults(Card, Spanning(Stops())));

    [Fact]
    public void ASpanWithAListStillMayNotReachTheFinale() =>
        Assert.Contains(
            $"{Id}: black[0].toward is set on a span that reaches the finale, which has its own destination.",
            StoryCatalog.Faults(Card, Secret with { Black = [new StoryBlackSpan("breakIntoTwo", null, Stops(At("A", 1)))] }));

    [Fact]
    public void AListAdvancesWhenTheCommanderIsWithinAThousandLightYearsOfTheCurrentStop()
    {
        var secret = Spanning(Stops(At("A", 5000), At("B", 20000), At("C", 40000)));
        var legs = new Dictionary<int, int>();

        Assert.Equal("A", StoryBlack.Leg(secret, ["breakIntoTwo"], legs, new StarPosition(0, 0, 3000))!.To.System);
        Assert.Equal("A", StoryBlack.Leg(secret, ["breakIntoTwo"], legs, null)!.To.System);
        Assert.Equal("B", StoryBlack.Leg(secret, ["breakIntoTwo"], legs, new StarPosition(0, 0, 4100))!.To.System);
        Assert.Equal("C", StoryBlack.Leg(secret, ["breakIntoTwo"], new Dictionary<int, int> { [0] = 1 }, new StarPosition(0, 0, 19100))!.To.System);
    }

    [Fact]
    public void TheLastStopStaysTheDestinationOnceReached()
    {
        var leg = StoryBlack.Leg(Spanning(Stops(At("A", 5000), At("B", 20000))), ["breakIntoTwo"], new Dictionary<int, int>(), new StarPosition(0, 0, 4500))!;

        Assert.Equal(1, leg.Leg);
        Assert.Equal("B", leg.To.System);
    }

    [Fact]
    public void AStopNeverGoesBackABeat()
    {
        var leg = StoryBlack.Leg(Spanning(Stops(At("A", 5000), At("B", 20000))), ["breakIntoTwo"], new Dictionary<int, int> { [0] = 1 }, new StarPosition(0, 0, 0))!;

        Assert.Equal("B", leg.To.System);
    }
}
