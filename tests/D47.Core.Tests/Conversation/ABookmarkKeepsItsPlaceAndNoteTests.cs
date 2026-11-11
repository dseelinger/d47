using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A bookmark can point at a body or station and carry a note (#2302).</summary>
public class ABookmarkKeepsItsPlaceAndNoteTests
{
    private const long Address = 1000;

    private static readonly DateTimeOffset At = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly MemoryFileSystem _files = new();

    private string Path_ => Path.Combine(@"C:\d47-test", "bookmarks.json");

    private BookmarkStore Store() => new(Path_, _files, NullLogger<BookmarkStore>.Instance);

    private BookmarkStore Loaded()
    {
        var store = Store();
        store.Load();
        return store;
    }

    private static JournalLocation Here(string system = "Sol") =>
        new(system, null, false, null) { SystemAddress = Address };

    private static GameStatus Targeting(long system, long body, string? name) =>
        new() { Destination = new StatusDestination(system, body, name) };

    private static StarSystemProfile Profile() => new()
    {
        Name = "Sol",
        SystemAddress = Address,
        Bodies = [new BodyProfile { BodyId = 5, Name = "Earth", Type = "Planet" }],
        Stations = [new StationProfile { Name = "Daedalus" }],
    };

    [Fact]
    public void AFileWrittenBeforeThisChangeLoadsAsSystemBookmarks()
    {
        _files.WriteText(
            Path_,
            """{"commanders":[{"frontierId":"F1","bookmarks":[{"name":"Home","system":"Sol","madeAt":"2026-01-01T00:00:00+00:00"}]}]}""");

        var bookmark = Assert.Single(Loaded().For("F1"));

        Assert.Equal(BookmarkKind.System, bookmark.Kind);
        Assert.Null(bookmark.Place);
        Assert.Null(bookmark.SystemAddress);
        Assert.Null(bookmark.Note);
    }

    [Fact]
    public void EveryFieldOfABookmarkSurvivesAReload()
    {
        var written = new Bookmark("Dock", "Sol", At, BookmarkKind.Station, "Daedalus", Address, "Good prices");

        Assert.True(Store().Add("F1", written));

        Assert.Equal(written, Assert.Single(Loaded().For("F1")));
    }

    [Fact]
    public void AnUnknownKindSurvivesAReloadAsUnknown()
    {
        Store().Add("F1", new Bookmark("Dock", "Sol", At, null, "Somewhere", Address));

        Assert.Null(Assert.Single(Loaded().For("F1")).Kind);
    }

    [Fact]
    public void UpdateChangesTheLocationAndNote()
    {
        var store = Store();
        store.Add("F1", new Bookmark("Dock", "Sol", At));

        Assert.True(store.Update("F1", "Dock", new Bookmark("Dock", "Alpha Centauri", At, BookmarkKind.Body, "A 1", 7, "Rings")));

        var bookmark = Assert.Single(Loaded().For("F1"));
        Assert.Equal("Alpha Centauri", bookmark.System);
        Assert.Equal("A 1", bookmark.Place);
        Assert.Equal("Rings", bookmark.Note);
    }

    [Fact]
    public void UpdateCanGiveABookmarkANewName()
    {
        var store = Store();
        store.Add("F1", new Bookmark("Dock", "Sol", At));

        Assert.True(store.Update("F1", "Dock", new Bookmark("Harbour", "Sol", At)));

        Assert.Equal("Harbour", Assert.Single(store.For("F1")).Name);
    }

    [Fact]
    public void UpdateAllowsACaseOnlyChangeOfItsOwnName()
    {
        var store = Store();
        store.Add("F1", new Bookmark("Dock", "Sol", At));

        Assert.True(store.Update("F1", "Dock", new Bookmark("DOCK", "Sol", At, Note: "n")));

        Assert.Equal("DOCK", Assert.Single(store.For("F1")).Name);
    }

    [Fact]
    public void UpdateRefusesAnotherBookmarksName()
    {
        var store = Store();
        store.Add("F1", new Bookmark("Dock", "Sol", At));
        store.Add("F1", new Bookmark("Home", "Sol", At));

        Assert.False(store.Update("F1", "Dock", new Bookmark("home", "Elsewhere", At)));

        Assert.Contains(store.For("F1"), b => b is { Name: "Dock", System: "Sol" });
        Assert.Equal(2, store.For("F1").Count);
    }

    [Fact]
    public void UpdateRefusesAMissingBookmark()
    {
        var store = Store();

        Assert.False(store.Update("F1", "Nothing", new Bookmark("Nothing", "Sol", At)));
        Assert.Empty(store.For("F1"));
    }

    [Fact]
    public async Task ASystemDestinationIsASystem()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            Targeting(Address, 0, "Sol"), Here(), null, TestContext.Current.CancellationToken);

        Assert.Equal(BookmarkKind.System, place.Kind);
        Assert.Null(place.Place);
        Assert.Equal("Sol", place.System);
        Assert.Equal(Address, place.SystemAddress);
    }

    [Fact]
    public async Task ABodyIsMatchedByItsBodyId()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            Targeting(Address, 5, "Earth"), Here(), new Profiles(Profile()), TestContext.Current.CancellationToken);

        Assert.Equal(BookmarkKind.Body, place.Kind);
        Assert.Equal("Earth", place.Place);
        Assert.Equal("Sol", place.System);
        Assert.Equal(Address, place.SystemAddress);
    }

    [Fact]
    public async Task AStationIsMatchedByName()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            Targeting(Address, 9, "daedalus"), Here(), new Profiles(Profile()), TestContext.Current.CancellationToken);

        Assert.Equal(BookmarkKind.Station, place.Kind);
        Assert.Equal("daedalus", place.Place);
    }

    [Fact]
    public async Task ADestinationTheProfileDoesNotListHasNoKind()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            Targeting(Address, 9, "Unknown Rock"), Here(), new Profiles(Profile()), TestContext.Current.CancellationToken);

        Assert.Null(place.Kind);
        Assert.Equal("Unknown Rock", place.Place);
        Assert.Null(place.Error);
    }

    [Fact]
    public async Task NoServiceLeavesTheKindUnknown()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            Targeting(Address, 5, "Earth"), Here(), null, TestContext.Current.CancellationToken);

        Assert.Null(place.Kind);
        Assert.Equal("Earth", place.Place);
    }

    [Fact]
    public async Task AFailingServiceLeavesTheKindUnknown()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            Targeting(Address, 5, "Earth"), Here(), new Profiles(null), TestContext.Current.CancellationToken);

        Assert.Null(place.Kind);
        Assert.Null(place.Error);
    }

    [Fact]
    public async Task ADestinationInAnotherSystemIsRefused()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            Targeting(2000, 5, "Earth"), Here(), new Profiles(Profile()), TestContext.Current.CancellationToken);

        Assert.Equal(BookmarkPlace.AnotherSystem, place.Error);
    }

    [Fact]
    public async Task NothingTargetedIsRefused()
    {
        var place = await BookmarkPlace.FromDestinationAsync(
            GameStatus.Unknown, Here(), null, TestContext.Current.CancellationToken);

        Assert.Equal(BookmarkPlace.NothingTargeted, place.Error);
    }

    [Fact]
    public async Task TheVoiceToolStoresThePlaceAndListsIt()
    {
        var store = Store();
        var registry = CapabilityRegistry.Build(
        [
            BookmarksCapability.Create(
                store,
                () => "F1",
                () => Targeting(Address, 9, "Daedalus"),
                () => CommanderAt("Sol"),
                () => PhraseBook.From(CapabilityRegistry.Build([]), []),
                () => At,
                new Profiles(Profile())),
        ]);

        var made = await registry.InvokeAsync(
            BookmarksCapability.BookmarkTool, new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

        Assert.False(made.IsError);

        var stored = Assert.Single(store.For("F1"));
        Assert.Equal(BookmarkKind.Station, stored.Kind);
        Assert.Equal("Daedalus", stored.Place);
        Assert.Equal(Address, stored.SystemAddress);

        var listed = await registry.InvokeAsync(
            BookmarksCapability.ListTool, new ToolArguments(new Dictionary<string, string>()), TestContext.Current.CancellationToken);

        Assert.Equal("Daedalus, Daedalus, in Sol.", listed.Content);
    }

    [Fact]
    public void SettingCourseForAnyKindPlotsToItsSystem()
    {
        var store = Store();
        BookmarkKind?[] kinds = [BookmarkKind.System, BookmarkKind.Body, BookmarkKind.Station, null];

        foreach (var kind in kinds)
        {
            store.Add("F1", new Bookmark($"Mark {kind}", "Sol", At, kind, kind is BookmarkKind.System ? null : "Somewhere"));
        }

        var phrases = BookmarkCourse.Phrases(store, () => "F1").ToList();

        Assert.NotEmpty(phrases);
        Assert.All(phrases, phrase => Assert.Equal("Sol", phrase.Arguments["system"]));
    }

    private static CommanderGameState CommanderAt(string system)
    {
        var state = new CommanderGameState(new CommanderIdentity("F1", "Test Commander"));
        state.Apply(new JournalEvent(
            At,
            "Location",
            System.Text.Json.JsonDocument.Parse($$"""{"StarSystem":"{{system}}","SystemAddress":{{Address}}}""").RootElement));
        return state;
    }

    private sealed class Profiles(StarSystemProfile? profile) : IStarSystemService
    {
        public Task<StarSystemProfile?> ProfileAsync(long systemAddress, CancellationToken cancellationToken) =>
            profile is null ? throw new HttpRequestException("down") : Task.FromResult<StarSystemProfile?>(profile);

        public Task<IReadOnlyList<SystemNameMatch>> MatchNamesAsync(string typed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<SystemNameMatch>> PrefixNamesAsync(string typed, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PowerplayNeighbourhood> PowerplayNearAsync(string system, double lightYears, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
