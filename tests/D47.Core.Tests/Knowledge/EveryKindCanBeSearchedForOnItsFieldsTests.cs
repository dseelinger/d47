using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

/// <summary>Each star and planet kind's fields are values the body search knows, so its Search tile is never refused (#2587).</summary>
public sealed class EveryKindCanBeSearchedForOnItsFieldsTests
{
    private static IEnumerable<CodexKind> Kinds() =>
        CodexCatalogue.Table.All.Select(entry => entry.Kind).OfType<CodexKind>();

    [Fact]
    public void EverySubtypeInTheKindTableIsOneTheSearchKnows()
    {
        Assert.Empty(Kinds().Select(kind => kind.SubType).Distinct().Except(BodyCatalogue.Subtypes, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryAtmosphereInTheKindTableIsOneTheSearchKnows()
    {
        Assert.Empty(Kinds().Select(kind => kind.Atmosphere).OfType<string>().Distinct().Except(BodyCatalogue.Atmospheres, StringComparer.Ordinal));
    }

    [Fact]
    public void EveryBandAndRingTypeInTheKindTableIsOneTheSearchKnows()
    {
        Assert.All(Kinds().Select(kind => kind.Band).OfType<string>().Distinct(), band => Assert.NotEmpty(BodyCatalogue.LuminosityClasses(band)));
        Assert.All(
            Kinds().Select(kind => kind.Rings).OfType<string>().Where(rings => rings != BodyCatalogue.NoRings).SelectMany(rings => rings.Split(',')).Distinct(),
            type => Assert.Contains(type, BodyCatalogue.RingTypes));
    }

    [Fact]
    public void EveryKindsSearchParsesWithTheKindsFieldsAndNoDistanceLimit()
    {
        foreach (var entry in CodexCatalogue.Table.All.Where(entry => entry is { Kind: not null, Regions.Count: > 0 }))
        {
            var arguments = CodexSearch.Arguments(entry, entry.Regions.Keys.First())!;
            var request = new BodyRequest
            {
                ReferenceSystem = "Sol",
                Subtype = arguments.GetValueOrDefault("body_type"),
                Luminosity = arguments.GetValueOrDefault("luminosity"),
                Terraformable = arguments.TryGetValue("terraformable", out var terraformable) ? bool.Parse(terraformable) : null,
                Atmosphere = arguments.GetValueOrDefault("atmosphere"),
                RingType = arguments.GetValueOrDefault("ring_type"),
                RingsExactly = arguments.ContainsKey("rings_exactly") ? true : null,
                Region = arguments.GetValueOrDefault("region"),
            };

            Assert.True(BodyQuery.TryParse(request, out var query, out var failure), $"{entry.Key}: {failure}");
            Assert.Equal(entry.Kind!.SubType, query.Subtype);
            Assert.Equal(entry.Kind.Atmosphere, query.Atmosphere);
            Assert.True(query.NoDistanceLimit, entry.Key);
        }
    }

    [Fact]
    public void AKindGroupHasNoSearch()
    {
        var group = CodexCatalogue.Table.Entry("gas.class_i_gas_giant")!;

        Assert.Empty(group.Regions);
        Assert.Null(CodexSearch.Arguments(group, 18));
    }

    [Theory]
    [InlineData(1200402)]
    [InlineData(1200902)]
    public void AGreenGasGiantTheServiceDoesNotRecordHasNoSearch(long entryId)
    {
        Assert.Null(CodexSearch.Arguments(CodexCatalogue.Table.Entry(entryId)!, 18));
    }
}
