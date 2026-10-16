using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Stories tab offers the Story log while the shown Commander has a story or short story under way, or a story
/// ending waiting for an answer, and not otherwise.
/// </summary>
public class TheStoryLogShowsOnlyInAStoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    private sealed record Tab(Window Window, PanelView Panel, AdventureBook Book, StoryStore Stories, MessageStore Messages);

    private static Tab Open()
    {
        var files = new MemoryFileSystem();
        var book = new AdventureBook(
            new AdventureStore("adventures.json", files, NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.InMemory();
        var messages = new MessageStore("messages.json", files, NullLogger<MessageStore>.Instance);

        var generator = new AdventureGenerator(
            () => null, () => null, () => null, () => null, () => null, () => null,
            () => null, () => null, null, null, NullLogger.Instance);
        var director = new StoryDirector(
            stories, book, () => StoryFixture.Catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => true, () => null, () => { },
            Messages: messages, Stories: director);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);
        var window = new Window { Content = panel, Width = 1280, Height = 860 };
        window.Show();
        panel.Tab = PanelTab.Stories;
        Dispatcher.UIThread.RunJobs();

        return new Tab(window, panel, book, stories, messages);
    }

    private static bool Offered(Tab tab)
    {
        Dispatcher.UIThread.RunJobs();
        tab.Panel.TickRouting();

        return tab.Panel.Nav.Roots(PanelTab.Stories).Any(root => root.Key == MessagesView.RootKey);
    }

    private static Story Running() => new()
    {
        Id = StoryFixture.Story.Id,
        Title = StoryFixture.Story.Title,
        PublicLayer = string.Empty,
        PickedAt = Now.AddHours(-1),
    };

    [AvaloniaFact]
    public void ALogOfFinishedStoriesIsNotOfferedOutsideOne()
    {
        var tab = Open();

        tab.Messages.Post(MessageStore.Narrator, "Next of Kin", "An old line.", Now.AddHours(-2), StoryLines.KeyPrefix + "gone");
        tab.Messages.Post("shipwright", "My Own Mayday", "An untied line.", Now.AddHours(-1));

        Assert.False(Offered(tab));

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void ARunningStoryOffersTheLogAndEndingItTakesItAway()
    {
        var tab = Open();

        tab.Stories.Save("F1", Running());
        Assert.True(Offered(tab));

        tab.Stories.Save("F1", Running() with { State = StoryState.Ended });
        Assert.False(Offered(tab));

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void AShortStoryUnderWayOffersTheLog()
    {
        var tab = Open();

        Assert.Null(tab.Book.Write("F1", new Adventure
        {
            Key = "the-last-dock",
            Name = "The Last Dock",
            Source = AdventureSource.Generated,
            Beats =
            [
                new AdventureBeat
                {
                    Title = "The Dock",
                    Trigger = new AdventureTrigger { Kind = TriggerKind.Arrive, SystemAddress = 1, System = "Sol" },
                    Line = "Dock.",
                },
            ],
            AcceptedAt = Now.AddHours(-1),
        }));

        Assert.True(Offered(tab));

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void AnEndingWaitingForAnAnswerOffersTheLogUntilItIsAnswered()
    {
        var tab = Open();

        tab.Stories.Save("F1", Running() with { State = StoryState.Finished });
        tab.Messages.Post(
            MessageStore.Narrator, StoryFixture.Story.Title, "Choose.", Now, StoryEnding.Key(StoryFixture.Story.Id),
            [new MessageAnswer("stay", "Stay"), new MessageAnswer("go", "Go")]);

        Assert.True(Offered(tab));

        tab.Stories.Save("F1", Running() with { State = StoryState.Finished, EndingChoice = "stay" });
        Assert.False(Offered(tab));

        tab.Window.Close();
    }
}
