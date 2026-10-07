using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Journal;
using Xunit;

namespace D47.App.Tests;

public sealed class TheExplainButtonAsksAboutTheSelectedEventTests
{
    private static (PanelView Panel, PanelViewModel Model, Window Window) Shown()
    {
        var model = new PanelViewModel();
        var log = new JournalLog();

        log.Add([
            new(new DateTimeOffset(2026, 8, 27, 12, 3, 1, TimeSpan.Zero),
                "DockingDenied",
                System.Text.Json.JsonDocument.Parse("""{"event":"DockingDenied","Reason":"NoSpace"}""").RootElement),
        ]);

        model.JournalSource = noise => log.Read(noise);
        model.JournalDocumentSource = noise => log.Document(noise);

        var panel = new PanelView { DataContext = model };
        panel.EnableRawJournal();

        var window = new Window { Content = panel, Width = 1180, Height = 800 };
        window.Show();
        panel.Page = TranscriptPage.Journal;
        panel.ShowJournalDetail(true);
        Dispatcher.UIThread.RunJobs();

        return (panel, model, window);
    }

    private static Button Explain(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ExplainButton");

    [AvaloniaFact]
    public void PressingItSendsExplainThatAsATypedTurn()
    {
        var (panel, model, window) = Shown();
        var asked = new List<string>();

        model.AskRequested += () => asked.Add(model.AskText);

        Assert.True(Explain(panel).IsVisible);
        Assert.True(Explain(panel).IsEnabled);

        model.Explain();

        Assert.Equal(["Explain that"], asked);

        window.Close();
    }

    [AvaloniaFact]
    public void FoldingTheDetailPaneHidesTheButton()
    {
        var (panel, _, window) = Shown();

        panel.ShowJournalDetail(false);
        Dispatcher.UIThread.RunJobs();

        Assert.False(Explain(panel).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void TheRawReadingHasNoButton()
    {
        var (panel, _, window) = Shown();

        panel.Page = TranscriptPage.RawJournal;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Explain(panel).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void NothingSelectedOrATurnInFlightDisablesIt()
    {
        var (panel, model, window) = Shown();
        var asked = 0;

        model.AskRequested += () => asked++;

        model.JournalSelected = null;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Explain(panel).IsEnabled);

        model.Explain();
        Assert.Equal(0, asked);

        model.JournalSelected = model.Journal[0];
        model.CanAsk = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(Explain(panel).IsEnabled);

        window.Close();
    }
}
