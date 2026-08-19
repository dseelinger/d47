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
}
