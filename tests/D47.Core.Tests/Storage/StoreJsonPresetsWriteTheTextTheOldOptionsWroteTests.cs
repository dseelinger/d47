using System.Text.Json;
using System.Text.Json.Serialization;
using D47.Core.Storage;
using Xunit;

namespace D47.Core.Tests.Storage;

public sealed class StoreJsonPresetsWriteTheTextTheOldOptionsWroteTests
{
    private enum Kind { FirstKind, SecondKind }

    private sealed record Item(string Name, int Count);

    private sealed record Sample(string? Note, Kind Kind, List<Item> Items);

    private static readonly Sample Value = new(null, Kind.SecondKind, [new Item("a", 1), new Item("b", 2)]);

    private static string Normalised(string text) => text.Replace("\r\n", "\n");

    [Fact]
    public void OmittingNullsLeavesTheNullOut()
    {
        const string old = """
            {
              "kind": "secondKind",
              "items": [
                {
                  "name": "a",
                  "count": 1
                },
                {
                  "name": "b",
                  "count": 2
                }
              ]
            }
            """;
        Assert.Equal(old, Normalised(JsonSerializer.Serialize(Value, StoreJson.OmittingNulls)));
    }

    [Fact]
    public void KeepingNullsWritesTheNull()
    {
        const string old = """
            {
              "note": null,
              "kind": "secondKind",
              "items": [
                {
                  "name": "a",
                  "count": 1
                },
                {
                  "name": "b",
                  "count": 2
                }
              ]
            }
            """;
        Assert.Equal(old, Normalised(JsonSerializer.Serialize(Value, StoreJson.KeepingNulls)));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BothPresetsReadCommentsAndTrailingCommas(bool omitting)
    {
        var options = omitting ? StoreJson.OmittingNulls : StoreJson.KeepingNulls;
        var read = JsonSerializer.Deserialize<Sample>(
            "{ // c\n \"kind\": \"firstKind\", \"items\": [ {\"name\":\"x\",\"count\":3}, ], }", options);
        Assert.Equal(Kind.FirstKind, read!.Kind);
        Assert.Equal(new Item("x", 3), Assert.Single(read.Items));
    }
}
