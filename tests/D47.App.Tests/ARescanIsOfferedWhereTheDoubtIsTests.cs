using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Settings;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The Commander can rebuild what is remembered about their ships, on demand.</summary>
public class ARescanIsOfferedWhereTheDoubtIsTests
{
    private const string ButtonName = "Press_ships_remembered";
    private const string BarName = "Progress_ships_remembered";

    private static T Find<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    /// <summary>The row is there, it says what is stored, and the button runs the rescan.</summary>
    [AvaloniaFact]
    public async Task TheRowSaysWhatIsStoredAndOffersToRebuildIt()
    {
        var running = new TaskCompletionSource<string?>();
        IProgress<double>? reporting = null;

        var (settings, viewState, paths) = TestSurface.Create(rescan: (progress, _) =>
        {
            reporting = progress;
            return running.Task;
        });

        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance)
            .FollowSettings(settings);

        var host = SettingsHost.Open(settings, viewState, paths);

        var button = Find<Button>(host.View, ButtonName);
        var bar = Find<ProgressBar>(host.View, BarName);

        Assert.Equal("Rescan my journals", button.Content);
        Assert.True(button.IsEnabled);
        Assert.False(bar.IsVisible);

        // What is stored is drawn above the button, from the host rather than from a guess.
        Assert.Contains(
            host.View.GetVisualDescendants().OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty),
            text => text.Contains("the oldest last seen 3 months ago", StringComparison.Ordinal));

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();

        // Shut and drawing, because a walk of a year of journals takes seconds and a surface that said
        // nothing is the defect the local voice download already had once.
        Assert.False(button.IsEnabled);
        Assert.True(bar.IsVisible);

        reporting!.Report(0.4);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0.4, bar.Value, 3);

        running.SetResult("Read 943 journals. 21 ship(s) remembered.");
        await running.Task;
        Dispatcher.UIThread.RunJobs();

        Assert.True(button.IsEnabled);
        Assert.False(bar.IsVisible);

        host.Close();
    }

    /// <summary>The help says the sentence the Commander asked for, in their words.</summary>
    [Fact]
    public void TheRowSaysNotLookRightRescan()
    {
        var row = Assert.Single(
            ShipsCapability.Create(surface: ShipsCapability.ShipsSurface.Inert).Settings,
            candidate => candidate.Key == ShipsCapability.RescanKey);

        Assert.Contains("Not look right? Rescan", row.Help, StringComparison.Ordinal);

        // And it says what the press does not touch, which is the reassurance that makes it pressable: a
        // repair nobody dares run is no repair.
        Assert.Contains("not read and not written", row.Help, StringComparison.Ordinal);
    }

    /// <summary>Info with a press, which is what keeps it off the tool surface.</summary>
    [Fact]
    public void NothingOnTheToolSurfaceCanStartIt()
    {
        var row = Assert.Single(
            ShipsCapability.Create(surface: ShipsCapability.ShipsSurface.Inert).Settings,
            candidate => candidate.Key == ShipsCapability.RescanKey);

        Assert.Equal(SettingKind.Info, row.Kind);
        Assert.Null(row.Binding!.Write);
        Assert.NotNull(row.PressAsync);
    }

    /// <summary>
    /// With no host behind it the row is still declared and simply has no button, which is the state
    /// under the designer.
    /// </summary>
    [Fact]
    public void WithNoHostThereIsNoButtonAndStillARow()
    {
        var row = Assert.Single(
            ShipsCapability.Create().Settings,
            candidate => candidate.Key == ShipsCapability.RescanKey);

        Assert.Null(row.PressAsync);
        Assert.Null(row.PressLabel);
    }
}
