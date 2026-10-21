using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Checklists;
using D47.Core.Goals;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The open Powerplay goal reads its figures, close-up and benefits, on Slab and within 900px (#1470).</summary>
[Trait("Category", "Integration")]
public class TheOpenPowerplayGoalIsDrawnFromItsFiguresTests
{
    private static readonly DateTimeOffset Now = new(3311, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static (Window Window, PanelView Panel) Open(string fields, double width)
    {
        var paths = new D47.Core.AppPaths(TestSurface.MemoryFolder("d47-powerplay-goal-tests"));

        var checklists = new ChecklistService(
            new ChecklistStore(Path.Combine(paths.Data, "checklist.json"), new MemoryFileSystem(), NullLogger<ChecklistStore>.Instance),
            new ChecklistProposalStore(
                Path.Combine(paths.Data, "checklist-proposals.json"),
                new MemoryFileSystem(),
                NullLogger<ChecklistProposalStore>.Instance),
            () => null);

        var state = new CommanderGameState(new CommanderIdentity("F1", "Jameson"));
        var text = $"{{ \"timestamp\":\"{Now.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}\", \"event\":\"Powerplay\", {fields} }}";
        state.Apply(new JournalEvent(Now, "Powerplay", JsonDocument.Parse(text).RootElement));

        var goals = new GoalBook(
            new GoalStore(Path.Combine(paths.Data, "goals.json"), new MemoryFileSystem(), NullLogger<GoalStore>.Instance),
            () => "F1",
            () => state,
            checklists);

        var panel = new PanelView { DataContext = new PanelViewModel() };
        panel.EnableChecklist(checklists, goals);

        var window = new Window { Content = panel, Width = width, Height = 1400 };
        window.Show();
        panel.Tab = PanelTab.Commander;
        Dispatcher.UIThread.RunJobs();

        panel.GetVisualDescendants().OfType<CheckBox>()
            .First(box => box.Content is string label && label.StartsWith("Goals", StringComparison.Ordinal))
            .IsChecked = true;

        Dispatcher.UIThread.RunJobs();

        var head = panel.GetVisualDescendants().OfType<Border>()
            .First(border => AutomationProperties.GetName(border) == "Powerplay rank");

        var middle = head.TranslatePoint(new Point(head.Bounds.Width / 2, head.Bounds.Height / 2), window)!.Value;
        window.MouseDown(middle, MouseButton.Left);
        window.MouseUp(middle, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static List<string> Words(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty).ToList();

    private const string LiYongRuiAtEight = "\"Power\":\"Li Yong-Rui\",\"Rank\":8,\"Merits\":45240,\"TimePledged\":900";

    [AvaloniaFact]
    public void TheFiguresCloseupAndBenefitsReadForRankEight()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, panel) = Open(LiYongRuiAtEight, 1280);
        var words = Words(panel);

        Assert.Contains("45,240", words);
        Assert.Contains("92", words);
        Assert.Contains("78%", words);
        Assert.Contains("9 · 78%", words);
        Assert.Contains("+20% exploration data sales", words);
        Assert.Contains("THIS CHAPTER · RANKS 2–10", words);
        Assert.Contains("RANKS 9–10 · NOW", words);
        Assert.DoesNotContain(words, word => word.StartsWith("RANK 9 · ", StringComparison.Ordinal));
        Assert.DoesNotContain(words, word => word.Contains("Ticks mark", StringComparison.Ordinal));

        var show = panel.GetVisualDescendants().OfType<Button>().Single(b => (b.Content as string)?.StartsWith("SHOW RANKS 31–100", StringComparison.Ordinal) == true);
        Assert.Equal("SHOW RANKS 31–100 · 7 MORE CHAPTERS", show.Content);

        window.Close();
    }

    [AvaloniaFact]
    public void PressingShowOpensTheRemainingChapters()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, panel) = Open(LiYongRuiAtEight, 1280);

        Assert.DoesNotContain(Words(panel), word => word.StartsWith("RANKS 91–100", StringComparison.Ordinal));

        panel.GetVisualDescendants().OfType<Button>()
            .Single(b => (b.Content as string)?.StartsWith("SHOW RANKS", StringComparison.Ordinal) == true)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Words(panel), word => word.StartsWith("RANKS 31–40", StringComparison.Ordinal));
        Assert.Contains(Words(panel), word => word.StartsWith("RANKS 91–100", StringComparison.Ordinal));

        window.Close();
    }

    [AvaloniaFact]
    public void AnUnknownMeritTotalReadsNotKnownYetAndDrawsNoPartialFill()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, panel) = Open("\"Power\":\"Li Yong-Rui\",\"Rank\":8,\"TimePledged\":900", 1280);
        var words = Words(panel);

        Assert.Equal(2, words.Count(word => word == "not known yet"));
        Assert.DoesNotContain(words, word => word.StartsWith("9 · ", StringComparison.Ordinal));
        Assert.Contains("9", words);

        window.Close();
    }

    [AvaloniaFact]
    public void TheOpenGoalIsOnSlabAndNoWiderThanNineHundred()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, panel) = Open(LiYongRuiAtEight, 1600);

        var card = panel.GetVisualDescendants().OfType<StackPanel>()
            .First(stack => AutomationProperties.GetName(stack) == "Powerplay rank");

        Assert.True(card.Bounds.Width <= 900, $"The goal is {card.Bounds.Width}px wide.");

        var ground = card.Children.OfType<Border>().Last();
        Assert.Equal(Ink(ThemeManager.SlabKey), (ground.Background as ISolidColorBrush)?.Color);
        Assert.DoesNotContain(Words(card), word => word.StartsWith("Rank 8 ", StringComparison.Ordinal) && word.Contains('%'));

        window.Close();
    }

    [AvaloniaFact]
    public void TheOpenGoalIsCaptured()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var (window, _) = Open(LiYongRuiAtEight, 1280);
        Dispatcher.UIThread.RunJobs();

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("goal-powerplay-open.png");
        }

        window.Close();
    }

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Application.Current!.Resources[key]!).Color;
}
