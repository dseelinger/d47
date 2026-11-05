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

namespace D47.App.Tests.Stories;

/// <summary>
/// The Stories tab has no Story log: Your story shows Waiting for your answer at the top, and under it every line no stock
/// story run keeps, newest first.
/// </summary>
public sealed class YourStoryListsWaitingFirstAndOtherLinesLastTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 20, 0, 0, TimeSpan.Zero);

    private sealed record Tab(Window Window, PanelView Panel, StoryStore Stories, MessageStore Messages);

    private static Story Running(StoryState state = StoryState.Running) => new()
    {
        Id = StoryFixture.Story.Id,
        Title = StoryFixture.Story.Title,
        PublicLayer = string.Empty,
        PickedAt = Now.AddDays(-1),
        State = state,
    };

    private static Tab Open(Action<StoryStore, MessageStore> fill)
    {
        var files = new MemoryFileSystem();
        var book = new AdventureBook(
            new AdventureStore("adventures.json", files, NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.InMemory();
        var record = new StoryRunRecord(files, "story-runs", stories, () => "F1", null, new NoopProtector(), NullLogger.Instance);
        var messages = new MessageStore("messages.json", files, NullLogger<MessageStore>.Instance) { Record = record };

        var generator = AdventureFixture.Generator();
        var director = new StoryDirector(
            stories, book, () => StoryFixture.Catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance)
        {
            Records = record,
        };

        fill(stories, messages);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "F1", () => Now, _ => { }, () => true, () => null, () => { },
            Messages: messages, Stories: director, AnswerEnding: _ => null);

        var panel = new PanelView { DataContext = new PanelViewModel(), Mode = PanelMode.Full };
        panel.EnableAdventures(surface);
        var window = new Window { Content = panel, Width = 1280, Height = 1400 };
        window.Show();
        panel.Tab = PanelTab.Stories;
        panel.Nav.SelectRoot(AdventuresPage.RootKey);
        Dispatcher.UIThread.RunJobs();

        return new Tab(window, panel, stories, messages);
    }

    private static List<string> Drawn(PanelView panel) =>
        [.. panel.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible)
            .Select(block => block.Inlines is { Count: > 0 } inlines ? inlines.Text ?? string.Empty : block.Text ?? string.Empty)];

    private static int IndexOf(List<string> drawn, string text) =>
        drawn.FindIndex(each => each.Equals(text, StringComparison.OrdinalIgnoreCase));

    [AvaloniaFact]
    public void TheStoryLogIsGoneAndTheFirstRootReadsYourStory()
    {
        var tab = Open((stories, _) => stories.Save("F1", Running()));
        tab.Panel.TickRouting();

        var roots = tab.Panel.Nav.Roots(PanelTab.Stories);

        Assert.Equal(AdventuresPage.RootName, roots[0].Word);
        Assert.DoesNotContain(roots, root => root.Word == "Story log" || root.Key == "messages");

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void WaitingForYourAnswerIsAtTheTopWithItsAnswersAndOtherLinesAreBelow()
    {
        var tab = Open((stories, messages) =>
        {
            stories.Save("F1", Running(StoryState.Finished));
            messages.Post("shipwright", "My Own Mayday", "A short story line.", Now.AddHours(-2), "my-own-mayday");
            messages.Post(
                MessageStore.Narrator, StoryFixture.Story.Title, "Choose.", Now.AddHours(-1), StoryEnding.Key(StoryFixture.Story.Id),
                [new MessageAnswer("stay", "Stay"), new MessageAnswer("go", "Go")]);
        });

        var drawn = Drawn(tab.Panel);
        var waiting = IndexOf(drawn, MessagesView.WaitingHeading);
        var other = IndexOf(drawn, MessagesView.OtherHeading);

        Assert.Equal(0, drawn.Take(waiting).Count(text => text.Equals(MessagesView.OtherHeading, StringComparison.OrdinalIgnoreCase)));
        Assert.True(waiting >= 0, "no waiting section");
        Assert.True(other > waiting, "Other lines is not below Waiting for your answer");
        Assert.Contains("Stay", drawn, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Go", drawn, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("My Own Mayday", drawn.Skip(other));

        tab.Window.Close();
    }

    [AvaloniaFact]
    public void ALineAStoryRunKeepsIsLeftForTheRunsPage()
    {
        var tab = Open((stories, messages) =>
        {
            stories.Save("F1", Running());
            messages.Post(MessageStore.Narrator, "Kept by the run", "A story line.", Now.AddHours(-2), StoryLines.Key(StoryFixture.Story.Id));
            messages.Post(MessageStore.Narrator, "Loose line", "Untied.", Now.AddHours(-1));
            messages.Post(MessageStore.Narrator, "Unresolved line", "Of a story never picked.", Now, StoryLines.Key("a-story-never-picked"));
        });

        var drawn = Drawn(tab.Panel);
        var other = IndexOf(drawn, MessagesView.OtherHeading);

        Assert.DoesNotContain("Kept by the run", drawn);
        Assert.True(IndexOf(drawn, "Unresolved line") > other && IndexOf(drawn, "Loose line") > IndexOf(drawn, "Unresolved line"));

        tab.Window.Close();
    }
}
