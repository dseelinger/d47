using D47.App.Panel;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The entry page of a star or planet kind lists the kind's first scan in the region under "Logged in this region", with
/// the kind's family and its parent's name on the Category and Subcategory tiles.
/// </summary>
public sealed class AKindsFirstFindIsShownOnItsPageTests
{
    private const string KGiant = "star.k_yellow_orange_giant_star.giant";

    private static readonly CodexTable Table = CodexTable.Parse(
        "star.k_yellow_orange_giant_star\tK (Yellow-Orange giant) Star\tStars\t\tK (Yellow-Orange giant) Star\t\t\t\t\t\t18:900\n"
        + "star.k_yellow_orange_giant_star.giant\tK (Yellow-Orange giant) Star, giant\tStars\tstar.k_yellow_orange_giant_star\tK (Yellow-Orange giant) Star\tgiant\t\t\t\t\t18:800\n");

    private static string Arrive(string time, string system, long address) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"" + system + "\",\"SystemAddress\":" + address
        + ",\"StarPos\":[0.0,0.0,0.0]}";

    private static string Scan(string time, string system, long address, string body) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"Scan\",\"ScanType\":\"AutoScan\",\"BodyName\":\"" + body + "\",\"StarSystem\":\"" + system
        + "\",\"SystemAddress\":" + address + ",\"StarType\":\"K_OrangeGiant\",\"Luminosity\":\"III\"}";

    private static CodexLedger Fold(params string[] lines) =>
        lines.Aggregate(CodexLedger.None, (ledger, line) =>
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

            return ledger.Apply(parsed!);
        });

    private static CodexEntryView Page(CodexLedger ledger, string key, int? region, string? current = null) =>
        CodexModel.EntryPage(ledger, Table, key, region, "Fallback", current, (_, _) => null);

    [Fact]
    public void AKindWithAScanInTheRegionShowsThatSystemBodyAndTime()
    {
        var ledger = Fold(
            Arrive("2026-11-01T09:00:00Z", "Fixture Sol", 1),
            Scan("2026-11-01T09:01:00Z", "Fixture Sol", 1, "Fixture Sol A"));

        var view = Page(ledger, KGiant, 18, "fixture sol");

        var row = Assert.Single(view.Loggings);
        Assert.Equal("Fixture Sol", row.System);
        Assert.Equal("A", row.Body);
        Assert.Equal(DateTimeOffset.Parse("2026-11-01T09:01:00Z", System.Globalization.CultureInfo.InvariantCulture), row.At);
        Assert.True(row.IsHere);
        Assert.False(row.IsNew);
    }

    [Fact]
    public void ASecondScanOfTheSameKindInTheRegionChangesNothing()
    {
        var first = Fold(
            Arrive("2026-11-01T09:00:00Z", "Fixture Sol", 1),
            Scan("2026-11-01T09:01:00Z", "Fixture Sol", 1, "Fixture Sol A"));
        var second = Fold(
            Arrive("2026-11-01T09:00:00Z", "Fixture Sol", 1),
            Scan("2026-11-01T09:01:00Z", "Fixture Sol", 1, "Fixture Sol A"),
            Arrive("2026-11-02T09:00:00Z", "Fixture Luna", 2),
            Scan("2026-11-02T09:01:00Z", "Fixture Luna", 2, "Fixture Luna B"));

        Assert.Equal(Page(first, KGiant, 18).Loggings, Page(second, KGiant, 18).Loggings);
    }

    [Fact]
    public void AKindWithNoScanInTheRegionHasNoRowSoThePageReadsNotHere()
    {
        var ledger = Fold(
            Arrive("2026-11-01T09:00:00Z", "Fixture Sol", 1),
            Scan("2026-11-01T09:01:00Z", "Fixture Sol", 1, "Fixture Sol A"));

        Assert.Empty(Page(ledger, KGiant, 30).Loggings);
        Assert.Empty(Page(CodexLedger.None, KGiant, 18).Loggings);
        Assert.Empty(Page(ledger, KGiant, null).Loggings);
    }

    [Fact]
    public void TheTilesReadTheFamilyAndTheParentsName()
    {
        var view = Page(CodexLedger.None, KGiant, 18);

        Assert.Equal("Stars", view.Category);
        Assert.Equal("K (Yellow-Orange giant) Star", view.SubCategory);
        Assert.Null(Page(CodexLedger.None, "star.k_yellow_orange_giant_star", 18).SubCategory);
    }
}
