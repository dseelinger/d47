using D47.Core.Adventures;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public class AnAsideIsFiledOnTheStoryItMentionsTests
{
    private static readonly string AdventuresFile = Path.Combine(MemoryInstall.FakeRoot, "adventures.json");

    private static AdventureBook Wired()
    {
        var store = new AdventureStore(AdventuresFile, new MemoryFileSystem(), NullLogger<AdventureStore>.Instance);
        var book = new AdventureBook(store, NullLogger<AdventureBook>.Instance);
        book.Write("F1", LanternRoute(Accepted));
        book.CatchUp([]);
        return book;
    }

    private static IReadOnlyList<AdventureTold> Told(AdventureBook book) =>
        book.Standing("F1", "the-lantern-route")!.Adventure.Told;

    [Fact]
    public void AnAnswerAboutTheStoryIsFiledOnItWithTheQuestion()
    {
        var book = Wired();
        var at = Accepted.AddMinutes(5);

        var filed = book.FileAsides("F1", " What is the Lantern Route about? ", " A survey filed late. ", at);

        Assert.Equal(1, filed);
        var aside = Assert.Single(Told(book));
        Assert.Equal(AdventureToldKind.Aside, aside.Kind);
        Assert.Equal("A survey filed late.", aside.Text);
        Assert.Equal("What is the Lantern Route about?", aside.Asked);
        Assert.Equal(at, aside.At);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ABlankAnswerFilesNothing(string? answered)
    {
        var book = Wired();

        Assert.Equal(0, book.FileAsides("F1", "What is the Lantern Route about?", answered, Accepted));
        Assert.Empty(Told(book));
    }

    [Fact]
    public void AnExchangeAboutSomethingElseFilesNothing()
    {
        var book = Wired();

        Assert.Equal(0, book.FileAsides("F1", "Where is the nearest material trader?", "Two jumps away.", Accepted));
        Assert.Empty(Told(book));
    }
}
