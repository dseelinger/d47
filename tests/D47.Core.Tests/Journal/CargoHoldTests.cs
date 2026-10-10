using System.Text;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary>Reading the cargo hold, and the commodity-name fold that joins it to a construction site.</summary>
public class CargoHoldTests
{
    private const string Folder = @"C:\d47-test\journals";

    private readonly MemoryFileSystem _files = new();

    private CargoManifestReader ReaderOver(string contents)
    {
        _files.WriteText(Path.Combine(Folder, CargoManifestReader.ManifestFile), contents);
        return new CargoManifestReader(Folder, _files, NullLogger.Instance);
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
        var reader = ReaderOver(Manifest);

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

    /// <summary>The join.</summary>
    [Fact]
    public void ADepotSpellingFindsTheSameCommodityInTheHold()
    {
        var reader = ReaderOver(Manifest);
        reader.Poll();

        Assert.Equal(200, reader.Current.Of("$aluminium_name;"));
    }

    /// <summary>
    /// The case fold, which is the whole of why <see cref="JournalJson.Symbol(string?)"/> lowercases.
    /// </summary>
    [Fact]
    public void AContributionSpellingFindsItTooDespiteTheCase()
    {
        var reader = ReaderOver(Manifest);
        reader.Poll();

        Assert.Equal(100, reader.Current.Of("$ComputerComponents_name;"));
        Assert.Equal("computercomponents", JournalJson.Symbol("$ComputerComponents_name;"));
    }

    /// <summary>Elite rewrites this file for the SRV's own hold.</summary>
    [Fact]
    public void TheSrvManifestSaysItIsTheSrvs()
    {
        var reader = ReaderOver(
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
        var reader = new CargoManifestReader(Folder, _files, NullLogger.Instance);

        Assert.False(reader.Poll());
        Assert.False(reader.Current.IsKnown);
    }

    [Fact]
    public void AFileCaughtMidWriteIsRetriedRatherThanSkipped()
    {
        var path = Path.Combine(Folder, CargoManifestReader.ManifestFile);

        _files.WriteText(path, """{ "event":"Cargo", "Vessel":"Ship", "Count":30, "Inv""");

        var reader = new CargoManifestReader(Folder, _files, NullLogger.Instance);

        Assert.False(reader.Poll());
        Assert.False(reader.Current.IsKnown);

        _files.WriteText(path, Manifest);

        Assert.True(reader.Poll());
        Assert.Equal(300, reader.Current.Count);
    }

    [Fact]
    public void AnUnchangedFileIsNotReRead()
    {
        var reader = ReaderOver(Manifest);

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

[Trait("Category", "Integration")]
public class ACargoFileEliteHoldsOpenStillReadsTests
{
    [Fact]
    public void ReadingWorksWhileAnotherHandleHasTheFileOpenForWriting()
    {
        using var install = new TempInstall();
        var path = Path.Combine(install.Root, CargoManifestReader.ManifestFile);
        File.WriteAllText(path, "");

        using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
        {
            writer.Write(Encoding.UTF8.GetBytes("""{ "event":"Cargo", "Vessel":"Ship", "Count":4, "Inventory":[ { "Name":"gold", "Count":4 } ] }"""));
            writer.Flush();

            var reader = new CargoManifestReader(install.Root, new DiskFileSystem(), NullLogger.Instance);

            Assert.True(reader.Poll());
            Assert.Equal(4, reader.Current.Count);
        }
    }
}
