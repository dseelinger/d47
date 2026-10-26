using D47.Core.Adventures;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

public sealed class ACodexPictureObjectiveWaitsOnTheCommandersOwnPictureTests
{
    private const string Key = "the-lantern-route";
    private const long EntryId = 2_100_301;
    private const long OtherEntryId = 2_100_302;
    private const int Region = 18;

    private static readonly string Folder = Path.Combine(MemoryInstall.FakeRoot, "codex-pictures");

    private readonly MemoryFileSystem _files = new();
    private readonly AdventureStore _store;
    private readonly AdventureBook _book;

    public ACodexPictureObjectiveWaitsOnTheCommandersOwnPictureTests()
    {
        _store = new AdventureStore(Path.Combine(Folder, "adventures.json"), _files, NullLogger<AdventureStore>.Instance);
        _store.Poll();
        _book = new AdventureBook(_store, NullLogger<AdventureBook>.Instance);
    }

    private static DateTimeOffset At(int seconds) => Accepted.AddSeconds(seconds);

    private static AdventureTrigger Picture(TriggerKind kind) => new() { Kind = kind, Count = 1 };

    private static JournalEvent Logged(long entry, int seconds, bool isNew) => Event(
        $$"""{ "timestamp":"{{Stamp(At(seconds))}}", "event":"CodexEntry", "EntryID":{{entry}}, "Region":"$Codex_RegionName_{{Region}};", "Category":"$Codex_Category_Biology;"{{(isNew ? ", \"IsNewEntry\":true" : string.Empty)}} }""");

    private static D47Event Saved(long entry, int seconds, bool onRecord = true) => D47Event.PictureSaved(entry, Region, onRecord, At(seconds));

    /// <summary>Arrive at the Lantern; then the objective under test; then fly home. The Commander arrives 60 seconds after acceptance, which is when the objective appears.</summary>
    private AdventureStanding AtTheObjective(TriggerKind kind, Action? beforeArriving = null)
    {
        var route = LanternRoute(Accepted);
        var adventure = route with { Beats = [route.Beats[0], Beat("The Picture", "catalyst", Picture(kind), "Pictured."), route.Beats[4]] };

        Assert.Null(_book.Write("F1", adventure));
        _book.CatchUp([]);
        beforeArriving?.Invoke();
        _book.Observe(Jump(Lantern, At(60)), "F1");

        return Standing();
    }

    private AdventureStanding Standing() => _book.Standing("F1", Key)!;

    [Fact]
    public void APictureForAnEntryLoggedNewSinceTheObjectiveAppearedMeetsTheNewDiscoveryObjective()
    {
        Assert.Equal(1, AtTheObjective(TriggerKind.NewCodexPicture).Current);

        _book.Observe(Logged(EntryId, 90, isNew: true), "F1");
        _book.Record("F1", Saved(EntryId, 120));

        Assert.Equal(2, Standing().Current);
        Assert.Equal(D47Event.CodexPicture, Standing().FiredBy[1]);
    }

    [Fact]
    public void APictureForAnotherEntryDoesNotMeetTheNewDiscoveryObjective()
    {
        AtTheObjective(TriggerKind.NewCodexPicture);

        _book.Observe(Logged(EntryId, 90, isNew: true), "F1");
        _book.Record("F1", Saved(OtherEntryId, 120));

        Assert.Equal(1, Standing().Current);
    }

    [Fact]
    public void AnEntryLoggedNewBeforeTheObjectiveAppearedDoesNotMeetTheNewDiscoveryObjective()
    {
        AtTheObjective(TriggerKind.NewCodexPicture, () => _book.Observe(Logged(EntryId, 30, isNew: true), "F1"));

        _book.Record("F1", Saved(EntryId, 120));

        Assert.Equal(1, Standing().Current);
    }

    [Fact]
    public void AnEntryLoggedAgainIsNotANewDiscovery()
    {
        AtTheObjective(TriggerKind.NewCodexPicture);

        _book.Observe(Logged(EntryId, 90, isNew: false), "F1");
        _book.Record("F1", Saved(EntryId, 120));

        Assert.Equal(1, Standing().Current);
    }

    [Fact]
    public void APictureSavedForAMissingEntryNeverMeetsEitherObjective()
    {
        AtTheObjective(TriggerKind.NewCodexPicture);
        _book.Observe(Logged(EntryId, 90, isNew: true), "F1");

        _book.Record("F1", Saved(EntryId, 120, onRecord: false));

        Assert.Equal(1, Standing().Current);
    }

    [Fact]
    public void APictureSavedBeforeTheEntryIsLoggedDoesNotMeetTheNewDiscoveryObjective()
    {
        AtTheObjective(TriggerKind.NewCodexPicture);

        _book.Record("F1", Saved(EntryId, 80, onRecord: false));
        _book.Observe(Logged(EntryId, 90, isNew: true), "F1");

        Assert.Equal(1, Standing().Current);
    }

    [Fact]
    public void APictureForAnEntryOnRecordMeetsTheKnownEntryObjective()
    {
        Assert.Equal(1, AtTheObjective(TriggerKind.CodexPicture).Current);

        _book.Record("F1", Saved(EntryId, 120));

        Assert.Equal(2, Standing().Current);
    }

    [Fact]
    public void APictureForAMissingEntryDoesNotMeetTheKnownEntryObjective()
    {
        AtTheObjective(TriggerKind.CodexPicture);

        _book.Record("F1", Saved(EntryId, 120, onRecord: false));

        Assert.Equal(1, Standing().Current);
    }

    [Fact]
    public void AnEntryWithoutANewPictureSinceTheObjectiveAppearedDoesNotMeetTheKnownEntryObjective()
    {
        AtTheObjective(TriggerKind.CodexPicture);

        _book.Observe(Logged(EntryId, 90, isNew: false), "F1");

        Assert.Equal(1, Standing().Current);
        Assert.Empty(_store.Find("F1", Key)!.Events);
    }

    [Fact]
    public void ThePictureEventNamesTheEntryTheRegionAndWhetherItIsOnRecord()
    {
        var made = Saved(EntryId, 120, onRecord: true);

        Assert.Equal(D47Event.CodexPicture, made.Name);
        Assert.Equal(["2100301", "18", "true"], made.Fields.Select(field => field.Value));
        Assert.Equal("false", Saved(EntryId, 120, onRecord: false).Fields[2].Value);
    }

    [Fact]
    public void TheObjectivesSayWhatTheyWaitFor()
    {
        Assert.Equal("log one new codex entry and save your own picture for it", Picture(TriggerKind.NewCodexPicture).Describe());
        Assert.Equal("save your own picture for one known codex entry", Picture(TriggerKind.CodexPicture).Describe());
        Assert.Equal("New codex entries pictured: 0 of 1", Picture(TriggerKind.NewCodexPicture).Progress(0));
        Assert.Equal("Known codex entries pictured: 0 of 1", Picture(TriggerKind.CodexPicture).Progress(0));
    }

    [Theory]
    [InlineData(TriggerKind.NewCodexPicture)]
    [InlineData(TriggerKind.CodexPicture)]
    public void TheObjectivesHaveRefusalAndConfirmText(TriggerKind kind)
    {
        Assert.NotEqual(kind.ToString(), RefusedActivities.Phrase(kind, null));
        Assert.NotNull(TriggerConfirms.Of(kind, story: false));
        Assert.Empty(AdventureValidation.CountedProblems("beat 1", Picture(kind)));
        Assert.Contains(kind.ToString().ToLowerInvariant(), AdventureValidation.Kinds);
    }
}
