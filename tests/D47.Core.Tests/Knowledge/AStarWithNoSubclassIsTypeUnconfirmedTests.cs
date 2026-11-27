using D47.Core.Capabilities.Builtin;
using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary><see cref="BodySummary.TypeUnconfirmed"/> marks a star whose spectral class has no digit.</summary>
public sealed class AStarWithNoSubclassIsTypeUnconfirmedTests
{
    private static BodySummary Star(string? spectralClass, string type = "Star") => new()
    {
        Name = "Body",
        SystemName = "System",
        Type = type,
        Subtype = "A (Blue-White super giant) Star",
        SpectralClass = spectralClass,
        Luminosity = "Ia0",
    };

    [Theory]
    [InlineData("A")]
    [InlineData("L")]
    [InlineData("T")]
    [InlineData("M")]
    public void AClassWithNoDigitIsUnconfirmed(string spectralClass) => Assert.True(Star(spectralClass).TypeUnconfirmed);

    [Theory]
    [InlineData("A0")]
    [InlineData("N0")]
    [InlineData("DA2")]
    [InlineData("TTS7")]
    [InlineData("AeBe0")]
    public void AClassWithADigitIsNot(string spectralClass) => Assert.False(Star(spectralClass).TypeUnconfirmed);

    [Fact]
    public void NoClassIsNotUnconfirmed() => Assert.False(Star(null).TypeUnconfirmed);

    [Fact]
    public void ABodyThatIsNotAStarIsNotUnconfirmed() => Assert.False(Star("A", "Planet").TypeUnconfirmed);

    [Fact]
    public void TheSpokenAnswerSaysSoAfterTheType()
    {
        Assert.True(BodyQuery.TryParse(new BodyRequest { ReferenceSystem = "Sol", Landable = true }, out var query, out var failure), failure);

        var marked = GalaxyCapability.Describe(new BodySearchResult("Sol", 1, [Star("A")]), query);
        var plain = GalaxyCapability.Describe(new BodySearchResult("Sol", 1, [Star("A0")]), query);

        Assert.Contains("A (Blue-White super giant) Star, type unconfirmed", marked);
        Assert.DoesNotContain("unconfirmed", plain);
    }
}
