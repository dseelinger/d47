using System.IO.Compression;
using System.Text.Json;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>A cast member's role is read from the hidden entry, reaches the member the Commander meets, and is not a hidden text.</summary>
public sealed class ACastMemberHasAPublicRoleTests
{
    private const string Role = "the dock hand who knows the old address";

    private static readonly StorySecret WithRole = Secret with
    {
        Cast = [.. Secret.Cast.Select(member => member with { Role = Role })],
    };

    private static StorySecret Sealed(StorySecret secret)
    {
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            JsonSerializer.Serialize(deflate, new[] { secret }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        return StoryCatalog.Unseal(Convert.ToBase64String(packed.ToArray())).Single();
    }

    [Fact]
    public void TheRoleSurvivesSealing() =>
        Assert.Equal(Role, Sealed(WithRole).Cast.Single().Role);

    [Fact]
    public void TheRoleIsNotAHiddenText() =>
        Assert.DoesNotContain(WithRole.Texts(), text => text.Text.Contains(Role, StringComparison.Ordinal));

    [Fact]
    public void TheRoleReachesTheMemberTheCommanderMeets()
    {
        var member = WithRole.Cast.Single();

        Assert.Equal(Role, member.Role);
        Assert.Equal("Ren", member.Shown(WithRole.Id, null).Name);
    }
}
