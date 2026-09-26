using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A bookmark is a named system, stored per Commander and reachable by voice (#488).</summary>
public class BookmarksArePlottedByNameTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-bookmarks").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private string StorePath => Path.Combine(_root, "bookmarks.json");

    private BookmarkStore Store() => new(StorePath, NullLogger<BookmarkStore>.Instance);

    [Fact]
    public void ABookmarkSurvivesAReload()
    {
        var store = Store();
        Assert.True(store.Add("F1", "Current CG", "Deciat", At));

        var reloaded = Store();
        reloaded.Load();

        var found = Assert.Single(reloaded.For("F1"));
        Assert.Equal("Current CG", found.Name);
        Assert.Equal("Deciat", found.System);
        Assert.Equal(At, found.MadeAt);
    }

    [Fact]
    public void TwoCommandersBookmarksAreIndependent()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);
        store.Add("F2", "Current CG", "Sol", At);

        Assert.Equal("Deciat", store.Find("F1", "Current CG")?.System);
        Assert.Equal("Sol", store.Find("F2", "Current CG")?.System);
    }

    [Fact]
    public void ADuplicateNameForTheSameCommanderIsRefused()
    {
        var store = Store();
        Assert.True(store.Add("F1", "Current CG", "Deciat", At));
        Assert.False(store.Add("F1", "current cg", "Sol", At));
    }

    [Fact]
    public void RenamingToItsOwnNameInADifferentCaseIsAllowed()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);

        var existingNames = store.For("F1")
            .Where(bookmark => !string.Equals(bookmark.Name, "Current CG", StringComparison.OrdinalIgnoreCase))
            .Select(bookmark => bookmark.Name)
            .ToList();

        var taken = BookmarkValidation.Less([], "Current CG");

        Assert.Null(BookmarkValidation.Problem("current CG", existingNames, taken));
        Assert.True(store.Rename("F1", "Current CG", "current CG"));
        Assert.Equal("Deciat", store.Find("F1", "current CG")?.System);
    }

    [Fact]
    public void DeletingABookmarkRemovesIt()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);

        Assert.True(store.Delete("F1", "Current CG"));
        Assert.Null(store.Find("F1", "Current CG"));
    }

    [Fact]
    public void AnEmptyNameIsRefused() =>
        Assert.Equal("A bookmark needs a name.", BookmarkValidation.Problem("   ", [], []));

    [Fact]
    public void ANameOverFortyCharactersIsRefused()
    {
        var name = new string('a', 41);
        Assert.Equal($"\"{name}\" is longer than 40 characters.", BookmarkValidation.Problem(name, [], []));
    }

    [Fact]
    public void PunctuationInTheNameIsRefused() =>
        Assert.Equal(
            "\"Deciat!\" has punctuation in it. Bookmark names are words, so they can be said out loud.",
            BookmarkValidation.Problem("Deciat!", [], []));

    [Fact]
    public void ADuplicateNameInADifferentCaseIsRefused() =>
        Assert.Equal(
            "You already have a bookmark called \"current cg\".",
            BookmarkValidation.Problem("current cg", ["Current CG"], []));

    [Fact]
    public void MyCarrierIsRefusedWithNoCarrierKnown() =>
        Assert.Equal(
            "\"set course for my carrier\" is already a command D47 understands, so a bookmark cannot take "
            + "that name.",
            BookmarkValidation.Problem("my carrier", [], []));

    [Fact]
    public void ANameThatCollidesWithATakenPhraseIsRefused()
    {
        var taken = new[] { "set course for clear" };

        Assert.Equal(
            "\"set course for clear\" is already a command D47 understands, so a bookmark cannot take that "
            + "name.",
            BookmarkValidation.Problem("clear", [], taken));
    }

    [Fact]
    public void ANameThatCollidesWithAGuardedPhraseFromThePhraseBookIsRefused()
    {
        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "test-nav",
                Group = "Test",
                Name = "Test navigation",
                Summary = "A protected tool command phrase for the test.",
                Tools =
                [
                    new ToolDefinition
                    {
                        Name = "go_home",
                        Description = "Test tool.",
                        Protected = true,
                        Commands =
                        [
                            new ToolCommandPhrase(
                                "set course for home",
                                new Dictionary<string, string>(StringComparer.Ordinal)),
                        ],
                        Handler = (_, _) => Task.FromResult(ToolResult.Ok("done")),
                    },
                ],
            },
        ]);

        var taken = PhraseBook.From(registry, []).Entries
            .Where(entry => entry.Guarded)
            .Select(entry => entry.Phrase)
            .ToList();

        var entry = Assert.Single(
            PhraseBook.From(registry, []).Entries, entry => entry.Phrase == "set course for home");
        Assert.True(entry.Guarded);

        Assert.Equal(
            "\"set course for home\" is already a command D47 understands, so a bookmark cannot take that "
            + "name.",
            BookmarkValidation.Problem("home", [], taken));
    }

    [Fact]
    public void ABookmarkedSystemIsPlottedByItsSpelling()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);

        var registry = CapabilityRegistry.Build([]);
        var router = new KeywordRouter(registry, () => BookmarkCourse.Phrases(store, () => "F1"));

        var match = router.MatchToolCommand("set course for Current CG");

        Assert.NotNull(match);
        Assert.Equal("navigation", match.CapabilityId);
        Assert.Equal("plot_course", match.ToolName);
        Assert.Equal("Deciat", match.Arguments.Values["system"]);

        store.Delete("F1", "Current CG");

        Assert.Null(router.MatchToolCommand("set course for Current CG"));
    }
}
