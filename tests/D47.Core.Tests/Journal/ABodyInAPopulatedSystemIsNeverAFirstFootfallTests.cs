using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ABodyInAPopulatedSystemIsNeverAFirstFootfallTests
{
    private const long System = 3274702866819;

    private static JournalEvent Parse(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static string Jump(long population) =>
        $$"""{"timestamp":"2026-09-26T19:00:00Z","event":"FSDJump","StarSystem":"Ossen","SystemAddress":{{System}},"Population":{{population}}}""";

    private const string Commander = """{"timestamp":"2026-09-26T18:00:00Z","event":"LoadGame","FID":"F1","Commander":"Fixture"}""";

    private static readonly string Scan =
        $$"""{"timestamp":"2026-09-26T19:05:00Z","event":"Scan","ScanType":"Detailed","BodyName":"Ossen 2 c","BodyID":7,"SystemAddress":{{System}},"Landable":true,"WasFootfalled":false}""";

    private static readonly string Disembark =
        $$"""{"timestamp":"2026-09-26T19:10:00Z","event":"Disembark","SRV":false,"OnPlanet":true,"OnStation":false,"SystemAddress":{{System}},"BodyID":7}""";

    private static readonly string Analyse =
        $$"""{"timestamp":"2026-09-26T19:20:00Z","event":"ScanOrganic","ScanType":"Analyse","Species":"$Codex_Ent_Cactoid_01_Name;","Species_Localised":"Cactoida Cortexum","SystemAddress":{{System}},"Body":7}""";

    private static AdventureWorld World(long? population)
    {
        var jump = population is { } value
            ? Jump(value)
            : $$"""{"timestamp":"2026-09-26T19:00:00Z","event":"FSDJump","StarSystem":"Ossen","SystemAddress":{{System}}}""";
        var world = AdventureWorld.Empty;

        foreach (var line in new[] { jump, Scan, Disembark })
        {
            world = world.Apply(Parse(line));
        }

        return world;
    }

    private static HeldAnalysis Held(long population, params string[] extra)
    {
        var ledger = new ExobiologyLedger(null, new MemoryFileSystem(), NullLogger.Instance);
        ledger.FoldHistory([], TestContext.Current.CancellationToken);
        ledger.Apply([.. new[] { Commander, Jump(population), Scan }.Concat(extra).Append(Analyse).Select(Parse)]);
        return Assert.Single(ledger.Unsold("F1").Held);
    }

    [Fact]
    public void ADisembarkInAPopulatedSystemTakesNoFirstFootfall()
    {
        var world = World(1_000_000);

        Assert.Null(world.Bodies.For(System, 7)!.FootfallTakenAt);
        Assert.False(world.IsFirstFootfall(Parse(Disembark)));
    }

    [Fact]
    public void AScanInAPopulatedSystemReadsAsAlreadyWalked() =>
        Assert.True(World(1_000_000).Bodies.For(System, 7)!.WasFootfalled);

    [Theory]
    [InlineData(0L)]
    [InlineData(null)]
    public void AnUnpopulatedSystemKeepsTheFlagAsWritten(long? population)
    {
        var world = World(population);

        Assert.False(world.Bodies.For(System, 7)!.WasFootfalled);
        Assert.True(world.IsFirstFootfall(Parse(Disembark)));
    }

    [Fact]
    public void AnAnalysisInAPopulatedSystemIsHeldWithoutTheBonus() =>
        Assert.False(Held(1_000_000).FirstFootfall);

    [Fact]
    public void AnAnalysisInAnUnpopulatedSystemKeepsTheBonus() =>
        Assert.True(Held(0).FirstFootfall);

    [Fact]
    public void ABackfillOverFilesGivesTheSameAnswerAsTheLiveFold()
    {
        var files = new MemoryFileSystem();
        files.WriteText("j.log", string.Join('\n', Commander, Jump(1_000_000), Scan, Analyse));
        var ledger = new ExobiologyLedger(null, files, NullLogger.Instance);

        ledger.FoldHistory(["j.log"], TestContext.Current.CancellationToken);

        Assert.False(Assert.Single(ledger.Unsold("F1").Held).FirstFootfall);
    }
}
