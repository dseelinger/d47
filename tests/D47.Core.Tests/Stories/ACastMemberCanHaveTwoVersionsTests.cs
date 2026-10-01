using System.IO.Compression;
using System.Text.Json;
using D47.Core.Stories;
using Xunit;
using static D47.Core.Tests.Stories.StoryFixtures;

namespace D47.Core.Tests.Stories;

/// <summary>
/// A cast member may carry a forMan and a forWoman version in place of its own name and voice. The catalog check
/// refuses a half-made member, a token for a member without versions, and a card that names such a member.
/// </summary>
public sealed class ACastMemberCanHaveTwoVersionsTests
{
    private static StorySpeaker Cray => Versioned.Cast.Single(speaker => speaker.Id == "cray");

    private static StorySecret WithCray(StorySpeaker cray) =>
        Versioned with { Cast = [.. Versioned.Cast.Where(speaker => speaker.Id != "cray"), cray] };

    private static (StoryCard Card, StorySecret Secret) Broken(string rule) => rule switch
    {
        "one-version" => (Card, WithCray(Cray with { Versions = Cray.Versions! with { ForWoman = null } })),
        "version-without-name" => (Card, WithCray(Cray with { Versions = Cray.Versions! with { ForMan = Cray.Versions.ForMan! with { Name = "" } } })),
        "version-without-voice" => (Card, WithCray(Cray with { Versions = Cray.Versions! with { ForWoman = Cray.Versions.ForWoman! with { Voice = " " } } })),
        "versions-and-a-name" => (Card, WithCray(Cray with { Name = "Ellie" })),
        "versions-and-a-voice" => (Card, WithCray(Cray with { Voice = "af_heart" })),
        "token-for-a-member-without-versions" => (Card, Versioned with { End = "{name:dock-hand} decides." }),
        "token-in-the-blurb" => (Card with { Blurb = "{name:cray} waits." }, Versioned),
        "name-in-in-your-words" => (Card with { InYourWords = "I owe Ellis a drink." }, Versioned),
        "name-in-the-beacon" => (Card with { Beacon = "Ellie said to scan it." }, Versioned),
        _ => throw new ArgumentOutOfRangeException(nameof(rule), rule, null),
    };

    [Fact]
    public void AStoryWithAMemberInTwoVersionsKeepsTheFormat() =>
        Assert.Empty(new StoryCatalog([Card], () => [Versioned]).Faults());

    [Fact]
    public void TheVersionsSurviveSealing()
    {
        using var packed = new MemoryStream();

        using (var deflate = new DeflateStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            JsonSerializer.Serialize(deflate, new[] { Versioned }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        }

        var cray = StoryCatalog.Unseal(Convert.ToBase64String(packed.ToArray())).Single().Cast.Single(speaker => speaker.Id == "cray");

        Assert.Null(cray.Name);
        Assert.Null(cray.Voice);
        Assert.Equal("Ellie", cray.Versions?.ForMan?.Name);
        Assert.Equal("Ellis", cray.Versions?.ForWoman?.Name);
        Assert.Equal(StorySpeaker.Chatterbox, cray.Versions?.ForWoman?.Provider);
    }

    [Theory]
    [InlineData("one-version", "cast[1] has one version")]
    [InlineData("version-without-name", "cast[1].for-man needs a name and a voice")]
    [InlineData("version-without-voice", "cast[1].for-woman needs a name and a voice")]
    [InlineData("versions-and-a-name", "cast[1] has versions and also a name or a voice")]
    [InlineData("versions-and-a-voice", "cast[1] has versions and also a name or a voice")]
    [InlineData("token-for-a-member-without-versions", "end names dock-hand with a token, and dock-hand has no versions")]
    [InlineData("token-in-the-blurb", "blurb names cray")]
    [InlineData("name-in-in-your-words", "inYourWords names cray")]
    [InlineData("name-in-the-beacon", "beacon names cray")]
    public void AHalfMadeMemberIsRefused(string rule, string named)
    {
        var (card, secret) = Broken(rule);

        var faults = new StoryCatalog([card], () => [secret]).Faults();

        Assert.Contains(faults, fault => fault.StartsWith($"{Id}: ", StringComparison.Ordinal) && fault.Contains(named, StringComparison.Ordinal));
        Assert.All(faults, fault => Assert.DoesNotContain("Ellie", fault, StringComparison.Ordinal));
        Assert.All(faults, fault => Assert.DoesNotContain("Ellis", fault, StringComparison.Ordinal));
    }
}
