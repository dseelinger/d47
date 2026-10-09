using D47.Core.Storage;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Tests.Stories;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Stories page lists every card by title, level and blurb, and the running story, a card reads in full with Pick or Switch,
/// and no hidden sentence is drawn anywhere. Captures are saved to <see cref="TestSurface.CaptureDirectory"/>.
/// </summary>
[Trait("Category", "Integration")]
public class TheStoriesPageShowsOnlyThePublicLayerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 20, 0, 0, TimeSpan.Zero);

    private sealed record Surface(Window Window, PanelView Panel, StoryDirector Director);

    private static Surface Open(
        bool running,
        StoryCatalog? catalog = null,
        Func<string?>? gender = null,
        SpeakerPictures? pictures = null,
        Func<StoryCatalog>? catalogs = null,
        StoryDownloader? downloads = null)
    {
        var paths = new AppPaths(TempFolders.Create("d47-stories-capture"));
        paths.EnsureCreated();

        var book = new AdventureBook(
            new AdventureStore(Path.Combine(paths.Data, "adventures.json"), new DiskFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);

        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), NullLogger<StoryStore>.Instance);
        var card = StoryFixture.Story;

        if (running)
        {
            Assert.Null(book.Write("F1", new Adventure
            {
                Key = "the-first-light",
                Name = "The First Light",
                Source = AdventureSource.Generated,
                StoryId = card.Id,
                Spine = new AdventureSpine { Premise = "A stock voice asks for something." },
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
            }));

            stories.Save("F1", new Story
            {
                Id = card.Id,
                Title = card.Title,
                PublicLayer = card.Describe(),
                Chapters = ["the-first-light"],
                PickedAt = Now.AddHours(-1),
            });
        }

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);

        var director = new StoryDirector(
            stories, book, catalogs ?? (() => catalog ?? StoryFixture.Catalog), generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Gender = gender ?? (() => null),
        };

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Stories: director,
            Pictures: pictures,
            Downloads: downloads);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();

        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(new NavCrumb(StoriesView.RootKey, "Stories"));
        Dispatcher.UIThread.RunJobs();

        return new Surface(window, panel, director);
    }

    private static string Save(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();

        var path = name;

        using (var frame = window.CaptureRenderedFrame()!)
        {
            frame.SaveCapture(path);
        }

        return path;
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

    /// <summary>Whether a text block draws <paramref name="text"/>, in any case, since list rows are drawn in capitals.</summary>
    private static bool Shows(PanelView panel, string text) =>
        Drawn(panel).Any(drawn => string.Equals(drawn, text, StringComparison.OrdinalIgnoreCase));

    private static List<string> Buttons(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string ?? string.Empty)];

    private static void NoHiddenSentence(PanelView panel)
    {
        var drawn = string.Join("\n", Drawn(panel));

        foreach (var secret in StoryFixture.Catalog.Secrets)
        {
            foreach (var (_, field) in secret.Texts().Where(text => text.Text.Length > 0))
            {
                Assert.True(!drawn.Contains(field, StringComparison.OrdinalIgnoreCase), $"The page draws hidden text from {secret.Id}.");
            }
        }
    }

    [AvaloniaFact]
    public void WithNoStoryTheCardsAreListedAndACardOffersPick()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(running: false);
        var panel = surface.Panel;
        Assert.True(Shows(panel, "The Test Story"));
        Assert.True(Shows(panel, "The Other Story"));
        Assert.True(Shows(panel, "1 year · New commander · Archivist"));
        Assert.False(Shows(panel, "Whydunit"));
        Assert.True(Shows(panel, StoryFixture.Story.Blurb));
        Assert.False(Shows(panel, StoryFixture.Story.InYourWords));
        NoHiddenSentence(panel);
        Save(surface.Window, "stories-root.png");

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Other.Id, StoryFixture.Other.Title));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Pick", Buttons(panel));
        Assert.True(Shows(panel, "1 year · For a new commander: no engineering done yet."));
        Assert.False(Shows(panel, "Buddy Love"));
        Assert.True(Shows(panel, "Core: Archivist"));
        Assert.True(Shows(panel, StoryFixture.Other.Blurb));
        Assert.True(Shows(panel, "In your words"));
        Assert.True(Shows(panel, "The beacon"));
        NoHiddenSentence(panel);
        Save(surface.Window, "stories-card.png");

        surface.Window.Close();
    }

    private static void WriteSquare(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var square = new Border { Width = 64, Height = 64, Background = Avalonia.Media.Brushes.Goldenrod };
        square.Measure(new Avalonia.Size(64, 64));
        square.Arrange(new Avalonia.Rect(0, 0, 64, 64));

        using var bitmap = new RenderTargetBitmap(new Avalonia.PixelSize(64, 64));
        bitmap.Render(square);
        bitmap.Save(path, new PngBitmapEncoderOptions());
    }

    private static List<Image> CastStrip(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<StackPanel>()
            .Where(strip => Avalonia.Automation.AutomationProperties.GetName(strip) == "Cast")
            .SelectMany(strip => strip.Children.OfType<Image>())];

    [AvaloniaFact]
    public void ACardShowsThePicturesOfItsPrimaryCastThatAreOnDisk()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var paths = new AppPaths(TempFolders.Create("d47-cast-strip"));
        var pictures = new SpeakerPictures(paths);
        var id = StoryFixture.Story.Id;

        WriteSquare(pictures.Default($"{id}.ren"));
        WriteSquare(pictures.Default($"{id}.cray.for-woman"));
        WriteSquare(pictures.Chosen($"{id}.ila"));

        var card = StoryFixture.Story with
        {
            CastPictures = [$"{id}.ren", $"{id}.cray.for-man", $"{id}.cray.for-woman", $"{id}.ila", $"{id}.absent"],
        };
        var catalog = new StoryCatalog([card, StoryFixture.Other], () => [StoryFixture.Secret]);

        var woman = Open(running: false, catalog, () => CommanderGender.Woman, pictures);
        var strip = CastStrip(woman.Panel);

        Assert.Equal(3, strip.Count);
        Assert.All(strip, image => Assert.Equal(44, image.Width));
        Assert.All(strip, image => Assert.Equal(44, image.Height));
        Save(woman.Window, "stories-cast-strip.png");
        woman.Window.Close();

        var unset = Open(running: false, catalog, () => null, pictures);
        Assert.Equal(2, CastStrip(unset.Panel).Count);
        unset.Window.Close();

        var none = Open(running: false, new StoryCatalog([StoryFixture.Story, StoryFixture.Other], () => [StoryFixture.Secret]), () => CommanderGender.Woman, pictures);
        Assert.Empty(CastStrip(none.Panel));
        none.Window.Close();
    }

    [AvaloniaFact]
    public void PickWaitsForTheCommandersGender()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        string? gender = null;
        var surface = Open(running: false, StoryFixture.Versioned, () => gender);
        var panel = surface.Panel;
        surface.Director.SetGender = chosen => gender = chosen;

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Story.Id, StoryFixture.Story.Title));
        Dispatcher.UIThread.RunJobs();

        Button Pick() => panel.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Pick");

        Assert.True(Shows(panel, "Your Commander is"));
        Assert.True(Shows(panel, StoryDirector.NeedsGender));
        Assert.False(Pick().IsEnabled);
        Save(surface.Window, "stories-card-gender-unset.png");

        var segment = panel.GetVisualDescendants().OfType<D47.App.Controls.Segment>().Single();
        segment.GetVisualDescendants().OfType<RadioButton>().Last().IsChecked = true;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CommanderGender.Woman, gender);
        Assert.True(Pick().IsEnabled);
        Assert.False(panel.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Text == StoryDirector.NeedsGender).IsEffectivelyVisible);
        Save(surface.Window, "stories-card-gender-set.png");

        surface.Window.Close();
    }

    /// <summary>Pumps the dispatcher until <paramref name="done"/> holds or five seconds pass.</summary>
    private static bool Until(Func<bool> done)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (!done() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        return done();
    }

    private static readonly StorySecret PrimaryRen = StoryReleaseFixture.Secret with
    {
        Cast = [.. StoryReleaseFixture.Secret.Cast.Select(member => member.Id == "ren" ? member with { Primary = true } : member)],
    };

    [AvaloniaFact]
    public void OpeningAStorysPageFetchesItsHiddenLayerAndThenDrawsItsCast()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);
        release.ServeSealed(PrimaryRen);
        var gate = new TaskCompletionSource();
        release.Hold = (StoryCatalog.SealedExtension, gate.Task);

        var downloads = release.Downloader();
        var id = StoryReleaseFixture.Id;
        var catalog = new StoryCatalog([StoryFixture.Story, StoryFixture.Other], () => []);
        downloads.Landed += () =>
        {
            if (downloads.IsOnDisk(id))
            {
                catalog = new StoryCatalog([StoryFixture.Story, StoryFixture.Other], () => [PrimaryRen]);
            }
        };

        var surface = Open(running: false, gender: () => CommanderGender.Woman, catalogs: () => catalog, downloads: downloads);
        var panel = surface.Panel;

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + id, StoryFixture.Story.Title));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Until(() => release.Asked.Contains(id + StoryCatalog.SealedExtension)));
        Assert.False(Shows(panel, "Cast"));
        Assert.False(Shows(panel, "Ren"));

        gate.SetResult();

        Assert.True(Until(() => Shows(panel, "Ren")));
        Assert.True(Shows(panel, "Cast"));
        Assert.Contains("Pick", Buttons(panel));
        Assert.Equal(1, release.Asked.Count(file => file == id + StoryCatalog.SealedExtension));
        NoHiddenSentence(panel);
        Save(surface.Window, "stories-card-cast-fetched.png");

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithDownloadsOffOpeningAStorysPageFetchesNothing()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);
        using var release = new StoryRelease();
        StoryReleaseFixture.ServeAll(release);

        var downloads = release.Downloader(allowed: false);
        var empty = new StoryCatalog([StoryFixture.Story, StoryFixture.Other], () => []);
        var surface = Open(running: false, catalogs: () => empty, downloads: downloads);

        surface.Panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryReleaseFixture.Id, StoryFixture.Story.Title));
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(200);
        Dispatcher.UIThread.RunJobs();

        Assert.Empty(release.Asked);

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithNoStoriesThePageSaysSo()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(running: false, StoryFixture.Empty);

        Assert.True(Shows(surface.Panel, "No stories yet."));
        Save(surface.Window, "stories-empty.png");

        surface.Window.Close();
    }

    [AvaloniaFact]
    public void WithAStoryRunningItLeadsTheListAndAnotherCardOffersSwitch()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var surface = Open(running: true);
        var panel = surface.Panel;

        Assert.True(Shows(panel, "Your story — chapter 1 under way."));
        Assert.Contains("Abandon", Buttons(panel));
        Assert.Contains("Read the chapter", Buttons(panel));
        Save(surface.Window, "stories-running.png");

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Other.Id, StoryFixture.Other.Title));
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("Switch", Buttons(panel));
        Assert.DoesNotContain("Pick", Buttons(panel));

        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Story.Id, StoryFixture.Story.Title));
        Dispatcher.UIThread.RunJobs();

        Assert.True(Shows(panel, "This is your story."));

        surface.Window.Close();
    }
}
