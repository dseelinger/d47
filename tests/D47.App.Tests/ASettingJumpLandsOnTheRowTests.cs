using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Checklists;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Ships;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A jump to a setting scrolls the row into view and marks it, on the settings page and on a tab's own strip;
/// a place can be opened and marked by its id (#1472).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ASettingJumpLandsOnTheRowTests
{
    private const string NoVoice = "No Chatterbox voice has been chosen. Pick one in Settings.";

    private static void Jobs()
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    private static (SettingsService Settings, ViewStateStore ViewState) Surface()
    {
        var (settings, viewState, _) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);
        settings.Apply(SpeechCapability.ProviderKey, TtsProviderCatalog.ChatterboxId, SettingsCaller.Panel);

        return (settings, viewState);
    }

    private static SettingsView Page(SettingsService settings, ViewStateStore viewState)
    {
        var page = new SettingsView();

        page.Attach(settings, viewState);

        return page;
    }

    private static (SettingsView Page, Window Window) ShownPage(double height = 420)
    {
        var (settings, viewState) = Surface();
        var page = new SettingsView();

        page.Attach(settings, viewState);

        var window = new Window { Content = page, Width = 1180, Height = height };
        window.Show();
        Jobs();

        return (page, window);
    }

    private static void AssertInView(SettingsView page, Control frame)
    {
        var scroller = (ScrollViewer)page.GetVisualDescendants().First(c => c.Name == "Scroller");
        var top = frame.TranslatePoint(default, scroller)!.Value.Y;

        Assert.True(top >= -1, $"the row starts {top} above the viewport's top");
        Assert.True(top + frame.Bounds.Height <= scroller.Bounds.Height + 1, $"the row ends at {top + frame.Bounds.Height} in a viewport {scroller.Bounds.Height} tall");
    }

    [AvaloniaFact]
    public void ARowBelowTheFoldIsScrolledIntoViewAndMarked()
    {
        using var look = AppLook.Put();
        var (page, window) = ShownPage();

        page.RevealRow(SpeechCapability.ChatterboxVoiceKey);
        Jobs();

        var frame = page.FrameFor(SpeechCapability.ChatterboxVoiceKey)!;

        Assert.Same(frame, page.LandedOn);
        AssertInView(page, frame);

        window.Close();
    }

    [AvaloniaFact]
    public void AFoldedAdvancedRowIsUnfoldedScrolledAndMarked()
    {
        using var look = AppLook.Put();
        var (page, window) = ShownPage();

        page.RevealRow(ListeningCapability.ModelKey);
        Jobs();

        Assert.True(page.LabelFor(ListeningCapability.ModelKey)?.IsEffectivelyVisible);
        Assert.Same(page.FrameFor(ListeningCapability.ModelKey), page.LandedOn);
        AssertInView(page, page.FrameFor(ListeningCapability.ModelKey)!);

        window.Close();
    }

    [AvaloniaFact]
    public void ARowInTheLevelsMixerIsMarkedOnItsTableRow()
    {
        using var look = AppLook.Put();
        var (page, window) = ShownPage();
        var key = AudioCapability.LevelKey(Enum.GetValues<AudioChannel>().Last());

        page.RevealRow(key);
        Jobs();

        var landed = Assert.IsType<Grid>(page.LandedOn);

        Assert.Contains(SettingsView.MixerRowClass, landed.Classes);
        AssertInView(page, landed);

        window.Close();
    }

    [AvaloniaFact]
    public void APlaceOpensByItsIdAndMarksItsTitle()
    {
        using var look = AppLook.Put();
        var (page, window) = ShownPage();

        page.RevealPlace("chatter");
        Jobs();

        Assert.Equal("chatter", page.SectionIds[page.ActiveSection]);
        Assert.Equal(SettingsView.PageHeadName, page.LandedOn?.Name);

        window.Close();
    }

    [AvaloniaFact]
    public void AKeyOrPlaceNoLayoutPlacesChangesNothing()
    {
        using var look = AppLook.Put();
        var (page, window) = ShownPage();
        var before = page.ActiveSection;

        page.RevealRow("nothing.here");
        page.RevealPlace("nowhere");
        Jobs();

        Assert.Equal(before, page.ActiveSection);
        Assert.Null(page.LandedOn);

        window.Close();
    }

    [AvaloniaFact]
    public void APressTakesTheMarkOff()
    {
        using var look = AppLook.Put();
        var (page, window) = ShownPage();

        page.RevealRow(SpeechCapability.ChatterboxVoiceKey);
        Jobs();

        Assert.NotNull(page.LandedOn);

        window.MouseDown(new Point(600, 10), MouseButton.Left);
        window.MouseUp(new Point(600, 10), MouseButton.Left);
        Jobs();

        Assert.Null(page.LandedOn);

        window.Close();
    }

    [AvaloniaFact]
    public void AJumpToAShipsRowOpensThatSurfacesOwnStripAndMarksTheRow()
    {
        using var look = AppLook.Put();
        var (settings, viewState) = Surface();
        var root = TempFolders.Create("d47-setting-jump-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var ships = new ShipPlanService(
            new ShipBuildStore(Path.Combine(root, "ships.json"), new MemoryFileSystem(), NullLogger<ShipBuildStore>.Instance),
            checklists,
            () => null);

        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };
        var strip = new SettingsView();
        var other = new SettingsView();

        strip.Attach(settings, viewState, tabPlaceId: "fleet-ships");
        other.Attach(settings, viewState, tabPlaceId: "fleet-ships");

        view.EnableSettings(() => Page(settings, viewState));
        view.EnableLoadout(ships, checklists, () => null, settingsStrip: () => strip);

        var window = new Window { Content = view, Width = 1000, Height = 800 };
        window.Show();
        Jobs();

        model.AppendError("The hull pictures are off. Turn them on in Settings.", "ships.art");
        Jobs();

        var block = view.TranscriptBlocks.Single();
        var start = block.Inlines!.Text!.LastIndexOf("Settings", StringComparison.Ordinal);
        var at = block.TranslatePoint(
            block.TextLayout.HitTestTextRange(start, "Settings".Length).First().Center + new Point(block.Padding.Left, block.Padding.Top),
            window)!.Value;

        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Jobs();
        Jobs();

        Assert.Equal(PanelTab.Assets, view.Nav.Tab);
        Assert.Equal("loadout.ships", view.Nav.RootKeyOf(PanelTab.Assets));

        Assert.StartsWith("▾ SETTINGS ", ((Button)strip.BarTool!).Content as string, StringComparison.Ordinal);
        Assert.Same(strip.FrameFor("ships.art"), strip.LandedOn);
        Assert.Null(other.LandedOn);

        window.Close();
    }

    [AvaloniaFact]
    public void AJumpToAChecklistRowShowsTheChecklistRootAndThrowsNothing()
    {
        using var look = AppLook.Put();
        var (settings, viewState) = Surface();
        var root = TempFolders.Create("d47-setting-jump-tests");

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(root, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(root, "checklist-proposals.json"), new MemoryFileSystem(), NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var model = new PanelViewModel();
        var view = new PanelView { DataContext = model };

        view.EnableSettings(() => Page(settings, viewState));
        view.EnableChecklist(checklists);

        var window = new Window { Content = view, Width = 1000, Height = 800 };
        window.Show();
        Jobs();

        model.AppendError("The checklist is off. Turn it on in Settings.", "checklists.summary");
        Jobs();

        var block = view.TranscriptBlocks.Single();
        var start = block.Inlines!.Text!.LastIndexOf("Settings", StringComparison.Ordinal);
        var at = block.TranslatePoint(
            block.TextLayout.HitTestTextRange(start, "Settings".Length).First().Center + new Point(block.Padding.Left, block.Padding.Top),
            window)!.Value;

        window.MouseMove(at);
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Jobs();
        Jobs();

        Assert.Equal(PanelTab.Commander, view.Nav.Tab);
        Assert.Equal("checklist", view.Nav.RootKeyOf(PanelTab.Commander));

        window.Close();
    }
}
