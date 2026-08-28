using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>
/// The markets the Commander has stood in themselves (Phase 36) — read from the file the
/// game writes, kept in one beside the executable, and preferred over a report when they are
/// newer.
/// </summary>
public class MarketBookTests
{
    private const string Board =
        """
        {
          "timestamp": "2026-08-19T10:00:00Z",
          "event": "Market",
          "MarketID": 128016640,
          "StationName": "Walz Depot",
          "StationType": "Coriolis",
          "StarSystem": "Sol",
          "Items": [
            {"id":1,"Name":"$gold_name;","Name_Localised":"Gold","BuyPrice":9400,"SellPrice":9300,
             "Stock":13148,"Demand":1,"Rare":false},
            {"id":2,"Name":"$painite_name;","BuyPrice":0,"SellPrice":52282,"Stock":0,"Demand":1200,"Rare":false}
          ]
        }
        """;

    private static MarketBook Book(TempInstall install) =>
        new(Path.Combine(install.Paths.Data, "markets.json"), NullLogger.Instance);

    [Fact]
    public void TheCommandersOwnBoardIsReadWithItsPricesAndItsPosition()
    {
        using var install = new TempInstall();
        File.WriteAllText(Path.Combine(install.Root, MarketReader.MarketFile), Board);

        var book = Book(install);
        var reader = new MarketReader(install.Root, book, NullLogger.Instance);

        Assert.True(reader.Poll(new StarPosition(0, 0, 0)));

        var market = Assert.Single(book.Markets);

        Assert.Equal("Walz Depot", market.Station);
        Assert.Equal("Sol", market.System);
        Assert.Equal(PriceSource.Seen, market.Source);
        Assert.Equal(9_400, market.Quote("Gold")?.BuyPrice);
        Assert.Equal(13_148, market.Quote("Gold")?.Supply);

        // Elite calls supply "Stock" in this file and the index calls it "supply". One name
        // downstream, translated at the two edges rather than in the middle.
        Assert.Equal(1_200, market.Quote("Painite")?.Demand);

        // No _Localised on the second row, so the symbol is folded and cased rather than spoken
        // as "$painite_name;".
        Assert.Equal(52_282, market.Pays("Painite"));

        // Nothing changed on disk, so nothing is filed again.
        Assert.False(reader.Poll(new StarPosition(0, 0, 0)));
    }

    [Fact]
    public void AMarketReadWithoutAPositionIsNotFiled()
    {
        // Market.json names the station and the system and stops. A market with no position
        // cannot be routed to, and filing it anyway would place it at Sol.
        using var install = new TempInstall();
        File.WriteAllText(Path.Combine(install.Root, MarketReader.MarketFile), Board);

        var book = Book(install);
        var reader = new MarketReader(install.Root, book, NullLogger.Instance);

        Assert.False(reader.Poll(position: null));
        Assert.Empty(book.Markets);
    }

    [Fact]
    public void TheBookSurvivesARestart()
    {
        using var install = new TempInstall();
        File.WriteAllText(Path.Combine(install.Root, MarketReader.MarketFile), Board);

        var written = Book(install);

        new MarketReader(install.Root, written, NullLogger.Instance).Poll(new StarPosition(1, 2, 3));

        var read = Book(install);
        read.Load();

        var market = Assert.Single(read.Markets);

        Assert.Equal("Walz Depot", market.Station);
        Assert.Equal(1, market.X);
        Assert.Equal(3, market.Z);
        Assert.Equal(9_400, market.Quote("Gold")?.BuyPrice);
        Assert.Equal(PriceSource.Seen, market.Source);
    }

    [Fact]
    public void AnOlderReadingNeverReplacesANewerOne()
    {
        using var install = new TempInstall();
        var book = Book(install);

        var newer = new MarketSnapshot
        {
            Station = "Walz Depot",
            System = "Sol",
            UpdatedAt = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero),
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Gold"] = new("Gold") { SellPrice = 9_300 },
            },
        };

        Assert.True(book.Remember(newer));
        Assert.False(book.Remember(newer with { UpdatedAt = newer.UpdatedAt?.AddDays(-1) }));
        Assert.Equal(9_300, Assert.Single(book.Markets).Quote("Gold")?.SellPrice);
    }

    [Fact]
    public void TheBookIsBoundedAndDropsTheOldestFirst()
    {
        // A market is a few hundred priced rows and the file is meant to stay readable. A price
        // ages out of usefulness long before a station does, so the oldest is the right end to
        // drop from.
        using var install = new TempInstall();
        var book = Book(install);

        for (var index = 0; index <= MarketBook.Capacity; index++)
        {
            book.Remember(new MarketSnapshot
            {
                Station = $"Station {index}",
                System = $"System {index}",
                UpdatedAt = new DateTimeOffset(2026, 8, 19, 0, 0, 0, TimeSpan.Zero).AddMinutes(index),
                Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Gold"] = new("Gold") { SellPrice = 9_300 },
                },
            });
        }

        Assert.Equal(MarketBook.Capacity, book.Markets.Count);
        Assert.Equal($"Station {MarketBook.Capacity}", book.Markets[0].Station);
        Assert.Null(book.At("System 0", "Station 0"));
    }
}
