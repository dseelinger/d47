using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Knowledge;
using Xunit;

namespace D47.App.Tests;

/// <summary>SEARCH › SYSTEM › BODIES draws every star and planet as one slab, in tree order (#826).</summary>
public sealed class TheSystemPageDrawsItsBodiesTests
{
    private static BodyProfile Body(string name) =>
        TheSystemPageListsItsStationsTests.Ltt7786Profile().Bodies.Single(body => body.Name == name);

    private static List<Border> Slabs(Control root) =>
        [.. root.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Child is StackPanel && border.Padding == new Thickness(14, 9, 14, 10))];

    private static List<string> Lines(Border slab) =>
        [.. slab.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void AllFiftySevenBodiesDrawInTreeOrderAndNoBarycentre()
    {
        var profile = TheSystemPageListsItsStationsTests.Ltt7786Profile();
        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();
        TheSystemPageListsItsStationsTests.Section(panel, "Bodies");

        var slabs = Slabs(panel);

        Assert.Equal(57, profile.Bodies.Count);
        Assert.Equal(57, slabs.Count);
        Assert.Equal(
            StarSystemPage.BodyTree(profile.Bodies).Select(level => level.Body.Name),
            slabs.Select(slab => AutomationProperties.GetName(slab)));
        Assert.DoesNotContain(slabs, slab => AutomationProperties.GetName(slab)!.Contains("Barycentre", StringComparison.OrdinalIgnoreCase));

        var moonOfAMoon = slabs.Single(slab => AutomationProperties.GetName(slab) == "LTT 7786 11 b a");
        Assert.Equal(84, moonOfAMoon.Margin.Left);
        Assert.Equal(0, slabs[0].Margin.Left);

        window.Close();
    }

    [AvaloniaFact]
    public void AMoonFollowsItsPlanetAndSiblingsKeepBodyIdOrder()
    {
        var names = StarSystemPage.BodyTree(TheSystemPageListsItsStationsTests.Ltt7786Profile().Bodies)
            .Select(level => level.Body.Name)
            .ToList();

        Assert.Equal("LTT 7786", names[0]);
        Assert.Equal(["LTT 7786 4", "LTT 7786 4 a"], names.Skip(names.IndexOf("LTT 7786 4")).Take(2));
    }

    [AvaloniaFact]
    public void ARingedRockyIceWorldReadsAsDesigned()
    {
        var world = Body("LTT 7786 3");

        Assert.Equal(
            ["5.71 Earth masses", "12,531 km", "1.48 g", "215 K", "1,408.07 atm", "Water Geysers", "Thick Argon-rich", "Not terraformable", "1 Human signal"],
            StarSystemPage.BodyFacts(world));
        Assert.Equal("Rocky Ice world", world.SubType);
        Assert.Equal("288 Ls", StarSystemPage.BodyDistanceText(world.DistanceToArrival));

        var ring = Assert.Single(world.Rings);
        Assert.Equal("A Ring", StarSystemPage.RingName(world, ring));
        Assert.Equal(["Rocky", "Reserves common", "1 Musgravite hotspot"], StarSystemPage.RingFacts(world, ring));
    }

    [AvaloniaFact]
    public void TheMainStarIsClassK7VAndScoopable()
    {
        var star = Body("LTT 7786");

        Assert.Equal(["0.78 solar masses", "0.84 solar radius", "Class K7V", "Scoopable"], StarSystemPage.BodyFacts(star));
    }

    [AvaloniaFact]
    public void ABodyWithNoAtmosphereOrVolcanismSaysSo()
    {
        var facts = StarSystemPage.BodyFacts(Body("LTT 7786 4 a"));

        Assert.Contains("No atmosphere", facts);
        Assert.DoesNotContain(facts, fact => fact.EndsWith(" atm", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void BodiesIsCaptured()
    {
        using var look = AppLook.Put();

        var (panel, window, _) = TheSystemPageListsItsStationsTests.Shown();
        TheSystemPageListsItsStationsTests.Section(panel, "Bodies");

        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, "system-bodies.png"), new PngBitmapEncoderOptions());

        window.Width = 1024;
        window.Height = 640;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        using var small = window.CaptureRenderedFrame()!;
        small.Save(Path.Combine(TestSurface.CaptureDirectory, "system-bodies-1024.png"), new PngBitmapEncoderOptions());

        window.Close();
    }
}
