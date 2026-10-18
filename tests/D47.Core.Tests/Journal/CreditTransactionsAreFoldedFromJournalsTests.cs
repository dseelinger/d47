using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class CreditTransactionsAreFoldedFromJournalsTests
{
    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string At(string time, string body) => "{\"timestamp\":\"" + time + "\"," + body + "}";

    private static string Line(string time, string kind, string rest = "") =>
        At(time, "\"event\":\"" + kind + "\"" + (rest.Length > 0 ? "," + rest : string.Empty));

    private static string Login(string time, long credits) => Line(time, "LoadGame", "\"Credits\":" + credits);

    private static CreditLedger Fold(params string[] lines) =>
        lines.Aggregate(CreditLedger.None, (ledger, line) => ledger.Apply(Event(line)));

    private static CreditEntry Only(CreditLedger ledger) => Assert.Single(ledger.Entries);

    public static TheoryData<string, string, CreditKind, long> Rows => new()
    {
        { "MarketSell", "\"TotalSale\":500", CreditKind.Trade, 500 },
        { "MarketBuy", "\"TotalCost\":500", CreditKind.Trade, -500 },
        { "BuyTradeData", "\"Cost\":50", CreditKind.Trade, -50 },
        { "RedeemVoucher", "\"Type\":\"trade\",\"Amount\":70", CreditKind.Trade, 70 },
        { "SellExplorationData", "\"TotalEarnings\":900", CreditKind.Exploration, 900 },
        { "MultiSellExplorationData", "\"TotalEarnings\":901", CreditKind.Exploration, 901 },
        { "SellOrganicData", "\"BioData\":[{\"Value\":100,\"Bonus\":20},{\"Value\":30,\"Bonus\":0}]", CreditKind.Exploration, 150 },
        { "RedeemVoucher", "\"Type\":\"codex\",\"Amount\":80", CreditKind.Exploration, 80 },
        { "BuyExplorationData", "\"Cost\":40", CreditKind.Exploration, -40 },
        { "MissionCompleted", "\"Reward\":1000", CreditKind.Missions, 1000 },
        { "MissionFailed", "\"Fine\":200", CreditKind.Missions, -200 },
        { "MissionAbandoned", "\"Fine\":300", CreditKind.Missions, -300 },
        { "RedeemVoucher", "\"Type\":\"bounty\",\"Amount\":10", CreditKind.Combat, 10 },
        { "RedeemVoucher", "\"Type\":\"CombatBond\",\"Amount\":11", CreditKind.Combat, 11 },
        { "RedeemVoucher", "\"Type\":\"settlement\",\"Amount\":12", CreditKind.Vouchers, 12 },
        { "SearchAndRescue", "\"Reward\":13", CreditKind.Rescue, 13 },
        { "CommunityGoalReward", "\"Reward\":14", CreditKind.CommunityGoals, 14 },
        { "PowerplaySalary", "\"Amount\":15", CreditKind.Powerplay, 15 },
        { "PowerplayFastTrack", "\"Cost\":16", CreditKind.Powerplay, -16 },
        { "ShipyardSell", "\"ShipPrice\":17", CreditKind.Ships, 17 },
        { "SellShipOnRebuy", "\"ShipPrice\":18", CreditKind.Ships, 18 },
        { "ShipyardTransfer", "\"TransferPrice\":19", CreditKind.Ships, -19 },
        { "ModuleSell", "\"SellPrice\":20", CreditKind.Modules, 20 },
        { "ModuleSellRemote", "\"SellPrice\":21", CreditKind.Modules, 21 },
        { "ModuleBuyAndStore", "\"BuyPrice\":22", CreditKind.Modules, -22 },
        { "FetchRemoteModule", "\"TransferCost\":23", CreditKind.Modules, -23 },
        { "ModuleStore", "\"Cost\":24", CreditKind.Modules, -24 },
        { "ModuleRetrieve", "\"Cost\":25", CreditKind.Modules, -25 },
        { "SellDrones", "\"TotalSale\":26", CreditKind.Upkeep, 26 },
        { "RefuelAll", "\"Cost\":27", CreditKind.Upkeep, -27 },
        { "RefuelPartial", "\"Cost\":28", CreditKind.Upkeep, -28 },
        { "Repair", "\"Cost\":29", CreditKind.Upkeep, -29 },
        { "RepairAll", "\"Cost\":30", CreditKind.Upkeep, -30 },
        { "BuyAmmo", "\"Cost\":31", CreditKind.Upkeep, -31 },
        { "RestockVehicle", "\"Cost\":32", CreditKind.Upkeep, -32 },
        { "BuyDrones", "\"TotalCost\":33", CreditKind.Upkeep, -33 },
        { "Resurrect", "\"Cost\":34", CreditKind.Rebuy, -34 },
        { "PayBounties", "\"Amount\":35", CreditKind.Fines, -35 },
        { "PayFines", "\"Amount\":36", CreditKind.Fines, -36 },
        { "PayLegacyFines", "\"Amount\":37", CreditKind.Fines, -37 },
        { "NpcCrewPaidWage", "\"Amount\":38", CreditKind.Crew, -38 },
        { "CrewHire", "\"Cost\":39", CreditKind.Crew, -39 },
        { "SellSuit", "\"Price\":40", CreditKind.OnFoot, 40 },
        { "SellWeapon", "\"Price\":41", CreditKind.OnFoot, 41 },
        { "SellMicroResources", "\"Price\":42", CreditKind.OnFoot, 42 },
        { "CancelTaxi", "\"Refund\":43", CreditKind.OnFoot, 43 },
        { "CancelDropship", "\"Refund\":44", CreditKind.OnFoot, 44 },
        { "BuySuit", "\"Price\":45", CreditKind.OnFoot, -45 },
        { "BuyWeapon", "\"Price\":46", CreditKind.OnFoot, -46 },
        { "BuyMicroResources", "\"Price\":47", CreditKind.OnFoot, -47 },
        { "UpgradeSuit", "\"Cost\":48", CreditKind.OnFoot, -48 },
        { "UpgradeWeapon", "\"Cost\":49", CreditKind.OnFoot, -49 },
        { "BookTaxi", "\"Cost\":50", CreditKind.OnFoot, -50 },
        { "BookDropship", "\"Cost\":51", CreditKind.OnFoot, -51 },
        { "EngineerContribution", "\"Type\":\"Credits\",\"Quantity\":52", CreditKind.Engineers, -52 },
        { "CarrierBuy", "\"Price\":53", CreditKind.Carrier, -53 },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void EachEventRecordsItsKindAndSignedAmountAndTheBalanceAfterIt(string kind, string fields, CreditKind expected, long amount)
    {
        var entry = Only(Fold(Login("2026-10-10T09:00:00Z", 1_000_000), Line("2026-10-10T10:00:00Z", kind, fields)));

        Assert.Equal((expected, kind, amount, (long?)(1_000_000 + amount)), (entry.Kind, entry.Event, entry.Amount, entry.Balance));
    }

    [Fact]
    public void ABuyOfAShipOrModuleRecordsTheTradeInAndThePriceOut()
    {
        var ships = Fold(Line("2026-10-10T10:00:00Z", "ShipyardBuy", "\"ShipPrice\":1000,\"SellPrice\":300"));
        var modules = Fold(Line("2026-10-10T10:00:00Z", "ModuleBuy", "\"BuyPrice\":100,\"SellPrice\":40"));

        Assert.Equal([300L, -1000L], ships.Entries.Select(entry => entry.Amount));
        Assert.Equal([40L, -100L], modules.Entries.Select(entry => entry.Amount));
    }

    [Fact]
    public void AMissionWithARewardAndADonationRecordsTwoEntries()
    {
        var ledger = Fold(Line("2026-10-10T10:00:00Z", "MissionCompleted", "\"Reward\":500,\"Donated\":200"));

        Assert.Equal([500L, -200L], ledger.Entries.Select(entry => entry.Amount));
    }

    [Fact]
    public void ACarrierBankTransferRecordsADepositAndAWithdrawalOnTheirOwnSides()
    {
        var ledger = Fold(
            Line("2026-10-10T10:00:00Z", "CarrierBankTransfer", "\"Withdraw\":70,\"PlayerBalance\":70"),
            Line("2026-10-10T10:01:00Z", "CarrierBankTransfer", "\"Deposit\":30,\"PlayerBalance\":40"));

        Assert.Equal([70L, -30L], ledger.Entries.Select(entry => entry.Amount));
        Assert.All(ledger.Entries, entry => Assert.Equal(CreditKind.CarrierBank, entry.Kind));
    }

    [Fact]
    public void AnEventWithZeroAmountOrNoFieldRecordsNothing()
    {
        var ledger = Fold(
            Line("2026-10-10T10:00:00Z", "NpcCrewPaidWage", "\"Amount\":0"),
            Line("2026-10-10T10:01:00Z", "MarketSell"),
            Line("2026-10-10T10:02:00Z", "EngineerContribution", "\"Type\":\"Commodity\",\"Quantity\":5"));

        Assert.Empty(ledger.Entries);
    }

    [Theory]
    [InlineData("Bounty", "\"TotalReward\":100")]
    [InlineData("FactionKillBond", "\"Reward\":100")]
    [InlineData("DatalinkVoucher", "\"Reward\":100")]
    [InlineData("CodexEntry", "\"VoucherAmount\":100")]
    [InlineData("MissionAccepted", "\"Reward\":100")]
    [InlineData("CommitCrime", "\"Fine\":100")]
    [InlineData("CrimeVictim", "\"Bounty\":100")]
    [InlineData("CarrierTradeOrder", "\"Price\":100")]
    [InlineData("CarrierDepositFuel", "\"Total\":100")]
    [InlineData("CarrierModulePack", "\"Cost\":100")]
    [InlineData("CarrierShipPack", "\"Cost\":100")]
    [InlineData("CarrierCrewServices", "\"Cost\":100")]
    [InlineData("CarrierFinance", "\"CarrierBalance\":100")]
    [InlineData("CarrierDecommission", "\"ScrapRefund\":100")]
    public void AnEventThatMovesNoCreditsWhenWrittenRecordsNothing(string kind, string fields)
    {
        Assert.Empty(Fold(Line("2026-10-10T10:00:00Z", kind, fields)).Entries);
    }

    [Fact]
    public void ALoginThatDiffersFromTheRunningBalanceRecordsTheGapAndResetsTheBalance()
    {
        var ledger = Fold(
            Login("2026-10-10T09:00:00Z", 1000),
            Line("2026-10-10T10:00:00Z", "MarketBuy", "\"TotalCost\":300"),
            Login("2026-10-10T11:00:00Z", 750));

        Assert.Equal(
            [(CreditKind.Trade, -300L, (long?)700), (CreditKind.Unaccounted, 50L, 750)],
            ledger.Entries.Select(entry => (entry.Kind, entry.Amount, entry.Balance)));
        Assert.Equal(750, ledger.Balance);
    }

    [Fact]
    public void ALoginWithZeroCreditsIsARealBalance()
    {
        var ledger = Fold(Login("2026-10-10T09:00:00Z", 1000), Login("2026-10-10T10:00:00Z", 0));

        Assert.Equal((CreditKind.Unaccounted, -1000L, (long?)0), (ledger.Entries[0].Kind, ledger.Entries[0].Amount, ledger.Entries[0].Balance));
    }

    [Fact]
    public void ABankTransferThatMatchesTheBalanceRecordsNoGap()
    {
        var ledger = Fold(
            Login("2026-10-10T09:00:00Z", 1000),
            Line("2026-10-10T10:00:00Z", "CarrierBankTransfer", "\"Deposit\":100,\"PlayerBalance\":900"));

        Assert.Equal([CreditKind.CarrierBank], ledger.Entries.Select(entry => entry.Kind));
    }

    [Fact]
    public void ABankTransferFiveBelowTheBalanceAlsoRecordsTheGap()
    {
        var ledger = Fold(
            Login("2026-10-10T09:00:00Z", 1000),
            Line("2026-10-10T10:00:00Z", "CarrierBankTransfer", "\"Deposit\":100,\"PlayerBalance\":895"));

        Assert.Equal(
            [(CreditKind.CarrierBank, -100L), (CreditKind.Unaccounted, -5L)],
            ledger.Entries.Select(entry => (entry.Kind, entry.Amount)));
    }

    [Fact]
    public void BeforeAnAnchorEntriesHaveNoBalance()
    {
        var ledger = Fold(Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":5"), Login("2026-10-10T11:00:00Z", 100));

        Assert.Null(Only(ledger).Balance);
        Assert.Equal(100, ledger.Balance);
    }

    private static string Depot(string time, long market, string name, long payment) =>
        Line(time, "ColonisationConstructionDepot", $"\"MarketID\":{market},\"ResourcesRequired\":[{{\"Name\":\"{name}\",\"Payment\":{payment}}}]");

    [Fact]
    public void ADeliveryIsPaidAtTheDepotsPriceAcrossALoginAndMatchedWithoutCase()
    {
        var ledger = Fold(
            Depot("2026-10-10T09:00:00Z", 7, "$Aluminium_name;", 3000),
            Login("2026-10-10T09:30:00Z", 50_000),
            Line("2026-10-10T10:00:00Z", "ColonisationContribution", "\"MarketID\":7,\"Contributions\":[{\"Name\":\"$aluminium_NAME;\",\"Amount\":10}]"));

        var entry = Only(ledger);

        Assert.Equal((CreditKind.Colonisation, 30_000L), (entry.Kind, entry.Amount));
    }

    [Fact]
    public void ADeliveryWithNoDepotEventSeenOrAtAnotherMarketRecordsNothing()
    {
        var ledger = Fold(
            Depot("2026-10-10T09:00:00Z", 7, "$aluminium_name;", 3000),
            Line("2026-10-10T10:00:00Z", "ColonisationContribution", "\"MarketID\":8,\"Contributions\":[{\"Name\":\"$aluminium_name;\",\"Amount\":10}]"),
            Line("2026-10-10T10:01:00Z", "ColonisationContribution", "\"MarketID\":7,\"Contributions\":[{\"Name\":\"$steel_name;\",\"Amount\":10}]"));

        Assert.Empty(ledger.Entries);
    }

    [Fact]
    public void ALegacyFileAddsNothingAndTheNextFileHeaderEndsIt()
    {
        var ledger = Fold(
            Line("2026-10-10T09:00:00Z", "Fileheader", "\"gameversion\":\"3.8.0.404\""),
            Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":5"),
            Line("2026-10-10T11:00:00Z", "Fileheader", "\"gameversion\":\"4.1.0.0\""),
            Line("2026-10-10T12:00:00Z", "MarketSell", "\"TotalSale\":6"));

        Assert.Equal([6L], ledger.Entries.Select(entry => entry.Amount));
    }

    [Fact]
    public void SinceCountsAndSumsOnlyTheEntriesAtOrAfterIt()
    {
        var ledger = Fold(
            Login("2026-10-10T09:00:00Z", 1000),
            Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":500"),
            Line("2026-10-10T11:00:00Z", "MarketBuy", "\"TotalCost\":100"),
            Line("2026-10-10T11:30:00Z", "MarketBuy", "\"TotalCost\":50"));

        var slice = ledger.Since(DateTimeOffset.Parse("2026-10-10T10:30:00Z"));

        Assert.Equal([new CreditKindTotal(CreditKind.Trade, 0, 150, 2)], slice.ByKind);
        Assert.Equal((1350L, DateTimeOffset.Parse("2026-10-10T11:30:00Z")), (slice.Balance, slice.BalanceAt));
        Assert.Equal(500, ledger.Since(null).ByKind[0].In);
    }

    [Fact]
    public void SinceTotalsTheGapsApartFromTheKinds()
    {
        var slice = Fold(Login("2026-10-10T09:00:00Z", 1000), Login("2026-10-10T10:00:00Z", 1030), Login("2026-10-10T11:00:00Z", 1020)).Since(null);

        Assert.Empty(slice.ByKind);
        Assert.Equal(20, slice.Unaccounted);
    }

    [Fact]
    public void AMergeCountsAnEntryInTheWalkAndTheLiveJournalOnce()
    {
        string Sale(string time) => Line(time, "MarketSell", "\"TotalSale\":5");

        var walk = Fold(Login("2026-10-10T09:00:00Z", 100), Sale("2026-10-10T10:00:00Z"), Sale("2026-10-10T10:00:30Z"), Sale("2026-10-10T10:00:30Z"));
        var live = Fold(
            Login("2026-10-10T09:00:00Z", 100),
            Sale("2026-10-10T10:00:00Z"),
            Sale("2026-10-10T10:00:30Z"),
            Sale("2026-10-10T10:00:30Z"),
            Sale("2026-10-10T10:00:30Z"),
            Sale("2026-10-10T10:05:00Z"));

        Assert.Equal(5, walk.With(live).Entries.Count);
        Assert.Equal(5, walk.With(walk.With(live)).Entries.Count);
    }

    [Fact]
    public void AMergeAddsTheGapBetweenTheWalksLastBalanceAndTheLiveLogin()
    {
        var walk = Fold(Login("2026-10-10T09:00:00Z", 1000), Line("2026-10-10T10:00:00Z", "MarketBuy", "\"TotalCost\":100"));
        var live = Fold(Login("2026-10-10T12:00:00Z", 850), Line("2026-10-10T13:00:00Z", "MarketSell", "\"TotalSale\":5"));

        var merged = walk.With(live);

        Assert.Equal(
            [(CreditKind.Trade, -100L), (CreditKind.Unaccounted, -50L), (CreditKind.Trade, 5L)],
            merged.Entries.Select(entry => (entry.Kind, entry.Amount)));
        Assert.Equal(855, merged.Balance);
    }

    [Fact]
    public void AMergeAddsNoGapWhenTheWalkReachedTheLiveLogin()
    {
        var walk = Fold(Login("2026-10-10T09:00:00Z", 1000), Login("2026-10-10T12:00:00Z", 850));
        var live = Fold(Login("2026-10-10T12:00:00Z", 850));

        Assert.Single(walk.With(live).Entries);
    }

    [Fact]
    public void AGapEntryIsNotMatchedAgainstALiveTransactionInTheSameSecond()
    {
        var walk = Fold(Login("2026-10-10T09:00:00Z", 100), Login("2026-10-10T10:00:00Z", 90));
        var live = Fold(Login("2026-10-10T09:00:00Z", 100), Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":5"));

        Assert.Equal(
            [CreditKind.Unaccounted, CreditKind.Trade],
            walk.With(live).Entries.Select(entry => entry.Kind));
    }

    [Fact]
    public void ARecordIsWholeOnlyAfterAMergeEvenWithAnEmptyWalk()
    {
        var live = Fold(Login("2026-10-10T09:00:00Z", 100));

        Assert.False(live.IsWhole);
        Assert.True(live.With(CreditLedger.None).IsWhole);
        Assert.True(CreditLedger.None.With(live).IsWhole);
    }

    [Fact]
    public void ARecordIsOnRecordSinceItsFirstEvent()
    {
        var ledger = Fold(Line("2026-10-10T09:00:00Z", "Music"), Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":5"));

        Assert.Equal(DateTimeOffset.Parse("2026-10-10T09:00:00Z"), ledger.OnRecordSince);
    }

    [Fact]
    public void ANewCommanderClearsTheEntriesButKeepsTheRecordWhole()
    {
        var ledger = CreditLedger.None
            .With(Fold(Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":5")))
            .Apply(Event(Line("2026-10-10T11:00:00Z", "NewCommander")));

        Assert.Empty(ledger.Entries);
        Assert.True(ledger.IsWhole);
    }

    [Fact]
    public void TwoCommandersTransactionsInOneWalkLandInTwoRecordsAndNeitherAnchorsTheOther()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            "j/Journal.1.log",
            string.Join("\n",
                Line("2026-10-10T08:59:00Z", "Fileheader", "\"gameversion\":\"4.1.0.0\""),
                At("2026-10-10T09:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F1\",\"Credits\":1000"),
                Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":50"),
                At("2026-10-10T10:30:00Z", "\"event\":\"Commander\",\"FID\":\"F2\",\"Name\":\"Two\""),
                Line("2026-10-10T10:31:00Z", "MarketSell", "\"TotalSale\":3"),
                At("2026-10-10T11:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F2\",\"Credits\":9000"),
                Line("2026-10-10T12:00:00Z", "MarketSell", "\"TotalSale\":70"),
                Line("2026-10-10T12:30:00Z", "Music"),
                At("2026-10-10T13:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F1\",\"Credits\":1050")));

        var found = CreditLedgerBackfill.FromHistory(files, ["j/Journal.1.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Equal([50L], found["F1"].Entries.Select(entry => entry.Amount));
        Assert.Equal([3L, 70L], found["F2"].Entries.Select(entry => entry.Amount));
        Assert.Equal((1050L, 9070L), (found["F1"].Balance, found["F2"].Balance));
    }

    [Fact]
    public void AWalkSkipsALegacyFile()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            "j/Journal.1.log",
            string.Join("\n",
                Line("2026-10-10T08:59:00Z", "Fileheader", "\"gameversion\":\"3.8.0.404\""),
                At("2026-10-10T09:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F1\",\"Credits\":1000"),
                Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":50")));

        var found = CreditLedgerBackfill.FromHistory(files, ["j/Journal.1.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Empty(found);
    }

    [Fact]
    public void ANewCommanderInAWalkClearsTheCommanderItNames()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            "j/Journal.1.log",
            string.Join("\n",
                At("2026-10-10T09:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F1\",\"Credits\":1000"),
                Line("2026-10-10T10:00:00Z", "MarketSell", "\"TotalSale\":50"),
                At("2026-10-10T11:00:00Z", "\"event\":\"LoadGame\",\"FID\":\"F2\",\"Credits\":9000"),
                Line("2026-10-10T12:00:00Z", "MarketSell", "\"TotalSale\":70"),
                At("2026-10-10T13:00:00Z", "\"event\":\"NewCommander\",\"FID\":\"F1\",\"Name\":\"Again\"")));

        var found = CreditLedgerBackfill.FromHistory(files, ["j/Journal.1.log"], NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.False(found.ContainsKey("F1"));
        Assert.Single(found["F2"].Entries);
    }
}
