using System.Net;
using System.Text;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Knowledge.Tests;

public class ArdentCommodityRequestTests
{
    /// <summary>Ega's coordinates, as the index gives them.</summary>
    private const string EgaSystem =
        """
        {"systemAddress":4923936737651,"systemName":"Ega","systemX":31.75,"systemY":17.5625,
         "systemZ":114.09375,"updatedAt":"2023-05-02T16:42:03.000Z"}
        """;

    /// <summary>Enough of the commodity catalogue to resolve a name.</summary>
    private const string Catalogue =
        """
        [{"commodityName":"gold"},{"commodityName":"landmines"},
         {"commodityName":"lowtemperaturediamond"},{"commodityName":"palladium"}]
        """;

    private const string Exporters =
        """
        [{"commodityName":"palladium","marketId":4257567747,"stationName":"Stechkin's Inheritance",
          "stationType":"Orbis","distanceToArrival":81.235029,"maxLandingPadSize":3,
          "systemAddress":5031721964274,"systemName":"Scorpii Sector HR-W c1-18",
          "systemX":28.5625,"systemY":-12.0625,"systemZ":170.1875,
          "buyPrice":48678,"demand":1,"meanPrice":50356,"sellPrice":47654,"stock":237748,
          "updatedAt":"2026-09-06T21:21:20.000Z","distance":64},
         {"commodityName":"palladium","marketId":4308700419,"stationName":"Zeppelin Depot",
          "stationType":"Dodec","distanceToArrival":7287.093485,"maxLandingPadSize":3,
          "systemAddress":9467047716297,"systemName":"Alrai Sector KC-U b3-4",
          "systemX":6.46875,"systemY":64.21875,"systemZ":80.03125,
          "buyPrice":48496,"demand":1,"meanPrice":50356,"sellPrice":47476,"stock":675184,
          "updatedAt":"2026-09-06T21:20:21.000Z","distance":63},
         {"commodityName":"palladium","marketId":4318177027,"stationName":"Holden Landing",
          "stationType":"Dodec","distanceToArrival":1475.727061,"maxLandingPadSize":2,
          "systemAddress":628969441659,"systemName":"HIP 79346",
          "systemX":32.625,"systemY":52.34375,"systemZ":168.5625,
          "buyPrice":48395,"demand":1,"meanPrice":50356,"sellPrice":47378,"stock":780462,
          "updatedAt":"2026-09-06T21:17:58.000Z","distance":65},
         {"commodityName":"palladium","marketId":4214150403,"stationName":"Moore Consulting",
          "stationType":"CraterOutpost","distanceToArrival":122.256262,"maxLandingPadSize":3,
          "systemAddress":2557887943394,"systemName":"Placet",
          "systemX":58.28125,"systemY":49.34375,"systemZ":70.34375,
          "buyPrice":48250,"demand":1,"meanPrice":50356,"sellPrice":47236,"stock":124936,
          "updatedAt":"2026-09-06T21:17:47.000Z","distance":60}]
        """;

    /// <summary>
    /// One row for the commodity whose spelling is the trap: the index answers under the word that was
    /// asked for, and Elite writes Low Temperature Diamonds into the Commander's own
    /// <c>Market.json</c>.
    /// </summary>
    private const string Diamonds =
        """
        [{"commodityName":"lowtemperaturediamond","marketId":4308700419,"stationName":"Zeppelin Depot",
          "stationType":"Dodec","distanceToArrival":7287.093485,"maxLandingPadSize":3,
          "systemAddress":9467047716297,"systemName":"Alrai Sector KC-U b3-4",
          "systemX":6.46875,"systemY":64.21875,"systemZ":80.03125,
          "buyPrice":51204,"demand":1,"meanPrice":50356,"sellPrice":50118,"stock":18239,
          "updatedAt":"2026-09-06T21:21:21.000Z","distance":63}]
        """;

    private static readonly DateTimeOffset Recorded = new(2026, 9, 6, 21, 22, 16, TimeSpan.Zero);

    /// <summary>A reply of a given size, for the two sides of the row ceiling.</summary>
    private static string Ceiling(int rows) =>
        "["
        + string.Join(
            ",",
            Enumerable.Range(0, rows).Select(row =>
                $$"""
                  {"commodityName":"palladium","marketId":{{row}},"stationName":"Station {{row}}",
                   "stationType":"Coriolis","distanceToArrival":100,"maxLandingPadSize":3,
                   "systemName":"System {{row}}","systemX":{{32 + row}},"systemY":18,"systemZ":114,
                   "buyPrice":48000,"demand":1,"sellPrice":47000,"stock":50000,
                   "updatedAt":"2026-09-06T21:20:00.000Z","distance":1}
                  """))
        + "]";

    /// <summary>Answers by path, and remembers every URL asked for.</summary>
    private sealed class Index(string? rows = null) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        /// <summary>The catalogue answering 200 with nothing in it, until it is cleared.</summary>
        public bool CatalogueIsEmpty { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.PathAndQuery;

            Urls.Add(url);

            var body =
                url.Contains("/v2/commodities", StringComparison.Ordinal) ? (CatalogueIsEmpty ? "[]" : Catalogue)
                : url.Contains("/nearby/", StringComparison.Ordinal) ? rows ?? "[]"
                : EgaSystem;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class Unreachable : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("no route to host");
    }

    /// <summary>Records what reaches spansh, and answers every station search with nothing.</summary>
    private sealed class Stations : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                Requests.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"count":0,"reference":{"id64":1,"name":"Ega","x":0,"y":0,"z":0},"results":[]}""",
                    Encoding.UTF8,
                    "application/json"),
            };
        }
    }

    private static ArdentCommodityService Commodities(HttpMessageHandler handler) =>
        new(
            NullLogger<ArdentCommodityService>.Instance,
            new HttpClient(handler) { BaseAddress = new Uri($"https://{ArdentCommodityService.Host}/") });

    private static SpanshTradePlanService Service(
        ArdentCommodityService commodities,
        HttpMessageHandler? stations = null,
        DateTimeOffset? now = null) =>
        new(
            NullLogger<SpanshTradePlanService>.Instance,
            commodities,
            book: null,
            new HttpClient(stations ?? new Stations()) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => now ?? Recorded);

    private static CommoditySearch Search(CommodityQuery query, int maxPriceAge = 8) =>
        new("Ega", null, query, maxPriceAge);

    private static CommodityQuery Palladium(
        TradeSide side = TradeSide.Buying,
        double radius = 70,
        int? floor = 10_000,
        bool carriers = false,
        bool surface = false,
        bool largePad = false,
        double? maxStationDistance = null) =>
        new(
            "Palladium",
            side,
            Tonnes: null,
            radius,
            largePad,
            carriers,
            Limit: 20,
            maxStationDistance,
            floor,
            surface,
            CommodityOrder.Distance);

    /// <summary>The one call that carries the knobs, out of the three the search may make.</summary>
    private static string Query(Index index) =>
        index.Urls.Single(url => url.Contains("/nearby/", StringComparison.Ordinal));

    [Fact]
    public async Task TheRecordedReplyAnswersWithQuotesUnderAnHourOld()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium(surface: true)), TestContext.Current.CancellationToken);

        Assert.Equal(0, answer.DroppedAsStale);
        Assert.Equal(4, answer.Offers.Count);
        Assert.True(answer.OriginKnown);

        foreach (var offer in answer.Offers)
        {
            var age = Recorded - offer.Market.UpdatedAt!.Value;

            Assert.True(age < TimeSpan.FromHours(1), $"{offer.Market.Station} quoted {age} ago");
        }

        // The station index reported these two at 22.8 and 1.7 hours old at the same moment.
        Assert.Contains(answer.Offers, offer => offer.Market.Station == "Zeppelin Depot");
        Assert.Contains(answer.Offers, offer => offer.Market.Station == "Stechkin's Inheritance");
    }

    /// <summary>
    /// The distance is d47's own arithmetic over the coordinates, never the reply's own <c>distance</c>
    /// field, which is rounded to a whole light year: the reply says 64 and 63 for two stations the
    /// issue measured at 63.5 and 63.1.
    /// </summary>
    [Fact]
    public async Task DistanceIsComputedFromTheCoordinatesRatherThanReadOffTheReply()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium()), TestContext.Current.CancellationToken);

        var stechkin = answer.Offers.Single(offer => offer.Market.Station == "Stechkin's Inheritance");
        var zeppelin = answer.Offers.Single(offer => offer.Market.Station == "Zeppelin Depot");

        Assert.Equal(63.5, stechkin.Distance, 1);
        Assert.Equal(63.1, zeppelin.Distance, 1);
    }

    /// <summary>The commodity: the index is keyed by symbol, so the path carries one.</summary>
    [Fact]
    public async Task TheCommodityNamesTheSearchedForSymbolInThePath()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(Search(Palladium()), TestContext.Current.CancellationToken);

        Assert.Contains("/v2/system/name/Ega/commodity/name/palladium/nearby/exports", Query(index));
    }

    [Fact]
    public async Task ACommodityWhoseSymbolIsNotItsSpellingIsStillFound()
    {
        var index = new Index("[]");
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(
            Search(new CommodityQuery("Low Temperature Diamonds")),
            TestContext.Current.CancellationToken);

        Assert.Contains("/commodity/name/lowtemperaturediamond/nearby/", Query(index));
    }

    /// <summary>The side: exporters to buy from, importers to sell to.</summary>
    [Fact]
    public async Task SellingAsksTheImportersInstead()
    {
        var index = new Index("[]");
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(
            Search(Palladium(TradeSide.Selling)), TestContext.Current.CancellationToken);

        Assert.Contains("/nearby/imports", Query(index));
        Assert.DoesNotContain("/nearby/exports", Query(index));
    }

    /// <summary>The floor is a search parameter, not a filter over a fixed set.</summary>
    [Fact]
    public async Task TheSupplyFloorGoesIntoTheRequest()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(
            Search(Palladium(floor: 10_000)), TestContext.Current.CancellationToken);

        Assert.Contains("minVolume=10000", Query(index));
    }

    /// <summary>One is the least the index will answer for at all.</summary>
    [Fact]
    public async Task WithoutAFloorTheBoundIsOne()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(
            Search(Palladium(floor: null)), TestContext.Current.CancellationToken);

        Assert.Contains("minVolume=1", Query(index));
    }

    [Theory]
    [InlineData(70)]
    [InlineData(2_000)]
    [InlineData(25_000)]
    public async Task TheRadiusReachesTheRequestUncapped(double radius)
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(
            Search(Palladium(radius: radius)), TestContext.Current.CancellationToken);

        Assert.Contains($"maxDistance={(int)radius}", Query(index));
    }

    [Fact]
    public async Task AWideSearchNamesTheDistanceTheRowsReallyCovered()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium(radius: 2_000, surface: true)), TestContext.Current.CancellationToken);

        Assert.True(answer.Complete);
        Assert.NotNull(answer.Horizon);
        Assert.Equal(64.6, answer.Horizon!.Value, 1);

        Assert.Null(
            (await service.FindCommodityAsync(
                Search(Palladium(radius: 60, surface: true)),
                TestContext.Current.CancellationToken)).Horizon);
    }

    [Fact]
    public async Task AReplyOnTheRowCeilingIsIncompleteAndNamesNoDistance()
    {
        var index = new Index(Ceiling(ArdentCommodityService.RowCeiling));
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium(radius: 2_000)), TestContext.Current.CancellationToken);

        Assert.Equal(ArdentCommodityService.RowCeiling, answer.Considered);
        Assert.False(answer.Complete);
        Assert.Null(answer.Horizon);

        var under = new Index(Ceiling(ArdentCommodityService.RowCeiling - 1));
        using var shortOf = Commodities(under);
        using var whole = Service(shortOf);

        var all = await whole.FindCommodityAsync(
            Search(Palladium(radius: 2_000)), TestContext.Current.CancellationToken);

        Assert.True(all.Complete);
        Assert.NotNull(all.Horizon);
    }

    /// <summary>
    /// The age bound on the request is the Commander's own, rounded up to the whole days the parameter
    /// is written in.
    /// </summary>
    [Theory]
    [InlineData(8, 1)]
    [InlineData(24, 1)]
    [InlineData(25, 2)]
    [InlineData(720, 30)]
    [InlineData(8_760, 365)]
    public async Task TheCommandersAgeBoundIsWhatTheRequestAsksFor(int hours, int days)
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(
            Search(Palladium(), hours), TestContext.Current.CancellationToken);

        Assert.Contains($"maxDaysAgo={days}", Query(index));
    }

    [Fact]
    public async Task AWideSearchThatFindsNothingClaimsNeitherHorizonNorRadius()
    {
        var index = new Index("[]");
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium(radius: 2_000)), TestContext.Current.CancellationToken);

        Assert.Empty(answer.Offers);
        Assert.Null(answer.Horizon);
        Assert.False(answer.Complete);

        Assert.Contains("maxDistance=2000", Query(index));
    }

    /// <summary>The cut is counted off the reply, not off the rows that read cleanly.</summary>
    [Fact]
    public async Task ACappedReplyWithAnUnreadableRowIsStillCutShort()
    {
        var rows = Ceiling(ArdentCommodityService.RowCeiling);

        // The first row loses its station name, which is what Read skips a row for.
        var index = new Index(rows.Replace("\"stationName\":\"Station 0\"", "\"stationName\":\"\"", StringComparison.Ordinal));

        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium(radius: 2_000)), TestContext.Current.CancellationToken);

        Assert.Equal(ArdentCommodityService.RowCeiling - 1, answer.Considered);
        Assert.False(answer.Complete);
        Assert.Null(answer.Horizon);
    }

    [Fact]
    public async Task ARememberedMarketSpelledTheOtherWayIsStillRanked()
    {
        using var install = new TempFile();
        var book = new MarketBook(install.Path, NullLogger.Instance);

        book.Remember(new MarketSnapshot
        {
            Station = "Zeppelin Depot",
            System = "Alrai Sector KC-U b3-4",
            UpdatedAt = Recorded.AddSeconds(-10),
            Source = PriceSource.Seen,
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                ["Low Temperature Diamonds"] = new("Low Temperature Diamonds")
                {
                    BuyPrice = 48_000,
                    Supply = 12_345,
                },
            },
        });

        var index = new Index(Diamonds);
        using var commodities = Commodities(index);

        using var service = new SpanshTradePlanService(
            NullLogger<SpanshTradePlanService>.Instance,
            commodities,
            book,
            new HttpClient(new Stations()) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => Recorded);

        var answer = await service.FindCommodityAsync(
            Search(new CommodityQuery("Low Temperature Diamond", MaxDistance: 70, MinAvailable: 10_000)),
            TestContext.Current.CancellationToken);

        var zeppelin = Assert.Single(
            answer.Offers, offer => offer.Market.Station == "Zeppelin Depot");

        // Theirs, not the index's — it is ten seconds old against the index's fifty-five.
        Assert.Equal(48_000, zeppelin.UnitPrice);
        Assert.Equal(PriceSource.Seen, zeppelin.Market.Source);
    }

    [Fact]
    public async Task AnEmptyCatalogueIsNotCachedForTheRestOfTheRun()
    {
        var index = new Index(Exporters) { CatalogueIsEmpty = true };
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await Assert.ThrowsAsync<GalaxyUnavailableException>(() =>
            service.FindCommodityAsync(Search(Palladium()), TestContext.Current.CancellationToken));

        index.CatalogueIsEmpty = false;

        var answer = await service.FindCommodityAsync(
            Search(Palladium(surface: true)), TestContext.Current.CancellationToken);

        Assert.Equal(4, answer.Offers.Count);
    }

    [Fact]
    public async Task CarriersAreExcludedByNameAndIncludedBySilence()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        await service.FindCommodityAsync(
            Search(Palladium(carriers: false)), TestContext.Current.CancellationToken);

        Assert.Contains("fleetCarriers=false", Query(index));

        var wanted = new Index(Exporters);
        using var withCarriers = Commodities(wanted);
        using var also = Service(withCarriers);

        await also.FindCommodityAsync(
            Search(Palladium(carriers: true)), TestContext.Current.CancellationToken);

        Assert.DoesNotContain("fleetCarriers", Query(wanted));
    }

    /// <summary>
    /// The large pad, applied here off <c>maxLandingPadSize</c>: 3 is large, and Holden Landing's 2 is
    /// not.
    /// </summary>
    [Fact]
    public async Task TheLargePadKnobDropsTheMediumPad()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium(largePad: true, surface: true)), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(answer.Offers, offer => offer.Market.Station == "Holden Landing");
        Assert.Equal(3, answer.Offers.Count);
    }

    [Fact]
    public async Task TheSurfaceKnobDropsTheCraterOutpost()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var excluded = await service.FindCommodityAsync(
            Search(Palladium()), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(excluded.Offers, offer => offer.Market.Station == "Moore Consulting");

        var included = await service.FindCommodityAsync(
            Search(Palladium(surface: true)), TestContext.Current.CancellationToken);

        Assert.Contains(included.Offers, offer => offer.Market.Station == "Moore Consulting");
    }

    [Fact]
    public async Task TheArrivalDistanceKnobDropsTheLongCruise()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities);

        var answer = await service.FindCommodityAsync(
            Search(Palladium(maxStationDistance: 5_000)), TestContext.Current.CancellationToken);

        Assert.DoesNotContain(answer.Offers, offer => offer.Market.Station == "Zeppelin Depot");
    }

    [Fact]
    public async Task ThePriceAgeBoundStillDropsAndStillCounts()
    {
        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Service(commodities, now: Recorded.AddHours(9));

        var answer = await service.FindCommodityAsync(
            Search(Palladium(surface: true)), TestContext.Current.CancellationToken);

        Assert.Empty(answer.Offers);
        Assert.Equal(4, answer.DroppedAsStale);
        Assert.Equal(4, answer.Considered);
    }

    [Fact]
    public async Task AnUnreachableIndexFailsTheSearchAndDoesNotFallBackToSpansh()
    {
        var stations = new Stations();
        using var commodities = Commodities(new Unreachable());
        using var service = Service(commodities, stations);

        await Assert.ThrowsAsync<GalaxyUnavailableException>(() =>
            service.FindCommodityAsync(Search(Palladium()), TestContext.Current.CancellationToken));

        Assert.Empty(stations.Requests);
    }

    [Fact]
    public async Task RoutePlanningAndColonisationSourcingStillAskTheStationIndex()
    {
        var index = new Index(Exporters);
        var stations = new Stations();
        using var commodities = Commodities(index);
        using var service = Service(commodities, stations);

        Assert.True(
            TradeQuery.TryParse(
                "Ega", "Fisher Terminal", 50_000_000, 384, 1, 40, 1_000, false, 720, false,
                out var trade, out var why),
            why);

        await service.PlanAsync(trade, TestContext.Current.CancellationToken);

        await service.SourceConstructionAsync(
            new SourcingSearch("Ega", null, [new D47.Core.Journal.ConstructionResource("Steel", 200, 0)]),
            TestContext.Current.CancellationToken);

        Assert.NotEmpty(stations.Requests);
        Assert.Empty(index.Urls);
    }

    [Fact]
    public async Task AStationMissingFromAWholeReplyIsNotReAddedFromTheBook()
    {
        using var install = new TempFile();
        var book = new MarketBook(install.Path, NullLogger.Instance);

        // The reported row, to the figure: eight hours old, inside the bound by minutes, well over the ten
        // thousand tonne floor, and nearer than anything the index returned.
        book.Remember(Remembered(
            "Pettit Relay",
            "Scorpii Sector FG-X b1-5",
            Recorded.AddHours(-8).AddMinutes(4),
            buyPrice: 48_622,
            supply: 162_529));

        // And one the index did return, seen more recently than the reply says.
        book.Remember(Remembered(
            "Zeppelin Depot",
            "Alrai Sector KC-U b3-4",
            Recorded.AddSeconds(-10),
            buyPrice: 48_111,
            supply: 675_000));

        var index = new Index(Exporters);
        using var commodities = Commodities(index);
        using var service = Book(commodities, book);

        var answer = await service.FindCommodityAsync(
            Search(Palladium()), TestContext.Current.CancellationToken);

        Assert.True(answer.Complete);
        Assert.NotEmpty(answer.Offers);
        Assert.DoesNotContain(answer.Offers, offer => offer.Market.Station == "Pettit Relay");

        // The nearest thing the index actually named is what the Commander is sent to: 63.1 light years
        // against the 59.7 the remembered station was winning on.
        Assert.Equal("Zeppelin Depot", answer.Offers[0].Market.Station);

        var zeppelin = Assert.Single(answer.Offers, offer => offer.Market.Station == "Zeppelin Depot");

        Assert.Equal(48_111, zeppelin.UnitPrice);
        Assert.Equal(PriceSource.Seen, zeppelin.Market.Source);
    }

    [Fact]
    public async Task ACutReplyStillTakesTheBookButOnTheSearchsOwnTerms()
    {
        using var install = new TempFile();
        var book = new MarketBook(install.Path, NullLogger.Instance);

        book.Remember(Remembered(
            "Pettit Relay",
            "Scorpii Sector FG-X b1-5",
            Recorded.AddHours(-1),
            buyPrice: 48_622,
            supply: 162_529));

        book.Remember(Remembered(
            "Nearly Empty Dock",
            "Scorpii Sector FG-X b1-6",
            Recorded.AddHours(-1),
            buyPrice: 40_000,
            supply: 500));

        var index = new Index(Ceiling(ArdentCommodityService.RowCeiling));
        using var commodities = Commodities(index);
        using var service = Book(commodities, book);

        var answer = await service.FindCommodityAsync(
            Search(new CommodityQuery(
                "Palladium",
                MaxDistance: 2_000,
                Limit: 2_000,
                MinAvailable: 10_000,
                OrderBy: CommodityOrder.Distance)),
            TestContext.Current.CancellationToken);

        Assert.False(answer.Complete);
        Assert.Contains(answer.Offers, offer => offer.Market.Station == "Pettit Relay");
        Assert.DoesNotContain(answer.Offers, offer => offer.Market.Station == "Nearly Empty Dock");
    }

    [Fact]
    public async Task AColonisationSweepStillFoldsInAStationItNeverFetched()
    {
        using var install = new TempFile();
        var book = new MarketBook(install.Path, NullLogger.Instance);

        book.Remember(Remembered(
            "Fisher Terminal",
            "Ega",
            Recorded.AddHours(-1),
            buyPrice: 1_200,
            supply: 8_000,
            commodity: "Steel"));

        using var commodities = Commodities(new Index());
        using var service = Book(commodities, book);

        var answer = await service.SourceConstructionAsync(
            new SourcingSearch("Ega", null, [new D47.Core.Journal.ConstructionResource("Steel", 200, 0)]),
            TestContext.Current.CancellationToken);

        // The station search answered with nothing, so the one market considered is theirs.
        Assert.Equal(1, answer.Considered);
        Assert.Equal(0, answer.DroppedAsStale);
    }

    private static SpanshTradePlanService Book(ArdentCommodityService commodities, MarketBook book) =>
        new(
            NullLogger<SpanshTradePlanService>.Instance,
            commodities,
            book,
            new HttpClient(new Stations()) { BaseAddress = new Uri("https://spansh.co.uk/") },
            () => Recorded);

    /// <summary>
    /// A market the Commander stood in, placed a stated number of light years from Ega along one axis
    /// so its distance is the figure the test names rather than something to work out.
    /// </summary>
    private static MarketSnapshot Remembered(
        string station,
        string system,
        DateTimeOffset seen,
        int buyPrice,
        int supply,
        string commodity = "Palladium",
        double lightYears = 59.7) =>
        new()
        {
            Station = station,
            System = system,
            X = 31.75,
            Y = 17.5625,
            Z = 114.09375 + lightYears,
            HasLargePad = true,
            UpdatedAt = seen,
            Source = PriceSource.Seen,
            Quotes = new Dictionary<string, MarketQuote>(StringComparer.OrdinalIgnoreCase)
            {
                [commodity] = new(commodity) { BuyPrice = buyPrice, Supply = supply },
            },
        };
}
