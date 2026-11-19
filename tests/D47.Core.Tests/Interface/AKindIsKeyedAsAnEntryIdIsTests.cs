using System.Text.Json;
using D47.Core.Interface;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Interface;

/// <summary>A d47 kind's key names its pictures and index rows as a Canonn EntryID's decimal text does.</summary>
public class AKindIsKeyedAsAnEntryIdIsTests
{
    private const string Kind = "star.k_yellow_orange_giant_star.giant";
    private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static CodexPictureRow Row(string key, int regionId) =>
        new(key, regionId, CodexPictureIndex.FirstTag, CodexPictureIndex.AssetName(new CodexPictureKey(key, regionId), Sha), Sha, 1000);

    [Fact]
    public void AKindKeysFileNameReadsBackToTheSameKey()
    {
        var key = new CodexPictureKey(Kind, 18);

        Assert.Equal(Kind + "-18.jpg", key.FileName);
        Assert.True(CodexPictureKey.TryParse(key.FileName, out var parsed));
        Assert.Equal(key, parsed);
        Assert.Null(parsed.EntryId);
    }

    [Fact]
    public void ANumericNameStillReadsAsItsEntryId()
    {
        Assert.True(CodexPictureKey.TryParse("2310505-18.jpg", out var parsed));
        Assert.Equal(new CodexPictureKey("2310505", 18), parsed);
        Assert.Equal(2310505L, parsed.EntryId);
    }

    [Theory]
    [InlineData("a..b-18.jpg")]
    [InlineData("../x-18.jpg")]
    [InlineData("X-18.jpg")]
    [InlineData("x/y-18.jpg")]
    [InlineData("_x-18.jpg")]
    [InlineData("x-18.source.jpg")]
    [InlineData("x-y-18-.jpg")]
    public void ANameThatIsNotAKeyDoesNotParse(string name)
    {
        Assert.False(CodexPictureKey.TryParse(name, out _));
    }

    [Fact]
    public void AnIndexWithAKindRowAndAnEntryIdRowReadsBoth()
    {
        var json = "[" + RowJson("\"kind\":\"" + Kind + "\"", Kind) + "," + RowJson("\"entryId\":2310505", "2310505") + "]";

        var parsed = CodexPictureIndex.Parse(json);

        Assert.Empty(parsed.Reasons);
        Assert.Equal([Kind, "2310505"], parsed.Rows.Select(row => row.EntryKey));
    }

    [Theory]
    [InlineData("\"2310505\"")]
    [InlineData("\"Star.x\"")]
    [InlineData("\"a..b\"")]
    [InlineData("12")]
    public void AKindThatIsNotAKindKeyIsLeftOut(string kind)
    {
        var parsed = CodexPictureIndex.Parse("[" + RowJson("\"kind\":" + kind, "x") + "]");

        Assert.Empty(parsed.Rows);
        Assert.Single(parsed.Reasons);
    }

    [Fact]
    public void WriteGivesAnEntryIdForANumericKeyAndAKindOtherwise()
    {
        var text = CodexPictureIndex.Write([Row(Kind, 18), Row("2310505", 18)]);

        using var document = JsonDocument.Parse(text);
        var rows = document.RootElement.EnumerateArray().ToList();

        Assert.Equal(2310505L, rows[0].GetProperty("entryId").GetInt64());
        Assert.False(rows[0].TryGetProperty("kind", out _));
        Assert.Equal(Kind, rows[1].GetProperty("kind").GetString());
        Assert.False(rows[1].TryGetProperty("entryId", out _));
        Assert.Equal([Row("2310505", 18), Row(Kind, 18)], CodexPictureIndex.Parse(text).Rows);
    }

    [Fact]
    public void AnIndexOfEntryIdsIsWrittenAsBefore()
    {
        var text = CodexPictureIndex.Write([Row("10", 3), Row("9", 3)]);

        Assert.Equal(
            """
            [
              {
                "entryId": 9,
                "regionId": 3,
                "tag": "codex-pictures-1",
                "asset": "9-3-0123456789ab.jpg",
                "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "bytes": 1000
              },
              {
                "entryId": 10,
                "regionId": 3,
                "tag": "codex-pictures-1",
                "asset": "10-3-0123456789ab.jpg",
                "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                "bytes": 1000
              }
            ]
            """.ReplaceLineEndings("\n"),
            text.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void AnAssetNamedForAKindKeyPassesTheCheck()
    {
        var asset = CodexPictureIndex.AssetName(new CodexPictureKey(Kind, 18), Sha);

        Assert.Equal(Kind + "-18-0123456789ab.jpg", asset);
        Assert.True(CodexPictureIndex.IsSafeAsset(asset));
    }

    [Theory]
    [InlineData("star/x-18-0123456789ab.jpg")]
    [InlineData("star..x-18-0123456789ab.jpg")]
    [InlineData("..-18-0123456789ab.jpg")]
    public void AnAssetWithASlashOrTwoDotsFailsTheCheck(string asset)
    {
        Assert.False(CodexPictureIndex.IsSafeAsset(asset));
    }

    [Fact]
    public void AnAssetNameIsAllowed128CharactersBeforeItsExtension()
    {
        Assert.True(CodexPictureIndex.IsSafeAsset(new string('a', 128) + ".jpg"));
        Assert.False(CodexPictureIndex.IsSafeAsset(new string('a', 129) + ".jpg"));
    }

    [Fact]
    public void ACanonnEntryIsFoundInTheTableByItsKeyOrItsEntryId()
    {
        var table = CodexTable.Parse("2310505\tTesta Alpha - Red\tBiological and Geological\tOrganic structures\t18:2\n");

        Assert.Same(table.Entry(2310505), table.Entry("2310505"));
        Assert.Equal("2310505", table.Entry(2310505)!.Key);
        Assert.Equal(2310505L, table.Entry("2310505")!.EntryId);
    }

    private static string RowJson(string keyField, string key) =>
        "{" + keyField + ",\"regionId\":18,\"tag\":\"codex-pictures-1\",\"asset\":\"" + key.Replace("\"", string.Empty, StringComparison.Ordinal)
        + "-18-0123456789ab.jpg\",\"sha256\":\"" + Sha + "\",\"bytes\":1000}";
}
