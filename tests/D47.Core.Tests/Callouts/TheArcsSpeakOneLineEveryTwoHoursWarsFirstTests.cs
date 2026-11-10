using D47.Core.Callouts;
using D47.Core.Campaigns;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using D47.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Callouts;

/// <summary>
/// The arcs speak one engine's lines a pass and then wait two hours: wars first, then elections, then the rest. An engine
/// whose lines are not spoken saves nothing and offers them again after the wait.
/// </summary>
public sealed class TheArcsSpeakOneLineEveryTwoHoursWarsFirstTests
{
    private const string Rival = "Lave Purple Gang";

    private readonly RefusableFiles _files = new();

    private readonly CampaignStore _store;

    private CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private CampaignFrontsSnapshot? _snapshot;

    private DateTimeOffset _clock = Now;

    public TheArcsSpeakOneLineEveryTwoHoursWarsFirstTests()
    {
        _store = new CampaignStore("campaigns.json", _files, NullLogger<CampaignStore>.Instance);
        _store.SetFaction("F1", Us, clearIntents: false);
    }

    private static FactionSystem[] Expansions(int count) =>
    [
        .. Enumerable.Range(1, count)
            .Select(n => Place($"Expansion {n}", 0.4, others: [new FactionNeighbour($"Expanding {n}", 0.2) { PendingStates = ["Expansion"] }])),
    ];

    private static Front Conflict(string system, string type, int ours, int theirs) =>
        new(FrontKind.Conflict, system, $"{type} against {Rival}.", SystemIntent.Hold, true, Rival, "", ours - theirs, Now, false)
        {
            Conflict = new FactionConflict(system, type, true, OpponentKind.Named, Rival, [], true, ours, theirs, "", ""),
        };

    private void Hold(IReadOnlyList<Front> fronts, params FactionSystem[] systems)
    {
        Front[] expanding =
        [
            .. systems.SelectMany(system => system.Others
                .Where(other => other.PendingStates.Contains("Expansion"))
                .Select(other => new Front(FrontKind.ThinMargin, system.Name, "Thin margin.", SystemIntent.Hold, true, other.Name, "", 0.05, Now, false))),
        ];

        _snapshot = new CampaignFrontsSnapshot(_state.Identity.FrontierId!, Us, Report(systems), _clock, [.. fronts, .. expanding], _clock, null, null);
    }

    private CampaignArcCallout Arcs() => new(new NearbyFight())
    {
        Campaigns = _store,
        Fronts = () => _snapshot,
    };

    private List<Announcement> Examine(CampaignArcCallout arcs, bool priming = false, StatusFlags flags = StatusFlags.Docked) =>
        [.. arcs.Examine(new CalloutContext(_clock, priming, _state, new GameStatus { Flags = flags }, NavRoute.None, []))];

    private CampaignArcCallout Started()
    {
        var arcs = Arcs();
        Examine(arcs, priming: true);
        return arcs;
    }

    /// <summary>
    /// A chosen war front the council sides on, an election front the Nemesis has just won a day in, and an Expansion
    /// threat for the Informant: in engine order the Nemesis would speak first and the council last.
    /// </summary>
    private CampaignArcCallout ThreeDue()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        _store.SetCouncil("F1", new CampaignCouncil("Director Varga", "Envoy Quist", Now, null, []));

        FactionSystem[] systems =
        [
            Place("Lave", 0.4, active: ["War"], others: [new FactionNeighbour(Rival, 0.4) { ActiveStates = ["War"] }]),
            Place("Leesti", 0.4, pending: ["Election"], others: [new FactionNeighbour(Rival, 0.4) { PendingStates = ["Election"] }]),
            Place("Diso", 0.4, others: [new FactionNeighbour("Second Rival", 0.2) { PendingStates = ["Expansion"] }]),
        ];

        Hold([Conflict("Lave", "War", 1, 1), Conflict("Leesti", "Election", 0, 0)], systems);
        var arcs = Started();

        Hold([Conflict("Lave", "War", 1, 1), Conflict("Leesti", "Election", 0, 1)], systems);
        _store.Choose("F1", FrontKind.Conflict, "Lave", _clock);

        return arcs;
    }

    [Fact]
    public void AHundredExpansionsGiveOneLeakAndTheNextWaitsTwoHours()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        Hold([], Expansions(100));
        var arcs = Started();

        var first = Assert.Single(Examine(arcs));
        Assert.Equal(CampaignArcCallout.KeyPrefix + "leak", first.Key);

        _clock += CampaignArcCallout.Gap - TimeSpan.FromMinutes(1);
        Assert.Empty(Examine(arcs));

        _clock += TimeSpan.FromMinutes(1);
        var second = Assert.Single(Examine(arcs));

        Assert.Equal(CampaignArcCallout.KeyPrefix + "leak", second.Key);
        Assert.NotEqual(first.Arc!.System, second.Arc!.System);
        Assert.Equal(2, _store.For("F1").Informant!.Leaks.Count);
    }

    [Fact]
    public void AFreshCalloutSpeaksOnItsFirstPassThatIsNotHeld()
    {
        Hold([], Expansions(3));
        var arcs = Started();

        Assert.Empty(Examine(arcs, flags: StatusFlags.Supercruise));
        Assert.Single(Examine(arcs));
    }

    [Fact]
    public void AnotherCommanderIsNotHeldByTheFirstCommandersWait()
    {
        Hold([], Expansions(3));
        var arcs = Started();
        Assert.Single(Examine(arcs));

        _store.SetFaction("F2", Us, clearIntents: false);
        _state = new CommanderGameState(new CommanderIdentity("F2", "Second Fixture"));
        Hold([], Expansions(3));

        Assert.Empty(Examine(arcs));
        Assert.Single(Examine(arcs));
    }

    [Fact]
    public void PassesTwoHoursApartSpeakTheWarThenTheElectionThenTheLeak()
    {
        var arcs = ThreeDue();

        var war = Assert.Single(Examine(arcs));
        _clock += CampaignArcCallout.Gap;
        var election = Assert.Single(Examine(arcs));
        _clock += CampaignArcCallout.Gap;
        var leak = Assert.Single(Examine(arcs));

        Assert.Equal((CampaignArcCallout.KeyPrefix + "sided", "Lave"), (war.Key, war.Arc!.System));
        Assert.Equal((CampaignArcCallout.KeyPrefix + "broadcast", "Leesti"), (election.Key, election.Arc!.System));
        Assert.Equal((CampaignArcCallout.KeyPrefix + "leak", "Diso"), (leak.Key, leak.Arc!.System));
    }

    [Fact]
    public void AStepThatLosesIsNotSavedAndIsSpokenOnALaterPass()
    {
        var arcs = ThreeDue();

        Assert.Single(Examine(arcs));
        Assert.Null(_store.For("F1").Informant);

        _clock += CampaignArcCallout.Gap - TimeSpan.FromMinutes(1);
        Assert.Empty(Examine(arcs));
        Assert.Null(_store.For("F1").Informant);

        _clock += TimeSpan.FromMinutes(1);
        Assert.Single(Examine(arcs));
        Assert.Null(_store.For("F1").Informant);

        _clock += CampaignArcCallout.Gap;
        Assert.Equal(CampaignArcCallout.KeyPrefix + "leak", Assert.Single(Examine(arcs)).Key);
        Assert.Single(_store.For("F1").Informant!.Leaks);
    }

    [Fact]
    public void ACrisisThatLostToAWarIsStillSpokenAfterANewReport()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        var lave = Place("Lave", 0.4, active: ["War"], others: [new FactionNeighbour(Rival, 0.4) { ActiveStates = ["War"] }]);

        Hold([Conflict("Lave", "War", 1, 1)], lave, Place("Diso", 0.4));
        var arcs = Started();

        Hold([Conflict("Lave", "War", 1, 2)], lave, Place("Diso", 0.4, active: ["Famine"]));
        Assert.Equal(CampaignArcCallout.KeyPrefix + "broadcast", Assert.Single(Examine(arcs)).Key);
        Assert.Empty(_store.For("F1").Crises);

        _clock += TimeSpan.FromMinutes(30);
        Hold([Conflict("Lave", "War", 1, 2)], lave, Place("Diso", 0.4, active: ["Famine"]));
        Assert.Empty(Examine(arcs));

        _clock = Now + CampaignArcCallout.Gap;
        var crisis = Assert.Single(Examine(arcs));

        Assert.Equal((CampaignArcCallout.KeyPrefix + "crisis", "Diso"), (crisis.Key, crisis.Arc!.System));
        Assert.Single(_store.For("F1").Crises);
    }

    [Fact]
    public void ACrisisThatBeganDuringTheWaitIsSpokenAfterIt()
    {
        _store.SetArc("F1", new CampaignArc(ArcKind.Nemesis, Rival, "Prefect Mbeki", Now, 0));
        var lave = Place("Lave", 0.4, active: ["War"], others: [new FactionNeighbour(Rival, 0.4) { ActiveStates = ["War"] }]);

        Hold([Conflict("Lave", "War", 1, 1)], lave, Place("Diso", 0.4));
        var arcs = Started();

        Hold([Conflict("Lave", "War", 1, 2)], lave, Place("Diso", 0.4));
        Assert.Equal(CampaignArcCallout.KeyPrefix + "broadcast", Assert.Single(Examine(arcs)).Key);

        _clock += TimeSpan.FromMinutes(30);
        Hold([Conflict("Lave", "War", 1, 2)], lave, Place("Diso", 0.4, active: ["Famine"]));
        Assert.Empty(Examine(arcs));

        _clock += TimeSpan.FromMinutes(30);
        Hold([Conflict("Lave", "War", 1, 2)], lave, Place("Diso", 0.4, active: ["Famine"]));
        Assert.Empty(Examine(arcs));

        _clock = Now + CampaignArcCallout.Gap;
        var crisis = Assert.Single(Examine(arcs));

        Assert.Equal((CampaignArcCallout.KeyPrefix + "crisis", "Diso"), (crisis.Key, crisis.Arc!.System));
    }

    [Fact]
    public void AStepThatCannotBeSavedIsNotSpokenAndIsTriedAgainAfterTheWait()
    {
        var arcs = ThreeDue();
        _files.Refuse = true;

        Assert.Empty(Examine(arcs));
        Assert.Empty(_store.For("F1").Council!.Sidings);

        _files.Refuse = false;
        _clock += CampaignArcCallout.Gap - TimeSpan.FromMinutes(1);
        Assert.Empty(Examine(arcs));

        _clock += TimeSpan.FromMinutes(1);
        var war = Assert.Single(Examine(arcs));

        Assert.Equal((CampaignArcCallout.KeyPrefix + "sided", "Lave"), (war.Key, war.Arc!.System));
        Assert.Single(_store.For("F1").Council!.Sidings);
    }

    /// <summary>A file system whose writes fail while <see cref="Refuse"/> is set.</summary>
    private sealed class RefusableFiles : IFileSystem
    {
        private readonly MemoryFileSystem _inner = new();

        public bool Refuse { get; set; }

        public FileState? Stat(string path) => _inner.Stat(path);

        public DateTime? FolderWritten(string folder) => _inner.FolderWritten(folder);

        public string? ReadText(string path) => _inner.ReadText(path);

        public byte[]? ReadBytes(string path) => _inner.ReadBytes(path);

        public Stream? OpenRead(string path) => _inner.OpenRead(path);

        public void WriteText(string path, string contents)
        {
            if (Refuse)
            {
                throw new IOException("Refused.");
            }

            _inner.WriteText(path, contents);
        }

        public void WriteBytes(string path, byte[] contents)
        {
            if (Refuse)
            {
                throw new IOException("Refused.");
            }

            _inner.WriteBytes(path, contents);
        }

        public void AppendText(string path, string contents) => _inner.AppendText(path, contents);

        public void CreateFolder(string folder) => _inner.CreateFolder(folder);

        public void Delete(string path) => _inner.Delete(path);

        public void DeleteFolder(string folder) => _inner.DeleteFolder(folder);

        public void Copy(string from, string to) => _inner.Copy(from, to);

        public IReadOnlyList<string> Enumerate(string folder, string pattern, bool recursive = false) =>
            _inner.Enumerate(folder, pattern, recursive);
    }
}
