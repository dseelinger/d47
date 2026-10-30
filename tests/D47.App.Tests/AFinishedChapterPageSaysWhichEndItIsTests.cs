using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Journal;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A finished chapter's reading page ends with "End of chapter.", and with "The end." once its story finishes on it,
/// without the page being opened again. Captures go to <see cref="TestSurface.CaptureDirectory"/>.
/// </summary>
[Trait("Category", "Integration")]
public class AFinishedChapterPageSaysWhichEndItIsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 20, 0, 0, TimeSpan.Zero);

    private sealed record Surface(Window Window, PanelView Panel, AdventureBook Book, StoryStore Stories);

    private static Adventure Chapter(string key, string name, string? storyId) => new()
    {
        Key = key,
        Name = name,
        Source = AdventureSource.Generated,
        StoryId = storyId,
        Beats =
        [
            new AdventureBeat
            {
                Title = "The Beacon",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 13872878396833, System = "IC 2391 Sector MX-T b3-6" },
                Line = "Scan it.",
            },
        ],
        AcceptedAt = Now.AddHours(-1),
    };

    private static Surface Open()
    {
        var paths = new AppPaths(TempFolders.Create("d47-chapter-end"));
        paths.EnsureCreated();

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var card = StoryFixture.Story;

        Assert.Null(book.Write("F1", Chapter("the-first-light", "The First Light", card.Id)));
        Assert.Null(book.Write("F1", Chapter("the-last-dock", "The Last Dock", storyId: null)));

        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), new DiskFileSystem(), NullLogger<StoryStore>.Instance);
        stories.Save("F1", new Story
        {
            Id = card.Id,
            Title = card.Title,
            PublicLayer = card.Describe(),
            Chapters = ["the-first-light"],
            PickedAt = Now.AddHours(-1),
        });

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);

        var director = new StoryDirector(
            stories, book, () => StoryFixture.Catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        Assert.True(JournalEvent.TryParse(
            """{ "timestamp":"2026-10-30T19:30:00Z", "event":"FSDJump", "StarSystem":"IC 2391 Sector MX-T b3-6", "SystemAddress":13872878396833 }""",
            NullLogger.Instance,
            out var jump));

        book.Observe(jump!, "F1");
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, book, stories);
    }

    private static void Read(PanelView panel, string key, string name)
    {
        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + key, name));
        Dispatcher.UIThread.RunJobs();
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    [AvaloniaFact]
    public void AStoryChapterEndsTheChapterUntilTheStoryFinishesOnIt()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open();
        Assert.True(surface.Book.Standing("F1", "the-first-light")?.IsDone);

        Read(surface.Panel, "the-first-light", "The First Light");
        Assert.Contains("End of chapter.", Drawn(surface.Panel));
        Assert.DoesNotContain("The end.", Drawn(surface.Panel));

        using (var frame = surface.Window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("chapter-end-running.png");
        }

        var story = surface.Stories.Current("F1")!;
        surface.Stories.Save("F1", story with { State = StoryState.Finished, StoppedAt = Now });
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("The end.", Drawn(surface.Panel));
        Assert.DoesNotContain("End of chapter.", Drawn(surface.Panel));

        using (var frame = surface.Window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture("chapter-end-finished.png");
        }

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AShortStoryChapterEndsTheChapter()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open();
        Assert.True(surface.Book.Standing("F1", "the-last-dock")?.IsDone);

        Read(surface.Panel, "the-last-dock", "The Last Dock");
        Assert.Contains("End of chapter.", Drawn(surface.Panel));
        Assert.DoesNotContain("The end.", Drawn(surface.Panel));

        surface.Window.Close();
    }
}
