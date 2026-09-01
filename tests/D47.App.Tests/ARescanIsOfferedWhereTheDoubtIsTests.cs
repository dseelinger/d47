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

/// <summary>
/// The Commander can rebuild what is remembered about their ships, on demand
/// (<a href="https://github.com/dseelinger/d47/issues/128">#128</a>).
/// <para>
/// <b>Asked for in these words:</b> <em>"rescan on demand rather than at startup — tell the
/// commander, not look right? Do a rescan."</em> Both halves are here, and the second is the one
/// that would be quietly dropped: a repair nobody can find is a repair nobody performs, and the
/// place a Commander doubts the data is the page drawing it rather than the settings card.
/// </para>
/// <para>
/// Driven through the real settings window rather than probed off the descriptor, for the reason
/// <c>LocalVoiceDownloadRowTests</c> records: a null host delegate makes its row <b>absent</b>, so
/// a probe of Core would pass on the day the button went missing.
/// </para>
/// </summary>
public class ARescanIsOfferedWhereTheDoubtIsTests
{
    private const string ButtonName = "Press_ships_remembered";
    private const string BarName = "Progress_ships_remembered";

    private static T Find<T>(Visual root, string name)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(found => found.Name == name);

    /// <summary>
    /// <b>The row is there, it says what is stored, and the button runs the rescan.</b> The row's
    /// own reading is the answer to <i>does this look right</i>, which is the question the press
    /// is the answer to — a button with nothing above it can only be answered by pressing it.
    /// </summary>
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

        // Shut and drawing, because a walk of a year of journals takes seconds and a surface that
        // said nothing is the defect the local voice download already had once.
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

    /// <summary>
    /// <b>The help says the sentence the Commander asked for, in their words.</b> Pinned because
    /// it is prose and prose is what gets rewritten by somebody tidying up — and because the whole
    /// point of the row is that a Commander who thinks the data is wrong finds it.
    /// </summary>
    [Fact]
    public void TheRowSaysNotLookRightRescan()
    {
        var row = Assert.Single(
            ShipsCapability.Create(surface: ShipsCapability.ShipsSurface.Inert).Settings,
            candidate => candidate.Key == ShipsCapability.RescanKey);

        Assert.Contains("Not look right? Rescan", row.Help, StringComparison.Ordinal);

        // And it says what the press does not touch, which is the reassurance that makes it
        // pressable: a repair nobody dares run is no repair.
        Assert.Contains("not read and not written", row.Help, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Info with a press, which is what keeps it off the tool surface.</b>
    /// <c>SettingsService.Apply</c> refuses a row with no binding to write, so a model cannot
    /// start minutes of disk reading with a sentence somebody put in a chat channel — and the row
    /// needs no protected flag of its own to say so.
    /// </summary>
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
    /// <b>With no host behind it the row is still declared and simply has no button</b>, which is
    /// the state under the designer. The row existing is what keeps its documentation page and its
    /// key real; the button is what needs an App.
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
