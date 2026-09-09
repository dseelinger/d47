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

/// <summary>Resetting the cost figures from the Details dialog.</summary>
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
    /// And absent otherwise. "This session" is a span the ledger can only be asked about with a launch
    /// instant, so a window without one would offer a reset it could not describe.
    /// </summary>
    [AvaloniaFact]
    public void AndAbsentOnAWindowThatDoesNot()
    {
        var window = Dialog(out _, launchedAt: null);
        window.Show();

        Assert.Null(Reset(window));

        window.Close();
    }

    /// <summary>Exactly the five windows the figures list shows, plus the session.</summary>
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
            ["This session", "Today", "This week", "Last 7 days", "This month", "Last 30 days"],
            offered);

        Assert.DoesNotContain(offered, name => name.Contains("31", StringComparison.Ordinal));

        window.Close();
    }

    /// <summary>The figures on screen move.</summary>
    [AvaloniaFact]
    public void TheFiguresRedrawAfterAReset()
    {
        var window = Dialog(out var ledger, Noon.AddHours(-1));
        window.Show();

        Assert.Contains("1.4180", Words(window), StringComparison.Ordinal);

        // The reset itself, without the dialog: what is under test here is that the window notices, and
        // driving a modal confirmation headlessly would be testing ConfirmWindow.
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
