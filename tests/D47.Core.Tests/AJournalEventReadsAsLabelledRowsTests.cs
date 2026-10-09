using System.Text.Json;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests;

/// <summary>One journal event read into a headline, rows and every field (#813).</summary>
public class AJournalEventReadsAsLabelledRowsTests
{
    private const string FleetCarrierDocked =
        """{"timestamp":"2026-09-29T21:01:23Z","event":"Docked","StationName":"BNH-T2F","StationType":"FleetCarrier","Taxi":false,"Multicrew":false,"StarSystem":"LTT 7786","SystemAddress":633608311522,"MarketID":3715429376,"StationFaction":{"Name":"FleetCarrier"},"StationGovernment":"$government_Carrier;","StationGovernment_Localised":"Private Ownership","StationServices":["dock","autodock","commodities","contacts","crewlounge","rearm","refuel","repair","engineer","flightcontroller","stationoperations","stationMenu","carriermanagement","carrierfuel","socialspace"],"StationEconomy":"$economy_Carrier;","StationEconomy_Localised":"Private Enterprise","StationEconomies":[{"Name":"$economy_Carrier;","Name_Localised":"Private Enterprise","Proportion":1.0}],"DistFromStarLS":213.28735,"LandingPads":{"Small":4,"Medium":4,"Large":8}}""";

    private static JournalEntry Entry(string json)
    {
        using var document = JsonDocument.Parse(json);
        var kind = document.RootElement.GetProperty("event").GetString()!;
        var log = new JournalLog();

        log.Add([new JournalEvent(DateTimeOffset.Parse("2026-09-29T21:01:23Z"), kind, document.RootElement.Clone())]);

        return Assert.Single(log.Read(noise: true));
    }

    private static EventReading Read(string json) => EventReadings.For(Entry(json));

    private static ReadingRow Row(EventReading reading, string label) =>
        Assert.Single(reading.Rows, row => row.Label == label);

    private static string Joined(ReadingRow row) => string.Join(" · ", row.Values.Select(value => value.Text));

    [Fact]
    public void ADockingAtACarrierReadsAsStationRunByPadsAndServices()
    {
        var reading = Read(FleetCarrierDocked);

        Assert.Equal("Docked at BNH-T2F, LTT 7786", reading.Headline);
        Assert.Equal("Fleet Carrier · 213 ls from the star", Joined(Row(reading, "Station")));
        Assert.Equal("FleetCarrier · Private Ownership · Private Enterprise", Joined(Row(reading, "Run by")));
        Assert.Equal("4 small · 4 medium · 8 large", Joined(Row(reading, "Landing pads")));

        var services = Row(reading, "You can do here");

        Assert.Equal(10, services.Values.Count);
        Assert.DoesNotContain(services.Values, value => value.Symbol is "dock" or "autodock" or "stationMenu");
        Assert.All(reading.Rows, row => Assert.DoesNotContain("Taxi", row.Label));
        Assert.DoesNotContain(reading.Rows, row => row.Label is "Multicrew" or "Market" or "System address");
        Assert.Equal(18, reading.Plumbing.Count);
        Assert.Equal("timestamp", reading.Plumbing[0].Name);
        Assert.Equal("""{"Small":4,"Medium":4,"Large":8}""", reading.Plumbing[^1].Value);
    }

    [Fact]
    public void AWantedDockingStartsWithAWarning()
    {
        var reading = Read(FleetCarrierDocked.Replace("\"Taxi\"", "\"Wanted\":true,\"Taxi\""));

        var first = reading.Rows[0];
        var value = Assert.Single(first.Values);

        Assert.Equal(string.Empty, first.Label);
        Assert.Equal("You are wanted here.", value.Text);
        Assert.Equal(ReadingTone.Warning, value.Tone);
    }

    [Fact]
    public void NumbersAreTheirOwnRunsAndNamesAreNeverSplit()
    {
        var reading = Read(FleetCarrierDocked);

        var distance = Row(reading, "Station").Values[1];
        var small = Row(reading, "Landing pads").Values[0];

        Assert.Equal([new ReadingRun("213", true), new ReadingRun(" ls from the star", false)], distance.Runs);
        Assert.Equal([new ReadingRun("4", true), new ReadingRun(" small", false)], small.Runs);

        var everyRun = reading.Rows.SelectMany(row => row.Values).SelectMany(value => value.Runs).ToList();

        Assert.DoesNotContain(everyRun, run => run.Text.StartsWith("LTT") && run.Text != "LTT 7786");
        Assert.Contains(everyRun, run => run.Text == "LTT 7786" && !run.Number);
        Assert.Contains(everyRun, run => run.Text == "BNH-T2F" && !run.Number);
    }

    [Fact]
    public void ALocalisedPairIsOneRowCarryingTheToken()
    {
        var reading = Read(FleetCarrierDocked);

        var economy = Assert.Single(reading.Rows, row => row.Values.Any(value => value.Symbol == "$economy_Carrier;"));
        var value = Assert.Single(economy.Values, value => value.Symbol == "$economy_Carrier;");

        Assert.Equal("Private Enterprise", value.Text);
    }

    [Fact]
    public void AKindNobodyCuratedGetsMechanicalRowsAndEveryField()
    {
        var reading = Read(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"ApproachBody","StarSystem":"LTT 7786","SystemAddress":633608311522,"Body":"LTT 7786 A 1","BodyID":12,"Fraction":0.5}""");

        Assert.Equal("LTT 7786", Assert.Single(Row(reading, "Star system").Values).Text);
        Assert.Equal(ReadingTone.System, Assert.Single(Row(reading, "Star system").Values).Tone);
        Assert.Equal("LTT 7786 A 1", Assert.Single(Row(reading, "Body").Values).Text);
        Assert.Equal(7, reading.Plumbing.Count);
    }

    [Fact]
    public void ATrueFlagWithNoSentenceIsYesAndAFalseOneIsNothing()
    {
        var reading = Read("""{"timestamp":"2026-09-29T21:01:23Z","event":"ShieldState","ShieldsUp":true,"Quiet":false}""");

        var row = Assert.Single(reading.Rows);

        Assert.Equal("Shields Up", row.Label);
        Assert.Equal("Yes", Assert.Single(row.Values).Text);
    }

    [Fact]
    public void ANumberCarriesTheUnitTheFieldTableGivesIt()
    {
        var reading = Read(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"Screenshot","Cost":1250,"JumpDist":12.5,"Health":0.5,"Count":2500}""");

        Assert.Equal("1,250 Cr", Assert.Single(Row(reading, "Cost").Values).Text);
        Assert.Equal("12.5 ly", Assert.Single(Row(reading, "Jump distance").Values).Text);
        Assert.Equal("50%", Assert.Single(Row(reading, "Health").Values).Text);
        Assert.Equal("2,500", Assert.Single(Row(reading, "Count").Values).Text);
    }

    [Fact]
    public void TheSameFieldHasADifferentUnitOnDifferentKinds()
    {
        var tritium = Read("""{"timestamp":"2026-09-29T21:01:23Z","event":"CarrierDepositFuel","Amount":75}""");
        var refuel = Read("""{"timestamp":"2026-09-29T21:01:23Z","event":"RepairAll","Amount":75}""");

        Assert.Equal("75 t", Assert.Single(Row(tritium, "Amount").Values).Text);
        Assert.Equal("75", Assert.Single(Row(refuel, "Amount").Values).Text);
    }

    [Fact]
    public void AHullIsDecodedThroughTheOneLadderAndAnUnknownOneStaysAsWritten()
    {
        var symbol = "federation_dropship";
        var reading = Read($$"""{"timestamp":"2026-09-29T21:01:23Z","event":"Loadout","Ship":"{{symbol}}","ShipID":7}""");

        var value = Assert.Single(Row(reading, "Ship").Values);

        Assert.Equal(EliteSpecifications.HullSaid(symbol), value.Text);
        Assert.Equal(symbol, value.Symbol);
        Assert.Equal(new ReadingLink(ReadingLinkKind.Ship, "7"), value.Link);

        var unknown = "smallcombat99_zz";
        var other = Read($$"""{"timestamp":"2026-09-29T21:01:23Z","event":"Loadout","Ship":"{{unknown}}"}""");

        Assert.Equal(EliteSpecifications.HullSaid(unknown), Assert.Single(Row(other, "Ship").Values).Text);
    }

    [Fact]
    public void AnEngineerIsDecodedByIdAndLinked()
    {
        var reading = Read(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"EngineerProgress","Engineer":"Felicity Farseer","EngineerID":300100}""");

        var value = Assert.Single(Row(reading, "Engineer").Values);

        Assert.Equal("Felicity Farseer", value.Text);
        Assert.Equal(ReadingTone.Name, value.Tone);
        Assert.Equal(new ReadingLink(ReadingLinkKind.Engineer, "300100"), value.Link);
    }

    [Fact]
    public void AModuleIsDecodedAndItsSizeAndClassIsANumberRun()
    {
        var reading = Read(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"Screenshot","Module":"int_engine_size2_class1"}""");

        var value = Assert.Single(Row(reading, "Module").Values);

        Assert.Equal(EliteSpecifications.ModuleName("int_engine_size2_class1"), value.Text);
        Assert.Equal("int_engine_size2_class1", value.Symbol);
        Assert.Equal(string.Concat(value.Runs.Select(run => run.Text)), value.Text);
    }

    [Fact]
    public void AnArrayOfNamedObjectsIsOneRowOfNamesAndAnUnnamedOneIsACount()
    {
        var named = Read(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"Screenshot","Signals":[{"Type":"$a;","Type_Localised":"A"},{"Name":"B"}]}""");
        var unnamed = Read("""{"timestamp":"2026-09-29T21:01:23Z","event":"Screenshot","Signals":[{"Count":1},{"Count":2}]}""");

        Assert.Equal("B", Joined(Row(named, "Signals")));
        Assert.Equal("2 entries", Joined(Row(unnamed, "Signals")));
    }

    [Fact]
    public void APlayersMessageIsShownExactlyAsTyped()
    {
        var reading = Read(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"ReceiveText","From":"Player One","Message":"look **here** $foo;","Channel":"local"}""");

        var message = Assert.Single(Row(reading, "Message").Values);
        var from = Assert.Single(Row(reading, "From").Values);

        Assert.Equal("look **here** $foo;", message.Text);
        Assert.True(message.Typed);
        Assert.True(from.Typed);
        Assert.Equal("Player One", from.Text);
        Assert.Equal("Local", Assert.Single(Row(reading, "Channel").Values).Text);
    }

    [Fact]
    public void AFrontiersMessageIsItsLocalisedTextAndNotTyped()
    {
        var reading = Read(
            """{"timestamp":"2026-09-29T21:01:23Z","event":"ReceiveText","From":"$npc_name_decorate:#name=Ilse Bruhn;","From_Localised":"Ilse Bruhn","Message":"$Pirate_Scan01;","Message_Localised":"Stop and be scanned.","Channel":"npc"}""");

        var message = Assert.Single(Row(reading, "Message").Values);
        var from = Assert.Single(Row(reading, "From").Values);

        Assert.Equal("Stop and be scanned.", message.Text);
        Assert.False(message.Typed);
        Assert.False(from.Typed);
        Assert.Equal("Ilse Bruhn", from.Text);
        Assert.Equal(ReadingTone.Name, from.Tone);
    }

    [Theory]
    [InlineData(0, "just now")]
    [InlineData(30, "just now")]
    [InlineData(60, "1 minute ago")]
    [InlineData(240, "4 minutes ago")]
    [InlineData(3 * 3600, "3 hours ago")]
    [InlineData(2 * 86400, "2 days ago")]
    public void AnAgeIsSaidInTheLargestWholeUnit(int seconds, string said)
    {
        var then = DateTimeOffset.Parse("2026-09-29T21:00:00Z");

        Assert.Equal(said, EventReadings.Age(then, then.AddSeconds(seconds)));
    }

    [Fact]
    public void AnEntryThatIsNotJsonKeepsItsHeadlineAndItsRawText()
    {
        var entry = new JournalEntry(DateTimeOffset.Parse("2026-09-29T21:01:23Z"), "Docked", "Docked", "{", "{ not json");

        var reading = EventReadings.For(entry);

        Assert.Equal("Docked", reading.Headline);
        Assert.Empty(reading.Rows);
        Assert.Equal("{ not json", Assert.Single(reading.Plumbing).Value);
    }

    [Trait("Category", "Integration")]
    [Fact]
    public void EveryFixtureEventReadsWithCompletePlumbingAndNoDollarKeys()
    {
        var folder = Path.Combine(RepositoryRoot(), "tests", "fixtures", "journal");
        var entries = Directory.GetFiles(folder, "*.log")
            .SelectMany(File.ReadAllLines)
            .Where(line => line.TrimStart().StartsWith('{'))
            .Select(line => Entry(line))
            .ToList();

        Assert.NotEmpty(entries);

        foreach (var entry in entries)
        {
            var reading = EventReadings.For(entry);

            using var document = JsonDocument.Parse(entry.Compact);

            Assert.Equal(document.RootElement.EnumerateObject().Count(), reading.Plumbing.Count);
            Assert.All(
                reading.Rows.SelectMany(row => row.Values),
                value => Assert.False(value.Text.StartsWith('$')));
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("No d47.slnx above the test binary.");
    }
}
