using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.Core.Updates;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The local-build badge opens what that build worked, on the desktop and nowhere else
/// (<a href="https://github.com/dseelinger/d47/issues/207">#207</a>).
/// <para>
/// <b>The gate is that nothing wires it, rather than that something hides it.</b>
/// <see cref="PanelView"/> is instantiated twice, so the headset's copy inherits anything added to
/// the class — and <c>#202</c> is open precisely because a local <c>IsVisible</c> outranks a style
/// setter, which is how clickable controls have leaked onto a mini panel before. A handler the
/// headset host never furnishes cannot leak, whatever any style says; and the badge asks
/// <c>output-only</c> as well, because two gates that answer different questions are not a
/// duplicate of one.
/// </para>
/// <para>
/// In the headset a click would open a browser on a monitor the Commander cannot see, which is the
/// argument that took the help button off that surface in the first place.
/// </para>
/// </summary>
public class TheBadgeOpensWhatTheBuildWorkedTests
{
    private static Border Badge(PanelView view) =>
        view.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PreReleaseBadge");

    private static (PanelView View, Window Window) Shown(bool outputOnly)
    {
        var view = new PanelView();

        if (outputOnly)
        {
            view.Classes.Add("output-only");
        }

        var window = new Window { Content = view, Width = 900, Height = 700 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (view, window);
    }

    /// <summary>A press where the badge is, as a pointer delivers one.</summary>
    private static void Press(PanelView view, Window window)
    {
        var badge = Badge(view);

        var at = badge.TranslatePoint(new Point(badge.Bounds.Width / 2, badge.Bounds.Height / 2), window)
                 ?? new Point(0, 0);

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void TheDesktopBadgeOpensTheList()
    {
        var opened = 0;
        var (view, window) = Shown(outputOnly: false);

        view.EnableBuildDetails(() => opened++);
        view.ShowChannel(ReleaseChannel.Local);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Badge(view).IsVisible);

        Press(view, window);

        Assert.Equal(1, opened);
    }

    /// <summary>
    /// <b>The headset host never furnishes it, and this is what that buys.</b> Even with the badge
    /// forced visible — which the headset's copy never does — there is no handler to reach.
    /// </summary>
    [AvaloniaFact]
    public void TheHeadsetHasNothingToPress()
    {
        var opened = 0;
        var (view, window) = Shown(outputOnly: true);

        view.ShowChannel(ReleaseChannel.Local);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // The badge is not even drawn there, which is the first gate.
        Assert.False(Badge(view).IsVisible);

        // And the second: a host that furnished it anyway would still not be able to open a
        // browser from a surface that is output-only.
        view.EnableBuildDetails(() => opened++);
        Badge(view).IsVisible = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Press(view, window);

        Assert.Equal(0, opened);
    }

    /// <summary>
    /// A published build has nothing to list, so the badge stays the plain mark it has always
    /// been. Pressing it does nothing rather than opening an empty box.
    /// </summary>
    [AvaloniaFact]
    public void ABadgeNobodyFurnishedIsStillJustAMark()
    {
        var (view, window) = Shown(outputOnly: false);

        view.ShowChannel(ReleaseChannel.PreRelease);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(Badge(view).IsVisible);
        Assert.Equal(Cursor.Default, Badge(view).Cursor);

        // No throw, and nothing to observe: the point is that a press is inert.
        Press(view, window);
    }

    /// <summary>
    /// It says it can be pressed. A badge that opens something and looks exactly like one that
    /// does not is a feature nobody finds.
    /// </summary>
    [AvaloniaFact]
    public void AClickableBadgeSaysSo()
    {
        var (view, window) = Shown(outputOnly: false);

        view.EnableBuildDetails(() => { });
        view.ShowChannel(ReleaseChannel.Local);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.NotEqual(Cursor.Default, Badge(view).Cursor);
        Assert.Contains("Click", $"{ToolTip.GetTip(Badge(view))}", StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Whichever order the host does it in.</b> <c>EnableBuildDetails</c> happens when the
    /// window is built and <c>ShowChannel</c> when GitHub has answered, and neither may depend on
    /// being second — a badge that only became clickable one way round would work on some starts
    /// and not others.
    /// </summary>
    [AvaloniaFact]
    public void FurnishingBeforeOrAfterTheChannelBothWork()
    {
        foreach (var furnishFirst in new[] { true, false })
        {
            var opened = 0;
            var (view, window) = Shown(outputOnly: false);

            if (furnishFirst)
            {
                view.EnableBuildDetails(() => opened++);
                view.ShowChannel(ReleaseChannel.Local);
            }
            else
            {
                view.ShowChannel(ReleaseChannel.Local);
                view.EnableBuildDetails(() => opened++);
            }

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Press(view, window);

            Assert.Equal(1, opened);
        }
    }

    /// <summary>
    /// The window itself, drawn. Its whole subject is which binary is running, so it shows the
    /// full stamp — and an empty list says so plainly rather than opening on nothing.
    /// </summary>
    [AvaloniaFact]
    public void TheWindowSaysWhatWasWorkedAndWhatItCannotSee()
    {
        var worked = LocalBuildNotes.Parse(Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(
                """[{"n":205,"s":"open","t":"Make the badge clickable","l":["ready"]}]""")));

        var window = new LocalBuildWindow("0.92.0-local+8b21b3d", worked);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var text = string.Join(
            "\n",
            window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

        Assert.Contains("0.92.0-local+8b21b3d", text, StringComparison.Ordinal);
        Assert.Contains("Make the badge clickable", text, StringComparison.Ordinal);
        Assert.Contains("Fixes #N", text, StringComparison.Ordinal);

        // The chip is the number, and it is a button because that is what every other
        // out-to-the-browser affordance in this app already is.
        Assert.Contains(
            window.GetVisualDescendants().OfType<Button>(),
            button => button.Name == "LocalBuildIssue");
    }

    [AvaloniaFact]
    public void AnEmptyListIsASentenceRatherThanAnEmptyBox()
    {
        var window = new LocalBuildWindow("0.92.0-local", []);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var text = string.Join(
            "\n",
            window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

        Assert.Contains("No commit in this build names an issue", text, StringComparison.Ordinal);

        // And the caveat is on this path too: it is a property of how the list is gathered rather
        // than of what happens to be in it, and it is what keeps an empty list from reading as
        // "nothing was done".
        Assert.Contains("Fixes #N", text, StringComparison.Ordinal);
    }
}
