using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><c>NewCommander</c> empties the unsold-data, crime and met-Commander ledgers for that Frontier id (#1803).</summary>
public class AnEraseClearsEveryJournalLedgerTests
{
    private const long System = 3274702866819;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Load(string fid, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"LoadGame","FID":"{{fid}}","Commander":"Doug","Ship":"Python","ShipID":2}""";

    private static string Erase(string fid, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"NewCommander","FID":"{{fid}}","Name":"Doug","Package":"x"}""";

    private static string Analysis(string at, int body) =>
        $$"""
          {"timestamp":"2026-09-05T{{at}}Z","event":"ScanOrganic","ScanType":"Analyse",
           "Genus":"$Codex_Ent_Cactoid_Genus_Name;","Genus_Localised":"Cactoida",
           "Species":"$Codex_Ent_Cactoid_01_Name;","Species_Localised":"Cactoida Cortexum","SystemAddress":{{System}},"Body":{{body}}}
          """.ReplaceLineEndings(" ");

    private static string Scan(string at, int body) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"Scan","ScanType":"Detailed","BodyName":"Body {{body}}","BodyID":{{body}},"StarSystem":"Praea Euq","SystemAddress":{{System}},"PlanetClass":"Earthlike body","TerraformState":"","MassEM":1.0,"WasDiscovered":false,"WasMapped":false}""";

    private static string Mapped(string at, int body) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"SAAScanComplete","BodyName":"Body {{body}}","BodyID":{{body}},"SystemAddress":{{System}},"ProbesUsed":4,"EfficiencyTarget":6}""";

    private static string Crime(string faction, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"CommitCrime","CrimeType":"assault","Faction":"{{faction}}","Fine":400}""";

    private static string Wing(string name, string at) =>
        $$"""{"timestamp":"2026-09-05T{{at}}Z","event":"WingAdd","Name":"{{name}}"}""";

    private static (MemoryFileSystem Files, IReadOnlyList<string> Paths) Journal(IEnumerable<string> lines)
    {
        var files = new MemoryFileSystem();
        files.WriteText("Journal.log", string.Join('\n', lines) + "\n");
        return (files, ["Journal.log"]);
    }

    private static List<JournalEvent> Events(IEnumerable<string> lines) => [.. lines.Select(Parse)];

    /// <summary>G before F's erase, F before and after it, so the walk switches Commander twice.</summary>
    private static string[] Organic() =>
    [
        Load("G", "09:00:00"), Analysis("09:01:00", 3),
        Load("F", "10:00:00"), Analysis("10:01:00", 1),
        Erase("F", "11:00:00"), Load("F", "11:00:01"), Analysis("12:00:00", 2),
    ];

    private static string[] Maps() =>
    [
        Load("G", "09:00:00"), Scan("09:00:30", 3), Mapped("09:01:00", 3),
        Load("F", "10:00:00"), Scan("10:00:30", 1), Mapped("10:01:00", 1),
        Erase("F", "11:00:00"), Load("F", "11:00:01"), Scan("11:30:00", 2), Mapped("12:00:00", 2),
    ];

    private static string[] Fines() =>
    [
        Load("G", "09:00:00"), Crime("Gnosis", "09:01:00"),
        Load("F", "10:00:00"), Crime("Alliance", "10:01:00"),
        Erase("F", "11:00:00"), Load("F", "11:00:01"), Crime("Federation", "12:00:00"),
    ];

    private static string[] Meetings() =>
    [
        Load("G", "09:00:00"), Wing("CMDR Old Friend", "09:01:00"),
        Load("F", "10:00:00"), Wing("CMDR Past", "10:01:00"),
        Erase("F", "11:00:00"), Load("F", "11:00:01"), Wing("CMDR Newer", "12:00:00"),
    ];

    private static ExobiologyLedger OrganicLedger(bool history, string[] lines)
    {
        var (files, paths) = Journal(lines);
        var ledger = new ExobiologyLedger(null, files, NullLogger.Instance);

        if (history)
        {
            ledger.FoldHistory(paths, TestContext.Current.CancellationToken);
        }
        else
        {
            ledger.Apply(Events(lines));
        }

        return ledger;
    }

    private static CartographyLedger MapLedger(bool history, string[] lines)
    {
        var (files, paths) = Journal(lines);
        var ledger = new CartographyLedger(null, files, NullLogger.Instance);

        if (history)
        {
            ledger.FoldHistory(paths, TestContext.Current.CancellationToken);
        }
        else
        {
            ledger.Apply(Events(lines));
        }

        return ledger;
    }

    private static OutstandingCrimes CrimeLedger(bool history, string[] lines)
    {
        var (files, paths) = Journal(lines);
        var ledger = new OutstandingCrimes(files, NullLogger.Instance);

        if (history)
        {
            ledger.FoldHistory(paths, TestContext.Current.CancellationToken);
        }
        else
        {
            ledger.Apply(Events(lines));
        }

        return ledger;
    }

    private static CommanderLog MetLedger(bool history, string[] lines)
    {
        var (files, paths) = Journal(lines);
        var ledger = new CommanderLog(files, NullLogger.Instance);

        if (history)
        {
            ledger.FoldHistory(paths, TestContext.Current.CancellationToken);
        }
        else
        {
            ledger.Apply(Events(lines));
        }

        return ledger;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEraseLeavesOnlyTheOrganicDataAnalysedAfterIt(bool history)
    {
        var ledger = OrganicLedger(history, Organic());

        Assert.Equal(DateTimeOffset.Parse("2026-09-05T12:00:00Z"), Assert.Single(ledger.Unsold("F").Held).At);
        Assert.Single(ledger.Unsold("G").Held);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEraseLeavesOnlyTheExplorationDataMappedAfterIt(bool history)
    {
        var ledger = MapLedger(history, Maps());

        Assert.Equal("Body 2", Assert.Single(ledger.Unsold("F").Held).BodyName);
        Assert.Equal("Body 3", Assert.Single(ledger.Unsold("G").Held).BodyName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEraseLeavesOnlyTheFinesCommittedAfterIt(bool history)
    {
        var ledger = CrimeLedger(history, Fines());

        Assert.Equal("Federation", Assert.Single(ledger.Owed("F", 2)).Faction);
        Assert.Equal("Gnosis", Assert.Single(ledger.Owed("G", 2)).Faction);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEraseLeavesOnlyTheCommandersMetAfterIt(bool history)
    {
        var ledger = MetLedger(history, Meetings());

        Assert.Equal("Newer", Assert.Single(ledger.Met("F")).Name);
        Assert.Equal("Old Friend", Assert.Single(ledger.Met("G")).Name);
    }

    [Fact]
    public void AHandResetOfOrganicDataAfterAnEraseStillClearsIt()
    {
        var ledger = OrganicLedger(history: true, Organic());

        ledger.Reset("F", DateTimeOffset.Parse("2026-09-05T13:00:00Z"));

        Assert.Empty(ledger.Unsold("F").Held);
    }

    [Fact]
    public void AHandResetOfExplorationDataAfterAnEraseStillClearsIt()
    {
        var ledger = MapLedger(history: true, Maps());

        ledger.Reset("F", DateTimeOffset.Parse("2026-09-05T13:00:00Z"));

        Assert.Empty(ledger.Unsold("F").Held);
    }

    [Fact]
    public void AnEraseAfterAHandResetStillClearsWhatCameBetween()
    {
        var ledger = OrganicLedger(history: true, [Load("F", "10:00:00"), Analysis("10:01:00", 1), Analysis("10:30:00", 2), Erase("F", "11:00:00")]);

        ledger.Reset("F", DateTimeOffset.Parse("2026-09-05T10:10:00Z"));

        Assert.Empty(ledger.Unsold("F").Held);
    }
}
