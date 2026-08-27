using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Updates;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The pre-release mark appears beside the help glyph on the desktop and never on the headset's
/// copy (<a href="https://github.com/dseelinger/d47/issues/92">#92</a>).
/// <para>
/// <b>Because a badge there would be chrome a Commander cannot dismiss while flying.</b>
/// <c>PanelView</c> is instantiated twice — once for the window and once for the overlay — so
/// anything added to it appears in VR by default. The help button already has this exact
/// treatment, and its comment says why: the VR copy is never handed one because the button opens a
/// browser the Commander cannot see. Whether a badge belongs there is a decision, and this is the
/// decision being made rather than inherited.
/// </para>
/// <para>
/// <b>And it has to come off as readily as it goes on.</b> Promoting a pre-release changes the
/// answer without changing the binary, so a running d47 must be able to stop showing the mark —
/// which a one-way furnish would not do.
/// </para>
/// </summary>
public class TheBadgeStaysOutOfTheHeadsetTests
{
    private static Border Badge(PanelView view) =>
        view.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PreReleaseBadge");

    private static PanelView Desktop()
    {
        var view = new PanelView();
        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return view;
    }

    /// <summary>The headset's copy is marked by a class, exactly as the help button reads it.</summary>
    private static PanelView Headset()
    {
        var view = new PanelView();
        view.Classes.Add("output-only");
        var window = new Window { Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return view;
    }

    [AvaloniaFact]
    public void TheDesktopShowsItOnAPreRelease()
    {
        var view = Desktop();

        view.ShowChannel(ReleaseChannel.PreRelease);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Badge(view).IsVisible);
    }

    [AvaloniaFact]
    public void TheHeadsetNeverDoes()
    {
        var view = Headset();

        view.ShowChannel(ReleaseChannel.PreRelease);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(
            Badge(view).IsVisible,
            "the pre-release mark is on the headset's panel. PanelView is instantiated twice, so "
            + "anything added to it appears in VR unless it reads the output-only class the way "
            + "the help button does.");
    }

    [AvaloniaFact]
    public void AndNothingIsMarkedOnAFinalReleaseOrWhenNobodyCouldAsk()
    {
        var view = Desktop();

        foreach (var channel in new[] { ReleaseChannel.Release, ReleaseChannel.Unknown })
        {
            view.ShowChannel(channel);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            Assert.False(Badge(view).IsVisible, $"{channel} should carry no mark.");
        }
    }

    /// <summary>
    /// Promotion is the case this exists for: same binary, same tag, different answer. The mark
    /// has to come down without a reinstall.
    /// </summary>
    [AvaloniaFact]
    public void AndItComesDownAgainWhenTheReleaseIsPromoted()
    {
        var view = Desktop();

        view.ShowChannel(ReleaseChannel.PreRelease);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(Badge(view).IsVisible);

        view.ShowChannel(ReleaseChannel.Release);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.False(Badge(view).IsVisible);
    }
}
