using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.Core.Ticking;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A subscriber the tick loop has paused is a feature that has stopped running, so the Diagnostics card
/// names it rather than leaving it to the log (https://github.com/dseelinger/d47/issues/58).
/// </summary>
public class APausedTickSubscriberIsOnThePageTests
{
    private const string Label = "Paused after repeated failures";

    private static TickLoop Loop(string? broken)
    {
        var loop = new TickLoop(NullLogger<TickLoop>.Instance);

        if (broken is null)
        {
            return loop;
        }

        loop.Add(broken, _ => throw new InvalidOperationException("broken"));

        for (var tick = 0; tick < 10; tick++)
        {
            loop.Tick(DateTimeOffset.UnixEpoch.AddMilliseconds(100 * tick));
        }

        return loop;
    }

    private static SettingsHost Open(TickLoop ticking)
    {
        var (settings, viewState, paths, _, _) = TestSurface.CreateFull(ticking: ticking);
        var host = SettingsHost.Open(settings, viewState, paths);

        // Diagnostics is one of the cards that starts closed, and a row inside a closed card is drawn but
        // not visible.
        host.View.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Name == "ExpandAll")
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    private static IReadOnlyList<TextBlock> Captions(SettingsHost host) =>
        [.. host.View.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(block => block.IsEffectivelyVisible && block.Text == Label)];

    [AvaloniaFact]
    public void TheJournalSubscriberIsNamedOnTheCard()
    {
        var host = Open(Loop("journal"));

        Assert.Single(Captions(host));

        var values = host.View.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Text)
            .ToList();

        Assert.Contains("journal", values);

        host.Close();
    }

    /// <summary>Nothing paused, no row: a card that always carried it would say nothing by saying it.</summary>
    [AvaloniaFact]
    public void NothingIsDrawnWhileEverySubscriberIsRunning()
    {
        var host = Open(Loop(broken: null));

        Assert.Empty(Captions(host));

        host.Close();
    }
}
