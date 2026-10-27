using System.Globalization;
using D47.App.Panel;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class APlanetCodexRowShowsTheHeldBodyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-27T20:00:00Z", CultureInfo.InvariantCulture);

    private static string Codex(string subCategory) =>
        "{\"timestamp\":\"2026-10-27T13:31:02Z\",\"event\":\"CodexEntry\",\"EntryID\":1,\"Name\":\"$Codex_Ent_Standard_Ter_Rocky_Name;\","
        + "\"SubCategory\":\"" + subCategory + "\",\"Category\":\"$Codex_Category_StellarBodies;\",\"Region\":\"$Codex_RegionName_18;\","
        + "\"System\":\"Sol\",\"SystemAddress\":100,\"BodyID\":0}";

    private const string Scan =
        "{\"timestamp\":\"2026-10-27T13:31:02Z\",\"event\":\"Scan\",\"BodyName\":\"Sol 2\",\"SystemAddress\":100,"
        + "\"TerraformState\":\"\",\"PlanetClass\":\"Rocky body\",\"Atmosphere\":\"thin atmosphere\",\"AtmosphereType\":\"Argon\"}";

    private static string? Body(string subCategory)
    {
        var ledger = CodexLedger.None;

        foreach (var line in new[] { Scan, Codex(subCategory) })
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));
            ledger = ledger.Apply(parsed!);
        }

        var view = CodexModel.Entries(ledger, CodexPeriod.Everything, 18, Now, HistoryState.Done, null, (_, _) => "Sol A");

        return view.Groups.Single().Rows.Single().Body;
    }

    [Fact]
    public void ATerrestrialRowShowsTheScannedBodyNotTheBodyIdLookup() =>
        Assert.Equal("2", Body("$Codex_SubCategory_Terrestrials;"));

    [Fact]
    public void ARowOfAnotherFamilyKeepsTheBodyIdLookup() =>
        Assert.Equal("A", Body("$Codex_SubCategory_Geology;"));
}
