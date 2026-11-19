using System.Globalization;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ACodexNodeCountsItsKindsPerScopeTests
{
    private const string Giant = "star.k_yellow_orange_giant_star.giant";
    private const string Dwarf = "star.k_yellow_orange_star.dwarf";
    private const string Parent = "star.k_yellow_orange_giant_star";

    private static string Kind(string key, string name, string parent, string regions) =>
        key + "\t" + name + "\tStars\t" + parent + "\tsub\t\t\t\t\t\t" + regions + "\n";

    private static string Canonn(long id, string name, string regions) =>
        id + "\t" + name + "\tBiology\tOrganic structures\tAlpha\t" + regions + "\n";

    private static readonly CodexTable Table = CodexTable.Parse(
        Kind(Parent, "K giant star", "", "")
        + Kind(Giant, "K giant", Parent, "18:10,3:4")
        + Kind(Dwarf, "K dwarf", "", "3:7")
        + Canonn(101, "Alpha One", "18:5,3:2")
        + Canonn(102, "Alpha Two", "18:3")
        + Canonn(103, "Alpha Three", "3:9"));

    private static readonly CodexTree Tree = CodexTree.Of(Table);

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static CodexLedger Fold(params string[] lines) =>
        lines.Aggregate(CodexLedger.None, (ledger, line) => ledger.Apply(Event(line)));

    private static string Logged(string time, long id, int region, string system = "Sol") =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"CodexEntry\",\"EntryID\":" + id
        + ",\"Name_Localised\":\"Alpha\",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_"
        + region + ";\",\"System\":\"" + system + "\",\"IsNewEntry\":true}";

    private static string Arrive(string time, int region)
    {
        var at = GalacticRegions.LabelAt(region)!.Value;
        Assert.Equal(region, GalacticRegions.IdOf(GalacticRegions.Find(at)!));

        return "{\"timestamp\":\"" + time + "\",\"event\":\"FSDJump\",\"StarSystem\":\"Jump " + region + "\",\"SystemAddress\":"
            + region + ",\"StarPos\":[" + at.X.ToString("R", CultureInfo.InvariantCulture) + ",0,"
            + at.Z.ToString("R", CultureInfo.InvariantCulture) + "]}";
    }

    private static string Scan(string time, string body, int region, string type, string luminosity) =>
        "{\"timestamp\":\"" + time + "\",\"event\":\"Scan\",\"ScanType\":\"AutoScan\",\"BodyName\":\"" + body
        + "\",\"StarSystem\":\"Jump " + region + "\",\"SystemAddress\":" + region + ",\"StarType\":\"" + type
        + "\",\"Luminosity\":\"" + luminosity + "\"}";

    private static IEnumerable<CodexKindDiscovery> AllKinds(CodexNodeDiscoveries node) =>
        node.Kinds.Concat(node.Groups.SelectMany(AllKinds));

    private static CodexKindDiscovery Of(CodexNodeDiscoveries node, string key) =>
        AllKinds(node).Single(kind => kind.Entry.Key == key);

    [Fact]
    public void ARegionCountsOnlyKindsListedOrFoundThereAndEveryRegionCountsAll()
    {
        var ledger = CodexLedger.None;

        var inEighteen = CodexNodeDiscoveries.For(ledger, Tree.Root, 18);
        var inThree = CodexNodeDiscoveries.For(ledger, Tree.Root, 3);
        var everywhere = CodexNodeDiscoveries.For(ledger, Tree.Root, null);

        Assert.Equal(["101", "102", Giant], AllKinds(inEighteen).Select(kind => kind.Entry.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["101", "103", Giant, Dwarf], AllKinds(inThree).Select(kind => kind.Entry.Key).Order(StringComparer.Ordinal));
        Assert.Equal(["101", "102", "103", Giant, Dwarf], AllKinds(everywhere).Select(kind => kind.Entry.Key).Order(StringComparer.Ordinal));
        Assert.Equal(5, everywhere.Counted);
        Assert.Equal(3, inEighteen.Counted);
        Assert.Equal(4, inThree.Counted);
    }

    [Fact]
    public void ARowWithChildrenIsAGroupAndNeverAKindInEitherScope()
    {
        var parent = Tree.Node(Parent)!;

        foreach (int? scope in new int?[] { null, 18, 3 })
        {
            var node = CodexNodeDiscoveries.For(CodexLedger.None, Tree.Root, scope);

            Assert.DoesNotContain(AllKinds(node), kind => kind.Entry.Key == Parent);
            Assert.DoesNotContain(CodexNodeDiscoveries.For(CodexLedger.None, parent, scope).Kinds, kind => kind.Entry.Key == Parent);
        }
    }

    [Fact]
    public void SystemsAreCountedInTheScopeAndInEveryRegion()
    {
        var ledger = CodexLedger.None;

        var inEighteen = Of(CodexNodeDiscoveries.For(ledger, Tree.Root, 18), "101");
        var everywhere = Of(CodexNodeDiscoveries.For(ledger, Tree.Root, null), "101");

        Assert.Equal((5, 7), (inEighteen.Systems, inEighteen.SystemsAllRegions));
        Assert.Equal((7, 7), (everywhere.Systems, everywhere.SystemsAllRegions));
    }

    [Fact]
    public void ACanonnEntryLoggedTwiceInARegionGivesTheEarlierAsItsFirstFind()
    {
        var ledger = Fold(Logged("2026-11-02T10:00:00Z", 101, 18, "Later"), Logged("2026-11-01T10:00:00Z", 101, 18, "Earlier"));

        var kind = Of(CodexNodeDiscoveries.For(ledger, Tree.Root, 18), "101");

        Assert.Equal(new CodexFirstFind(DateTimeOffset.Parse("2026-11-01T10:00:00Z", CultureInfo.InvariantCulture), "Earlier", null, 18), kind.FirstFind);
        Assert.True(kind.IsOnRecord);
    }

    [Fact]
    public void AKindGivesItsFirstScanInTheRegion()
    {
        var ledger = Fold(
            Arrive("2026-11-01T09:00:00Z", 18),
            Scan("2026-11-01T09:01:00Z", "Jump 18 A", 18, "K_OrangeGiant", "III"),
            Scan("2026-11-01T09:05:00Z", "Jump 18 B", 18, "K_OrangeGiant", "III"));

        var kind = Of(CodexNodeDiscoveries.For(ledger, Tree.Root, 18), Giant);

        Assert.Equal("Jump 18 A", kind.FirstFind!.Body);
        Assert.Equal(18, kind.FirstFind.RegionId);
    }

    [Fact]
    public void WithEveryRegionTheFirstFindIsTheEarliestAndCarriesItsRegion()
    {
        var ledger = Fold(
            Logged("2026-11-02T10:00:00Z", 101, 18),
            Logged("2026-11-01T10:00:00Z", 101, 3),
            Arrive("2026-11-03T09:00:00Z", 18),
            Scan("2026-11-03T09:01:00Z", "Jump 18 A", 18, "K_OrangeGiant", "III"),
            Arrive("2026-11-04T09:00:00Z", 3),
            Scan("2026-11-04T09:01:00Z", "Jump 3 A", 3, "K_OrangeGiant", "III"));

        var node = CodexNodeDiscoveries.For(ledger, Tree.Root, null);

        Assert.Equal(3, Of(node, "101").FirstFind!.RegionId);
        Assert.Equal(18, Of(node, Giant).FirstFind!.RegionId);
    }

    [Fact]
    public void AKindFoundWhereTheTableDoesNotListItIsOnRecordWithZeroSystems()
    {
        var ledger = Fold(
            Arrive("2026-11-01T09:00:00Z", 18),
            Scan("2026-11-01T09:01:00Z", "Jump 18 A", 18, "K", "V"));

        var node = CodexNodeDiscoveries.For(ledger, Tree.Root, 18);
        var dwarf = Of(node, Dwarf);

        Assert.Equal(0, dwarf.Systems);
        Assert.True(dwarf.IsOnRecord);
        Assert.Equal(4, node.Counted);
    }

    [Fact]
    public void AGroupsCountsAreTheSumsOfItsKindsThroughEveryLevel()
    {
        var ledger = Fold(
            Logged("2026-11-01T10:00:00Z", 101, 18),
            Arrive("2026-11-01T09:00:00Z", 18),
            Scan("2026-11-01T09:01:00Z", "Jump 18 A", 18, "K_OrangeGiant", "III"));

        var root = CodexNodeDiscoveries.For(ledger, Tree.Root, 18);

        Assert.Equal((3, 2, 1), (root.Counted, root.OnRecord, root.Missing));
        AssertSums(root);
    }

    private static void AssertSums(CodexNodeDiscoveries node)
    {
        Assert.Equal(node.Kinds.Count + node.Groups.Sum(group => group.Counted), node.Counted);
        Assert.Equal(node.Kinds.Count(kind => kind.IsOnRecord) + node.Groups.Sum(group => group.OnRecord), node.OnRecord);
        Assert.Equal(node.Kinds.Count(kind => !kind.IsOnRecord) + node.Groups.Sum(group => group.Missing), node.Missing);

        foreach (var group in node.Groups)
        {
            AssertSums(group);
        }
    }

    [Fact]
    public void MissingLeavesOutAGroupWhoseKindsAreAllOnRecord()
    {
        var ledger = Fold(
            Logged("2026-11-01T10:00:00Z", 101, 18),
            Logged("2026-11-01T10:00:00Z", 102, 18));

        var missing = CodexNodeDiscoveries.For(ledger, Tree.Root, 18, CodexKindStatus.Missing);
        var onRecord = CodexNodeDiscoveries.For(ledger, Tree.Root, 18, CodexKindStatus.OnRecord);

        Assert.Equal([Giant], AllKinds(missing).Select(kind => kind.Entry.Key));
        Assert.DoesNotContain(missing.Groups, group => AllKinds(group).Any(kind => kind.Entry.Key == "101"));
        Assert.Equal(["101", "102"], AllKinds(onRecord).Select(kind => kind.Entry.Key).Order());
        Assert.Equal(3, missing.Counted);
    }
}
