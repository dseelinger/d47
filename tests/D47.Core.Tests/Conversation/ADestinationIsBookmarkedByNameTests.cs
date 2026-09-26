using System.Text.Json;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A voice command names the current destination, so it can be returned to by that name (#489).</summary>
public class ADestinationIsBookmarkedByNameTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-bookmarks-capability").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly DateTimeOffset At = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private BookmarkStore Store() => new(Path.Combine(_root, "bookmarks.json"), NullLogger<BookmarkStore>.Instance);

    private static Func<PhraseBook> EmptyBook() => () => PhraseBook.From(CapabilityRegistry.Build([]), []);

    private static CommanderGameState Commander(string system, long address)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Test Commander"));
        var location = new JournalEvent(
            At,
            "Location",
            JsonDocument.Parse($$"""{"StarSystem":"{{system}}","SystemAddress":{{address}}}""").RootElement);

        state.Apply(location);
        return state;
    }

    private static GameStatus StatusFor(long system, long body, string? name) =>
        new() { Destination = new StatusDestination(system, body, name) };

    private CapabilityRegistry Registry(
        BookmarkStore store,
        Func<GameStatus>? status = null,
        Func<CommanderGameState?>? commander = null,
        Func<PhraseBook>? phraseBook = null,
        Func<string>? frontierId = null) =>
        CapabilityRegistry.Build(
        [
            BookmarksCapability.Create(
                store,
                frontierId ?? (() => "F1"),
                status ?? (() => GameStatus.Unknown),
                commander ?? (() => null),
                phraseBook ?? EmptyBook(),
                () => At),
        ]);

    private static async Task<ToolResult> Bookmark(
        CapabilityRegistry registry, string? name = null, ToolCaller caller = ToolCaller.Commander)
    {
        var arguments = name is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = name };

        return await registry.InvokeAsync(
            BookmarksCapability.BookmarkTool,
            new ToolArguments(arguments),
            TestContext.Current.CancellationToken,
            caller: caller);
    }

    [Fact]
    public async Task ADestinationThatIsTheSystemItselfIsBookmarkedUnderItsElitesName()
    {
        var store = Store();
        var registry = Registry(store, status: () => StatusFor(1, 0, "LTT 7786"));

        var result = await Bookmark(registry);

        Assert.False(result.IsError);
        Assert.Equal("LTT 7786", store.Find("F1", "LTT 7786")?.System);
    }

    [Fact]
    public async Task AStationInTheCurrentSystemIsBookmarkedPointingAtThatSystem()
    {
        var store = Store();
        var commander = Commander("Deciat", 555);
        var registry = Registry(
            store,
            status: () => StatusFor(555, 7, "Farseer Inc"),
            commander: () => commander);

        var result = await Bookmark(registry);

        Assert.False(result.IsError);
        Assert.Equal("Deciat", store.Find("F1", "Farseer Inc")?.System);
    }

    [Fact]
    public async Task ABodyInAnotherSystemIsRefusedAndStoresNothing()
    {
        var store = Store();
        var commander = Commander("Deciat", 555);
        var registry = Registry(
            store,
            status: () => StatusFor(999, 7, "Some Outpost"),
            commander: () => commander);

        var result = await Bookmark(registry);

        Assert.True(result.IsError);
        Assert.Equal(
            "That is in another system, and Elite does not name the system. Target the system itself.",
            result.Content);
        Assert.Empty(store.For("F1"));
    }

    [Fact]
    public async Task NoDestinationIsRefusedAndStoresNothing()
    {
        var store = Store();
        var registry = Registry(store, status: () => GameStatus.Unknown);

        var result = await Bookmark(registry);

        Assert.True(result.IsError);
        Assert.Equal("Nothing is targeted. Select a system or a station first.", result.Content);
        Assert.Empty(store.For("F1"));
    }

    [Fact]
    public async Task AProceduralNameIsCleanedOfPunctuation()
    {
        var store = Store();
        var registry = Registry(store, status: () => StatusFor(1, 0, "Col 285 Sector AB-C d1-23"));

        await Bookmark(registry);

        Assert.NotNull(store.Find("F1", "Col 285 Sector AB C d1 23"));
    }

    [Fact]
    public async Task APlaceholderNameBecomesTheLowestFreeBookmarkNumber()
    {
        var store = Store();
        var registry = Registry(store, status: () => StatusFor(1, 0, "$AGRICULTURE_MEDIUM;"));

        await Bookmark(registry);

        Assert.NotNull(store.Find("F1", "Bookmark 1"));
    }

    [Fact]
    public async Task ASecondBookmarkThatCollidesWhenCleanedTakesTheNextNumberSuffix()
    {
        var store = Store();
        store.Add("F1", "Clean Name", "Sol", At);

        var registry = Registry(store, status: () => StatusFor(1, 0, "Clean Name!!!"));

        await Bookmark(registry);

        Assert.NotNull(store.Find("F1", "Clean Name 2"));
    }

    [Fact]
    public async Task AGivenNameThatIsAlreadyTakenIsRefusedAndStoresNothing()
    {
        var store = Store();
        store.Add("F1", "Existing", "Sol", At);

        var registry = Registry(store, status: () => StatusFor(1, 0, "Deciat"));

        var result = await Bookmark(registry, name: "Existing");

        Assert.True(result.IsError);
        Assert.Equal("You already have a bookmark called \"Existing\".", result.Content);
        Assert.Single(store.For("F1"));
    }

    [Fact]
    public async Task ListingWithNoBookmarksSaysHowToMakeOne()
    {
        var store = Store();
        var registry = Registry(store);

        var result = await registry.InvokeAsync(
            BookmarksCapability.ListTool,
            new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal("Say 'bookmark this' with a system or station targeted.", result.Content);
    }

    [Fact]
    public void ListingIsReachedThroughTheRouterWithoutTheModel()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);

        var registry = Registry(store);
        var router = new KeywordRouter(registry);

        var match = router.MatchToolCommand("what are my bookmarks");

        Assert.NotNull(match);
        Assert.Equal(BookmarksCapability.ListTool, match.ToolName);
    }

    [Fact]
    public async Task DeletingByVoiceReachesTheToolThroughTheRouterWithoutTheModel()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);

        var registry = Registry(store);
        var router = new KeywordRouter(registry, () => BookmarksCapability.Phrases(store, () => "F1"));

        var match = router.MatchToolCommand("delete bookmark Current CG");

        Assert.NotNull(match);
        Assert.Equal(BookmarksCapability.DeleteTool, match.ToolName);
        Assert.Equal("Current CG", match.Arguments.Values["name"]);

        var result = await registry.InvokeAsync(
            match.ToolName, match.Arguments, TestContext.Current.CancellationToken, caller: ToolCaller.Commander);

        Assert.False(result.IsError);
        Assert.Null(store.Find("F1", "Current CG"));
    }

    [Fact]
    public async Task TheModelCallingDeleteIsRefused()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);

        var registry = Registry(store);

        var result = await registry.InvokeAsync(
            BookmarksCapability.DeleteTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["name"] = "Current CG" }),
            TestContext.Current.CancellationToken,
            caller: ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.NotNull(store.Find("F1", "Current CG"));
    }

    [Fact]
    public async Task RenamingMovesWhichSpellingPlotsTheCourse()
    {
        var store = Store();
        store.Add("F1", "Current CG", "Deciat", At);

        var registry = Registry(store);

        var result = await registry.InvokeAsync(
            BookmarksCapability.RenameTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = "Current CG",
                    ["new_name"] = "Colonia Bridge",
                }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        var courseRouter = new KeywordRouter(
            CapabilityRegistry.Build([]), () => BookmarkCourse.Phrases(store, () => "F1"));

        var renamed = courseRouter.MatchToolCommand("set course for Colonia Bridge");
        Assert.NotNull(renamed);
        Assert.Equal("Deciat", renamed.Arguments.Values["system"]);

        Assert.Null(courseRouter.MatchToolCommand("set course for Current CG"));
    }
}
