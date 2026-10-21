using D47.Core.Interface;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A codex picture is keyed by EntryID and region, and found nearest-region-first for the same entry only.</summary>
public class ACodexPictureIsFoundByEntryAndRegionTests
{
    private const long Entry = 2420101;
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static CodexPictureRow Row(long entryId, int regionId) =>
        new(entryId, regionId, CodexPictureIndex.FirstTag, CodexPictureIndex.AssetName(entryId, regionId, Sha), Sha, 1000);

    private static HashSet<CodexPictureKey> Local(params (long Entry, int Region)[] keys) =>
        [.. keys.Select(key => new CodexPictureKey(key.Entry, key.Region))];

    [Fact]
    public void ALocalPictureForTheRegionBeatsAStockOne()
    {
        var found = CodexPictures.Find(Entry, 18, Local((Entry, 18)), [Row(Entry, 18)]);

        Assert.NotNull(found);
        Assert.Equal(CodexPictureSource.Local, found.Source);
        Assert.Equal(18, found.RegionId);
        Assert.False(found.FromOtherRegion);
    }

    [Fact]
    public void AStockPictureForTheRegionBeatsALocalOneFromAnotherRegion()
    {
        var found = CodexPictures.Find(Entry, 18, Local((Entry, 3)), [Row(Entry, 18)]);

        Assert.NotNull(found);
        Assert.Equal(CodexPictureSource.Stock, found.Source);
        Assert.Equal(18, found.RegionId);
        Assert.False(found.FromOtherRegion);
        Assert.Equal(Row(Entry, 18), found.Row);
    }

    [Fact]
    public void ALocalPictureFromAnotherRegionBeatsAStockOneFromAnotherRegion()
    {
        var found = CodexPictures.Find(Entry, 18, Local((Entry, 5)), [Row(Entry, 3)]);

        Assert.NotNull(found);
        Assert.Equal(CodexPictureSource.Local, found.Source);
        Assert.Equal(5, found.RegionId);
        Assert.True(found.FromOtherRegion);
    }

    [Fact]
    public void AStockPictureFromAnotherRegionIsTheLastResort()
    {
        var found = CodexPictures.Find(Entry, 18, Local(), [Row(Entry, 5), Row(Entry, 3)]);

        Assert.NotNull(found);
        Assert.Equal(CodexPictureSource.Stock, found.Source);
        Assert.Equal(3, found.RegionId);
        Assert.True(found.FromOtherRegion);
    }

    [Fact]
    public void TheLowestOtherRegionIsChosen()
    {
        var found = CodexPictures.Find(Entry, 18, Local((Entry, 7), (Entry, 4), (Entry, 9)), []);

        Assert.NotNull(found);
        Assert.Equal(4, found.RegionId);
    }

    [Fact]
    public void ANullRegionGoesStraightToTheOtherRegionSteps()
    {
        var found = CodexPictures.Find(Entry, null, Local(), [Row(Entry, 18), Row(Entry, 3)]);

        Assert.NotNull(found);
        Assert.Equal(3, found.RegionId);
        Assert.True(found.FromOtherRegion);
    }

    [Fact]
    public void NothingForTheEntryGivesNull()
    {
        Assert.Null(CodexPictures.Find(Entry, 18, Local(), []));
    }

    [Fact]
    public void AnotherEntrysPictureIsNeverUsed()
    {
        var found = CodexPictures.Find(Entry, 18, Local((Entry + 1, 18)), [Row(Entry + 1, 18), Row(Entry - 1, 3)]);

        Assert.Null(found);
    }

    [Fact]
    public void AKeyReadsBackFromItsFileName()
    {
        var key = new CodexPictureKey(Entry, 18);

        Assert.Equal("2420101-18.jpg", key.FileName);
        Assert.True(CodexPictureKey.TryParse(key.FileName, out var parsed));
        Assert.Equal(key, parsed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("2420101.jpg")]
    [InlineData("2420101-18.png")]
    [InlineData("2420101-18.jpg.exe")]
    [InlineData("0-18.jpg")]
    [InlineData("-5-18.jpg")]
    [InlineData("2420101-99999.jpg")]
    [InlineData("02420101-18.jpg")]
    [InlineData("a-b.jpg")]
    [InlineData("../2420101-18.jpg")]
    public void AnythingElseIsNotAKey(string name)
    {
        Assert.False(CodexPictureKey.TryParse(name, out _));
    }

    [Fact]
    public void ANullNameIsNotAKey()
    {
        Assert.False(CodexPictureKey.TryParse(null, out _));
    }

    [Fact]
    public void TheAssetNameCarriesTheFirstTwelveHashDigits()
    {
        Assert.Equal("2420101-18-0123456789ab.jpg", CodexPictureIndex.AssetName(Entry, 18, Sha));
    }

    [Fact]
    public void TheRightBytesMatchAndOthersDoNot()
    {
        byte[] picture = [1, 2, 3, 4];
        var sha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(picture));
        var row = Row(Entry, 18) with { Sha256 = sha, Bytes = 4 };

        Assert.True(CodexPictureIndex.Matches(row, picture));
        Assert.False(CodexPictureIndex.Matches(row, new byte[] { 1, 2, 3, 5 }));
        Assert.False(CodexPictureIndex.Matches(row, new byte[] { 1, 2, 3 }));
        Assert.False(CodexPictureIndex.Matches(row with { Bytes = 3 }, picture));
    }

    [Fact]
    public void WriteThenParseGivesBackTheRowsInAnyOrder()
    {
        var rows = new[] { Row(30, 18), Row(10, 5), Row(10, 3) };

        var forward = CodexPictureIndex.Write(rows);
        var backward = CodexPictureIndex.Write(rows.Reverse());

        Assert.Equal(forward, backward);

        var parsed = CodexPictureIndex.Parse(forward);

        Assert.Empty(parsed.Reasons);
        Assert.Equal([Row(10, 3), Row(10, 5), Row(30, 18)], parsed.Rows);
        Assert.Contains("\"entryId\": 10", forward);
    }

    [Fact]
    public void UpsertReplacesTheSameKeyAndAddsANewOne()
    {
        var rows = new[] { Row(10, 3), Row(10, 5) };
        var replacement = Row(10, 3) with { Bytes = 2000 };

        var replaced = CodexPictureIndex.Upsert(rows, replacement);
        var added = CodexPictureIndex.Upsert(rows, Row(11, 3));

        Assert.Equal(2, replaced.Count);
        Assert.Contains(replacement, replaced);
        Assert.Equal(3, added.Count);
    }

    [Fact]
    public void RemoveTakesOutOnlyThatKey()
    {
        var rows = new[] { Row(10, 3), Row(10, 5), Row(11, 3) };

        var left = CodexPictureIndex.Remove(rows, new CodexPictureKey(10, 3));

        Assert.Equal([Row(10, 5), Row(11, 3)], left);
    }

    [Fact]
    public void JsonThatIsNotAnArrayGivesNoRowsAndOneReason()
    {
        var parsed = CodexPictureIndex.Parse("{\"entryId\":1}");

        Assert.Empty(parsed.Rows);
        Assert.Single(parsed.Reasons);
    }

    [Fact]
    public void BrokenJsonGivesNoRowsAndOneReason()
    {
        var parsed = CodexPictureIndex.Parse("[{");

        Assert.Empty(parsed.Rows);
        Assert.Single(parsed.Reasons);
    }

    [Theory]
    [InlineData("entryId", "0")]
    [InlineData("entryId", "-4")]
    [InlineData("regionId", "99999")]
    [InlineData("regionId", "0")]
    [InlineData("tag", "\"other-1\"")]
    [InlineData("asset", "\"../x.jpg\"")]
    [InlineData("asset", "\"x.png\"")]
    [InlineData("asset", "\"X.jpg\"")]
    [InlineData("sha256", "\"ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef0123456789\"")]
    [InlineData("sha256", "\"abc\"")]
    [InlineData("bytes", "0")]
    [InlineData("bytes", "2097153")]
    public void ARowThatFailsACheckIsLeftOutWithAReason(string field, string value)
    {
        var json = RowJson(field, value);

        var parsed = CodexPictureIndex.Parse($"[{json}, {RowJson(null, null, 2420102)}]");

        Assert.Single(parsed.Rows);
        Assert.Equal(2420102, parsed.Rows[0].EntryId);
        Assert.Single(parsed.Reasons);
        Assert.StartsWith("Row 1:", parsed.Reasons[0], StringComparison.Ordinal);
    }

    [Fact]
    public void TheLargestSizeIsKept()
    {
        var parsed = CodexPictureIndex.Parse($"[{RowJson("bytes", CodexPictureIndex.MaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture))}]");

        Assert.Single(parsed.Rows);
        Assert.Empty(parsed.Reasons);
    }

    [Fact]
    public void AKeyListedTwiceKeepsTheFirstAndReportsTheSecond()
    {
        var parsed = CodexPictureIndex.Parse($"[{RowJson(null, null)}, {RowJson(null, null)}]");

        Assert.Single(parsed.Rows);
        Assert.Single(parsed.Reasons);
        Assert.StartsWith("Row 2:", parsed.Reasons[0], StringComparison.Ordinal);
    }

    private static string RowJson(string? field, string? value, long entryId = Entry)
    {
        var fields = new Dictionary<string, string>
        {
            ["entryId"] = entryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["regionId"] = "18",
            ["tag"] = "\"codex-pictures-1\"",
            ["asset"] = "\"2420101-18-0123456789ab.jpg\"",
            ["sha256"] = $"\"{Sha}\"",
            ["bytes"] = "1000",
        };

        if (field is not null)
        {
            fields[field] = value!;
        }

        return "{" + string.Join(",", fields.Select(pair => $"\"{pair.Key}\":{pair.Value}")) + "}";
    }
}
