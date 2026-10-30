using Avalonia.Headless.XUnit;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>The checker behind the path gate rejects what it should, against the real furnished panel.</summary>
[Trait("Category", "Gate")]
public sealed class APathTheHelpCitesMustNameARealPlaceTests
{
    private static string? Failure(string prose)
    {
        using var host = PanelPathFixture.Open();
        var checker = new PanelPathChecker(host.Panel.Nav.Destinations, host.Panel.TabWords);

        return PanelPathReader.Read(prose).Select(checker.Failure).FirstOrDefault(why => why is not null);
    }

    [AvaloniaFact]
    public void APlantedUnresolvablePathFails()
    {
        Assert.NotNull(Failure("Open Settings › Nowhere at all › Nothing."));
        Assert.NotNull(Failure("Open Asset Mgmt › Nothing."));
    }

    [AvaloniaFact]
    public void APlantedWrongGroupNameFails()
    {
        var place = SettingsLayout.Areas.SelectMany(area => area.Places.Select(p => (area, p))).First(pair => pair.p.Groups.Count > 0);

        Assert.Null(Failure($"Settings › {place.area.Title} › {place.p.Title}, group {place.p.Groups[0].Title}."));
        Assert.NotNull(Failure($"Settings › {place.area.Title} › {place.p.Title}, group Not a group."));
    }

    [AvaloniaFact]
    public void APlantedThirdPartThatIsNotASubPageFails()
    {
        Assert.Null(Failure("See Commander › Ledgers › Codex."));
        Assert.NotNull(Failure("See Commander › Ledgers › Not a page."));
    }
}
