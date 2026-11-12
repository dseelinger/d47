using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The words of mini's trail and how they shorten (#2340), and the open section as its last level (#2341).</summary>
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

        var row = Row(view);

        Assert.True(row.IsVisible);
        Assert.False(row.IsHitTestVisible);
        Assert.NotEqual(0, InkedPixels(view, row, surface.Render()));

        view.Mode = PanelMode.Full;
        surface.Render();

        Assert.False(row.IsVisible);
    }

    [AvaloniaFact]
    public void TheHeadsetMiniPanelDrawsTheTrailAfterASwitchFromFull()
    {
        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(ThemeCatalog.Elite);

        var view = new PanelView { DataContext = new PanelViewModel() };
        using var surface = new OffscreenSurface(view, new PixelSize(640, 280));
        surface.Render();

        view.Classes.Set("output-only", true);
        view.Mode = PanelMode.Mini;

        var row = Row(view);

        for (var redraw = 0; redraw < 3; redraw++)
        {
            var frame = surface.Render(() => { });

            Assert.True(row.Bounds.Width > 0);
            Assert.NotEqual(0, InkedPixels(view, row, frame));
        }

        surface.Render().SaveCapture("mini-trail-headset.png");
    }

    private static Control Row(PanelView view) =>
        view.GetLogicalDescendants().OfType<Control>().Single(control => control.Name == "MiniTrailRow");

    /// <summary>How many pixels inside the trail's bounds differ from the panel's background.</summary>
    private static int InkedPixels(PanelView view, Control row, Avalonia.Media.Imaging.RenderTargetBitmap frame)
    {
        var pixels = frame.Pixels();
        var stride = frame.PixelSize.Width * 4;
        var box = row.Bounds.TransformToAABB(row.GetVisualParent()!.TransformToVisual(view)!.Value);
        var background = ((ISolidColorBrush)Application.Current!.Resources[Theming.ThemeManager.BgKey]!).Color;
        var inked = 0;

        for (var y = (int)box.Top; y < (int)box.Bottom; y++)
        {
            for (var x = (int)box.Left; x < (int)box.Right; x++)
            {
                var at = (y * stride) + (x * 4);

                if (Math.Abs(pixels[at] - background.B) + Math.Abs(pixels[at + 1] - background.G) + Math.Abs(pixels[at + 2] - background.R) > 48)
                {
                    inked++;
                }
            }
        }

        return inked;
    }

    [Fact]
    public void AnOpenSectionIsTheLastLevelAndAnEmptyOneAddsNothing()
    {
        var trail = new[] { new NavCrumb("faction", "Faction") };

        Assert.Equal(["SEARCH", "FACTION", "CONFLICTS"], MiniTrail.Words("Search", trail, 3, "Conflicts"));
        Assert.Equal(["SEARCH", "FACTION"], MiniTrail.Words("Search", trail, 3, ""));
    }

    [Fact]
    public void TheOpenSectionStaysWhenTheLevelsBeforeItGiveWay()
    {
        var parts = MiniTrail.Fit(MiniTrail.Words("Tab", [new NavCrumb("a", "Root"), new NavCrumb("b", "Deep")], 2, "Sec"), 40, OnePerChar);

        Assert.EndsWith("SEC", Text(parts));
        Assert.Equal(MiniTrailInk.Current, parts[^1].Ink);
    }

    [AvaloniaFact]
    public void MiniNamesTheOpenSectionAndFollowsASectionChange()
    {
        new Theming.ThemeManager(Application.Current!, NullLogger<Theming.ThemeManager>.Instance)
            .Apply(ThemeCatalog.Elite);

        var state = JournalScript.Commander("F2341", "Kade Orrin");
        var checklists = TestSurface.Checklists(TestSurface.MemoryFolder("d47-mini-trail-section-tests"));
        var view = new PanelView { DataContext = new PanelViewModel() };
        view.EnableChecklist(checklists);
        view.EnableMissions(() => state, () => DateTimeOffset.UtcNow);
        view.EnableStanding(() => state);
        view.EnableStatistics(() => state);
        view.EnableCampaign(new CampaignSources(
            () => state,
            new CampaignStore("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance),
            () => null,
            () => "Kestrel Reach Compact",
            () => null,
            CapabilityRegistry.Build([])));
        view.EnableCommanders(new CommanderRoster(() => null, () => 0, () => null, () => false, _ => { }));

        var window = new Window
        {
            Content = view,
            Width = 640,
            Height = 280,
            Background = (IBrush)Application.Current!.Resources[Theming.ThemeManager.BgKey]!,
        };
        window.Show();
        view.Tab = PanelTab.Commander;
        view.Nav.SelectRoot(PanelTab.Commander, CampaignPage.RootKey);
        view.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        var row = Row(view);

        string Trail() => Avalonia.Automation.AutomationProperties.GetName(row) ?? string.Empty;

        Assert.EndsWith($"{MiniTrail.Separator} FRONTS", Trail());

        Assert.EndsWith($"{MiniTrail.Separator} FRONTS", Trail());

        var segment = view.GetVisualDescendants().OfType<Segment>().First(found => found.Name == CampaignPage.SectionsName);
        segment.PressOption(CampaignModel.SystemsSection);
        Dispatcher.UIThread.RunJobs();

        Assert.EndsWith($"{MiniTrail.Separator} SYSTEMS", Trail());

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("mini-trail-section.png");

        view.Mode = PanelMode.Full;
        Dispatcher.UIThread.RunJobs();

        Assert.False(row.IsVisible);
        window.Close();
    }
}
