using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using D47.App.Panel;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The words of mini's trail and how they shorten (#2340).</summary>
public class TheMiniPanelNamesWhereItIsTests
{
    private static double OnePerChar(string text) => text.Length;

    private static string Text(IEnumerable<MiniTrailPart> parts) => string.Concat(parts.Select(part => part.Text == MiniTrail.Separator ? ">" : part.Text));

    [Fact]
    public void ATabWithOneRootIsJustItsWord()
    {
        var words = MiniTrail.Words("Stories", [new NavCrumb("stories", "Stories")], 1);

        Assert.Equal(["STORIES"], words);
    }

    [Fact]
    public void ATabWithSeveralRootsNamesTheRootToo()
    {
        var words = MiniTrail.Words("Asset mgmt", [new NavCrumb("fleet", "Fleet")], 3);

        Assert.Equal(["ASSET MGMT", "FLEET"], words);
    }

    [Fact]
    public void ADrilledTrailAddsEveryLevelAndAChooserIsTheLast()
    {
        var trail = new[]
        {
            new NavCrumb("fleet", "Fleet"),
            new NavCrumb("ship", "Python"),
            new NavCrumb("loadout", "Loadout"),
            new NavCrumb("pick", "Pick a module", Modal: true),
        };

        var words = MiniTrail.Words("Asset mgmt", trail, 2);

        Assert.Equal(["ASSET MGMT", "FLEET", "PYTHON", "LOADOUT", "PICK A MODULE"], words);
    }

    [Fact]
    public void AnEmptyTrailIsTheTabAlone() =>
        Assert.Equal(["COMMANDER"], MiniTrail.Words("Commander", [], 2));

    [Fact]
    public void ATrailThatFitsIsDrawnWhole()
    {
        var parts = MiniTrail.Fit(["AB", "CD", "EF"], 1000, OnePerChar);

        Assert.Equal("AB>CD>EF", Text(parts));
        Assert.Equal(MiniTrailInk.Earlier, parts[0].Ink);
        Assert.Equal(MiniTrailInk.Separator, parts[1].Ink);
        Assert.Equal(MiniTrailInk.Current, parts[^1].Ink);
    }

    [Fact]
    public void TheSecondLevelGivesWayFirstAndTheRunIsOneEllipsis()
    {
        // Separators are 13 wide: whole is 47, with the second level elided 46, with two elided 31.
        var tab = MiniTrail.Fit(["AB", "CD", "EF", "GH"], 47, OnePerChar);
        var one = MiniTrail.Fit(["AB", "CD", "EF", "GH"], 46, OnePerChar);
        var two = MiniTrail.Fit(["AB", "CD", "EF", "GH"], 45, OnePerChar);

        Assert.Equal("AB>CD>EF>GH", Text(tab));
        Assert.Equal("AB>…>EF>GH", Text(one));
        Assert.Equal("AB>…>GH", Text(two));
        Assert.Equal(MiniTrailInk.Separator, one[2].Ink);
    }

    [Fact]
    public void TheCurrentLevelTrimsWithAnEllipsisWhenNothingElseCanGive()
    {
        var parts = MiniTrail.Fit(["AB", "CD", "EFGHIJKLMNOP"], 30, OnePerChar);

        var text = Text(parts);
        Assert.StartsWith("AB>…>", text);
        Assert.EndsWith("…", text);
        Assert.True(parts.Sum(part => part.Text == MiniTrail.Separator ? 13 : part.Text.Length) <= 30);
    }

    [Fact]
    public void ATabAloneStaysWhateverTheRoom() =>
        Assert.Equal("COMMANDER", Text(MiniTrail.Fit(["COMMANDER"], 3, OnePerChar)));

    [AvaloniaFact]
    public void MiniDrawsTheTrailAndTheFullPanelDoesNot()
    {
        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Mini };
        using var surface = new OffscreenSurface(view, new PixelSize(640, 280));
        surface.Render();

        var row = view.GetLogicalDescendants().OfType<StackPanel>().Single(panel => panel.Name == "MiniTrailRow");

        Assert.True(row.IsVisible);
        Assert.False(row.IsHitTestVisible);
        Assert.NotEmpty(row.Children);

        view.Mode = PanelMode.Full;
        surface.Render();

        Assert.False(row.IsVisible);
    }
}
