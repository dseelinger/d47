using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class APlanetCodexRowNamesTheScannedBodyTests
{
    private const string Hmc = "$Codex_Ent_Standard_Ter_High_Metal_Content_Name;";

    private static string Codex(string at, string symbol = Hmc, long address = 100) =>
        "{\"timestamp\":\"" + at + "\",\"event\":\"CodexEntry\",\"EntryID\":1,\"Name\":\"" + symbol
        + "\",\"SubCategory\":\"$Codex_SubCategory_Terrestrials;\",\"Category\":\"$Codex_Category_StellarBodies;\","
        + "\"Region\":\"$Codex_RegionName_18;\",\"System\":\"Sol\",\"SystemAddress\":" + address + ",\"BodyID\":0}";

    private static string Scan(string at, string name, string planetClass = "High metal content body", string atmosphereType = "SulphurDioxide", string terraform = "", long address = 100) =>
        "{\"timestamp\":\"" + at + "\",\"event\":\"Scan\",\"ScanType\":\"AutoScan\",\"BodyName\":\"" + name + "\",\"SystemAddress\":" + address
        + ",\"TerraformState\":\"" + terraform + "\",\"PlanetClass\":\"" + planetClass + "\",\"Atmosphere\":\""
        + (atmosphereType == "None" ? string.Empty : "thin atmosphere") + "\",\"AtmosphereType\":\"" + atmosphereType + "\"}";

    private static string StarScan(string at) =>
        "{\"timestamp\":\"" + at + "\",\"event\":\"Scan\",\"BodyName\":\"Sol\",\"SystemAddress\":100,\"StarType\":\"G\"}";

    private static string? BodyAfter(params string[] lines)
    {
        var ledger = CodexLedger.None;

        foreach (var line in lines)
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            ledger = ledger.Apply(parsed!);
        }

        return Assert.Single(ledger.Entries).Body;
    }

    private const string Now = "2026-10-27T13:31:02Z";

    [Fact]
    public void AScanWrittenBeforeTheEntryIsTheBody() =>
        Assert.Equal("Sol 2", BodyAfter(Scan(Now, "Sol 2"), Codex(Now)));

    [Fact]
    public void AScanWrittenAfterTheEntryIsTheBody() =>
        Assert.Equal("Sol 2", BodyAfter(Codex(Now), Scan(Now, "Sol 2")));

    [Fact]
    public void TwoMatchingScansGiveNoBody() =>
        Assert.Null(BodyAfter(Scan(Now, "Sol 2"), Codex(Now), Scan(Now, "Sol 3")));

    [Fact]
    public void TheSameBodyScannedTwiceIsStillOneBody() =>
        Assert.Equal("Sol 2", BodyAfter(Scan(Now, "Sol 2"), Codex(Now), Scan(Now, "Sol 2")));

    [Fact]
    public void AScanInAnotherSecondIsNotUsed() =>
        Assert.Null(BodyAfter(Scan("2026-10-27T13:31:01Z", "Sol 2"), Codex(Now), Scan("2026-10-27T13:31:03Z", "Sol 3")));

    [Fact]
    public void AScanInAnotherSystemIsNotUsed() =>
        Assert.Null(BodyAfter(Scan(Now, "Other 2", address: 200), Codex(Now)));

    [Fact]
    public void TheParentStarIsNeverTheBody() =>
        Assert.Null(BodyAfter(StarScan(Now), Codex(Now)));

    [Fact]
    public void AScanOfAnotherKindOfBodyIsNotTheBody() =>
        Assert.Equal("Sol 3", BodyAfter(Scan(Now, "Sol 2", "Rocky body"), Scan(Now, "Sol 3"), Codex(Now)));

    [Fact]
    public void TheAtmosphereAndTerraformStateMustAgree()
    {
        Assert.Null(BodyAfter(Scan(Now, "Sol 2", atmosphereType: "None"), Codex(Now)));
        Assert.Null(BodyAfter(Scan(Now, "Sol 2", terraform: "Terraformable"), Codex(Now)));
        Assert.Equal("Sol 2", BodyAfter(Scan(Now, "Sol 2", "Rocky body", "None", "Terraformable"), Codex(Now, "$Codex_Ent_TRF_Rocky_No_Atmos_Name;")));
    }

    [Fact]
    public void AGasGiantIsMatchedByItsClass() =>
        Assert.Equal("Sol 5", BodyAfter(Scan(Now, "Sol 5", "Sudarsky class II gas giant", ""), Codex(Now, "$Codex_Ent_Standard_Sudarsky_Class_II_Name;")));

    [Fact]
    public void AnEntryOfAnotherKindHoldsNoBody()
    {
        var ledger = CodexLedger.None;
        Assert.True(JournalEvent.TryParse(Scan(Now, "Sol 2"), NullLogger.Instance, out var scan));
        Assert.True(JournalEvent.TryParse(Codex(Now, "$Codex_Ent_Cactoid_Name;"), NullLogger.Instance, out var entry));

        Assert.Null(Assert.Single(ledger.Apply(scan!).Apply(entry!).Entries).Body);
    }
}
