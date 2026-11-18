using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The wait mark sits beside the control whose work it waits on.</summary>
public class TheWaitMarkSitsBesideWhatWasPressedTests
{
    /// <summary>
    /// The Log file reading is the one that touches a disk, so it is the one that says it is working —
    /// on the affordance that was touched.
    /// </summary>
    [AvaloniaFact]
    public void TheModeControlCarriesTheMarkOnBothSurfaces()
    {
        var model = new PanelViewModel();

        foreach (var mode in new[] { PanelMode.Full, PanelMode.Mini })
        {
            // The headset renders this same view, so the mark has to be part of the panel rather than
            // something the window hangs on it afterwards.
            var view = new PanelView { DataContext = model, Mode = mode };

            // Through the logical tree rather than the visual one, because this view has never been shown and
            // the claim is about a panel the headset rasterises offscreen.
            var mark = view.GetControl<DockPanel>("ModePicker")
                ?.Children
                .OfType<WaitingState>()
                .SingleOrDefault();

            Assert.NotNull(mark);
            Assert.False(mark.IsVisible);
        }
    }

    /// <summary>
    /// Opening the Log page reads the file off the thread that draws, which is the half of this that
    /// stops a slow read looking like a frozen window rather than a slow one.
    /// </summary>
    [AvaloniaFact]
    public async Task OpeningTheLogPageReadsItOffTheDrawingThread()
    {
        var read = new TaskCompletionSource<int>();

        var model = new PanelViewModel
        {
            LogSource = () =>
            {
                read.TrySetResult(Environment.CurrentManagedThreadId);
                return "today's log";
            },
        };

        var view = new PanelView { DataContext = model };
        var window = new Window { Content = view, Width = 700, Height = 500 };
        window.Show();

        var drawing = Environment.CurrentManagedThreadId;

        view.Page = TranscriptPage.Log;

        Assert.NotEqual(drawing, await read.Task);

        window.Close();
    }

    /// <summary>The picker row's mark is built with the row and starts hidden.</summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void ThePickerRowCarriesItsMark()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);
        host.ShowPlaceOf(D47.Core.Capabilities.Builtin.SpeechCapability.VoiceKey);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var marks = host.View.GetVisualDescendants().OfType<WaitingState>().ToList();

        Assert.NotEmpty(marks);
        Assert.All(marks, mark => Assert.False(mark.IsVisible));

        host.Close();
    }

    /// <summary>
    /// A core takes a model round trip to work out its first line, and the row the Commander picked it
    /// on is where that is said.
    /// </summary>
    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheCoreRowWaitsWhileTheNewCoreDecidesWhatToSay()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        // Persona's own area (#220).
        host.View.Reveal(D47.Core.Capabilities.Builtin.PersonaCapability.Id);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(host.View.GetVisualDescendants().OfType<WaitingState>(), mark => mark.IsVisible);

        host.View.ShowBusy(D47.Core.Capabilities.Builtin.PersonaCapability.PersonaKey, busy: true);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var row = CoreRow(host);

        Assert.Contains(row.GetVisualDescendants().OfType<WaitingState>(), mark => mark.IsVisible);
        Assert.False(Tile(row).IsEffectivelyEnabled);

        host.View.ShowBusy(D47.Core.Capabilities.Builtin.PersonaCapability.PersonaKey, busy: false);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(row.GetVisualDescendants().OfType<WaitingState>(), mark => mark.IsVisible);
        Assert.True(Tile(row).IsEffectivelyEnabled);

        host.Close();
    }

    private static Button Tile(Grid row) =>
        row.GetVisualDescendants().OfType<Button>().First(button => button.Name == "DropdownTile");

    private static Grid CoreRow(SettingsHost host) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .First(grid => grid.Classes.Contains(D47.App.Settings.SettingsView.CompactRowClass)
                && grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Core"));
}
