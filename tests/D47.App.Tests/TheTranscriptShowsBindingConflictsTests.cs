using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Hotas;
using D47.Core.Interface;
using D47.Core.Input;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Transcript tab's Binding conflicts notice, and its tile opening the page on the window and the headset (#2384).</summary>
[Trait("Category", "Integration")]
public class TheTranscriptShowsBindingConflictsTests
{
    private const string BindsPath = @"C:\d47-test\transcript.binds";

    /// <summary>Ctrl+Alt+O, D47's Show or hide the overlay key, is Elite's spotlight too.</summary>
    private static EliteBinds Binds()
    {
        var files = new MemoryFileSystem();

        files.WriteText(BindsPath, """
            <Root PresetName="Custom">
              <ShipSpotLightToggle>
                <Primary Device="Keyboard" Key="Key_O">
                  <Modifier Device="Keyboard" Key="Key_LeftControl" />
                  <Modifier Device="Keyboard" Key="Key_LeftAlt" />
                </Primary>
                <Secondary Device="{NoDevice}" Key="" />
              </ShipSpotLightToggle>
            </Root>
            """);

        return EliteBinds.Parse(files, BindsPath, "Custom", NullLogger.Instance);
    }

    private sealed record Surface(Window Window, PanelView Panel, PanelViewModel Model)
    {
        public Notice Notice => Panel.GetVisualDescendants().OfType<Notice>().Single(notice => notice.Name == "BindingConflictsNotice");

        public BindingConflictsPage? Page => Panel.GetVisualDescendants().OfType<BindingConflictsPage>().FirstOrDefault();

        public void Close() => Window.Close();
    }

    /// <summary>A panel on its Transcript tab whose Settings tab has not been drawn yet.</summary>
    private static Surface Open(bool headset = false)
    {
        var binds = Binds();
        IReadOnlyList<HotasReading> readings = [];
        IReadOnlyList<SwitchMapping> switches = [];

        var (settings, viewState, _, _, _) = TestSurface.CreateFull(listening: surface => surface with
        {
            Binds = () => binds,
            Controllers = () => readings,
            Switches = () => switches,
        });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).FollowSettings(settings);

        var model = new PanelViewModel();
        var panel = new PanelView { DataContext = model };

        if (headset)
        {
            panel.Classes.Add("headset");
        }

        panel.EnableSettings(() =>
        {
            var view = new SettingsView();
            view.Attach(
                settings,
                viewState,
                null,
                null,
                null,
                null,
                null,
                null,
                bindingConflicts: new BindingConflictEditing(() => binds, () => readings, () => switches, _ => () => { }));
            return view;
        });

        panel.Tab = PanelTab.Transcript;

        var window = new Window { Content = panel, Width = 1180, Height = 880 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, model);
    }

    [AvaloniaFact]
    public void TheNoticeShowsWhileThereAreConflictsAndGoesAtNone()
    {
        var surface = Open();

        Assert.False(surface.Notice.IsVisible);

        surface.Model.BindingConflictCount = 5;
        Dispatcher.UIThread.RunJobs();

        Assert.True(surface.Notice.IsVisible);
        Assert.Equal("5 binding conflicts with Elite", surface.Notice.Label);
        Assert.Equal(
            "Elite also binds 5 of D47's keys and buttons. Rebind or unbind them on the Binding conflicts page.",
            surface.Notice.Text);

        using (var frame = surface.Window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("binding-conflicts-transcript-notice.png");
        }

        surface.Model.BindingConflictCount = 0;
        Dispatcher.UIThread.RunJobs();

        Assert.False(surface.Notice.IsVisible);

        surface.Close();
    }

    [AvaloniaFact]
    public void TheNoticeIsOnTheTranscriptTabOnly()
    {
        var surface = Open();

        surface.Model.BindingConflictCount = 1;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("1 binding conflict with Elite", surface.Notice.Label);
        Assert.True(surface.Notice.IsVisible);

        surface.Panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.False(surface.Notice.IsVisible);

        surface.Close();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItsTileOpensTheBindingConflictsPage(bool headset)
    {
        var surface = Open(headset);

        surface.Model.BindingConflictCount = 1;
        Dispatcher.UIThread.RunJobs();

        var tile = surface.Notice.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "OpenBindingConflictsButton");
        BindSlots.Click(surface.Window, tile);

        Assert.NotNull(surface.Page);
        Assert.Equal(PanelTab.Transcript, surface.Panel.Tab);
        Assert.Single(surface.Page!.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains(BindingConflictsPage.RowClass));
        Assert.False(surface.Notice.IsVisible);

        surface.Close();
    }

    [AvaloniaFact]
    public void TheSettingsTabStillDrawsItsPageAfterTheTileWasUsed()
    {
        var surface = Open();

        surface.Model.BindingConflictCount = 1;
        Dispatcher.UIThread.RunJobs();

        var tile = surface.Notice.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "OpenBindingConflictsButton");
        BindSlots.Click(surface.Window, tile);

        surface.Panel.Tab = PanelTab.Settings;
        Dispatcher.UIThread.RunJobs();

        Assert.Single(surface.Panel.GetVisualDescendants().OfType<SettingsView>(), view => view.IsEffectivelyVisible);

        surface.Close();
    }
}
