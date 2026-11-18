using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

public sealed class AHostileSurfaceTemperatureIsDrawnRedTests
{
    private static BodyProfile Planet(double kelvin, bool landable) =>
        TheSystemPageListsItsStationsTests.Ltt7786Profile().Bodies.First(body => body.Type != "Star")
        with
        { SurfaceTemperature = kelvin, IsLandable = landable };

    private static StarSystemPage.BodyFact Temperature(BodyProfile body) =>
        StarSystemPage.BodyFacts(body).Single(fact => fact.Text.EndsWith(" K", StringComparison.Ordinal));

    [Fact]
    public void ALandableBodyAtOrAbove700KShowsItsTemperatureInRed()
    {
        Assert.Equal(ThemeManager.RedKey, Temperature(Planet(700, landable: true)).Key);
    }

    [Theory]
    [InlineData(699, true)]
    [InlineData(1315, false)]
    public void ACoolerOrUnlandableBodyKeepsTheLinesInk(double kelvin, bool landable)
    {
        Assert.Null(Temperature(Planet(kelvin, landable)).Key);
    }
}
