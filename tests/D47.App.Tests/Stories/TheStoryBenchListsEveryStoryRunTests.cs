using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests.Stories;

/// <summary>The Story bench lists the Commander's stock-story runs in four groups and switches between them.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryBenchListsEveryStoryRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 20, 0, 0, TimeSpan.Zero);

    private sealed record Rig(PanelView Panel, StoryStore Stories, StoryDirector Director);

    private static Story Run(string id, string title, StoryState state, int day, DateTimeOffset? stopped = null) => new()
    {
        Id = id,
        Title = title,
        PublicLayer = "-",
        Length = StoryPacing.OneMonth.Key,
        PickedAt = Now.AddDays(-day),
        State = state,
        StoppedAt = stopped,
        Chapters = ["one", "two"],
        CluesGiven = 3,
    };

    private static Rig Build(Action<StoryStore> fill, Func<IFileSystem, string, StoryStore, StoryRunRecord?>? record = null)
    {
        var paths = new AppPaths(TempFolders.Create("d47-story-bench-list"));
        paths.EnsureCreated();

        var files = new DiskFileSystem();
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), files, NullLogger<StoryStore>.Instance);
        var adventures = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), files, NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(adventures, NullLogger<AdventureBook>.Instance);
        adventures.Changed += book.Reconcile;

        var generator = AdventureFixture.Generator();
        var director = new StoryDirector(stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        fill(stories);
        director.Records = record?.Invoke(files, Path.Combine(paths.Data, "story-runs"), stories);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => null, () => { },
            Stories: director);
        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);
        new Window { Content = panel, Width = 1280, Height = 900 }.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Rig(panel, stories, director);
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Inlines is { Count: > 0 } inlines ? inlines.Text ?? string.Empty : block.Text ?? string.Empty)];

    private static Button? Find(PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == name && button.IsEffectivelyVisible);

    private static Button? Labelled(PanelView panel, string label) =>
        panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.IsEffectivelyVisible && button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == label));

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

    private static void FillEveryState(StoryStore stories)
    {
        stories.Save("F1", Run("a", "Active Story", StoryState.Paused, 1));
        stories.Save("F1", Run("b", "Shelved Story", StoryState.Shelved, 2));
        stories.Save("F1", Run("c", "Finished Story", StoryState.Finished, 3, Now.AddDays(-1)));
        stories.Save("F1", Run("d", "Abandoned Story", StoryState.Abandoned, 4, Now.AddDays(-2)));
        stories.Save("F1", Run("e", "Ended Story", StoryState.Ended, 5, Now.AddDays(-3)));
    }

    [AvaloniaFact]
    public void TheSubTabIsThereWithTheBenchOffAndFollowsShortStory()
    {
        var rig = Build(FillEveryState);

        var bar = rig.Panel.Nav.Roots(PanelTab.Stories).Select(root => root.Key).ToList();

        Assert.True(bar.IndexOf(StoryBenchPage.RootKey) > bar.IndexOf(AdventuresPage.AskKey));
    }

    [AvaloniaFact]
    public void EveryRunIsListedUnderItsOwnGroup()
    {
        var rig = Build(FillEveryState);
        var drawn = Drawn(rig.Panel);

        foreach (var group in new[] { "Active", "Shelved", "Finished", "Stopped" })
        {
            Assert.Contains(drawn, text => string.Equals(text, group, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var title in new[] { "Active Story", "Shelved Story", "Finished Story", "Abandoned Story", "Ended Story" })
        {
            Assert.Contains(title, drawn);
        }

        Assert.Contains(drawn, text => text.StartsWith("Abandoned 20", StringComparison.Ordinal));
        Assert.Contains(drawn, text => text.StartsWith("Ended 20", StringComparison.Ordinal));
        Assert.Contains(drawn, text => text.StartsWith("Finished 20", StringComparison.Ordinal));
        Assert.Contains("3 / " + StoryPacing.OneMonth.ClueDays.Count, drawn);
        Assert.Contains(StoryBenchPage.NoLine, drawn);
        Assert.Contains(StoryBenchPage.NoStoryName, drawn);
        Assert.DoesNotContain(StoryBenchPage.BenchFlag, drawn);
        Assert.Null(Find(rig.Panel, "Jump"));
    }

    [AvaloniaFact]
    public void AShortStoryIsNotListed()
    {
        var rig = Build(FillEveryState);

        Assert.DoesNotContain("The Lantern Route", Drawn(rig.Panel));
        Assert.Equal(5, rig.Stories.Runs("F1").Count);
    }

    [AvaloniaFact]
    public void WithNoRunsThePageOffersTheCatalogue()
    {
        var rig = Build(_ => { });

        Assert.Contains(StoryBenchPage.NoRuns, Drawn(rig.Panel));

        Press(Find(rig.Panel, "OpenCatalogue")!);

        Assert.Equal(StoriesView.RootKey, rig.Panel.Nav.Roots(PanelTab.Stories).First(root => root.Key == StoriesView.RootKey).Key);
    }

    [AvaloniaFact]
    public void SwitchingFromThePageShelvesTheActiveRunAndPausesTheOther()
    {
        var rig = Build(stories =>
        {
            stories.Save("F1", Run("other", "Other Story", StoryState.Running, 1));
            stories.Save("F1", Run(StoryFixture.Story.Id, StoryFixture.Story.Title, StoryState.Shelved, 2));
        });

        Press(Find(rig.Panel, "SwitchTo")!);

        var confirm = Labelled(rig.Panel, "Switch");
        Assert.NotNull(confirm);
        Assert.Contains(Drawn(rig.Panel), text => text.StartsWith("Switch to " + StoryFixture.Story.Title, StringComparison.Ordinal));

        Press(confirm!);
        Settle(() => rig.Stories.Find("F1", StoryFixture.Story.Id)!.State != StoryState.Shelved);

        Assert.Equal(StoryState.Shelved, rig.Stories.Find("F1", "other")!.State);
        Assert.Equal(StoryState.Paused, rig.Stories.Find("F1", StoryFixture.Story.Id)!.State);
    }

    [AvaloniaFact]
    public void NoStoryShelvesTheActiveRunAndLeavesNoneActive()
    {
        var rig = Build(stories => stories.Save("F1", Run("other", "Other Story", StoryState.Running, 1)));

        Press(Find(rig.Panel, "SwitchToNoStory")!);
        Press(Labelled(rig.Panel, "Switch")!);
        Settle(() => rig.Stories.Current("F1") is null);

        Assert.Null(rig.Stories.Current("F1"));
        Assert.Equal(StoryState.Shelved, rig.Stories.Find("F1", "other")!.State);
    }

    [AvaloniaFact]
    public void CancellingTheConfirmationChangesNothing()
    {
        var rig = Build(stories =>
        {
            stories.Save("F1", Run("other", "Other Story", StoryState.Running, 1));
            stories.Save("F1", Run(StoryFixture.Story.Id, StoryFixture.Story.Title, StoryState.Shelved, 2));
        });

        Press(Find(rig.Panel, "SwitchTo")!);
        Press(Labelled(rig.Panel, "Cancel")!);

        Assert.Equal(StoryState.Running, rig.Stories.Find("F1", "other")!.State);
        Assert.Equal(StoryState.Shelved, rig.Stories.Find("F1", StoryFixture.Story.Id)!.State);
    }

    [AvaloniaFact]
    public void LastLineIsTheNewestLineOfTheRunsRecord()
    {
        var run = Run("a", "Active Story", StoryState.Paused, 1);
        var newest = new DateTimeOffset(2026, 11, 4, 21, 12, 0, TimeSpan.Zero);

        var rig = Build(
            stories => stories.Save("F1", run),
            (files, folder, stories) =>
            {
                var record = new StoryRunRecord(files, folder, stories, () => "F1", null, new DpapiSecretProtector(), NullLogger.Instance);
                var path = Path.Combine(folder, StoryRunRecord.FolderName("F1", run.Id, run.PickedAt), StoryRunRecord.LinesFile);
                files.AppendText(path, System.Text.Json.JsonSerializer.Serialize(new { sent = newest.AddHours(-2), from = "x", key = "k1" }) + "\n");
                files.AppendText(path, System.Text.Json.JsonSerializer.Serialize(new { sent = newest, from = "x", key = "k2" }) + "\n");
                return record;
            });

        Assert.Contains(StoryBenchPage.Minute(newest), Drawn(rig.Panel));
        Assert.DoesNotContain(StoryBenchPage.NoLine, Drawn(rig.Panel));
    }
}
