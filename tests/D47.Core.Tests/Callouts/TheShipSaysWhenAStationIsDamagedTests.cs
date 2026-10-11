using System.Text.Json;
using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

public class TheShipSaysWhenAStationIsDamagedTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Docked(string? state)
    {
        var fields = new Dictionary<string, object?>
        {
            ["timestamp"] = "2026-10-10T12:00:00Z",
            ["event"] = "Docked",
            ["StationName"] = "Galileo",
            ["StationType"] = "Coriolis",
            ["MarketID"] = 128000000,
        };
        if (state is not null)
        {
            fields["StationState"] = state;
        }

        Assert.True(JournalEvent.TryParse(JsonSerializer.Serialize(fields), NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static List<Announcement> Examine(bool priming, params JournalEvent[] events) =>
        [.. new StationDamageCallout().Examine(new CalloutContext(Now, priming, null, GameStatus.Unknown, NavRoute.None, events))];

    [Theory]
    [InlineData("UnderAttack", "Galileo is under attack.")]
    [InlineData("Damaged", "Galileo is damaged.")]
    [InlineData("DamagedHuman", "Galileo is damaged.")]
    [InlineData("UnderRepairs", "Galileo is under repairs.")]
    [InlineData("Abandoned", "Galileo is abandoned.")]
    public void ADamagedStationIsNamedOnDocking(string state, string line)
    {
        var said = Assert.Single(Examine(false, Docked(state)));
        Assert.Equal(line, said.Text);
    }

    [Theory]
    [InlineData("Construction")]
    [InlineData("Boom")]
    [InlineData(null)]
    public void OtherStatesSayNothing(string? state)
    {
        Assert.Empty(Examine(false, Docked(state)));
    }

    [Fact]
    public void NothingIsSaidWhilePriming()
    {
        Assert.Empty(Examine(true, Docked("Damaged")));
    }
}
