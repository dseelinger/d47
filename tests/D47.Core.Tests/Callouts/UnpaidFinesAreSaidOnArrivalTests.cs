using D47.Core.Callouts;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Callouts;

/// <summary>The unpaid fine or bounty owed to a faction in the system arrived in (#639).</summary>
public class UnpaidFinesAreSaidOnArrivalTests
{
    private const string Fid = "F1234";

    private const string Sirius = "Sirius Inc";

    private const string Turner = "Turner Research Group";

    private static readonly DateTimeOffset Noon = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Load(int ship, string at = "10:00:00") =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"LoadGame","FID":"{{Fid}}","Commander":"Doug","Ship":"Python","ShipID":{{ship}}}""";

    private static string Swap(int ship, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"ShipyardSwap","ShipType":"cobramkiii","ShipID":{{ship}},"StoreOldShip":"Python","StoreShipID":2,"MarketID":1}""";

    private static string Crime(string faction, string type, string amount, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"CommitCrime","CrimeType":"{{type}}","Faction":"{{faction}}",{{amount}}}""";

    private static string PayFines(string? faction, long ship, string at, bool all = false) =>
        faction is null
            ? $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"PayFines","Amount":187,"AllFines":{{all.ToString().ToLowerInvariant()}},"ShipID":{{ship}},"BrokerPercentage":25.0}"""
            : $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"PayFines","Amount":187,"AllFines":{{all.ToString().ToLowerInvariant()}},"Faction":"{{faction}}","ShipID":{{ship}}}""";

    private static string PayBounties(string faction, long ship, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"PayBounties","Amount":9062,"AllFines":false,"Faction":"{{faction}}","ShipID":{{ship}},"BrokerPercentage":25.0}""";

    private static JournalEvent Jump(params string[] factions) =>
        Parse($$"""{"timestamp":"2026-09-05T12:00:00Z","event":"FSDJump","StarSystem":"Sirius","SystemAddress":121569805492,"Factions":[{{string.Join(',', factions.Select(name => $$"""{"Name":"{{name}}","FactionState":"None"}"""))}}]}""");

    private static OutstandingCrimes Ledger(params string[] lines)
    {
        var crimes = new OutstandingCrimes(NullLogger.Instance);
        crimes.Apply([.. lines.Select(Parse)]);
        crimes.FoldHistory([]);
        return crimes;
    }

    private static List<Announcement> Arrive(OutstandingCrimes crimes, JournalEvent arrival, bool priming = false)
    {
        var state = new CommanderGameState(new CommanderIdentity(Fid, "Doug"));
        var context = new CalloutContext(Noon, priming, state, new GameStatus(), new NavRoute(), [arrival]);
        return [.. new OutstandingCrimesCallout(crimes).Examine(context)];
    }

    [Fact]
    public void AFineOwedToAFactionInTheSystemIsSaidOnce()
    {
        var crimes = Ledger(Load(2), Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"));

        var said = Assert.Single(Arrive(crimes, Jump(Sirius, "Other Faction")));

        Assert.Equal("You owe Sirius Inc 150 credits in fines on this ship.", said.Text);
        Assert.Equal(OutstandingCrimesCallout.KeyPrefix + "121569805492", said.Key);
        Assert.Equal(TimeSpan.FromHours(1), said.Cooldown);
    }

    [Fact]
    public void AFactionNotInTheSystemIsNotSaid() =>
        Assert.Empty(Arrive(Ledger(Load(2), Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00")), Jump("Other Faction")));

    [Fact]
    public void PayingTheFactionsFinesSilencesIt()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"),
            PayFines(Sirius, 2, "10:20:00"));

        Assert.Empty(Arrive(crimes, Jump(Sirius)));
    }

    [Fact]
    public void AllFinesClearsOnlyThatShip()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"),
            Swap(7, "10:10:00"),
            Crime(Sirius, "recklessWeaponsDischarge", "\"Fine\":100", "10:15:00"),
            PayFines(null, 7, "10:20:00", all: true));

        Assert.Empty(crimes.Owed(Fid, 7));
        Assert.Equal(150, Assert.Single(crimes.Owed(Fid, 2)).Fines);
    }

    [Fact]
    public void AnotherShipIsSilentForAShipDebtAndSpeaksForAnOnFootOne()
    {
        var shipDebt = Ledger(Load(2), Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"), Swap(7, "10:10:00"));
        var footDebt = Ledger(Load(2), Crime(Turner, "onFoot_murder", "\"Bounty\":1000", "10:05:00"), Swap(7, "10:10:00"));

        Assert.Empty(Arrive(shipDebt, Jump(Sirius)));
        Assert.Equal(
            "You owe Turner Research Group 1,000 credits in bounties on foot.",
            Assert.Single(Arrive(footDebt, Jump(Turner))).Text);
    }

    [Fact]
    public void AnOnFootBountyPaidFromAnotherShipIsCleared()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Turner, "onFoot_murder", "\"Bounty\":1000", "10:05:00"),
            Swap(4, "10:10:00"),
            PayBounties(Turner, 4, "10:20:00"));

        Assert.Empty(crimes.Owed(Fid, 4));
        Assert.Empty(Arrive(crimes, Jump(Turner)));
    }

    [Fact]
    public void PayingAFactionsBountiesClearsItsOnFootFine()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Turner, "onFoot_recklessEndangerment", "\"Fine\":250", "10:05:00"),
            Crime(Turner, "onFoot_murder", "\"Bounty\":1000", "10:06:00"),
            PayBounties(Turner, 2, "10:20:00"));

        Assert.Empty(crimes.Owed(Fid, 2));
    }

    [Fact]
    public void AllFinesFromASuitLoadoutClearsEveryOnFootFine()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Turner, "onFoot_recklessEndangerment", "\"Fine\":250", "10:05:00"),
            Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:06:00"),
            PayFines(null, 4293000001, "10:20:00", all: true));

        Assert.Equal(Sirius, Assert.Single(crimes.Owed(Fid, 2)).Faction);
    }

    [Fact]
    public void DyingClearsTheShipFlownAndNotTheOnFootLedger()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"),
            Crime(Turner, "onFoot_murder", "\"Bounty\":1000", "10:06:00"),
            """{"timestamp":"2026-09-05T10:20:00Z","event":"Died"}""");

        Assert.Equal(Turner, Assert.Single(crimes.Owed(Fid, 2)).Faction);
    }

    [Fact]
    public void SellingAShipClearsIt()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"),
            Swap(7, "10:10:00"),
            """{"timestamp":"2026-09-05T10:20:00Z","event":"ShipyardSell","ShipType":"python","SellShipID":2,"ShipPrice":1000,"MarketID":1}""");

        Assert.Empty(crimes.Owed(Fid, 2));
    }

    [Fact]
    public void TheLargestDebtIsNamedWithBothKinds()
    {
        var crimes = Ledger(
            Load(2),
            Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"),
            Crime(Turner, "assault", "\"Bounty\":400", "10:06:00"),
            Crime(Turner, "dumpingDangerous", "\"Fine\":250", "10:07:00"));

        Assert.Equal(
            "You owe Turner Research Group 650 credits in fines and bounties on this ship.",
            Assert.Single(Arrive(crimes, Jump(Sirius, Turner))).Text);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void ADebtFromAnEarlierJournalIsSaidAfterARestart()
    {
        var folder = Directory.CreateTempSubdirectory("d47-crimes-");

        try
        {
            var old = Path.Combine(folder.FullName, "Journal.2026-09-01T100000.01.log");
            File.WriteAllLines(old, [Load(2), Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00")]);

            var crimes = new OutstandingCrimes(NullLogger.Instance);
            crimes.Apply([Parse(Load(2, "11:00:00"))]);
            crimes.FoldHistory([old], TestContext.Current.CancellationToken);

            Assert.Single(Arrive(crimes, Jump(Sirius)));
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void TheSameJournalFoldedTwiceCountsOnce()
    {
        var folder = Directory.CreateTempSubdirectory("d47-crimes-");

        try
        {
            string[] lines =
            [
                Load(2),
                Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"),
                Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"),
            ];

            var current = Path.Combine(folder.FullName, "Journal.2026-09-05T100000.01.log");
            File.WriteAllLines(current, lines);

            var crimes = new OutstandingCrimes(NullLogger.Instance);
            crimes.Apply([.. lines.Select(Parse)]);
            crimes.FoldHistory([current], TestContext.Current.CancellationToken);

            Assert.Equal(300, Assert.Single(crimes.Owed(Fid, 2)).Fines);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }

    [Fact]
    public void NothingIsSaidWhilePriming() =>
        Assert.Empty(Arrive(Ledger(Load(2), Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00")), Jump(Sirius), priming: true));

    [Fact]
    public void NothingIsSaidBeforeTheHistoryIsFolded()
    {
        var crimes = new OutstandingCrimes(NullLogger.Instance);
        crimes.Apply([Parse(Load(2)), Parse(Crime(Sirius, "dumpingDangerous", "\"Fine\":150", "10:05:00"))]);

        Assert.Empty(Arrive(crimes, Jump(Sirius)));
    }
}
