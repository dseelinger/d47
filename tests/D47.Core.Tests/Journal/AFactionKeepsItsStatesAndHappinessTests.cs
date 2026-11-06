using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class AFactionKeepsItsStatesAndHappinessTests
{
    private static JournalEvent Event(string kind, string at, string active, string happiness = "Happy")
    {
        var json = "{\"timestamp\":\"" + at + "\",\"event\":\"" + kind + "\",\"StarSystem\":\"Alpha\",\"SystemAddress\":1,"
            + "\"Factions\":[{\"Name\":\"Alpha Mining\",\"Influence\":0.4,\"Government\":\"$government_Democracy;\","
            + "\"Allegiance\":\"Federation\",\"Happiness\":\"$Faction_HappinessBand2;\",\"Happiness_Localised\":\"" + happiness + "\","
            + "\"ActiveStates\":[" + active + "],\"PendingStates\":[{\"State\":\"Boom\",\"Trend\":1}],"
            + "\"RecoveringStates\":[{\"State\":\"Famine\",\"Trend\":0}]},"
            + "{\"Name\":\"Beta Union\",\"Influence\":0.6}]}";

        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));

        return parsed!;
    }

    private static SystemStandings Fold(params JournalEvent[] events) =>
        events.Aggregate(SystemStandings.Empty, (standings, e) => standings.Apply(e));

    private static DateTimeOffset At(string time) => DateTimeOffset.Parse(time, System.Globalization.CultureInfo.InvariantCulture);

    [Theory]
    [InlineData("Location")]
    [InlineData("FSDJump")]
    [InlineData("CarrierJump")]
    public void TheFieldsAreReadFromEachEventThatCarriesFactions(string kind)
    {
        var faction = Fold(Event(kind, "2026-10-10T10:00:00Z", "{\"State\":\"War\"},{\"State\":\"Election\"}"))
            .Latest("Alpha")!.Factions[0];

        Assert.Equal("$government_Democracy;", faction.Government);
        Assert.Equal("Federation", faction.Allegiance);
        Assert.Equal("Happy", faction.Happiness);
        Assert.Equal(["War", "Election"], faction.ActiveStates);
        Assert.Equal(["Boom"], faction.PendingStates);
        Assert.Equal(["Famine"], faction.RecoveringStates);
    }

    [Fact]
    public void AMissingFieldReadsAsEmpty()
    {
        var faction = Fold(Event("FSDJump", "2026-10-10T10:00:00Z", string.Empty)).Latest("Alpha")!.Factions[1];

        Assert.Equal("Beta Union", faction.Name);
        Assert.Equal(string.Empty, faction.Government);
        Assert.Equal(string.Empty, faction.Allegiance);
        Assert.Equal(string.Empty, faction.Happiness);
        Assert.Empty(faction.ActiveStates);
        Assert.Empty(faction.PendingStates);
        Assert.Empty(faction.RecoveringStates);
    }

    private static bool SpanHolds(string firstActive, string secondActive)
    {
        var first = Fold(Event("FSDJump", "2026-10-10T10:00:00Z", firstActive)).Latest("Alpha")!;
        var second = Fold(Event("Location", "2026-10-10T12:00:00Z", secondActive)).Latest("Alpha")!;
        var span = new StandingsSpan(first.SeenAt, first.SeenAt, first.Factions, first.Conflicts);

        return span.Holds(second);
    }

    [Fact]
    public void AnUnchangedReadingExtendsTheCurrentSpan()
    {
        Assert.True(SpanHolds("{\"State\":\"War\"}", "{\"State\":\"War\"}"));
    }

    [Fact]
    public void AReadingThatDiffersOnlyInAStateStartsANewSpan()
    {
        Assert.False(SpanHolds("{\"State\":\"War\"}", "{\"State\":\"Boom\"}"));
    }
}
