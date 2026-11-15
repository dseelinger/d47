using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>With one chapter under way Your story is that chapter's page; otherwise it lists the stories and a chapter opened from it takes the whole width.</summary>
[Trait("Category", "Integration")]
public sealed class YourStoryIsTheChapterWhenOneIsUnderWayTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 20, 0, 0, TimeSpan.Zero);

    private static Adventure Other(string key = "the-other-route") =>
        AdventuresTabTests.Story(Now.AddHours(-1)) with { Key = key, Name = "The Other Route" };

    private static List<Control> Cards(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Control>().Where(c => c.Focusable && c.Classes.Contains(ListRow.OpensClass) && c.IsEffectivelyVisible)];

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible).Select(block => block.Text ?? string.Empty)];

    private static List<string?> Buttons(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).Select(button => button.Content as string)];

    [AvaloniaFact]
    public void OneChapterUnderWayIsShownAsThePageWithNoCardAndNoReadButton()
    {
        var (panel, _, _) = AdventuresTabTests.Open(AdventuresTabTests.Story(Now.AddHours(-1)));

        Assert.Empty(Cards(panel));
        Assert.Equal(["adventures"], panel.Nav.Trail.Select(crumb => crumb.Key));
        Assert.Single(Drawn(panel), text => text == "THE LANTERN ROUTE");
        Assert.Contains("NEXT", Drawn(panel));
        Assert.DoesNotContain("Read the chapter", Buttons(panel));
    }

    [AvaloniaFact]
    public void TwoChaptersUnderWayAreListedAndOneOpensAloneAtFullWidth()
    {
        var (panel, _, _) = AdventuresTabTests.Open(AdventuresTabTests.Story(Now.AddHours(-1)), Other());
        var window = (Window)TopLevel.GetTopLevel(panel)!;

        var cards = Cards(panel);
        Assert.Equal(2, cards.Count);
        Assert.DoesNotContain("NEXT", Drawn(panel));

        cards[0].Focus(NavigationMethod.Tab);
        Dispatcher.UIThread.RunJobs();
        window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, panel.Nav.Trail.Count);
        Assert.True(panel.Nav.Trail[^1].Whole);
        Assert.Empty(Cards(panel));
        Assert.Contains("NEXT", Drawn(panel));
    }

    [AvaloniaFact]
    public void ADraftKeepsYourStoryAListWithTheDraftCard()
    {
        var draft = AdventuresTabTests.Story(null, AdventureSource.Generated) with { Key = "the-draft", Name = "The Draft" };
        var (panel, _, _) = AdventuresTabTests.Open(AdventuresTabTests.Story(Now.AddHours(-1)), draft);

        Assert.Contains("Accept", Buttons(panel));
        Assert.Single(Cards(panel));
        Assert.DoesNotContain("NEXT", Drawn(panel));
    }

    [AvaloniaFact]
    public void WhenTheOnlyChapterIsRemovedYourStoryRedrawsAsTheList()
    {
        var (panel, book, _) = AdventuresTabTests.Open(AdventuresTabTests.Story(Now.AddHours(-1)));
        Assert.Contains("NEXT", Drawn(panel));

        book.Remove("F1", "the-lantern-route");
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("THE LANTERN ROUTE", Drawn(panel));
        Assert.Contains(Drawn(panel), text => text.StartsWith("No stories yet", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void BeginningAStoryWhenYourStoryIsAlreadyItsChapterDrawsTheChapterOnce()
    {
        using var look = AppLook.Put(ThemeCatalog.Elite, null);

        var paths = new AppPaths(TempFolders.Create("d47-your-story-chapter"));
        paths.EnsureCreated();

        var files = new DiskFileSystem();
        var adventures = new AdventureStore(Path.Combine(paths.Data, "adventures.json"), files, NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(adventures, NullLogger<AdventureBook>.Instance);
        adventures.Changed += book.Reconcile;
        var stories = StoryStore.Open(Path.Combine(paths.Data, "story.json"), files, NullLogger<StoryStore>.Instance);
        var generator = AdventureFixture.Generator();

        var director = new StoryDirector(
            stories, book, () => StoryFixture.Catalog,
            (_, _, _) => Task.FromResult(new AdventureOutcome(
                AdventuresTabTests.Story(null, AdventureSource.Generated) with { Key = "the-first-light", Name = "The First Light" }, null, null, [])),
            () => null, _ => { }, NullLogger.Instance)
        {
            Gender = () => CommanderGender.Woman,
        };

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => null, () => { },
            Stories: director);

        var (window, panel) = StoriesPanelHosting.Show(surface);
        panel.Nav.GoTo(new NavCrumb(StoriesView.ReadPrefix + StoryFixture.Other.Id, StoryFixture.Other.Title));
        Dispatcher.UIThread.RunJobs();

        panel.GetVisualDescendants().OfType<Button>().Single(button => button.Content as string == "Begin story")
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline && !(panel.Nav.Trail.Count == 1 && panel.Nav.Trail[0].Key == AdventuresPage.RootKey))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }

        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["adventures"], panel.Nav.Trail.Select(crumb => crumb.Key));
        Assert.Single(Drawn(panel), text => text == "THE FIRST LIGHT");

        window.Close();
    }
}
