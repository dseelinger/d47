using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Windowing;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A dialog is drawn at the size of the window that opened it (remediation.md 11, item 11).
/// <para>
/// <see cref="ZoomHost"/> was written to be attached to a window rather than built into one,
/// with a comment saying that a zoom stopping at the panel's edge would be a zoom the Commander
/// has to remember the boundaries of. Exactly one window ever attached it, so every dialog opened
/// over a zoomed panel came up at 100% and read as another application's window. The settings
/// surface escaped by becoming a tab rather than by being fixed.
/// </para>
/// </summary>
public class DialogsMatchTheWindowsZoomTests
{
    private static (Window Owner, SettingsService Settings) Zoomed(int percent)
    {
        var (settings, _, _) = TestSurface.Create();

        settings.Apply(InterfaceCapability.ZoomKey, percent.ToString(), SettingsCaller.Panel);

        var owner = new Window { Content = new TextBlock { Text = "panel" }, Width = 800, Height = 600 };

        ZoomHost.Attach(owner, settings);
        owner.Show();
        Dispatcher.UIThread.RunJobs();

        return (owner, settings);
    }

    /// <summary>
    /// Read off the window's own content rather than its visuals: a dialog that has not been shown
    /// has no visual tree, and showing a modal in a headless test blocks on an answer nobody gives.
    /// </summary>
    private static double? ScaleOf(Window window) =>
        window.Content is ScrollViewer { Content: LayoutTransformControl host }
            ? (host.LayoutTransform as ScaleTransform)?.ScaleX
            : null;

    /// <summary>The report: a dialog over a zoomed panel is drawn at the panel's size.</summary>
    [AvaloniaFact]
    public void ADialogIsDrawnAtTheOwnersZoom()
    {
        var (owner, _) = Zoomed(150);

        var dialog = new Window { Content = new TextBlock { Text = "dialog" }, Width = 400, Height = 300 };

        ZoomHost.Match(dialog, owner);

        Assert.Equal(1.5, ScaleOf(dialog));
    }

    /// <summary>And grows with it, or it opens showing a scaled corner of itself.</summary>
    [AvaloniaFact]
    public void AndTheWindowGrowsWithIt()
    {
        var (owner, _) = Zoomed(150);

        var dialog = new Window { Content = new TextBlock(), Width = 400, Height = 300 };

        ZoomHost.Match(dialog, owner);

        Assert.Equal(600, dialog.Width);
        Assert.Equal(450, dialog.Height);
    }

    /// <summary>
    /// At 100% nothing is wrapped at all. A scaling host that scales by one is a layout pass and a
    /// scroll viewer bought for nothing, on every dialog, forever.
    /// </summary>
    [AvaloniaFact]
    public void AtOneHundredPercentNothingIsWrapped()
    {
        var (owner, _) = Zoomed(100);

        var dialog = new Window { Content = new TextBlock(), Width = 400, Height = 300 };

        ZoomHost.Match(dialog, owner);

        Assert.Null(ScaleOf(dialog));
        Assert.Equal(400, dialog.Width);
    }

    /// <summary>A window nobody attached zoom to owns nothing to match, and is left alone.</summary>
    [AvaloniaFact]
    public void AnUnzoomedOwnerChangesNothing()
    {
        var owner = new Window { Content = new TextBlock() };

        owner.Show();

        var dialog = new Window { Content = new TextBlock(), Width = 400, Height = 300 };

        ZoomHost.Match(dialog, owner);

        Assert.Null(ScaleOf(dialog));
    }

    /// <summary>
    /// And nothing in the app opens a dialog the other way. This is the assertion that keeps the
    /// fix — a dialog added next year would otherwise be one more window at 100%, and the fault
    /// only shows on a machine with zoom turned on.
    /// </summary>
    [Fact]
    public void NothingCallsShowDialogDirectly()
    {
        var source = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "src", "D47.App"));

        Assert.True(Directory.Exists(source), $"the App sources are not at {source}");

        var offenders = Directory
            .EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => Path.GetFileName(file) != "Dialogs.cs")
            .Where(file => File.ReadAllText(file).Contains(".ShowDialog(", StringComparison.Ordinal)
                           || File.ReadAllText(file).Contains(".ShowDialog<", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"open a dialog with .Over(owner) so it matches the window's zoom: {string.Join(", ", offenders)}");
    }

    /// <summary>
    /// A zoomed dialog fits inside its window
    /// (<a href="https://github.com/dseelinger/d47/issues/265">#265</a>).
    /// <para>
    /// <b>Reported as a help button past the fold</b>, and the mark was a witness rather than the
    /// fault: it is docked right, so it is the first thing over the edge when the content is wider
    /// than the window. What was wrong is that every zoomed dialog overflowed by <em>its own
    /// margin</em> — a control's <c>DesiredSize</c> includes its margin and <c>MaxWidth</c> does
    /// not, so constraining a panel with <c>Margin(20)</c> to exactly the viewport made it ask for
    /// the viewport plus forty.
    /// </para>
    /// <para>
    /// <b>The tests above never caught it because they never laid anything out.</b> They read the
    /// scale off the window's own content, which is the right way to ask whether a dialog was
    /// wrapped — and it says nothing about whether what is inside the wrapper fits. This one shows
    /// a real dialog and measures.
    /// </para>
    /// </summary>
    [AvaloniaTheory]
    [InlineData(110)]
    [InlineData(125)]
    [InlineData(150)]
    public void AZoomedDialogDoesNotScrollSideways(int percent)
    {
        var (owner, _) = Zoomed(percent);

        var dialog = new Controls.HelpImproveWindow(
            new DateTimeOffset(2026, 9, 1, 21, 0, 0, TimeSpan.Zero),
            _ => "a line",
            destination: "donations.example");

        ZoomHost.Match(dialog, owner);
        dialog.Show();

        // Several passes: Fit runs off the viewport, and there is no viewport until a layout has
        // happened — so the first measure is deliberately unconstrained and settles after it.
        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var viewport = Assert.IsType<ScrollViewer>(dialog.Content);

        Assert.True(
            viewport.Extent.Width <= viewport.Viewport.Width + 0.5,
            $"""
             At {percent}% the dialog is {viewport.Extent.Width:0} wide inside a
             {viewport.Viewport.Width:0} viewport, so it scrolls sideways and whatever is docked
             right is off the edge. Fit is not subtracting the content's margin.
             """);

        dialog.Close();
        owner.Close();
    }

    /// <summary>
    /// And the mark is somewhere a Commander can reach, which is the half that was reported. A
    /// control past the right edge still has bounds, so this asks <em>where</em> it is.
    /// </summary>
    [AvaloniaFact]
    public void TheHelpMarkOnAZoomedDialogIsOnScreen()
    {
        var (owner, _) = Zoomed(125);

        var dialog = new Controls.HelpImproveWindow(
            new DateTimeOffset(2026, 9, 1, 21, 0, 0, TimeSpan.Zero),
            _ => "a line",
            destination: "donations.example");

        ZoomHost.Match(dialog, owner);
        dialog.Show();

        for (var i = 0; i < 5; i++)
        {
            Dispatcher.UIThread.RunJobs();
        }

        var mark = dialog.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "HelpImproveHelp");

        var at = mark.TranslatePoint(new Point(mark.Bounds.Width, 0), dialog);

        Assert.NotNull(at);
        Assert.True(
            at.Value.X <= dialog.Width + 0.5,
            $"The mark's right edge is at {at.Value.X:0} in a window {dialog.Width:0} wide.");

        dialog.Close();
        owner.Close();
    }
}
