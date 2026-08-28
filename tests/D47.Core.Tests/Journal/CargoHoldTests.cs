using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>
/// Reading the cargo hold, and the commodity-name fold that joins it to a construction site
/// (Phase 18, "Colonisation and construction tracking").
/// <para>
/// Every shape asserted here was measured against the 912-journal corpus — see
/// <c>docs/spikes/colonisation-sources.md</c> §7 — and the two that would fail silently in the game
/// are the case fold and the SRV manifest.
/// </para>
/// </summary>
public class CargoHoldTests
{
    private static string Folder()
    {
        var path = Path.Combine(Path.GetTempPath(), "d47-cargo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static CargoManifestReader ReaderOver(string folder, string contents)
    {
        File.WriteAllText(Path.Combine(folder, CargoManifestReader.ManifestFile), contents);
        return new CargoManifestReader(folder, NullLogger.Instance);
    }

    private const string Manifest =
        """
        { "timestamp":"2026-08-16T10:00:00Z", "event":"Cargo", "Vessel":"Ship", "Count":300,
          "Inventory":[
            { "Name":"aluminium", "Count":200, "Stolen":0 },
            { "Name":"computercomponents", "Name_Localised":"Computer Components", "Count":100, "Stolen":4 } ] }
        """;

    [Fact]
    public void TheManifestIsReadFromTheFile()
    {
        var reader = ReaderOver(Folder(), Manifest);

        Assert.True(reader.Poll());

        var hold = reader.Current;

        Assert.True(hold.IsKnown);
        Assert.Equal("Ship", hold.Vessel);
        Assert.True(hold.IsShip);
        Assert.Equal(300, hold.Count);
        Assert.Equal(200, hold.Of("aluminium"));
        Assert.Equal(100, hold.Of("computercomponents"));
        Assert.Equal(0, hold.Of("tritium"));
    }

    /// <summary>
    /// The join. A depot writes <c>$aluminium_name;</c> and the hold writes <c>aluminium</c>; if
    /// these did not meet, every shortfall would be reported against an empty hold and would read
    /// exactly like a correct answer.
    /// </summary>
    [Fact]
    public void ADepotSpellingFindsTheSameCommodityInTheHold()
    {
        var reader = ReaderOver(Folder(), Manifest);
        reader.Poll();

        Assert.Equal(200, reader.Current.Of("$aluminium_name;"));
    }

    /// <summary>
    /// The case fold, which is the whole of why <see cref="JournalJson.Symbol(string?)"/> lowercases.
    /// <c>ColonisationContribution</c> writes mixed case on 30 of 30 distinct symbols measured,
    /// against 0 of 31 for the depot — so without this, a Commander's own completed delivery joins
    /// to nothing and is reported as never having happened.
    /// </summary>
    [Fact]
    public void AContributionSpellingFindsItTooDespiteTheCase()
    {
        var reader = ReaderOver(Folder(), Manifest);
        reader.Poll();

        Assert.Equal(100, reader.Current.Of("$ComputerComponents_name;"));
        Assert.Equal("computercomponents", JournalJson.Symbol("$ComputerComponents_name;"));
    }

    /// <summary>
    /// Elite rewrites this file for the SRV's own hold. Eight tonnes of scoopings reported as the
    /// ship's four hundred would be a wrong answer nobody could see was wrong, so the vessel is
    /// carried rather than assumed.
    /// </summary>
    [Fact]
    public void TheSrvManifestSaysItIsTheSrvs()
    {
        var reader = ReaderOver(
            Folder(),
            """
            { "timestamp":"2026-08-16T10:00:00Z", "event":"Cargo", "Vessel":"SRV", "Count":8,
              "Inventory":[ { "Name":"aluminium", "Count":8, "Stolen":0 } ] }
            """);

        reader.Poll();

        Assert.Equal("SRV", reader.Current.Vessel);
        Assert.False(reader.Current.IsShip);
    }

    [Fact]
    public void AnEmptyHoldIsKnownRatherThanUnknown()
    {
        var reader = ReaderOver(
            Folder(),
            """
            { "timestamp":"2026-08-16T10:00:00Z", "event":"Cargo", "Vessel":"Ship", "Count":0, "Inventory":[] }
            """);

        Assert.True(reader.Poll());
        Assert.True(reader.Current.IsKnown);
        Assert.Equal(0, reader.Current.Count);
        Assert.Empty(reader.Current.Items);
    }

    /// <summary>A Commander who has not launched since installing has no file, and that is not an error.</summary>
    [Fact]
    public void AnAbsentFileLeavesTheHoldUnknown()
    {
        var reader = new CargoManifestReader(Folder(), NullLogger.Instance);

        Assert.False(reader.Poll());
        Assert.False(reader.Current.IsKnown);
    }

    /// <summary>
    /// Caught mid-write. Expected at 10 Hz against a file the game rewrites — and the stamp must
    /// not move, so the next poll retries rather than waiting for Elite to touch the file again.
    /// </summary>
    [Fact]
    public void AFileCaughtMidWriteIsRetriedRatherThanSkipped()
    {
        var folder = Folder();
        var path = Path.Combine(folder, CargoManifestReader.ManifestFile);

        File.WriteAllText(path, """{ "event":"Cargo", "Vessel":"Ship", "Count":30, "Inv""");

        var reader = new CargoManifestReader(folder, NullLogger.Instance);

        Assert.False(reader.Poll());
        Assert.False(reader.Current.IsKnown);

        // Same last-write time is not enough to prove the retry, so rewrite whole and move it on.
        File.WriteAllText(path, Manifest);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(1));

        Assert.True(reader.Poll());
        Assert.Equal(300, reader.Current.Count);
    }

    [Fact]
    public void AnUnchangedFileIsNotReRead()
    {
        var reader = ReaderOver(Folder(), Manifest);

        Assert.True(reader.Poll());
        Assert.False(reader.Poll());
    }

    [Theory]
    [InlineData("$aluminium_name;", "aluminium")]
    [InlineData("$ComputerComponents_name;", "computercomponents")]
    [InlineData("aluminium", "aluminium")]
    [InlineData("Tritium", "tritium")]
    [InlineData("  $Water_name;  ", "water")]
    public void TheSymbolFoldIsTheSameForEveryWayEliteSpellsIt(string written, string expected) =>
        Assert.Equal(expected, JournalJson.Symbol(written));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("$_name;")]
    public void TheSymbolFoldRefusesRatherThanInventingAnEmptyKey(string? written) =>
        Assert.Null(JournalJson.Symbol(written));
}
