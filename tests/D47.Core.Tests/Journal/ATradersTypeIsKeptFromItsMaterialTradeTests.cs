using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ATradersTypeIsKeptFromItsMaterialTradeTests
{
    private const long Market = 3226424832;

    private static string Trade(long marketId, string type) =>
        "{\"timestamp\":\"2026-10-10T10:00:00Z\",\"event\":\"MaterialTrade\",\"MarketID\":" + marketId
        + ",\"TraderType\":\"" + type + "\",\"Paid\":{\"Material\":\"iron\",\"Category\":\"Raw\",\"Quantity\":6},"
        + "\"Received\":{\"Material\":\"nickel\",\"Category\":\"Raw\",\"Quantity\":1}}";

    private static string Header(string version) =>
        "{\"timestamp\":\"2026-10-10T09:00:00Z\",\"event\":\"Fileheader\",\"gameversion\":\"" + version + "\"}";

    private static TraderSightings Folded(params string[] lines)
    {
        var sightings = new TraderSightings(new MemoryFileSystem(), NullLogger.Instance);
        sightings.Fold(lines);
        return sightings;
    }

    [Theory]
    [InlineData("raw", "Raw")]
    [InlineData("manufactured", "Manufactured")]
    [InlineData("encoded", "Encoded")]
    public void ATradeNamesTheTradersTypeAsTheSearchSpellsIt(string written, string spelled) =>
        Assert.Equal(spelled, Folded(Trade(Market, written)).TypeOf(Market));

    [Fact]
    public void AMarketWithNoTradeHasNoType() =>
        Assert.Null(Folded(Trade(Market, "raw")).TypeOf(Market + 1));

    [Fact]
    public void TheLaterOfTwoTradesWins() =>
        Assert.Equal("Encoded", Folded(Trade(Market, "raw"), Trade(Market, "encoded")).TypeOf(Market));

    [Fact]
    public void ATypeTheSearchDoesNotKnowIsLeftOut() =>
        Assert.Null(Folded(Trade(Market, "grade5")).TypeOf(Market));

    [Fact]
    public void ALegacyJournalIsNotFolded()
    {
        var sightings = Folded(Header("3.8.0.404"), Trade(Market, "raw"));

        Assert.Null(sightings.TypeOf(Market));
    }

    [Fact]
    public void ACurrentJournalAfterALegacyOneIsFolded() =>
        Assert.Equal("Raw", Folded(Header("3.8.0.404"), Header("4.1.0.0"), Trade(Market, "raw")).TypeOf(Market));

    [Fact]
    public void EveryCommandersTradeCounts()
    {
        var fileSystem = new MemoryFileSystem();
        fileSystem.WriteLines("j/Journal.2026-10-10T000000.01.log", [Header("4.1.0.0"), Trade(Market, "raw")]);
        fileSystem.WriteLines("j/Journal.2026-10-11T000000.01.log", [Header("4.1.0.0"), Trade(Market + 1, "encoded")]);
        var sightings = new TraderSightings(fileSystem, NullLogger.Instance);

        sightings.FoldHistory(["j/Journal.2026-10-10T000000.01.log", "j/Journal.2026-10-11T000000.01.log"], TestContext.Current.CancellationToken);

        Assert.Equal("Raw", sightings.TypeOf(Market));
        Assert.Equal("Encoded", sightings.TypeOf(Market + 1));
    }

    [Fact]
    public void ALiveEventIsFolded()
    {
        var sightings = Folded();

        sightings.Apply([JournalEvent.TryParse(Trade(Market, "manufactured"), NullLogger.Instance, out var parsed) ? parsed! : throw new InvalidOperationException()]);

        Assert.Equal("Manufactured", sightings.TypeOf(Market));
    }
}
