using D47.Core.Storage;
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
[Trait("Category", "Integration")]
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

    private SpendDialog Dialog(out SpendLedger ledger, DateTimeOffset? launchedAt)
    {
        ledger = new SpendLedger(
            Path.Combine(_root, "spend.jsonl"), new MemoryFileSystem(),
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

        return new SpendDialog(
            null,
            new SpendTracker(),
            new SpeechSpend(),
            ledger,
            TestSurface.Settings().Current,
            TimeZoneInfo.Utc,
            launchedAt);
    }

    private static InlinePicker? Reset(Control control) =>
        control.GetVisualDescendants().OfType<InlinePicker>().FirstOrDefault(p => p.Name == "SpendReset");

    private static List<string> Offered(InlinePicker picker)
    {
        picker.IsOpen = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return [.. picker.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains(InlinePicker.OptionClass))
            .Select(button => $"{((TextBlock)button.Content!).Text}")];
    }

    private static string Words(Control control) =>
        string.Join(
            "\n",
            control.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    [AvaloniaFact]
    public void ThePickerIsThereWhenTheWindowKnowsWhenTheProcessStarted()
    {
        var dialog = Dialog(out _, Noon.AddHours(-1));
        var window = dialog.Show();

        Assert.NotNull(Reset(dialog));

        window.Close();
    }

    /// <summary>
    /// And absent otherwise. "This session" is a span the ledger can only be asked about with a launch
    /// instant, so a window without one would offer a reset it could not describe.
    /// </summary>
    [AvaloniaFact]
    public void AndAbsentOnAWindowThatDoesNot()
    {
        var dialog = Dialog(out _, launchedAt: null);
        var window = dialog.Show();

        Assert.Null(Reset(dialog));

        window.Close();
    }

    /// <summary>Exactly the five windows the figures list shows, plus the session.</summary>
    [AvaloniaFact]
    public void ItOffersTheWindowsTheDialogAlreadyShowsPlusTheSession()
    {
        var dialog = Dialog(out _, Noon.AddHours(-1));
        var window = dialog.Show();

        var offered = Offered(Reset(dialog)!);

        Assert.Equal(
            ["This session", "Today", "This week", "Last 7 days", "This month", "Last 30 days"],
            offered);

        Assert.DoesNotContain(offered, name => name.Contains("31", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void TheDialogHoldsNoPopupAndNoFlyout()
    {
        var dialog = Dialog(out _, Noon.AddHours(-1));
        var window = dialog.Show();

        var parts = dialog.GetVisualDescendants().ToList();

        Assert.DoesNotContain(parts, part => part is Avalonia.Controls.Primitives.Popup);
        Assert.DoesNotContain(parts, part => part is Button { Flyout: not null });
        Assert.DoesNotContain(parts, part => part is MenuItem);
        Assert.DoesNotContain(parts, part => part is Button { Name: "SpendReset" });

        window.Close();
    }

    [AvaloniaFact]
    public void PickingAWindowAsksAboutThatWindowAndConfirmingResetsIt()
    {
        var dialog = Dialog(out var ledger, Noon.AddHours(-1));
        var window = dialog.Show();

        Assert.Contains("1.4180", Words(dialog), StringComparison.Ordinal);

        var picker = Reset(dialog)!;
        picker.IsOpen = true;
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var today = picker.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains(InlinePicker.OptionClass)
                && Avalonia.Automation.AutomationProperties.GetName(button) == "Today");

        today.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var ask = Assert.Single(window.Modals().OfType<ConfirmDialog>());

        Assert.Contains("today", Words(ask), StringComparison.Ordinal);

        ask.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Reset")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("1.4180", Words(dialog), StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>The figures on screen move.</summary>
    [AvaloniaFact]
    public void TheFiguresRedrawAfterAReset()
    {
        var dialog = Dialog(out var ledger, Noon.AddHours(-1));
        var window = dialog.Show();

        Assert.Contains("1.4180", Words(dialog), StringComparison.Ordinal);

        // The reset itself, without the dialog: what is under test here is that the window notices, and
        // driving a modal confirmation headlessly would be testing ConfirmDialog.
        ledger.Reset(D47.Core.Conversation.SpendPeriods.Today(Noon, TimeZoneInfo.Utc));

        typeof(SpendDialog)
            .GetMethod("Draw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(dialog, null);

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var after = Words(dialog);

        Assert.DoesNotContain("1.4180", after, StringComparison.Ordinal);
        Assert.Contains("nothing yet", after, StringComparison.Ordinal);

        window.Close();
    }

    /// <summary>Close survives the new row, which is the button that was there first.</summary>
    [AvaloniaFact]
    public void CloseIsStillThereBesideIt()
    {
        var dialog = Dialog(out _, Noon.AddHours(-1));
        var window = dialog.Show();

        Assert.Contains(
            dialog.GetVisualDescendants().OfType<Button>(),
            button => $"{button.Content}" == "Close");

        window.Close();
    }
}
