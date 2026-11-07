using D47.Core.Knowledge;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class AMaterialGuideSaysWhatTheToolsSayTests
{
    [Fact]
    public void AGrade4RawMaterialHasItsGradeCapacityAndFarming()
    {
        var detail = MaterialGuide.For("selenium", null)!;

        Assert.Equal(4, detail.Grade);
        Assert.Equal(150, detail.Capacity);
        Assert.Null(detail.Held);
        Assert.NotEmpty(detail.HowToFarm);
    }

    [Fact]
    public void AnUnknownMaterialHasNoDetail()
    {
        Assert.Null(MaterialGuide.For("not a material at all", null));
    }

    [Fact]
    public async Task WithTheSearchOffTheGalaxyIsNeverAsked()
    {
        var searchOn = false;
        IGalaxyService? galaxy = searchOn ? new Galaxy([]) : null;

        var places = await MaterialGuide.NearestAsync("selenium", null, galaxy, TestContext.Current.CancellationToken, near: "Sol");

        Assert.Same(MaterialPlaces.None, places);
    }

    [Fact]
    public async Task ARawMaterialsPlacesComeRichestFirst()
    {
        var galaxy = new Galaxy(
        [
            new BodySummary { Name = "Poor 1", SystemName = "Poor", Distance = 3, Materials = [("Selenium", 1.5)] },
            new BodySummary { Name = "Rich 2", SystemName = "Rich", Distance = 9, Materials = [("Selenium", 4.25)] },
        ]);

        var places = await MaterialGuide.NearestAsync("selenium", null, galaxy, TestContext.Current.CancellationToken, near: "Sol");

        Assert.Equal(["Rich 2", "Poor 1"], places.Places.Select(p => p.Place));
        Assert.Equal("Rich", places.FirstSystem);
        Assert.Equal("4.3%", places.Places[0].Note);
        Assert.Equal(9, places.Places[0].Distance);
    }

    private sealed class Galaxy(IReadOnlyList<BodySummary> bodies) : StubGalaxyService
    {
        public override Task<BodySearchResult> FindBodiesAsync(BodyQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new BodySearchResult(query.ReferenceSystem, bodies.Count, bodies));
    }
}
