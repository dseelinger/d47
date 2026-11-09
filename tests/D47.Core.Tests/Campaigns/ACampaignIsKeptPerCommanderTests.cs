using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Campaigns;

/// <summary><see cref="CampaignStore"/> round trips each Commander's faction, intents and chronicle through <c>campaigns.json</c>.</summary>
public sealed class ACampaignIsKeptPerCommanderTests
{
    private const string Path = "campaigns.json";

    private readonly MemoryFileSystem _files = new();

    private CampaignStore Store()
    {
        var store = new CampaignStore(Path, _files, NullLogger<CampaignStore>.Instance);
        store.Load();
        return store;
    }

    [Fact]
    public void TheFactionAndIntentsSurviveARestart()
    {
        var store = Store();

        Assert.True(store.SetFaction("F1", "Kestrel Reach Compact", clearIntents: false));
        Assert.True(store.SetIntent("F1", "Lave", SystemIntent.Grow));
        Assert.True(store.SetIntent("F1", "Diso", SystemIntent.StayUnder75));
        Assert.True(store.SetIntent("F2", "Leesti", SystemIntent.TakeControl));

        var reloaded = Store();
        var first = reloaded.For("F1");

        Assert.Equal("Kestrel Reach Compact", first.Faction);
        Assert.Equal(SystemIntent.Grow, first.Intents["lave"]);
        Assert.Equal(SystemIntent.StayUnder75, first.Intents["Diso"]);
        Assert.Null(reloaded.For("F2").Faction);
        Assert.Equal(SystemIntent.TakeControl, reloaded.For("F2").Intents["Leesti"]);
        Assert.Same(Campaign.None, reloaded.For("F3"));
    }

    [Fact]
    public void IntentsAreWrittenByName()
    {
        Store().SetIntent("F1", "Lave", SystemIntent.ExpandFromHere);

        Assert.Contains("\"Lave\": \"ExpandFromHere\"", _files.ReadText(Path), StringComparison.Ordinal);
    }

    [Fact]
    public void ASecondIntentForASystemReplacesTheFirst()
    {
        var store = Store();

        store.SetIntent("F1", "Lave", SystemIntent.Grow);
        store.SetIntent("F1", "LAVE", SystemIntent.LeaveAlone);

        var intent = Assert.Single(Store().For("F1").Intents);
        Assert.Equal("LAVE", intent.Key);
        Assert.Equal(SystemIntent.LeaveAlone, intent.Value);
    }

    [Fact]
    public void NamingAFactionWithClearIntentsDropsThem()
    {
        var store = Store();
        store.SetIntent("F1", "Lave", SystemIntent.Grow);

        store.SetFaction("F1", "Hutton Truckers", clearIntents: true);

        Assert.Empty(Store().For("F1").Intents);
        Assert.Equal("Hutton Truckers", Store().For("F1").Faction);
    }

    [Fact]
    public void TheChronicleSurvivesAWriteAndARestart()
    {
        _files.WriteText(
            Path,
            """
            {
              "commanders": [
                {
                  "frontierId": "F1",
                  "faction": "Kestrel Reach Compact",
                  "chronicle": [
                    {
                      "kind": "War",
                      "system": "Lave",
                      "opponent": "Lave Purple Gang",
                      "outcome": "Won",
                      "ticks": ["2026-11-01T14:00:00+00:00", "2026-11-02T14:00:00+00:00"],
                      "work": [{ "kind": "CombatBond", "amount": 2400000 }]
                    },
                    { "kind": "Election", "system": "Diso", "outcome": "Lost", "ticks": [] }
                  ]
                }
              ]
            }
            """);

        Store().SetIntent("F1", "Lave", SystemIntent.Hold);

        var chronicle = Store().For("F1").Chronicle;

        Assert.Equal(2, chronicle.Count);
        Assert.Equal("War", chronicle[0].Kind);
        Assert.Equal("Lave Purple Gang", chronicle[0].Opponent);
        Assert.Equal("Won", chronicle[0].Outcome);
        Assert.Equal(2, chronicle[0].Ticks.Count);
        Assert.Equal(new ChronicleWork(BgsWorkKind.CombatBond, 2_400_000), Assert.Single(chronicle[0].Work));
        Assert.Null(chronicle[1].Opponent);
        Assert.Empty(chronicle[1].Work);
    }

    [Fact]
    public void AnUnreadableFileHoldsNoCampaigns()
    {
        _files.WriteText(Path, "{ not json");

        Assert.Same(Campaign.None, Store().For("F1"));
    }

    [Fact]
    public void AnUnreadableFileIsNotWrittenOver()
    {
        const string unreadable = """{ "commanders": [ { "frontierId": "F1", "intents": { "Lave": "Conquer" } } ] }""";
        _files.WriteText(Path, unreadable);

        Assert.False(Store().SetIntent("F1", "Diso", SystemIntent.Grow));
        Assert.Equal(unreadable, _files.ReadText(Path));
    }

    [Fact]
    public void TwoSpellingsOfOneSystemLoadAsOne()
    {
        _files.WriteText(Path, """{ "commanders": [ { "frontierId": "F1", "intents": { "Lave": "Grow", "LAVE": "Hold" } } ] }""");

        var intent = Assert.Single(Store().For("F1").Intents);
        Assert.Equal(SystemIntent.Hold, intent.Value);
    }

    [Fact]
    public void AFailedWriteLeavesTheCampaignAsItWas()
    {
        var store = new CampaignStore(Path, new RefusingFiles(), NullLogger<CampaignStore>.Instance);

        Assert.False(store.SetIntent("F1", "Lave", SystemIntent.Grow));
        Assert.Same(Campaign.None, store.For("F1"));
    }

    [Theory]
    [InlineData(true, SystemIntent.Hold)]
    [InlineData(false, SystemIntent.LeaveAlone)]
    public void ASystemWithNoIntentFollowsControl(bool controls, SystemIntent expected) =>
        Assert.Equal(expected, Campaign.None.IntentFor("Lave", controls));

    [Fact]
    public void ASetIntentWinsOverControl() =>
        Assert.Equal(
            SystemIntent.Grow,
            (Campaign.None with { Intents = Campaign.None.Intents.Add("Lave", SystemIntent.Grow) }).IntentFor("lave", controls: true));

    [Fact]
    public void TheFactionFollowsTheSquadronUntilOneIsNamed()
    {
        Assert.Equal("Squadron Faction", Campaign.None.FactionOr("Squadron Faction"));
        Assert.Null(Campaign.None.FactionOr(null));
        Assert.Equal("Named", (Campaign.None with { Faction = "Named" }).FactionOr("Squadron Faction"));
    }

    private sealed class RefusingFiles : IFileSystem
    {
        private readonly MemoryFileSystem _inner = new();

        public FileState? Stat(string path) => _inner.Stat(path);

        public DateTime? FolderWritten(string folder) => _inner.FolderWritten(folder);

        public string? ReadText(string path) => _inner.ReadText(path);

        public byte[]? ReadBytes(string path) => _inner.ReadBytes(path);

        public Stream? OpenRead(string path) => _inner.OpenRead(path);

        public void WriteText(string path, string contents) => throw new IOException("Refused.");

        public void WriteBytes(string path, byte[] contents) => throw new IOException("Refused.");

        public void AppendText(string path, string contents) => throw new IOException("Refused.");

        public void CreateFolder(string folder) => _inner.CreateFolder(folder);

        public void Delete(string path) => _inner.Delete(path);

        public void DeleteFolder(string folder) => _inner.DeleteFolder(folder);

        public void Copy(string from, string to) => _inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            _inner.Enumerate(folder, pattern, recursive);
    }
}
