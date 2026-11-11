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

/// <summary>In a narrow pane the Story bench list keeps its Story column and drops Picked and Chapters; at full width all six heads show.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryBenchListKeepsItsStoryColumnTests
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

    private static PanelView Build(double width)
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
        new Window { Content = panel, Width = width, Height = 900 }.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return panel;
    }

    private static List<string> Texts(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(text => text.IsEffectivelyVisible).Select(text => text.Text ?? string.Empty)];

    [AvaloniaFact]
    public void ANarrowPaneLeavesOutPickedAndChaptersAndKeepsTheTitleReadable()
    {
        var panel = Build(640);
        var texts = Texts(panel);

        Assert.DoesNotContain("PICKED", texts);
        Assert.DoesNotContain("CHAPTERS", texts);
        Assert.Contains("LAST LINE", texts);

        var title = panel.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Active Story");
        Assert.True(title.Bounds.Width >= 160, $"title drawn {title.Bounds.Width}px wide");
        Assert.True(title.Bounds.Height < 40);

        var none = panel.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == StoryBenchPage.NoStoryLine);
        Assert.True(none.Bounds.Height < 2.5 * none.FontSize * 1.5, $"No story line is {none.Bounds.Height}px tall");
    }

    [AvaloniaFact]
    public void TheFullWidthShowsAllSixHeads()
    {
        var texts = Texts(Build(1400));

        foreach (var head in new[] { "STORY", "LENGTH", "CHAPTERS", "CLUES", "PICKED", "LAST LINE" })
        {
            Assert.Contains(head, texts);
        }
    }
}
