using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Windowing;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The way out of mini, seated rather than given a row of its own
/// (<a href="https://github.com/dseelinger/d47/issues/194">#194</a>), and drawn half the size it
/// was (<a href="https://github.com/dseelinger/d47/issues/193">#193</a>).
/// <para>
/// Driven through the real <see cref="PanelView"/> rather than by reading properties off a probe,
/// because the thing being changed is what a Commander can see and press.
/// </para>
/// <para>
/// <b>The invariant underneath all of it is that there is always exactly one way out.</b> It was
/// given its own row in the first place because <c>StatusRow</c> is the transcript's and is hidden
/// on every other tab, so a seat alone would put a hole in it exactly where a Commander is reading
/// something else. Two controls now cover between them what one row covered before, and the tests
/// that matter are the ones that would catch either of them being missing.
/// </para>
/// </summary>
public class TheWayOutTakesASeatTests
{
    [AvaloniaFact]
    public void TheTranscriptSeatsItAndSpendsNoRowOnIt()
    {
        var (window, panel) = Open();

        panel.EnableModeToggle(_ => { });
        Dispatcher.UIThread.RunJobs();

        foreach (var mode in new[] { PanelMode.Full, PanelMode.Mini })
        {
            panel.Mode = mode;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(PanelTab.Transcript, panel.Tab);
            Assert.True(panel.GetControl<Button>("ModeToggleSeat").IsVisible, $"no seat in {mode}");
            Assert.False(panel.GetControl<DockPanel>("ModeRow").IsVisible, $"a row was spent in {mode}");
        }

        window.Close();
    }

    /// <summary>
    /// And every other tab draws the row, because the seat rides a status line those tabs do not
    /// show. This is the half that keeps the way out from having a hole in it.
    /// </summary>
    [AvaloniaFact]
    public void EveryOtherTabDrawsTheRowInstead()
    {
        var (window, panel) = Open();

        panel.EnableModeToggle(_ => { });
        panel.EnableSettings(() => new TextBlock { Text = "settings" });
        panel.EnableAdventures(AdventureFixture.Surface());
        Dispatcher.UIThread.RunJobs();

        foreach (var tab in new[] { PanelTab.Settings, PanelTab.Adventures })
        {
            panel.Tab = tab;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(tab, panel.Tab);

            // The status line is the transcript's, so the seat cannot be what answers here.
            Assert.False(panel.GetControl<DockPanel>("StatusRow").IsVisible, $"status row showed on {tab}");
            Assert.True(panel.GetControl<DockPanel>("ModeRow").IsVisible, $"no way out on {tab}");
            Assert.False(panel.GetControl<Button>("ModeToggleSeat").IsVisible, $"a seat drew on {tab}");
        }

        // And in mini on a tab that has a short reading, where the row is the only thing that can
        // answer and the surface can least afford to be stuck.
        panel.Tab = PanelTab.Adventures;
        panel.Mode = PanelMode.Mini;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PanelTab.Adventures, panel.Tab);
        Assert.True(panel.GetControl<DockPanel>("ModeRow").IsVisible);

        window.Close();
    }

    /// <summary>
    /// Two buttons, one control: they are marked by one call so they cannot drift into two
    /// behaviours, and the words a screen reader says are the same whichever is drawing.
    /// </summary>
    [AvaloniaFact]
    public void BothWaysOutSayTheSameThingInBothDirections()
    {
        var (window, panel) = Open();

        panel.EnableModeToggle(_ => { });
        Dispatcher.UIThread.RunJobs();

        foreach (var (mode, says) in new[]
                 {
                     (PanelMode.Full, "Shrink to the mini panel"),
                     (PanelMode.Mini, "Expand to the whole panel"),
                 })
        {
            panel.Mode = mode;
            Dispatcher.UIThread.RunJobs();

            foreach (var name in new[] { "ModeToggle", "ModeToggleSeat" })
            {
                var button = panel.GetControl<Button>(name);

                Assert.Equal(says, Avalonia.Automation.AutomationProperties.GetName(button));
                Assert.Equal(says, ToolTip.GetTip(button));
            }
        }

        window.Close();
    }

    /// <summary>
    /// Half of the 17 it was, and smaller than the 14 every other mark takes (#193). Asserted on
    /// the drawn <see cref="Path"/> rather than on the number in the call, so it is the glyph that
    /// is being measured.
    /// </summary>
    [AvaloniaFact]
    public void TheMarkIsHalfTheSizeItWas()
    {
        var (window, panel) = Open();

        panel.EnableModeToggle(_ => { });
        Dispatcher.UIThread.RunJobs();

        foreach (var name in new[] { "ModeToggle", "ModeToggleSeat" })
        {
            var glyph = Assert.IsType<Avalonia.Controls.Shapes.Path>(panel.GetControl<Button>(name).Content);

            Assert.Equal(8.5, glyph.Width);
            Assert.Equal(8.5, glyph.Height);
        }

        window.Close();
    }

    /// <summary>
    /// <b>The one thing that could have made this cost a line instead of saving one</b>, measured
    /// rather than eyeballed on a desktop window where it would look fine regardless (#194).
    /// <para>
    /// The seat is docked right, so it narrows the width the provenance line has to wrap into —
    /// and <c>TurnLine</c> wraps. At 512 pixels, with the microphone docked left and Details
    /// docked right as well, a line that went from one row to two would have spent exactly what
    /// removing <c>ModeRow</c> gave back.
    /// </para>
    /// </summary>
    [AvaloniaFact]
    public void TheSeatDoesNotPushTheProvenanceLineOntoASecondRowAtMiniWidth()
    {
        var (window, panel) = Open();
        var view = Assert.IsType<PanelViewModel>(panel.DataContext);

        // The worst case the row actually has: everything that docks beside the line, showing at
        // once, with a provenance line of the length one really reaches.
        view.Microphone = D47.Core.Listening.MicrophoneState.Idle;
        view.TurnLine = "gpt-5.4-nano · 4,182 in · 611 out · $0.0038 · 2.4 s · 37 turns · $0.24 today";

        panel.EnableModeToggle(_ => { });
        Dispatcher.UIThread.RunJobs();

        panel.GetControl<Button>("TurnDetails").IsVisible = true;
        Dispatcher.UIThread.RunJobs();

        var status = panel.GetControl<DockPanel>("StatusRow");
        var seat = panel.GetControl<Button>("ModeToggleSeat");

        Assert.True(seat.IsVisible, "the seat is what is being measured and it is not drawn");

        double Wanted()
        {
            status.Measure(new Size(PanelResolution.Mini.Width, double.PositiveInfinity));

            var wanted = status.DesiredSize.Height;

            status.InvalidateMeasure();
            Dispatcher.UIThread.RunJobs();

            return wanted;
        }

        var seated = Wanted();

        seat.IsVisible = false;
        Dispatcher.UIThread.RunJobs();

        var without = Wanted();

        Assert.True(
            seated <= without,
            $"The seat made the status row taller at mini width — {seated} against {without} — so "
            + "the provenance line wrapped and the row #194 saved was spent again here.");

        window.Close();
    }

    private static (Window Window, PanelView Panel) Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = new Window { Content = panel, Width = 900, Height = 640 };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }
}
