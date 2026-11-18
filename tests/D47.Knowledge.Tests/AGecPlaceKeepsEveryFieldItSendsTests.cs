using D47.Core.Knowledge;
using Xunit;

namespace D47.Knowledge.Tests;

public sealed class AGecPlaceKeepsEveryFieldItSendsTests
{
    private static NotablePlace Place(string name)
    {
        using var document = Fixture.Json("edastro-gec-all-full.json");
        return Assert.Single(GecNotablePlacesService.Read(document), place => place.Name == name);
    }

    [Fact]
    public void APlaceKeepsItsDescriptionImageAndVotes()
    {
        var place = Place("Dancing Rings of Atabey");

        Assert.StartsWith("Named after the Taino Goddess", place.DescriptionMarkdown);
        Assert.StartsWith("<p>Named after the Taino Goddess", place.DescriptionHtml);
        Assert.Equal("https://edastro.com/poiphotos/poi/000/000/029.jpg?1664477229", place.MainImage);
        Assert.Equal(24, place.Votes);
    }

    [Fact]
    public void APlaceKeepsItsIdentityRatingsAndLinks()
    {
        var place = Place("Dancing Rings of Atabey");

        Assert.Equal(29, place.Id);
        Assert.Equal(4, place.CategoryId);
        Assert.Equal("Inner Orion-Perseus Conflux", place.Region);
        Assert.Equal("X5K-WXL", place.Callsign);
        Assert.Equal("GEC", place.Source);
        Assert.True(place.Rare);
        Assert.Equal(5.5, place.Curation);
        Assert.Equal(3.29, place.AvgStars);
        Assert.Equal(32565.3, place.SolDistance);
        Assert.Equal("https://edastro.com/gec/view/29", place.PoiUrl);
        Assert.StartsWith("https://www.edsm.net/", place.GalMapUrl);
    }

    [Fact]
    public void APlaceWithoutACallsignOrSurfacePositionLeavesThemNull()
    {
        var place = Place("Tres Gigantes");

        Assert.Null(place.Callsign);
        Assert.Null(place.Latitude);
        Assert.False(place.Rare);
        Assert.Equal(-0.0275, Place("A Mercurial Circumnavigation").Latitude);
    }
}
