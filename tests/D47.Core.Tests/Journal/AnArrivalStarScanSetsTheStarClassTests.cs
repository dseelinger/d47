using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AnArrivalStarScanSetsTheStarClassTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalLocation At(string? starClass = null)
    {
        var location = JournalLocation.Unknown.Apply(Event(
            """{"timestamp":"2026-08-18T09:00:00Z","event":"Location","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0]}"""));
        return location with { StarClass = starClass };
    }

    private static string Scan(string fields) =>
        "{\"timestamp\":\"2026-08-18T09:00:01Z\",\"event\":\"Scan\"," + fields + "}";

    [Fact]
    public void TheArrivalStarSetsTheClass()
    {
        var location = At().Apply(Event(Scan("\"StarType\":\"K\",\"DistanceFromArrivalLS\":0.0,\"SystemAddress\":10477373803")));

        Assert.Equal("K", location.StarClass);
    }

    [Fact]
    public void AGiantCodeReplacesTheBareLetterFromStartJump()
    {
        var location = JournalLocation.Unknown
            .Apply(Event("""{"timestamp":"2026-08-18T09:00:00Z","event":"StartJump","JumpType":"Hyperspace","StarSystem":"Sol","StarClass":"K"}"""))
            .Apply(Event("""{"timestamp":"2026-08-18T09:00:05Z","event":"FSDJump","StarSystem":"Sol","SystemAddress":10477373803,"StarPos":[0,0,0]}"""));
        Assert.Equal("K", location.StarClass);

        location = location.Apply(Event(Scan("\"StarType\":\"K_OrangeGiant\",\"DistanceFromArrivalLS\":0.0,\"SystemAddress\":10477373803")));

        Assert.Equal("K_OrangeGiant", location.StarClass);
        Assert.True(StarClasses.IsScoopable(location.StarClass));
    }

    [Fact]
    public void AnotherStarInTheSystemLeavesTheClass()
    {
        var location = At("K").Apply(Event(Scan("\"StarType\":\"M\",\"DistanceFromArrivalLS\":512.3,\"SystemAddress\":10477373803")));

        Assert.Equal("K", location.StarClass);
    }

    [Fact]
    public void AnotherSystemsScanLeavesTheClass()
    {
        var location = At("K").Apply(Event(Scan("\"StarType\":\"M\",\"DistanceFromArrivalLS\":0.0,\"SystemAddress\":99")));

        Assert.Equal("K", location.StarClass);
    }

    [Fact]
    public void ABodyScanLeavesTheClass()
    {
        var location = At("K").Apply(Event(Scan("\"BodyName\":\"Sol 3\",\"PlanetClass\":\"Earthlike body\",\"DistanceFromArrivalLS\":0.0,\"SystemAddress\":10477373803")));

        Assert.Equal("K", location.StarClass);
    }

    [Theory]
    [InlineData("K_OrangeGiant", "class K orange giant")]
    [InlineData("A_BlueWhiteSuperGiant", "class A blue-white supergiant")]
    [InlineData("M_RedGiant", "class M red giant")]
    [InlineData("M_RedSuperGiant", "class M red supergiant")]
    [InlineData("K", "class K")]
    public void GiantCodesAreSpokenInWords(string code, string spoken)
    {
        Assert.Equal(spoken, StarClasses.Speak(code));
    }
}
