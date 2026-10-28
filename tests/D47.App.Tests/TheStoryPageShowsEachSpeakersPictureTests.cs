using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>A story's page shows the picture of whoever spoke each line beside it.</summary>
[Trait("Category", "Integration")]
public sealed class TheStoryPageShowsEachSpeakersPictureTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private const string Key = "chapter-one";

    private const string Ship = "core.warden";

    private static readonly StorySecret Secret = StoryFixture.Secret with
    {
        Cast =
        [
            new StorySpeaker { Id = "ren", Name = "Ren", Who = "A dock hand.", Provider = StorySpeaker.Kokoro, Voice = "bm_george", Primary = true },
            new StorySpeaker { Id = "yui", Name = "Yui", Who = "A courier.", Provider = StorySpeaker.Kokoro, Voice = "af_heart" },
        ],
    };

    private static Adventure Story(IReadOnlyList<AdventureLine> opening, IReadOnlyList<AdventureTold> told) => new()
    {
        Key = Key,
        Name = "Yard Paint",
        StoryId = StoryFixture.Story.Id,
        AcceptedAt = Now.AddHours(-1),
        OpeningLines = opening,
        Beats =
        [
            new AdventureBeat
            {
                Title = "Bay Four",
                Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Ossen's Lantern" },
                Line = "Bay four.",
            },
        ],
        Told = told,
    };

    private static AdventureTold Beat(string text, string? picture, string? speaker = null) => new()
    {
        Kind = AdventureToldKind.Beat,
        Text = text,
        At = Now,
        Beat = 0,
        Title = "Bay Four",
        Speaker = speaker,
        Picture = picture,
    };

    private static (Window Window, PanelView Panel) Open(Adventure story, SpeakerPictureFixture fixture)
    {
        var paths = fixture.Paths;
        var store = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), fixture.Files, NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        Assert.Null(book.Write("F1", story));

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);

        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), fixture.Files, NullLogger<StoryStore>.Instance);
        var director = new StoryDirector(
            stories, book, () => new StoryCatalog([StoryFixture.Story], () => [Secret]), generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => false, () => false, () => null, () => { },
            Stories: director,
            Pictures: fixture.Portraits.Pictures,
            ShipPicture: () => Ship);

        new D47.App.Theming.ThemeManager(Avalonia.Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var panel = new PanelView { DataContext = fixture.Model };
        panel.EnableSpeakerPictures(fixture.Portraits);
        panel.EnableAdventures(surface);

        var window = new Window { Content = panel, Width = 900, Height = 700 };
        window.Show();
        panel.Tab = PanelTab.Stories;
        panel.Nav.GoTo(new NavCrumb(AdventuresPage.ReadPrefix + Key, "Yard Paint"));
        Dispatcher.UIThread.RunJobs();

        return (window, panel);
    }

    private static SpeakerPictureFixture Fixture(string name)
    {
        var fixture = new SpeakerPictureFixture(name);
        fixture.Ship("the-test-story.ren", Colors.Green);
        fixture.Ship(Ship, Colors.Red);
        fixture.Ship(SpeakerPictures.Narrator, Colors.Blue);
        return fixture;
    }

    private static IReadOnlyList<Image> Pictures(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Image>().Where(image => image.Name == "SpeakerPicture")];

    private static void AssertShows(SpeakerPictureFixture fixture, Image image, string picture) =>
        Assert.Same(fixture.Portraits.For(picture), image.Source);

    [AvaloniaFact]
    public void ACastLineIsDrawnWithTheMembersPicture()
    {
        var fixture = Fixture("d47-story-page-cast");
        var (_, panel) = Open(Story([], [Beat("Bay four.", "the-test-story.ren", "Ren")]), fixture);

        AssertShows(fixture, Assert.Single(Pictures(panel)), "the-test-story.ren");
    }

    [AvaloniaFact]
    public void AnAsideIsDrawnWithTheCoresPicture()
    {
        var fixture = Fixture("d47-story-page-aside");

        var aside = new AdventureTold { Kind = AdventureToldKind.Aside, Text = "Nobody I can name.", Asked = "Who?", At = Now };
        var (_, panel) = Open(Story([], [aside]), fixture);

        AssertShows(fixture, Assert.Single(Pictures(panel)), Ship);
    }

    [AvaloniaFact]
    public void ANudgeIsDrawnWithTheNarratorsPicture()
    {
        var fixture = Fixture("d47-story-page-nudge");

        var nudge = new AdventureTold { Kind = AdventureToldKind.Nudge, Text = "The beacon is waiting.", At = Now };
        var (_, panel) = Open(Story([], [nudge]), fixture);

        AssertShows(fixture, Assert.Single(Pictures(panel)), SpeakerPictures.Narrator);
    }

    [AvaloniaFact]
    public void AMemberWithNoFileIsDrawnWithNoPicture()
    {
        var fixture = Fixture("d47-story-page-none");
        var (_, panel) = Open(Story([], [Beat("Parcel for you.", "the-test-story.yui", "Yui")]), fixture);

        Assert.Empty(Pictures(panel));
        Assert.Contains(panel.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == "Parcel for you.");
    }

    [AvaloniaFact]
    public void AnAuthoredCastLineIsDrawnWithTheMembersPicture()
    {
        var fixture = Fixture("d47-story-page-authored");

        var (_, panel) = Open(
            Story([new AdventureLine { Text = "Bay four.", Speaker = "ren" }, new AdventureLine { Text = "Sign here." }], []), fixture);

        var drawn = Pictures(panel);

        Assert.Equal(2, drawn.Count);
        AssertShows(fixture, drawn[0], "the-test-story.ren");
        AssertShows(fixture, drawn[1], Ship);
    }

    [AvaloniaFact]
    public void AFinishedStorysMemberIsNamedNotShownByItsId()
    {
        var fixture = Fixture("d47-story-page-named");

        var (_, panel) = Open(Story([new AdventureLine { Text = "Bay four.", Speaker = "ren" }], []), fixture);

        var texts = panel.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text).ToList();

        Assert.Contains("Ren", texts);
        Assert.DoesNotContain("ren", texts);
    }

    [AvaloniaFact]
    public void PressingAPicturePutsTheViewerOnTheOverlay()
    {
        var fixture = Fixture("d47-story-page-press");
        var (window, panel) = Open(Story([], [Beat("Bay four.", "the-test-story.ren", "Ren")]), fixture);

        var picture = Assert.Single(Pictures(panel));
        var at = picture.TranslatePoint(new Point(picture.Bounds.Width / 2, picture.Bounds.Height / 2), window)!.Value;

        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Single(OverlayLayer.GetOverlayLayer(panel)!.Children.OfType<SpeakerPictureViewer>());
    }
}
