using D47.App.Panel;
using D47.Core.Adventures;
using D47.Core.Interface;
using D47.Core.Messages;
using D47.Core.Storage;
using D47.Core.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

public class AStoppedStorysQuestionIsNotWaitingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static readonly StoryCard Card = StoryFixture.Story with { Id = "story-x", Number = 1, Title = "Story X" };

    private static (AdventureSurface Surface, D47Message Question) Open(StoryState state)
    {
        var catalog = new StoryCatalog([Card], () => [StoryFixture.Secret with { Id = Card.Id }]);
        var book = new AdventureBook(
            new AdventureStore("adventures.json", new MemoryFileSystem(), NullLogger<AdventureStore>.Instance),
            NullLogger<AdventureBook>.Instance);
        var stories = StoryStore.InMemory();
        var generator = AdventureFixture.Generator();
        var director = new StoryDirector(
            stories, book, () => catalog, generator.GenerateAsync, () => null, _ => { }, NullLogger.Instance);

        stories.Save("A", new Story { Id = Card.Id, Title = Card.Title, PublicLayer = string.Empty, State = state });

        var messages = new MessageStore("messages.json", new MemoryFileSystem(), NullLogger<MessageStore>.Instance);
        var question = messages.Post("covas", "Ending", "Pick one.", Now, StoryEnding.Key(Card.Id), [new MessageAnswer("a", "Stay")]);

        var surface = new AdventureSurface(
            book, generator, () => null, () => "A", () => Now, _ => { }, () => true, () => null, () => { },
            Stories: director, Messages: messages);

        return (surface, question);
    }

    [Theory]
    [InlineData(StoryState.Running, true)]
    [InlineData(StoryState.Finished, true)]
    [InlineData(StoryState.Abandoned, false)]
    [InlineData(StoryState.Ended, false)]
    public void OnlyAStoryThatIsNotStoppedHasAQuestionWaiting(StoryState state, bool waiting)
    {
        var (surface, question) = Open(state);

        Assert.Equal(waiting, MessagesView.IsWaiting(question, surface));
    }

    [Fact]
    public void AStoppedStorysQuestionDoesNotKeepTheStoryLogShown()
    {
        var (surface, question) = Open(StoryState.Abandoned);

        Assert.False(AdventuresPage.InAStory(surface, surface.Messages!));
    }
}
