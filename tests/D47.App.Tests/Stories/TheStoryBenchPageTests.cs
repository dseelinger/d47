using Avalonia.Controls;
using Avalonia.Headless;
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

/// <summary>The Story bench page: present only while the bench is on, and each action redraws the Stories pages.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryBenchPageTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 20, 0, 0, TimeSpan.Zero);

    private sealed record Rig(PanelView Panel, StoryBench? Bench, StoryStore Stories, StoryDirector Director, AdventureBook Book);

    private static Rig Build(bool benchOn)
    {
        var paths = new AppPaths(TempFolders.Create("d47-story-bench-page"));
        paths.EnsureCreated();

        var files = new DiskFileSystem();
        var catalog = new StoryCatalog([StoryFixture.Story], () => [StoryFixture.Secret]);
        var mine = StorySlotFiles.In(paths.Data);
        var stories = StoryStore.Open(mine.Stories, files, NullLogger<StoryStore>.Instance);
        var adventures = new AdventureStore(mine.Adventures, files, NullLogger<AdventureStore>.Instance);
        var archive = StoryChapterArchive.Open(files, mine.Chapters, NullLogger<StoryChapterArchive>.Instance);
        var book = new AdventureBook(adventures, NullLogger<AdventureBook>.Instance);
        adventures.Changed += book.Reconcile;

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Archive = archive,
        };

        stories.Save("F1", new Story
        {
            Id = StoryFixture.Story.Id,
            Title = StoryFixture.Story.Title,
            PublicLayer = "-",
            Length = StoryPacing.OneMonth.Key,
            PickedAt = Now,
            BeaconScanAt = Now,
            Chapters = ["one"],
        });

        var bench = benchOn
            ? new StoryBench(paths.Data, files, stories, adventures, archive, NullLogger<StoryBench>.Instance) { Director = director }
            : null;

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director, Bench: bench);
        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);
        new Window { Content = panel, Width = 1280, Height = 860 }.Show();

        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        return new Rig(panel, bench, stories, director, book);
    }

    private static AdventureBeat Beat(string title, string function, long address) => new()
    {
        Title = title,
        Function = function,
        Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = address, System = title },
        Line = "Said.",
    };

    private static void BeginShortStory(Rig rig)
    {
        rig.Book.Write("F1", new Adventure
        {
            Key = "the-lantern-route",
            Name = "The Lantern Route",
            Source = AdventureSource.Generated,
            Opening = "Begins.",
            Beats = [Beat("The Lantern", "setup", 1001), Beat("The Survey", "catalyst", 1002)],
        });
        Assert.Null(rig.Book.Begin("F1", "the-lantern-route", Now));
        rig.Book.Drain();
        rig.Book.Told("F1", "the-lantern-route", new AdventureTold { Kind = AdventureToldKind.Beat, Text = "Begins.", At = Now });
        rig.Book.CatchUp([]);
    }

    [AvaloniaFact]
    public void WithNothingUnderWayTheBeatsSectionSaysSo()
    {
        var rig = Build(benchOn: true);

        rig.Panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(StoryBenchPage.NoBeatsNotice, Drawn(rig.Panel));
        Assert.Null(Find(rig.Panel, "NextBeat"));
    }

    [AvaloniaFact]
    public void AShortStoryListsItsBeatsAndNextBeatStepsTheWaitingOne()
    {
        var rig = Build(benchOn: true);
        BeginShortStory(rig);

        rig.Panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        var drawn = Drawn(rig.Panel);
        Assert.Contains(drawn, text => text.StartsWith("The Lantern Route · Short Story", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(drawn, text => text.StartsWith("setup · The Lantern · Waiting", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(drawn, text => string.Equals(text, "catalyst · The Survey", StringComparison.OrdinalIgnoreCase));

        Press(Find(rig.Panel, "NextBeat")!);
        Settle(() => rig.Book.Standing("F1", "the-lantern-route")!.Current == 1);

        Assert.Equal(1, rig.Book.Standing("F1", "the-lantern-route")!.Current);
        Assert.True(rig.Book.IsStirring("F1", "the-lantern-route"));
        Assert.False(Find(rig.Panel, "NextBeat")!.IsEnabled);
        Assert.Contains(Drawn(rig.Panel), text => text.StartsWith("setup · The Lantern · Fired ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Drawn(rig.Panel), text => text.StartsWith("catalyst · The Survey · Waiting", StringComparison.OrdinalIgnoreCase));

        rig.Book.Told("F1", "the-lantern-route", new AdventureTold { Kind = AdventureToldKind.Beat, Text = "Said.", At = Now });
        Settle(() => Find(rig.Panel, "NextBeat")?.IsEnabled == true);

        Assert.True(Find(rig.Panel, "NextBeat")!.IsEnabled);
    }

    private static Button? Find(PanelView panel, string name) =>
        panel.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == name && button.IsEffectivelyVisible);

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Inlines is { Count: > 0 } inlines ? inlines.Text ?? string.Empty : block.Text ?? string.Empty)];

    private static void Press(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

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
    public void WithTheBenchOffThereIsNoWayToThePage()
    {
        var rig = Build(benchOn: false);

        Assert.DoesNotContain(rig.Panel.Nav.Roots(PanelTab.Stories), root => root.Key == StoryBenchPage.RootKey);
        Assert.DoesNotContain(Drawn(rig.Panel), text => text.Contains("Story bench", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void WithTheBenchOnTheSubTabOpensThePage()
    {
        var rig = Build(benchOn: true);

        rig.Panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Assert.NotNull(Find(rig.Panel, "NewSlot"));
        Assert.Contains(Drawn(rig.Panel), text => text.StartsWith("My story (active)", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void SwitchingAndGoingBackRedrawTheSlotList()
    {
        var rig = Build(benchOn: true);
        Assert.Null(rig.Bench!.Create("test"));

        rig.Panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        Press(Find(rig.Panel, "SwitchTo")!);
        Settle(() => rig.Bench.Active == "test");
        Assert.Contains(Drawn(rig.Panel), text => text.StartsWith("test (active)", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("Back to my story", Find(rig.Panel, "SwitchTo")!.Content);

        Press(Find(rig.Panel, "SwitchTo")!);
        Settle(() => rig.Bench.Active is null);
        Assert.Contains(Drawn(rig.Panel), text => text.StartsWith("My story (active)", StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void AJumpMovesTheStoryAndTheDropdownFollows()
    {
        var rig = Build(benchOn: true);

        rig.Panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, rig.Stories.Current("F1")!.CluesGiven);

        var stepper = rig.Panel.GetVisualDescendants().OfType<Control>().Last(control => control is D47.App.Controls.IChoiceControl && control.IsEffectivelyVisible);
        ((D47.App.Controls.IChoiceControl)stepper).SelectedIndex = 2;

        Press(Find(rig.Panel, "Jump")!);
        Settle(() => rig.Stories.Current("F1")!.CluesGiven == 2);

        Assert.True(rig.Stories.Current("F1")!.CluesGiven == 2, string.Join(" | ", Drawn(rig.Panel)));
        Assert.Equal($"Clue 3 — day {StoryPacing.OneMonth.ClueDays[2]}", StoryBenchPage.JumpChoices(rig.Stories.Current("F1")!)[0].Label);
    }

    [AvaloniaFact]
    public void TheRewindButtonRestoresACheckpointAndPausesTheStory()
    {
        var rig = Build(benchOn: true);
        var id = StoryFixture.Story.Id;

        rig.Stories.Update("F1", id, story => story with { CluesGiven = 1 });
        Settle(() => rig.Bench!.Checkpoints("F1").Count == 1);
        rig.Stories.Update("F1", id, story => story with { CluesGiven = 2 });

        rig.Panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Drawn(rig.Panel), text => text.StartsWith("Chapter 1 — ", StringComparison.OrdinalIgnoreCase));

        Press(Find(rig.Panel, "Rewind")!);
        Settle(() => rig.Stories.Current("F1")!.State == StoryState.Paused);

        Assert.Equal(StoryState.Paused, rig.Stories.Current("F1")!.State);
        Assert.Equal(1, rig.Stories.Current("F1")!.CluesGiven);
    }

    [AvaloniaFact]
    public void ABenchWithAPartSteppedChapterIsCaptured()
    {
        var rig = Build(benchOn: true);
        BeginShortStory(rig);
        Assert.Null(rig.Bench!.Step("F1", "the-lantern-route", Now.AddMinutes(5)));
        rig.Book.Told("F1", "the-lantern-route", new AdventureTold { Kind = AdventureToldKind.Beat, Text = "Said.", At = Now });

        rig.Panel.Nav.SelectRoot(StoryBenchPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        using var frame = ((Window)TopLevel.GetTopLevel(rig.Panel)!).CaptureRenderedFrame()!;
        frame.SaveCapture("story-bench-beats.png");
    }

    [Fact]
    public void CluesAlreadyGivenAreNotOffered()
    {
        var story = new Story
        {
            Id = "s",
            Title = "S",
            PublicLayer = "-",
            Length = StoryPacing.OneMonth.Key,
            CluesGiven = 2,
        };

        var labels = StoryBenchPage.JumpChoices(story).Select(choice => choice.Label).ToList();
        var days = StoryPacing.OneMonth.ClueDays;

        Assert.Equal($"Clue 3 — day {days[2]}", labels[0]);
        Assert.DoesNotContain(labels, label => label.StartsWith("Clue 1 ", StringComparison.Ordinal) || label.StartsWith("Clue 2 ", StringComparison.Ordinal));
        Assert.Equal(StoryBenchPage.FinaleLabel, labels[^1]);
        Assert.Equal(days.Count - 2 + 1, labels.Count);
    }
}
