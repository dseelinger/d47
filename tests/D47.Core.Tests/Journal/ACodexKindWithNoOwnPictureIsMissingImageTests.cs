using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

public class ACodexKindWithNoOwnPictureIsMissingImageTests
{
    private static string Canonn(long id, string name, string regions) =>
        id + "\t" + name + "\tBiology\tOrganic structures\tAlpha\t" + regions + "\n";

    private static readonly CodexTable Table = CodexTable.Parse(
        Canonn(101, "Alpha One", "18:5,3:2")
        + Canonn(102, "Alpha Two", "18:3")
        + Canonn(103, "Alpha Three", "3:9"));

    private static readonly CodexTree Tree = CodexTree.Of(Table);

    private static CodexLedger Fold(params string[] lines) =>
        lines.Aggregate(CodexLedger.None, (ledger, line) =>
        {
            Assert.True(JournalEvent.TryParse(line, NullLogger.Instance, out var parsed));

            return ledger.Apply(parsed!);
        });

    private static string Logged(long id, int region) =>
        "{\"timestamp\":\"2026-11-01T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":" + id
        + ",\"Name_Localised\":\"Alpha\",\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"$Codex_RegionName_"
        + region + ";\",\"System\":\"Sol\",\"IsNewEntry\":true}";

    private static IReadOnlySet<CodexPictureKey> Own(params (long Id, int Region)[] pictures) =>
        new HashSet<CodexPictureKey>(pictures.Select(picture => new CodexPictureKey(CodexKeys.Of(picture.Id), picture.Region)));

    private static string[] Listed(CodexLedger ledger, int? region, IReadOnlySet<CodexPictureKey>? own)
    {
        var found = CodexNodeDiscoveries.For(ledger, Tree.Root, region, CodexKindStatus.MissingImage, own);

        return [.. Kinds(found).Select(kind => kind.Entry.Key).Order(StringComparer.Ordinal)];
    }

    private static IEnumerable<CodexKindDiscovery> Kinds(CodexNodeDiscoveries node) =>
        node.Kinds.Concat(node.Groups.SelectMany(Kinds));

    [Fact]
    public void InARegionAKindOnRecordThereWithNoOwnPictureForIt()
    {
        var ledger = Fold(Logged(101, 18), Logged(102, 18));

        Assert.Equal(["101", "102"], Listed(ledger, 18, Own()));
        Assert.Equal(["102"], Listed(ledger, 18, Own((101, 18))));
        Assert.Equal(["102"], Listed(ledger, 18, Own((101, 18), (102, 3))));
    }

    [Fact]
    public void AMissingKindIsNotListed()
    {
        var ledger = Fold(Logged(101, 18));

        Assert.Equal(["101"], Listed(ledger, 18, Own()));
        Assert.DoesNotContain("102", Listed(ledger, 18, Own()));
        Assert.DoesNotContain("103", Listed(ledger, null, Own()));
    }

    [Fact]
    public void InEveryRegionAKindOnRecordInTwoRegionsWithAPictureForOneIsListed()
    {
        var ledger = Fold(Logged(101, 18), Logged(101, 3));

        Assert.Equal(["101"], Listed(ledger, null, Own((101, 18))));
        Assert.Equal(["101"], Listed(ledger, null, Own((101, 3))));
        Assert.Empty(Listed(ledger, null, Own((101, 18), (101, 3))));
    }

    [Fact]
    public void ARegionWithoutARegionIdDoesNotMakeAKindMissingImage()
    {
        var ledger = Fold(
            "{\"timestamp\":\"2026-11-01T10:00:00Z\",\"event\":\"CodexEntry\",\"EntryID\":101,\"Name_Localised\":\"Alpha\","
            + "\"SubCategory_Localised\":\"Organic structures\",\"Region\":\"Unknown\",\"System\":\"Sol\",\"IsNewEntry\":true}");

        Assert.Empty(Listed(ledger, null, Own()));
    }

    [Fact]
    public void TheCountCoversTheKindsThatPassWhateverTheFilter()
    {
        var ledger = Fold(Logged(101, 18), Logged(102, 18));

        var all = CodexNodeDiscoveries.For(ledger, Tree.Root, 18, CodexKindStatus.All, Own((101, 18)));
        var filtered = CodexNodeDiscoveries.For(ledger, Tree.Root, 18, CodexKindStatus.MissingImage, Own((101, 18)));

        Assert.Equal(1, all.MissingImage);
        Assert.Equal(1, filtered.MissingImage);
        Assert.Equal(["102"], Kinds(filtered).Select(kind => kind.Entry.Key));
    }
}
