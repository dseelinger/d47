using D47.Core.Campaigns;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Journal;
using D47.Core.Knowledge;
using D47.Core.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Campaigns.CampaignReports;

namespace D47.Core.Tests.Campaigns;

/// <summary>
/// The campaign faction's report is fetched on the pool at session start and after each new tick, a failure keeps
/// the fronts held, and get_campaign_fronts answers from the snapshot without fetching.
/// </summary>
public sealed class CampaignFrontsAreFetchedOffTheTickTests
{
    private readonly CampaignStore _store = new("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);

    private readonly CommanderGameState _state = new(new CommanderIdentity("F1", "Fixture"));

    private readonly Queue<Func<Task>> _dispatched = new();

    private readonly List<(string Faction, bool Refresh)> _fetches = [];

    private readonly Dictionary<string, FactionReport> _reports = new(StringComparer.OrdinalIgnoreCase);

    private DateTimeOffset? _tick = Now.AddHours(-5);

    private bool _fail;

    public CampaignFrontsAreFetchedOffTheTickTests()
    {
        _store.SetFaction("F1", Us, clearIntents: false);
        _reports[Us] = Report(Place("Lave", 0.40, others: [new FactionNeighbour("Lave Purple Gang", 0.36)]));
        _reports["Diso Gold Cartel"] = Report(Place("Diso", 0.40, active: ["Famine"])) with { Name = "Diso Gold Cartel" };
    }

    private CampaignFrontsWatch Watch() =>
        new(
            (faction, refresh, _) =>
            {
                _fetches.Add((faction, refresh));

                return _fail ? throw new GalaxyUnavailableException("Galaxy search did not answer.") : Task.FromResult(_reports[faction]);
            },
            () => _state,
            _store,
            null,
            () => _tick,
            null,
            NullLogger<CampaignFrontsWatch>.Instance)
        {
            Dispatch = _dispatched.Enqueue,
        };

    private void RunDispatched()
    {
        while (_dispatched.TryDequeue(out var work))
        {
            work().GetAwaiter().GetResult();
        }
    }

    private static ToolResult Ask(CampaignStore store, CommanderGameState state, CampaignFrontsWatch watch)
    {
        var tool = CampaignCapability.Create(store, () => state, fronts: () => watch.Snapshot).Tools
            .Single(tool => tool.Name == CampaignCapability.FrontsTool);

        return tool.Handler(ToolArguments.Empty, CancellationToken.None).GetAwaiter().GetResult();
    }

    [Fact]
    public void TheFirstPollStartsAFetchAndReturnsWithoutIt()
    {
        var watch = Watch();

        watch.Poll(Now);

        Assert.Single(_dispatched);
        Assert.Empty(_fetches);
        Assert.Null(watch.Snapshot!.Report);
    }

    [Fact]
    public void TheFetchedReportsFrontsAreHeldWithWhenTheyWereComputed()
    {
        var watch = Watch();

        watch.Poll(Now);
        RunDispatched();
        watch.Poll(Now.AddSeconds(1));

        var snapshot = watch.Snapshot!;

        Assert.Equal([(Us, false)], _fetches);
        Assert.Same(_reports[Us], snapshot.Report);
        Assert.Equal(FrontKind.ThinMargin, Assert.Single(snapshot.Fronts).Kind);
        Assert.Equal(Now.AddSeconds(1), snapshot.ComputedAt);
        Assert.Equal(Now, snapshot.ReportFetchedAt);
        Assert.Null(snapshot.Failure);
    }

    [Fact]
    public void ANewTickFetchesAFreshReportAndTheRestoredTickDoesNot()
    {
        var watch = Watch();

        watch.Poll(Now);
        RunDispatched();
        watch.Poll(Now.AddSeconds(1));
        watch.Poll(Now.AddHours(2));

        Assert.Empty(_dispatched);

        _tick = Now.AddHours(3);
        watch.Poll(Now.AddHours(3));
        RunDispatched();

        Assert.Equal([(Us, false), (Us, true)], _fetches);
    }

    [Fact]
    public void AFailedFetchKeepsTheFrontsAndSaysWhenTheyWereComputed()
    {
        var watch = Watch();

        watch.Poll(Now);
        RunDispatched();
        watch.Poll(Now.AddSeconds(1));

        _fail = true;
        _tick = Now.AddHours(1);
        watch.Poll(Now.AddHours(1));
        RunDispatched();
        watch.Poll(Now.AddHours(1).AddSeconds(1));

        var snapshot = watch.Snapshot!;

        Assert.Single(snapshot.Fronts);
        Assert.Equal(Now, snapshot.ReportFetchedAt);
        Assert.Equal("Galaxy search did not answer.", snapshot.Failure);

        var answer = Ask(_store, _state, watch);

        Assert.Contains("Computed 2026-11-09 12:00 UTC.", answer.Content, StringComparison.Ordinal);
        Assert.Contains("The last fetch failed: Galaxy search did not answer.", answer.Content, StringComparison.Ordinal);
        Assert.Contains("these fronts are from an older report", answer.Spoken, StringComparison.Ordinal);
    }

    [Fact]
    public void AFailedFetchIsTriedAgainAfterHalfAnHour()
    {
        _fail = true;
        var watch = Watch();

        watch.Poll(Now);
        RunDispatched();
        watch.Poll(Now.AddMinutes(29));

        Assert.Empty(_dispatched);

        _fail = false;
        watch.Poll(Now.AddMinutes(31));
        RunDispatched();
        watch.Poll(Now.AddMinutes(32));

        Assert.Equal(2, _fetches.Count);
        Assert.NotNull(watch.Snapshot!.Report);
        Assert.Null(watch.Snapshot.Failure);
    }

    [Fact]
    public void NamingAnotherFactionFetchesItsReportAndDropsTheOldFronts()
    {
        var watch = Watch();

        watch.Poll(Now);
        RunDispatched();
        watch.Poll(Now.AddSeconds(1));

        _store.SetFaction("F1", "Diso Gold Cartel", clearIntents: true);
        watch.Poll(Now.AddSeconds(2));

        Assert.Equal("Diso Gold Cartel", watch.Snapshot!.Faction);
        Assert.Empty(watch.Snapshot.Fronts);

        RunDispatched();
        watch.Poll(Now.AddSeconds(3));

        Assert.Equal(FrontKind.BadState, Assert.Single(watch.Snapshot!.Fronts).Kind);
    }

    [Fact]
    public void NoCampaignFactionMeansNoFetch()
    {
        var store = new CampaignStore("campaigns.json", new MemoryFileSystem(), NullLogger<CampaignStore>.Instance);
        var watch = new CampaignFrontsWatch(
            (_, _, _) => throw new InvalidOperationException("No fetch is expected."),
            () => _state,
            store,
            null,
            () => _tick,
            null,
            NullLogger<CampaignFrontsWatch>.Instance)
        {
            Dispatch = _dispatched.Enqueue,
        };

        watch.Poll(Now);

        Assert.Empty(_dispatched);
        Assert.Null(watch.Snapshot);
    }

    [Fact]
    public void ChangingAnIntentRecomputesTheFronts()
    {
        var watch = Watch();

        watch.Poll(Now);
        RunDispatched();
        watch.Poll(Now.AddSeconds(1));

        _store.SetIntent("F1", "Lave", SystemIntent.LeaveAlone);
        watch.Poll(Now.AddSeconds(2));

        Assert.Empty(watch.Snapshot!.Fronts);
        Assert.Equal(Now.AddSeconds(2), watch.Snapshot.ComputedAt);
    }

    [Fact]
    public void TheToolNamesTheTopThreeWithReasonsAndCountsTheRest()
    {
        _reports[Us] = Report(
            Place("Alpha", 0.40, active: ["War"], others: [new FactionNeighbour("Rival", 0.40) { ActiveStates = ["War"] }]),
            Place("Beta", 0.40, pending: ["Retreat"]),
            Place("Gamma", 0.40, active: ["Famine"]),
            Place("Delta", 0.40, active: ["Expansion"]),
            Place("Epsilon", 0.40, reportedAt: Now.AddDays(-40)));
        var watch = Watch();

        watch.Poll(Now);
        RunDispatched();
        watch.Poll(Now);

        var answer = Ask(_store, _state, watch);

        Assert.Equal(
            $"{Us} has 5 fronts. Conflict in Alpha: War against Rival, active. Retreat in Beta: Retreat is pending here. "
            + "Bad state in Gamma: Famine is active. And 2 more.",
            answer.Spoken);
        Assert.Equal(answer.Spoken + " Computed 2026-11-09 12:00 UTC.", answer.Content);
    }

    [Fact]
    public void TheToolSaysWhenThereIsNoReportYet()
    {
        var watch = Watch();

        watch.Poll(Now);

        Assert.Equal($"There are no fronts for {Us} yet. D47 is fetching the faction report.", Ask(_store, _state, watch).Content);
    }

    [Fact]
    public void TheToolNeverFetches()
    {
        var watch = Watch();

        Ask(_store, _state, watch);

        Assert.Empty(_fetches);
        Assert.Empty(_dispatched);
    }
}
