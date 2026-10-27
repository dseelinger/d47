using D47.Core.Activities;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Journal;

/// <summary><c>NewCommander</c> empties activity dates, unread mail and every reputation reading, and keeps d47's own choices (#1804).</summary>
public sealed class AnEraseClearsActivitiesMailAndReputationTests
{
    private const string F = "F1";

    private const string G = "G2";

    private static JournalEvent Event(string json)
    {
        Assert.True(JournalEvent.TryParse(json, NullLogger.Instance, out var parsed));
        return parsed!;
    }

    private static JournalEvent Load(string fid, string at) =>
        Event($$"""{"timestamp":"2026-10-10T{{at}}Z","event":"LoadGame","FID":"{{fid}}","Commander":"Jameson"}""");

    private static JournalEvent Erase(string fid, string at) =>
        Event($$"""{"timestamp":"2026-10-10T{{at}}Z","event":"NewCommander","FID":"{{fid}}","Name":"Jameson","Package":"Default"}""");

    private static JournalEvent Refined(string at) =>
        Event($$"""{"timestamp":"2026-10-10T{{at}}Z","event":"MiningRefined","Type":"Gold"}""");

    private static JournalEvent Completed(string at, long id) =>
        Event($$"""{"timestamp":"2026-10-10T{{at}}Z","event":"MissionCompleted","Faction":"Party","Name":"Mission_Courier","MissionID":{{id}},"Reward":1000}""");

    [Fact]
    public void AnEraseLeavesOnlyTheActivitiesDoneAfterIt()
    {
        var ledger = new ActivityLedger(null, new MemoryFileSystem(), NullLogger.Instance);

        ledger.Apply([Load(G, "08:00:00"), Refined("08:10:00"), Load(F, "09:00:00"), Refined("09:10:00"), Erase(F, "10:00:00")]);

        Assert.Null(ledger.LastDone("mining", F));
        Assert.Equal(DateTimeOffset.Parse("2026-10-10T10:00:00Z"), ledger.CorpusFrom(F));
        Assert.NotNull(ledger.LastDone("mining", G));

        ledger.Apply([Load(F, "10:00:01"), Refined("11:00:00")]);

        Assert.Equal(DateTimeOffset.Parse("2026-10-10T11:00:00Z"), ledger.LastDone("mining", F));
    }

    [Fact]
    public void FoldingTheSameEraseAgainChangesNoActivityDate()
    {
        var ledger = new ActivityLedger(null, new MemoryFileSystem(), NullLogger.Instance);

        ledger.Apply([Load(G, "08:00:00"), Refined("08:10:00"), Load(F, "09:00:00"), Refined("09:10:00"), Erase(F, "10:00:00"), Load(F, "10:00:01"), Refined("11:00:00")]);

        var mining = ledger.LastDone("mining", F);
        var from = ledger.CorpusFrom(F);
        var other = ledger.LastDone("mining", G);

        ledger.Apply([Erase(F, "10:00:00")]);

        Assert.Equal(mining, ledger.LastDone("mining", F));
        Assert.Equal(from, ledger.CorpusFrom(F));
        Assert.Equal(other, ledger.LastDone("mining", G));
    }

    [Fact]
    public void FoldingTheSameEraseAgainChangesNoMail()
    {
        var mail = new MailLedger("mail-read.json", new MemoryFileSystem(), NullLogger.Instance);

        mail.Fold([Load(G, "08:00:00"), Completed("08:10:00", 1), Load(F, "09:00:00"), Completed("09:10:00", 2), Erase(F, "10:00:00"), Load(F, "10:00:01"), Completed("11:00:00", 3)], live: true);

        var forF = mail.Compose(F);
        var forG = mail.Compose(G);

        mail.Fold([Erase(F, "10:00:00")], live: true);

        Assert.Equal(forF, mail.Compose(F));
        Assert.Equal(forG, mail.Compose(G));
    }

    [Fact]
    public void AnEraseKeepsWhichActivitiesAreSuggested()
    {
        var ledger = new ActivityLedger(null, new MemoryFileSystem(), NullLogger.Instance);

        ledger.Apply([Load(F, "09:00:00"), Refined("09:10:00")]);
        ledger.SetSuggested("mining", F, suggested: false);
        ledger.Apply([Erase(F, "10:00:00")]);

        Assert.False(ledger.IsSuggested("mining", F));
    }

    [Fact]
    public void AnEraseLeavesOnlyTheMailReceivedAfterIt()
    {
        var mail = new MailLedger("mail-read.json", new MemoryFileSystem(), NullLogger.Instance);
        var empty = mail.Compose(F);

        mail.Fold([Load(G, "08:00:00"), Completed("08:10:00", 1), Load(F, "09:00:00"), Completed("09:10:00", 2), Erase(F, "10:00:00")], live: true);

        Assert.Equal(empty, mail.Compose(F));
        Assert.NotEqual(empty, mail.Compose(G));

        mail.Fold([Load(F, "10:00:01"), Completed("11:00:00", 3)], live: true);

        Assert.NotEqual(empty, mail.Compose(F));
    }

    [Fact]
    public void MailAfterAnEraseWithNoLoginIsFoldedFromHistory()
    {
        var files = new MemoryFileSystem();
        files.WriteText(
            "Journal.log",
            string.Join('\n', new[] { Load(G, "08:00:00"), Completed("08:10:00", 1), Erase(F, "10:00:00"), Completed("10:10:00", 2) }.Select(line => line.Raw.GetRawText())) + "\n");
        var mail = new MailLedger("mail-read.json", files, NullLogger.Instance);
        var empty = mail.Compose(F);

        mail.FoldHistory(["Journal.log"], TestContext.Current.CancellationToken);

        Assert.NotEqual(empty, mail.Compose(F));
        Assert.NotEqual(empty, mail.Compose(G));
    }

    [Fact]
    public void AnEraseClearsSuperpowerReputation()
    {
        var store = new GameStateStore();

        store.Apply(Load(F, "09:00:00"));
        store.Apply(Event("""{"timestamp":"2026-10-10T09:01:00Z","event":"Reputation","Federation":10.0,"Empire":5.0,"Alliance":1.0}"""));
        Assert.True(store.Active!.Reputation.IsKnown);

        store.Apply(Erase(F, "10:00:00"));

        Assert.False(store.Active!.Reputation.IsKnown);
    }
}
