using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.Core;
using D47.Core.Audio;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Resetting the cost figures from the Details dialog
/// (<a href="https://github.com/dseelinger/d47/issues/197">#197</a>).
/// <para>
/// The arithmetic is asserted in Core, where a month of spending can be written in a millisecond.
/// What is asserted here is the control: that the button is there on a window that knows when the
/// process started and absent on one that does not, that it offers exactly the windows the figures
/// list shows plus the session, and that the numbers on screen move when one is taken.
/// </para>
/// <para>
/// <b>It is the one eraser in the app that asks first.</b> Every other one is an <c>Info</c>
/// settings row with a <c>Press</c> and no confirmation, and their safety is that the tool surface
/// cannot reach them. This one sits where the numbers are, which is the right place and also
/// somewhere a stray click reaches.
/// </para>
/// </summary>
public class TheDetailsDialogResetsTheFiguresTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "d47-spend-dialog-tests",
        Guid.NewGuid().ToString("n"));

    private static readonly DateTimeOffset Noon = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class StoppedClock(DateTimeOffset at) : IWallClock
    {
        public DateTimeOffset UtcNow => at;
    }

    public TheDetailsDialogResetsTheFiguresTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private SpendWindow Dialog(out SpendLedger ledger, DateTimeOffset? launchedAt)
    {
        ledger = new SpendLedger(
            Path.Combine(_root, "spend.jsonl"),
            new StoppedClock(Noon),
            NullLogger.Instance);

        ledger.Append(new SpendEntry
        {
            At = Noon.AddHours(-2),
            Kind = SpendKind.Model,
            ProviderId = "anthropic",
            Model = "claude-opus-5",
            Dollars = 1.4180m,
            Priced = true,
        });

        return new SpendWindow(
            null,
            new SpendTracker(),
            new SpeechSpend(),
            ledger,
            TestSurface.Settings().Current,
            TimeZoneInfo.Utc,
            launchedAt);
    }

    private static Button? Reset(Window window) =>
        window.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => b.Name == "SpendReset");

    private static string Words(Window window) =>
        string.Join(
            "\n",
            window.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    [AvaloniaFact]
    public void TheButtonIsThereWhenTheWindowKnowsWhenTheProcessStarted()
    {
        var window = Dialog(out _, Noon.AddHours(-1));
        window.Show();

        Assert.NotNull(Reset(window));

        window.Close();
    }

    /// <summary>
    /// And absent otherwise. "This session" is a span the ledger can only be asked about with a
    /// launch instant, so a window without one would offer a reset it could not describe.
    /// </summary>
    [AvaloniaFact]
    public void AndAbsentOnAWindowThatDoesNot()
    {
        var window = Dialog(out _, launchedAt: null);
        window.Show();

        Assert.Null(Reset(window));

        window.Close();
    }

    /// <summary>
    /// <b>Exactly the five windows the figures list shows, plus the session.</b> The ask named a
    /// 31-day option; the Commander settled it at thirty on 2026-08-30, because the reason 31 was
    /// wanted is what <c>This month</c> already does — and a reset list offering a span the figures
    /// list does not show would be two lists disagreeing about what a window is.
    /// </summary>
    [AvaloniaFact]
    public void ItOffersTheWindowsTheDialogAlreadyShowsPlusTheSession()
    {
        var window = Dialog(out _, Noon.AddHours(-1));
        window.Show();

        var flyout = Assert.IsType<MenuFlyout>(Reset(window)!.Flyout);

        var offered = flyout.Items
            .OfType<MenuItem>()
            .Select(item => $"{item.Header}")
            .ToList();

        Assert.Equal(
            ["This session", "Today", "Last 7 days", "Last 30 days", "This week", "This month"],
            offered);

        Assert.DoesNotContain(offered, name => name.Contains("31", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>
    /// <b>The figures on screen move.</b> Every one of them is a query, so redrawing is the whole
    /// of showing the new answer — and a window that still read the old totals after a reset would
    /// be the feature visibly not working.
    /// </summary>
    [AvaloniaFact]
    public void TheFiguresRedrawAfterAReset()
    {
        var window = Dialog(out var ledger, Noon.AddHours(-1));
        window.Show();

        Assert.Contains("1.4180", Words(window), StringComparison.Ordinal);

        // The reset itself, without the dialog: what is under test here is that the window
        // notices, and driving a modal confirmation headlessly would be testing ConfirmWindow.
        ledger.Reset(D47.Core.Conversation.SpendPeriods.Today(Noon, TimeZoneInfo.Utc));

        typeof(SpendWindow)
            .GetMethod("Draw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(window, null);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var after = Words(window);

        Assert.DoesNotContain("1.4180", after, StringComparison.Ordinal);
        Assert.Contains("nothing yet", after, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>Close survives the new row, which is the button that was there first.</summary>
    [AvaloniaFact]
    public void CloseIsStillThereBesideIt()
    {
        var window = Dialog(out _, Noon.AddHours(-1));
        window.Show();

        Assert.Contains(
            window.GetVisualDescendants().OfType<Button>(),
            button => $"{button.Content}" == "Close");

        window.Close();
    }
}
