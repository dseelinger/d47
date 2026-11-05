using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>Finished and stopped runs offer Delete on the Story bench; the active run does not; a shelved run is abandoned first.</summary>
[Trait("Category", "Integration")]
public sealed class AFinishedRunIsDeletedFromTheStoryBenchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 20, 0, 0, TimeSpan.Zero);

    private static Story Run(string id, string title, StoryState state, int day) => new()
    {
        Id = id,
        Title = title,
        PublicLayer = "-",
        Length = StoryPacing.OneMonth.Key,
        PickedAt = Now.AddDays(-day),
        State = state,
        StoppedAt = state is StoryState.Running or StoryState.Paused or StoryState.Shelved ? null : Now.AddDays(-day + 1),
        Chapters = ["one"],
    };

    private static (PanelView Panel, StoryStore Stories) Build()
    {
        var paths = new AppPaths(TempFolders.Create("d47-story-bench-delete"));
        paths.EnsureCreated();

        var files = new DiskFileSystem();
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), files, NullLogger<StoryStore>.Instance);
        var adventures = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), files, NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(adventures, NullLogger<AdventureBook>.Instance);
        adventures.Changed += book.Reconcile;

        var generator = AdventureFixture.Generator();
        var director = new StoryDirector(stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Archive = StoryChapterArchive.Open(files, Path.Combine(paths.Data, "story-chapters.jsonl"), NullLogger<StoryChapterArchive>.Instance),
        };

        stories.Save("F1", Run("a", "Active Story", StoryState.Paused, 1));
        stories.Save("F1", Run("b", "Shelved Story", StoryState.Shelved, 2));
        stories.Save("F1", Run("c", "Finished Story", StoryState.Finished, 3));
        stories.Save("F1", Run("d", "Abandoned Story", StoryState.Abandoned, 4));
        director.Records = new StoryRunRecord(files, Path.Combine(paths.Data, "story-runs"), stories, () => "F1", null, new NoopProtector(), NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => null, () => { },
            Stories: director);
        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);
        new Window { Content = panel, Width = 1280, Height = 900 }.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return (panel, stories);
    }

    private static List<Button> Deletes(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "DeleteRun" && button.IsEffectivelyVisible)];

    private static Button? RunPageDelete(PanelView panel) =>
        panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "DeleteThisRun" && button.IsEffectivelyVisible);

    private static Button? Labelled(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(button =>
            button.IsEffectivelyVisible && (button.Content as string == label || button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label)));

    private static void Press(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle(Func<bool> done)
    {
        for (var attempt = 0; attempt < 200 && !done(); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(25);
        }

        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void OnlyFinishedAndStoppedRowsOfferDelete()
    {
        var (panel, _) = Build();

        Assert.Equal(2, Deletes(panel).Count);
    }

    [AvaloniaFact]
    public void DeleteRunAsksFirstAndThenRemovesTheRun()
    {
        var (panel, stories) = Build();

        Press(Deletes(panel)[0]);

        var drawn = panel.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();
        Assert.Contains(drawn, text => text.StartsWith("Delete this run of ", StringComparison.Ordinal));
        Assert.Equal(4, stories.For("F1").Count);

        Press(Labelled(panel, StoryRunDeletion.CancelLabel)!);
        Assert.Equal(4, stories.For("F1").Count);

        Press(Deletes(panel)[0]);
        Press(Labelled(panel, StoryRunDeletion.DeleteRunLabel)!);
        Settle(() => stories.For("F1").Count == 3);

        Assert.Equal(3, stories.For("F1").Count);
        Assert.Contains(stories.For("F1"), run => run.Id == "a");
    }

    [AvaloniaFact]
    public void AShelvedRunsPageAsksToAbandonItFirst()
    {
        var (panel, stories) = Build();

        Press(panel.GetVisualDescendants().OfType<Button>().Where(button => button.Name == "OpenRun" && button.IsEffectivelyVisible).ElementAt(1));
        Press(RunPageDelete(panel)!);

        var drawn = panel.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text ?? string.Empty).ToList();
        Assert.Contains(StoryRunDeletion.ShelvedFirstTitle, drawn);
        Assert.Equal(4, stories.For("F1").Count);

        Press(Labelled(panel, StoryRunDeletion.AbandonLabel)!);
        Settle(() => stories.For("F1").Any(run => run.Id == "b" && run.State == StoryState.Abandoned));

        Assert.Equal(StoryState.Abandoned, stories.For("F1").Single(run => run.Id == "b").State);
        Assert.Equal(4, stories.For("F1").Count);
    }

    [AvaloniaFact]
    public void TheActiveRunsPageOffersNoDelete()
    {
        var (panel, _) = Build();

        Press(panel.GetVisualDescendants().OfType<Button>().First(button => button.Name == "OpenRun" && button.IsEffectivelyVisible));

        Assert.Null(RunPageDelete(panel));
    }
}
