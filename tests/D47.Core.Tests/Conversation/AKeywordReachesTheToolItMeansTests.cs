using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>Which tool a declared keyword actually reaches.</summary>
public class AKeywordReachesTheToolItMeansTests
{
    private static KeywordRouter Router(TempInstall install) =>
        new(TestSurface.For(install).Registry);

    /// <summary>Four sentences that were broken the same way, three of which nobody had asked yet.</summary>
    [Theory]
    [InlineData("where is my fleet carrier", "get_fleet")]
    [InlineData("where is my carrier", "get_fleet")]
    [InlineData("what ships do i own", "get_fleet")]
    [InlineData("what materials am i carrying", "get_materials")]
    [InlineData("how have i done this session", "get_session_summary")]
    [InlineData("what am i flying", "get_ship")]
    public void AQuestionReachesTheToolThatAnswersIt(string asked, string tool)
    {
        using var install = new TempInstall();

        var match = Router(install).MatchToolCommand(asked)
            ?? (ToolCommandMatch?)null;

        Assert.NotNull(match);
        Assert.Equal(tool, match!.ToolName);
    }

 /// <summary>And the keyword route now reaches it too.</summary>
    [Fact]
    public void ACapabilityKeywordNamesTheToolItMeans()
    {
        using var install = new TempInstall();

        var match = Router(install).Match("where is my fleet carrier");

        Assert.NotNull(match);
        Assert.Equal("get_fleet", match!.ToolName);
    }

 /// <summary>And it reaches it asking the question that was asked.</summary>
    [Theory]
    [InlineData("where is my carrier", false)]
    [InlineData("what system is my fleet carrier in", false)]
    [InlineData("what ships do i own", true)]
    [InlineData("what ships are on the carrier", true)]
    public void AskingWhereTheCarrierIsDoesNotAskForTheShipList(string asked, bool ships)
    {
        using var install = new TempInstall();

        var match = Router(install).MatchToolCommand(asked);

        Assert.NotNull(match);
        Assert.Equal("get_fleet", match!.ToolName);
        Assert.Equal(ships, match.Arguments.TryGetBoolean("ships", out var asks) && asks);
    }

    /// <summary>The same claim on the capability-keyword route, which is the one that catches a padded
    /// sentence.</summary>
    [Theory]
    [InlineData("so where is my carrier parked at the moment", false)]
    [InlineData("where's my fleet carrier at the moment", false)]
    [InlineData("how far is my fleet carrier from here", false)]
    [InlineData("remind me what ships do i own again", true)]
    public void ThePaddedQuestionCarriesTheSameAnswer(string asked, bool ships)
    {
        using var install = new TempInstall();

        var match = Router(install).Match(asked);

        Assert.NotNull(match);
        Assert.Equal("get_fleet", match!.ToolName);
        Assert.Equal(ships, match.Arguments.TryGetBoolean("ships", out var asks) && asks);
    }

    /// <summary>And the answer it now reaches is the Commander's own carrier, not a squadron's.</summary>
    [Fact]
    public void ASquadronCarrierDoesNotMoveTheCommandersOwn()
    {
        var store = new D47.Core.Journal.GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-21T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-08-21T09:00:00Z", "event":"CarrierStats", "CarrierID":3715429376, "Callsign":"KLM-31F", "Name":"JOHN DEPARAGON", "FuelLevel":800, "DockingAccess":"all" }""",
                     """{ "timestamp":"2026-08-21T09:33:30Z", "event":"CarrierLocation", "CarrierType":"FleetCarrier", "CarrierID":3715429376, "StarSystem":"Wyrd" }""",

                     // Three seconds later, and it is not the Commander's.
                     """{ "timestamp":"2026-08-21T09:33:33Z", "event":"CarrierLocation", "CarrierType":"SquadronCarrier", "CarrierID":3713474048, "StarSystem":"Col 285 Sector GT-G c11-9" }""",
                 })
        {
            Assert.True(D47.Core.Journal.JournalEvent.TryParse(
                line, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));

            store.Apply(parsed!);
        }

        Assert.Equal("Wyrd", store.Active!.Carrier.StarSystem);
        Assert.Equal("JOHN DEPARAGON", store.Active!.Carrier.Name);
    }

    /// <summary>A journal from before Frontier added <c>CarrierType</c> still reports a position.</summary>
    [Fact]
    public void AnEventWithNoCarrierTypeIsStillTheCommandersOwn()
    {
        var store = new D47.Core.Journal.GameStateStore();

        foreach (var line in new[]
                 {
                     """{ "timestamp":"2026-08-21T08:00:00Z", "event":"Commander", "FID":"F1", "Name":"Jameson" }""",
                     """{ "timestamp":"2026-08-21T09:00:00Z", "event":"CarrierLocation", "CarrierID":3712682240, "Callsign":"X9K-B1T", "StarSystem":"Deciat" }""",
                 })
        {
            Assert.True(D47.Core.Journal.JournalEvent.TryParse(
                line, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, out var parsed));

            store.Apply(parsed!);
        }

        Assert.Equal("Deciat", store.Active!.Carrier.StarSystem);
    }

    /// <summary>
    /// And the one that was always right stays right: asking where you are is the question
    /// <c>get_location</c> is for.
    /// </summary>
    [Theory]
    [InlineData("where am i")]
    [InlineData("am i docked")]
    public void AndAskingWhereYouAreStillReachesTheLocation(string asked)
    {
        using var install = new TempInstall();

        var tool = Router(install).MatchToolCommand(asked)?.ToolName
                   ?? Router(install).Match(asked)?.ToolName;

        Assert.Equal("get_location", tool);
    }
}
