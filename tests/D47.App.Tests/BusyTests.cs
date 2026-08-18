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

/// <summary>
/// Anything that might take a moment says it is working (list.md Phase 12).
/// <para>
/// Two failure modes held off at once, and both are here: no silence on the slow case, and no
/// flash on the fast one. The two numbers that decide it are stated in one place, which is what
/// this drives rather than a per-call-site choice.
/// </para>
/// </summary>
public class BusyTests
{
    /// <summary>Whether the glyph was ever made visible, not merely whether it is now.</summary>
    private sealed class Watch
    {
        public Watch(BusyGlyph glyph) =>
            glyph.PropertyChanged += (_, change) =>
            {
                if (change.Property == Visual.IsVisibleProperty && change.NewValue is true)
                {
                    Shown = true;
                }
            };

        public bool Shown { get; private set; }
    }

    [AvaloniaFact]
    public async Task TheFastCaseNeverShowsTheGlyph()
    {
        // Hidden to begin with, as every one of these is on the surface: a glyph that starts
        // visible would never raise the change this is watching for.
        var glyph = new BusyGlyph { IsVisible = false };
        var watch = new Watch(glyph);
        var button = new Button();

        await Busy.While(button, glyph, () => Task.CompletedTask);

        Assert.False(watch.Shown, "a fast operation strobed a spinner on and off again");
        Assert.True(button.IsEnabled);
    }

    /// <summary>
    /// And the slow case shows it, and keeps it up for the minimum — an operation that runs
    /// just past the delay must not put a spinner on screen for ten milliseconds, which is the
    /// same flash moved to the other end.
    /// </summary>
    [AvaloniaFact]
    public async Task TheSlowCaseShowsTheGlyphAndKeepsItUpForTheMinimum()
    {
        // Hidden to begin with, as every one of these is on the surface: a glyph that starts
        // visible would never raise the change this is watching for.
        var glyph = new BusyGlyph { IsVisible = false };
        var watch = new Watch(glyph);

        var started = DateTime.UtcNow;

        // Just past the delay, so the minimum is what decides how long this takes rather than
        // the work.
        await Busy.While(null, glyph, () => Task.Delay(Busy.Delay + TimeSpan.FromMilliseconds(40)));

        var took = DateTime.UtcNow - started;

        Assert.True(watch.Shown, "a slow operation ran in silence");
        Assert.True(
            took >= Busy.Minimum,
            $"the glyph was up for less than the stated minimum: {took.TotalMilliseconds:0} ms");

        Assert.False(glyph.IsVisible);
    }

    /// <summary>
    /// The control is shut while it runs, so a second click cannot queue a second copy of the
    /// same fetch — and it is reopened even when the work throws, or a row that failed once is
    /// a row that can never be tried again.
    /// </summary>
    [AvaloniaFact]
    public async Task TheControlIsShutWhileItRunsAndReopenedEvenOnAFailure()
    {
        var button = new Button();
        var enabledDuring = true;

        await Busy.While(button, null, () =>
        {
            enabledDuring = button.IsEnabled;
            return Task.CompletedTask;
        });

        Assert.False(enabledDuring);
        Assert.True(button.IsEnabled);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Busy.While(button, null, () => throw new InvalidOperationException("the host refused")));

        Assert.True(button.IsEnabled, "a row that failed once can never be tried again");
    }

    /// <summary>
    /// The Log file reading is the one that touches a disk, so it is the one that says it is
    /// working — on the affordance that was touched.
    /// <para>
    /// On the <em>mode control</em> rather than on the tab since Phase 25, and that is the
    /// asymmetry the collapse deliberately keeps: Conversation and Technical are one exchange at
    /// two verbosities and the log is a file, so only one of the three has anything to say about
    /// being busy.
    /// </para>
    /// <para>
    /// The control is a drop-down inside the pane now rather than a row of segments beside the
    /// tabs (remediation.md 10, item 1), so the glyph rides the button that opens it — which is
    /// still the thing the Commander pressed.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheModeControlCarriesTheGlyphOnBothSurfaces()
    {
        var model = new PanelViewModel();

        foreach (var mode in new[] { PanelMode.Full, PanelMode.Mini })
        {
            // The headset renders this same view, so the glyph has to be part of the panel rather
            // than something the window hangs on it afterwards.
            var view = new PanelView { DataContext = model, Mode = mode };

            // Through the logical tree rather than the visual one, because this view has never
            // been shown and the claim is about a panel the headset rasterises offscreen.
            var glyph = (view.GetControl<Button>("ModeButton").Content as StackPanel)
                ?.Children
                .OfType<BusyGlyph>()
                .SingleOrDefault();

            Assert.NotNull(glyph);
            Assert.False(glyph.IsVisible);
        }
    }

    /// <summary>
    /// Opening the Log page reads the file off the thread that draws, which is the half of this
    /// that stops a slow read looking like a frozen window rather than a slow one.
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

    /// <summary>
    /// The picker button's hand-rolled version collapsed into the helper, so the glyph it always
    /// had is still there and still starts hidden.
    /// </summary>
    [AvaloniaFact]
    public void ThePickerRowStillCarriesItsGlyph()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var glyphs = host.View.GetVisualDescendants().OfType<BusyGlyph>().ToList();

        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, glyph => Assert.False(glyph.IsVisible));

        host.Close();
    }

    /// <summary>
    /// A core takes a model round trip to work out its first line, and the row the Commander
    /// picked it on is where that is said. The glyph is made on demand rather than built with
    /// every row, because a hidden one still animates.
    /// </summary>
    [AvaloniaFact]
    public void TheCoreRowSaysItIsWorkingWhileTheNewCoreDecidesWhatToSay()
    {
        var (settings, viewState, paths) = TestSurface.Create();

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        Assert.DoesNotContain(host.View.GetVisualDescendants().OfType<BusyGlyph>(), glyph => glyph.IsVisible);

        host.View.ShowBusy(D47.Core.Capabilities.Builtin.PersonaCapability.PersonaKey, busy: true);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var row = CoreRow(host);

        Assert.Contains(row.GetVisualDescendants().OfType<BusyGlyph>(), glyph => glyph.IsVisible);
        Assert.False(row.GetVisualDescendants().OfType<ComboBox>().First().IsEnabled);

        host.View.ShowBusy(D47.Core.Capabilities.Builtin.PersonaCapability.PersonaKey, busy: false);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(row.GetVisualDescendants().OfType<BusyGlyph>(), glyph => glyph.IsVisible);
        Assert.True(row.GetVisualDescendants().OfType<ComboBox>().First().IsEnabled);

        host.Close();
    }

    private static Grid CoreRow(SettingsHost host) =>
        host.View.GetVisualDescendants().OfType<Grid>()
            .First(grid => grid.ColumnDefinitions.Count == 3
                && grid.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Persona"));
}
