using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Interface;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.Core.Watches;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Commander › Watches draws and edits next-system watches (#2349).</summary>
[Trait("Category", "Integration")]
public class TheWatchesPageDrawsNextSystemWatchesTests
{
    private static readonly DateTimeOffset Fired = new(2026, 11, 11, 21, 14, 0, TimeSpan.Zero);

    private sealed record Rig(PanelView Panel, Window Window, WatchStore Store, Watch Spoken);

    private static Rig Open(bool headset = false, bool galaxy = true)
    {
        var (_, _, paths, _, _) = TestSurface.CreateFull(coverage: () => "x");
        var store = new WatchStore(Path.Combine(paths.Data, "watches.json"), new MemoryFileSystem(), NullLogger<WatchStore>.Instance);

        store.Add(new Watch(Guid.NewGuid(), "Trader on the way", WatchKind.NextSystem, Service: "Material Trader", ServiceKind: "Manufactured", CheckRoute: true));
        var spoken = new Watch(Guid.NewGuid(), "Interstellar Factors", WatchKind.NextSystem, Service: "Interstellar Factors");
        store.Add(spoken);
        store.MarkArrived(spoken.Id, "Kremainn", Fired);

        var panel = new PanelView { DataContext = new PanelViewModel() };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableWatches(new WatchesDesk(
            store,
            new WatchSetup(store, new StubGalaxyService(), new StubTradePlanService()),
            null,
            () => galaxy ? new StubGalaxyService() : null));

        var window = new Window { Content = panel, Width = 1280, Height = 1300 };
        window.Show();
        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        return new Rig(panel, window, store, spoken);
    }

    private static string Texts(Control root) =>
        string.Join("\n", root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

    private static void Click(Control control)
    {
        control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(PanelView panel, string label) =>
        Click(panel.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, label)));

    private static string TextOf(Control control) =>
        string.Concat(control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

    private static void Pick(InlinePicker picker, string name)
    {
        Click(picker.GetVisualDescendants().OfType<Button>().Single(button => button.Name == InlinePicker.ButtonName));
        Click(picker.GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains(InlinePicker.OptionClass) && TextOf(button) == name));
    }

    private static void ChooseNextSection(PanelView panel)
    {
        var segment = panel.GetVisualDescendants().OfType<Segment>().Single(found => found.Name == WatchesPage.SectionsName);
        segment.GetVisualDescendants().OfType<RadioButton>().ElementAt(2).IsChecked = true;
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheNextSystemSectionShowsTheRowsAsDrawn()
    {
        var rig = Open();
        var page = rig.Panel.GetVisualDescendants().OfType<WatchesPage>().Single();

        Assert.DoesNotContain("Material Trader · Manufactured", Texts(page), StringComparison.Ordinal);

        ChooseNextSection(rig.Panel);
        var text = Texts(page);

        Assert.Contains(WatchesPage.Intro, text, StringComparison.Ordinal);
        Assert.Contains("0 WATCHES", text, StringComparison.Ordinal);
        Assert.Contains("2 WATCHES · 1 ON", text, StringComparison.Ordinal);
        Assert.True(text.Contains("Material Trader · Manufactured · any pad · on arrival or on a plotted route", StringComparison.Ordinal), text);
        Assert.Contains("Interstellar Factors · any pad · on arrival", text, StringComparison.Ordinal);
        Assert.Contains("Watching", text, StringComparison.Ordinal);
        Assert.Contains(WatchFired.Arrived("Kremainn", Fired, TimeZoneInfo.Local), text, StringComparison.Ordinal);
        Assert.Contains(WatchesPage.NextNote, text, StringComparison.Ordinal);
        Assert.Contains("2 OF 32", text, StringComparison.Ordinal);
        Assert.Contains(page.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "ADD NEXT-SYSTEM WATCH"));
    }

    [AvaloniaFact]
    public void TickingEnabledOnAFiredRowKeepsWhereAndWhenItSpoke()
    {
        var rig = Open();
        ChooseNextSection(rig.Panel);

        var box = rig.Panel.GetVisualDescendants().OfType<CheckBox>().Single(found => Avalonia.Automation.AutomationProperties.GetName(found) == "Interstellar Factors enabled");
        box.IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        var watch = rig.Store.Watches.Single(found => found.Id == rig.Spoken.Id);
        Assert.True(watch.Enabled);
        Assert.Equal("Kremainn", watch.FiredSystem);
        Assert.Equal(Fired, watch.FiredAt);

        rig.Panel.GetVisualDescendants().OfType<WatchesPage>().Single().Tick();
        Dispatcher.UIThread.RunJobs();
        var after = Texts(rig.Panel.GetVisualDescendants().OfType<WatchesPage>().Single());
        Assert.True(after.Contains("Kremainn · ", StringComparison.Ordinal), after);
    }

    [AvaloniaFact]
    public void TheFormSavesAMaterialTraderAndHidesKindForAShipyard()
    {
        var rig = Open();
        Press(rig.Panel, "ADD NEXT-SYSTEM WATCH");

        var form = rig.Panel.GetVisualDescendants().OfType<WatchFormPage>().Single();
        Assert.Empty(form.GetVisualDescendants().OfType<Notice>());
        Assert.Contains(WatchFormPage.NamePlaceholder, form.GetVisualDescendants().OfType<TextBox>().Select(box => box.PlaceholderText));

        var picker = form.GetVisualDescendants().OfType<InlinePicker>().Single();
        var kind = form.GetVisualDescendants().OfType<Segment>().Single(found => Avalonia.Automation.AutomationProperties.GetName(found) == "Kind");

        Pick(picker, "Material Trader");
        Assert.True(kind.IsVisible);
        Assert.Equal(["Any", "Raw", "Manufactured", "Encoded"], kind.ItemsSource);

        kind.SelectedIndex = 2;
        Press(rig.Panel, "SAVE");

        var saved = rig.Store.Watches.Single(watch => watch.Name == "Manufactured material trader");
        Assert.Equal(WatchKind.NextSystem, saved.Kind);
        Assert.Equal("Material Trader", saved.Service);
        Assert.Equal("Manufactured", saved.ServiceKind);
        Assert.Null(saved.MinPad);
        Assert.False(saved.CheckRoute);

        Press(rig.Panel, "ADD NEXT-SYSTEM WATCH");
        form = rig.Panel.GetVisualDescendants().OfType<WatchFormPage>().Single();
        picker = form.GetVisualDescendants().OfType<InlinePicker>().Single();
        Pick(picker, "Material Trader");
        Pick(picker, "Shipyard");

        kind = form.GetVisualDescendants().OfType<Segment>().Single(found => Avalonia.Automation.AutomationProperties.GetName(found) == "Kind");
        Assert.False(kind.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void WithGalaxySearchOffTheFormWarnsAndStillSaves()
    {
        var rig = Open(galaxy: false);
        Press(rig.Panel, "ADD NEXT-SYSTEM WATCH");

        var form = rig.Panel.GetVisualDescendants().OfType<WatchFormPage>().Single();
        var notice = Assert.Single(form.GetVisualDescendants().OfType<Notice>());
        Assert.Contains("Galaxy search is off", Texts(notice), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(WatchFormPage.GalaxyOffText, Texts(notice), StringComparison.Ordinal);

        Pick(form.GetVisualDescendants().OfType<InlinePicker>().Single(), "Refuel");
        Press(rig.Panel, "SAVE");

        Assert.Contains(rig.Store.Watches, watch => watch.Service == "Refuel");
    }

    [AvaloniaFact]
    public void TheHeadsetSectionSwitchIs44PixelsHighWithNoStatusLine()
    {
        var rig = Open(headset: true);
        var segment = rig.Panel.GetVisualDescendants().OfType<Segment>().Single(found => found.Name == WatchesPage.SectionsName);

        Assert.Equal(44, segment.Height);
        Assert.Empty(segment.Statuses);
    }
}
