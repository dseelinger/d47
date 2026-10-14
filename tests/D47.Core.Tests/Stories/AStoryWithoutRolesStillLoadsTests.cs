using System.IO.Compression;
using System.Text;
using System.Text.Json;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A hidden entry published without roles loads with none, and a property this build does not know is skipped.</summary>
public sealed class AStoryWithoutRolesStillLoadsTests
{
    private static StorySecret Unsealed(string json)
    {
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(Encoding.UTF8.GetBytes(json));
        }

        return StoryCatalog.Unseal(Convert.ToBase64String(packed.ToArray())).Single();
    }

    private static string Json(string castExtra)
    {
        var tree = JsonSerializer.SerializeToNode(new[] { Secret }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })!.AsArray();
        var cast = tree[0]!["cast"]!.AsArray()[0]!.AsObject();

        foreach (var pair in JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{" + castExtra + "}")!)
        {
            cast[pair.Key] = JsonSerializer.SerializeToNode(pair.Value);
        }

        return tree.ToJsonString();
    }

    [Fact]
    public void AMemberWithNoRoleHasNone()
    {
        var member = Unsealed(Json(string.Empty)).Cast.Single();

        Assert.Null(member.Role);
        Assert.Equal("Ren", member.Name);
    }

    [Fact]
    public void APropertyTheReaderDoesNotKnowIsSkipped()
    {
        var member = Unsealed(Json("\"mannerism\": \"taps the table\", \"role\": \"the dock hand\"")).Cast.Single();

        Assert.Equal("the dock hand", member.Role);
        Assert.Equal("Ren", member.Name);
    }
}
