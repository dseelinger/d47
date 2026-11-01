using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AnOutfittingRouteTicksOffWhatIsBoughtTests
{
    private static readonly DateTimeOffset PlottedAt = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    private static RoutePlanBook BookWithRoute()
    {
        var book = new RoutePlanBook("plans.json", new MemoryFileSystem(), NullLogger<RoutePlanBook>.Instance);
        var route = new OutfittingRoute(
            [
                new OutfittingStop("Eravate", "Ackerman Market", 12.4, ["4A Thrusters", "4A Thrusters"]),
                new OutfittingStop("Sol", "Daedalus", 40, ["Python MkII"]),
            ],
            []);

        book.Record(route, "Thrusters and a Python", PlottedAt, frontierId: "F1");

        return book;
    }

    private static JournalEvent Buy(string kind, string field, string symbol, TimeSpan after) =>
        JournalEvent.TryParse(
            $$"""{"timestamp":"{{PlottedAt + after:O}}","event":"{{kind}}","{{field}}":"{{symbol}}"}""",
            NullLogger.Instance,
            out var parsed)
            ? parsed!
            : throw new InvalidOperationException("bad journal fixture");

    private static IReadOnlyList<string> Bought(RoutePlanBook book) => book.Last(RoutePlanKind.Outfitting, "F1")!.Bought;

    [Fact]
    public void ABuyBeforeThePlanWasPlottedIsIgnored()
    {
        var book = BookWithRoute();

        book.Apply([Buy("ModuleBuy", "BuyItem", "int_engine_size4_class5", TimeSpan.FromMinutes(-5))], "F1");

        Assert.Empty(Bought(book));
    }

    [Fact]
    public void AMatchingBuyTicksTheItem()
    {
        var book = BookWithRoute();

        book.Apply([Buy("ModuleBuyAndStore", "BuyItem", "int_engine_size4_class5", TimeSpan.FromMinutes(5))], "F1");

        Assert.Equal(["4A Thrusters"], Bought(book));
    }

    [Fact]
    public void ABuyOfAnotherRatingDoesNotTickTheItem()
    {
        var book = BookWithRoute();

        book.Apply([Buy("ModuleBuy", "BuyItem", "int_engine_size4_class4", TimeSpan.FromMinutes(5))], "F1");

        Assert.Empty(Bought(book));
    }

    [Fact]
    public void AHullPurchaseTicksTheHullLine()
    {
        var book = BookWithRoute();

        book.Apply([Buy("ShipyardBuy", "ShipType", "python_nx", TimeSpan.FromMinutes(5))], "F1");

        Assert.Equal(["Python MkII"], Bought(book));
    }

    [Fact]
    public void OnePurchaseTicksOneOfTwoIdenticalItems()
    {
        var book = BookWithRoute();
        var route = book.Last(RoutePlanKind.Outfitting, "F1")!.Outfitting!;

        book.Apply([Buy("ModuleBuy", "BuyItem", "int_engine_size4_class5", TimeSpan.FromMinutes(5))], "F1");

        var flags = OutfittingBought.Flags(route, Bought(book));

        Assert.Equal([true, false], flags[0]);
        Assert.Equal(["4A Thrusters", "Python MkII"], OutfittingBought.Remaining(route, Bought(book)));
    }
}
