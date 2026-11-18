using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every field draws each nested object and list as a row that opens to its fields (#2511).</summary>
public sealed class EveryFieldDrawsNestedGroupsAsRowsTests
{
    private const string Docked =
        """{"timestamp":"2026-09-29T21:01:23Z","event":"Docked","StationName":"Kukai City","StationType":"Outpost","StarSystem":"IC 2391 Sector GR-V b2-7","SystemAddress":16071364781465,"MarketID":4356386563,"StationFaction":{"Name":"Example Interstellar PLC","FactionState":"Expansion"},"StationServices":["dock","autodock","commodities"],"StationEconomies":[{"Name":"$economy_Industrial;","Name_Localised":"Industrial","Proportion":1.4}],"DistFromStarLS":522.79,"LandingPads":{"Small":2,"Medium":1,"Large":0}}""";

    private const string Loadout =
        """{"timestamp":"2026-11-15T22:46:21Z","event":"Loadout","Ship":"lakonminer","ShipID":39,"ShipName":"Test Ship","ShipIdent":"TS-01","FuelCapacity":{"Main":32.0,"Reserve":0.6},"Modules":[{"Slot":"TinyHardpoint1","Item":"hpt_mrascanner_size0_class4","On":true,"Priority":0,"Health":1.0,"Value":310844},{"Slot":"TinyHardpoint2","Item":"hpt_plasmapointdefence_turret_tiny","On":true,"Priority":0,"Health":1.0,"Value":15765,"Engineering":{"Engineer":"Ram Tah","EngineerID":300110,"BlueprintName":"Misc_PointDefenseCapacity","Level":1,"Quality":1.0,"Modifiers":[{"Label":"Mass","Value":1.0,"OriginalValue":0.5,"LessIsGood":1}]}}],"Cargo":[]}""";

    private static (JournalReadingPane Pane, Window Window) Shown(string json, double width = 900)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var log = new JournalLog();
        log.Add(
            [new JournalEvent(
                DateTimeOffset.Parse(root.GetProperty("timestamp").GetString()!, System.Globalization.CultureInfo.InvariantCulture),
                root.GetProperty("event").GetString()!,
                root.Clone())],
            []);

        var pane = new JournalReadingPane(_ => null, () => null);
        var window = new Window
        {
            Content = new ScrollViewer { Content = pane, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled },
            Width = width,
            Height = 1000,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };

        window.Show();
        pane.Show(Assert.Single(log.Read(noise: true)));
        Dispatcher.UIThread.RunJobs();
        Press(pane.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ReadingEveryField"));

        return (pane, window);
    }

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static StackPanel Fields(Control pane) =>
        pane.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "ReadingFields");

    private static string Said(TextBlock block) => block.Inlines?.Text ?? block.Text ?? string.Empty;

    private static IEnumerable<Button> Groups(Control pane) =>
        pane.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "ReadingGroup");

    private static Button Group(Control pane, string name) =>
        Groups(pane).Single(button => button.GetVisualDescendants().OfType<TextBlock>().Any(block => Said(block) == name));

    private static string Triangle(Button group) =>
        group.GetVisualDescendants().OfType<TextBlock>().Select(Said).Single(text => text is "▶" or "▼");

    private static List<SelectableTextBlock> Lines(Control pane) =>
        [.. Fields(pane).GetVisualDescendants().OfType<SelectableTextBlock>()];

    private static SelectableTextBlock Line(Control pane, string text) =>
        Lines(pane).Single(block => block.Text == text);

    [AvaloniaFact]
    public void DockedOpensWithTheSmallGroupsOpenAndTheEconomiesFolded()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked);

        Assert.Equal("▼", Triangle(Group(pane, "LandingPads")));
        Assert.Equal("▼", Triangle(Group(pane, "StationFaction")));
        Assert.Equal("▶", Triangle(Group(pane, "StationEconomies")));
        Assert.Equal("1 item", Said(Group(pane, "StationEconomies").GetVisualDescendants().OfType<TextBlock>().Last()));
        Assert.Equal("3 fields", Said(Group(pane, "LandingPads").GetVisualDescendants().OfType<TextBlock>().Last()));
        Assert.True(Line(pane, "Medium").IsEffectivelyVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void AFoldedGroupsLinesDoNotExistUntilItIsFirstOpened()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked);

        var before = Lines(pane).Count;
        Assert.DoesNotContain(Lines(pane), block => block.Text == "Proportion");

        var economies = Group(pane, "StationEconomies");
        Press(economies);

        var element = Group(pane, "0 · $economy_Industrial;");
        Assert.Equal("▼", Triangle(economies));
        Assert.Equal("▼", Triangle(element));
        Assert.Equal(before + 6, Lines(pane).Count);
        Assert.True(Line(pane, "Proportion").IsEffectivelyVisible);

        Press(economies);

        Assert.Equal("▶", Triangle(economies));
        Assert.False(Line(pane, "Proportion").IsEffectivelyVisible);
        Assert.False(element.IsEffectivelyVisible);
        Assert.Equal(before + 6, Lines(pane).Count);

        window.Close();
    }

    [AvaloniaFact]
    public void NoGroupIsDrawnAsJsonText()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Loadout);

        Press(Group(pane, "Modules"));
        Press(Group(pane, "1 · TinyHardpoint2"));
        Press(Group(pane, "Engineering"));
        Press(Group(pane, "Modifiers"));

        Assert.DoesNotContain(Lines(pane), block => block.Text!.StartsWith('{') || block.Text.StartsWith('['));
        Assert.Same(Application.Current!.Resources[ThemeManager.GreyKey], Line(pane, "empty list").Foreground);
        Assert.Same(Application.Current!.Resources[ThemeManager.WhiteKey], Line(pane, "Ram Tah").Foreground);

        window.Close();
    }

    [AvaloniaFact]
    public void EveryGroupRowIsAtLeastTheMinimumTarget()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Loadout);

        Press(Group(pane, "Modules"));
        Press(Group(pane, "1 · TinyHardpoint2"));
        Press(Group(pane, "Engineering"));
        Press(Group(pane, "Modifiers"));

        var shown = Groups(pane).Where(group => group.IsEffectivelyVisible).ToList();

        Assert.True(shown.Count >= 7);
        Assert.All(shown, group => Assert.True(group.Bounds.Height >= TypeScale.MinimumTarget, $"{group.Bounds.Height}"));

        window.Close();
    }

    [AvaloniaFact]
    public void ValuesLineUpInOneColumnAtEveryDepth()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Loadout);

        Press(Group(pane, "Modules"));
        Press(Group(pane, "1 · TinyHardpoint2"));
        Press(Group(pane, "Engineering"));
        Press(Group(pane, "Modifiers"));

        var fields = Fields(pane);
        double X(Control control) => control.TranslatePoint(default, fields)!.Value.X;

        Assert.Equal(278, X(Line(pane, "lakonminer")));
        Assert.Equal(278, X(Line(pane, "32.0")));
        Assert.Equal(278, X(Line(pane, "Ram Tah")));
        Assert.Equal(278, X(Line(pane, "Mass")));
        Assert.Equal(26, X(Line(pane, "Ship")));
        Assert.Equal(50, X(Line(pane, "Main")));
        Assert.Equal(98, X(Line(pane, "Engineer")));
        Assert.Equal(146, X(Line(pane, "Label")));

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("every-field-groups.png");

        window.Close();
    }

    [AvaloniaFact]
    public void NarrowPutsTheNameAboveItsValueAndKeepsAGroupsCountBesideItsName()
    {
        using var look = AppLook.Put();
        var (pane, window) = Shown(Docked, width: 420);

        var fields = Fields(pane);
        Point At(Control control) => control.TranslatePoint(default, fields)!.Value;

        Assert.True(At(Line(pane, "Outpost")).Y > At(Line(pane, "StationType")).Y);
        Assert.Equal(At(Line(pane, "Outpost")).X, At(Line(pane, "StationType")).X);

        var economies = Group(pane, "StationEconomies");
        var texts = economies.GetVisualDescendants().OfType<TextBlock>().ToList();
        var name = texts.Single(block => Said(block) == "StationEconomies");
        var count = texts.Single(block => Said(block) == "1 item");

        Assert.Equal(At(name).Y + name.Bounds.Height / 2, At(count).Y + count.Bounds.Height / 2, 1);

        Press(economies);
        Assert.Equal(At(Line(pane, "Proportion")).X, At(Line(pane, "Name_Localised")).X);
        Assert.True(At(Line(pane, "1.4")).Y > At(Line(pane, "Proportion")).Y);

        using var frame = window.CaptureRenderedFrame()!;
        frame.SaveCapture("every-field-groups-narrow.png");

        window.Close();
    }
}
