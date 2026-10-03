using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A stock story's chapter shows no Premise, Turn or Ending on the Adventures page, and offers no Edit that would;
/// an adventure of the Commander's own still shows its premise. Captures go to <see cref="TestSurface.CaptureDirectory"/>.
/// </summary>
public class AStoryChapterShowsNoPremiseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);

    private const string ChapterPremise = "A stock voice asks for something it should not know.";

    private const string OwnPremise = "A courier run that goes wrong at the last dock.";

    private sealed record Surface(Window Window, PanelView Panel, AdventureBook Book);

    private static Adventure Chapter(string key, string name, string premise, string? storyId) => new()
    {
        Key = key,
        Name = name,
        Source = AdventureSource.Generated,
        StoryId = storyId,
        Spine = new AdventureSpine { Premise = premise, Turn = "The turn of " + name + ".", Ending = "The end of " + name + "." },
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
        var paths = new AppPaths(TempFolders.Create("d47-story-chapter-capture"));
        paths.EnsureCreated();

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var card = StoryFixture.Story;

        Assert.Null(book.Write("F1", Chapter("the-first-light", "The First Light", ChapterPremise, card.Id)));
        Assert.Null(book.Write("F1", Chapter("the-last-dock", "The Last Dock", OwnPremise, storyId: null)));

        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), NullLogger<StoryStore>.Instance);
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

        return new Surface(window, panel, book);
    }

    private static void Read(PanelView panel, string key, string name)
    {
        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + key, name));
        Dispatcher.UIThread.RunJobs();
    }

    private static string Drawn(PanelView panel) =>
        string.Join("\n", panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty));

    private static List<string> Buttons(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string ?? string.Empty)];

    private static void Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        frame.Save(Path.Combine(TestSurface.CaptureDirectory, name), new PngBitmapEncoderOptions());
    }

    private static void NoSpineOfTheChapter(PanelView panel)
    {
        var drawn = Drawn(panel);

        Assert.DoesNotContain(ChapterPremise, drawn, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The turn of The First Light.", drawn, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("The end of The First Light.", drawn, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void AChapterUnderWayShowsNoPremise()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open();
        NoSpineOfTheChapter(surface.Panel);

        Read(surface.Panel, "the-first-light", "The First Light");
        NoSpineOfTheChapter(surface.Panel);
        Save(surface.Window, "story-chapter-reading.png");

        Read(surface.Panel, "the-last-dock", "The Last Dock");
        Assert.Contains(OwnPremise, Drawn(surface.Panel), StringComparison.OrdinalIgnoreCase);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void AnAbandonedChapterOffersNoEditAndItsEditPageShowsNoSpine()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open();
        surface.Book.Abandon("F1", "the-first-light", Now);
        surface.Book.Abandon("F1", "the-last-dock", Now);

        Read(surface.Panel, "the-first-light", "The First Light");
        Assert.Contains("Begin again", Buttons(surface.Panel));
        Assert.DoesNotContain("Edit", Buttons(surface.Panel));
        NoSpineOfTheChapter(surface.Panel);

        surface.Panel.Nav.GoTo(new NavCrumb(AdventuresPage.EditPrefix + "the-first-light", "Edit"));
        Dispatcher.UIThread.RunJobs();
        NoSpineOfTheChapter(surface.Panel);

        Read(surface.Panel, "the-last-dock", "The Last Dock");
        Assert.Contains("Edit", Buttons(surface.Panel));

        surface.Window.Close();
    }
}
