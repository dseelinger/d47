using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class ColonisationPlansAreFetchedOffTheTickTests
{
    private const string System = "Hyades Sector AB-C d1";
    private const long MarketId = 3960010001;

    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class Service : IColonisationPlanService
    {
        public List<string> Asked { get; } = [];

        /// <summary>The next answer; null answers at once with a plan.</summary>
        public Func<string, Task<SystemPlan?>>? Answer { get; set; }

        public Task<SystemPlan?> SystemAsync(string systemName, CancellationToken cancellationToken)
        {
            Asked.Add(systemName);
            return Answer?.Invoke(systemName) ?? Task.FromResult<SystemPlan?>(PlanFor(systemName));
        }
    }

    private sealed class Rig
    {
        public Service Service { get; } = new();

        public bool Enabled { get; set; } = true;

        public CommanderGameState State { get; } = new(new CommanderIdentity("F1", "Tester"));

        public ColonisationPlans Plans()
        {
            var plans = new ColonisationPlans(Service, () => Enabled, () => State, NullLogger<ColonisationPlans>.Instance)
            {
                Dispatch = work => _ = work(),
            };

            return plans;
        }

        public void Docked(DateTimeOffset at) =>
            State.Colonisation = State.Colonisation.Apply(Depot(at), System, "Planetary Construction Site");
    }

    private static SystemPlan PlanFor(string system) =>
        new(system, [new PlannedSite("site-1", "Port", PlanStatus.Build) { MarketId = MarketId }], Start);

    private static JournalEvent Depot(DateTimeOffset at) =>
        JournalEvent.TryParse(
            $$"""{"timestamp":"{{at:yyyy-MM-ddTHH:mm:ssZ}}","event":"ColonisationConstructionDepot","MarketID":{{MarketId}},"ConstructionProgress":0.1,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$steel_name;","Name_Localised":"Steel","RequiredAmount":1000,"ProvidedAmount":0,"Payment":100}]}""",
            NullLogger.Instance,
            out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    [Fact]
    public async Task PollAndWantReturnWithoutAwaitingTheService()
    {
        var rig = new Rig();
        var pending = new TaskCompletionSource<SystemPlan?>();
        rig.Service.Answer = _ => pending.Task;
        rig.Docked(Start);
        var plans = rig.Plans();

        plans.Poll(Start);
        plans.Want("Other System", Start);

        Assert.Equal([System, "Other System"], rig.Service.Asked);
        Assert.Empty(plans.Snapshot);

        var fetched = PlanFor(System);
        pending.SetResult(fetched);
        var answer = await plans.GetAsync(System, Start, TestContext.Current.CancellationToken);

        Assert.Same(fetched, answer);
        Assert.Same(answer, plans.Snapshot[System]);
        Assert.Equal(2, rig.Service.Asked.Count);
    }

    [Fact]
    public void ASecondWantWithinTenMinutesDoesNotFetchAgain()
    {
        var rig = new Rig();
        var plans = rig.Plans();

        plans.Want(System, Start);
        plans.Want(System, Start + TimeSpan.FromMinutes(9));
        Assert.Single(rig.Service.Asked);

        plans.Want(System, Start + TimeSpan.FromMinutes(11));
        Assert.Equal(2, rig.Service.Asked.Count);
    }

    [Fact]
    public void SystemNamesAreComparedWithoutCase()
    {
        var rig = new Rig();
        var plans = rig.Plans();

        plans.Want(System, Start);
        plans.Want(System.ToUpperInvariant(), Start + TimeSpan.FromMinutes(1));

        Assert.Single(rig.Service.Asked);
        Assert.True(plans.Snapshot.ContainsKey(System.ToLowerInvariant()));
    }

    [Fact]
    public void ASitesSeenAtMovingForwardFetchesItsSystem()
    {
        var rig = new Rig();
        rig.Docked(Start);
        var plans = rig.Plans();

        plans.Poll(Start);
        plans.Poll(Start + TimeSpan.FromMinutes(5));
        Assert.Single(rig.Service.Asked);

        rig.Docked(Start + TimeSpan.FromMinutes(6));
        plans.Poll(Start + TimeSpan.FromMinutes(6));

        Assert.Equal([System, System], rig.Service.Asked);
    }

    [Fact]
    public void ADepotSeenAgainWithinTwoMinutesOfAFetchDoesNotFetchAgain()
    {
        var rig = new Rig();
        rig.Docked(Start);
        var plans = rig.Plans();

        plans.Poll(Start);
        rig.Docked(Start + TimeSpan.FromMinutes(1));
        plans.Poll(Start + TimeSpan.FromMinutes(1));

        Assert.Single(rig.Service.Asked);
    }

    [Fact]
    public void AFailureKeepsTheLastPlanAndDoesNotRefetchWithinTenMinutes()
    {
        var rig = new Rig();
        rig.Docked(Start);
        var plans = rig.Plans();

        plans.Poll(Start);
        var held = plans.Snapshot[System];

        rig.Service.Answer = _ => Task.FromException<SystemPlan?>(new ColonisationPlanUnavailableException("No answer."));
        rig.Docked(Start + TimeSpan.FromMinutes(3));
        plans.Poll(Start + TimeSpan.FromMinutes(3));
        Assert.Equal(2, rig.Service.Asked.Count);
        Assert.Same(held, plans.Snapshot[System]);

        rig.Docked(Start + TimeSpan.FromMinutes(6));
        plans.Poll(Start + TimeSpan.FromMinutes(6));
        plans.Want(System, Start + TimeSpan.FromMinutes(12));
        Assert.Equal(2, rig.Service.Asked.Count);

        plans.Want(System, Start + TimeSpan.FromMinutes(14));
        Assert.Equal(3, rig.Service.Asked.Count);
    }

    [Fact]
    public void AClaimedSystemThatFailedIsTriedAgainByPollAfterTenMinutes()
    {
        var rig = new Rig();
        rig.Service.Answer = _ => Task.FromException<SystemPlan?>(new ColonisationPlanUnavailableException("No answer."));
        rig.Docked(Start);
        var plans = rig.Plans();

        plans.Poll(Start);
        rig.Service.Answer = null;
        plans.Poll(Start + TimeSpan.FromMinutes(9));
        Assert.Single(rig.Service.Asked);

        plans.Poll(Start + TimeSpan.FromMinutes(11));
        plans.Poll(Start + TimeSpan.FromMinutes(30));

        Assert.Equal(2, rig.Service.Asked.Count);
        Assert.True(plans.Snapshot.ContainsKey(System));
    }

    [Fact]
    public void AnotherCommandersSystemsAreSweptWhenTheyTakeOver()
    {
        var rig = new Rig();
        rig.Docked(Start);
        var other = new CommanderGameState(new CommanderIdentity("F2", "Other"));
        other.Colonisation = rig.State.Colonisation;
        var active = rig.State;
        var plans = new ColonisationPlans(rig.Service, () => true, () => active, NullLogger<ColonisationPlans>.Instance)
        {
            Dispatch = work => _ = work(),
        };

        plans.Poll(Start);
        active = other;
        plans.Poll(Start + TimeSpan.FromMinutes(1));

        Assert.Equal([System, System], rig.Service.Asked);
    }

    [Fact]
    public async Task GetAsyncAnswersWithTheFailedFetchsLastPlan()
    {
        var rig = new Rig();
        var plans = rig.Plans();
        var held = await plans.GetAsync(System, Start, TestContext.Current.CancellationToken);

        rig.Service.Answer = _ => Task.FromException<SystemPlan?>(new ColonisationPlanUnavailableException("No answer."));
        var after = await plans.GetAsync(System, Start + TimeSpan.FromMinutes(11), TestContext.Current.CancellationToken);

        Assert.NotNull(held);
        Assert.Same(held, after);
        Assert.Equal(2, rig.Service.Asked.Count);
    }

    [Fact]
    public async Task GetAsyncJoinsAFetchAlreadyRunning()
    {
        var rig = new Rig();
        var pending = new TaskCompletionSource<SystemPlan?>();
        rig.Service.Answer = _ => pending.Task;
        var plans = rig.Plans();

        plans.Want(System, Start);
        var asked = plans.GetAsync(System, Start, TestContext.Current.CancellationToken);
        var fetched = PlanFor(System);
        pending.SetResult(fetched);

        Assert.Same(fetched, await asked);
        Assert.Single(rig.Service.Asked);
    }

    [Fact]
    public async Task WithTheSwitchOffNothingIsFetchedAndNothingIsHeld()
    {
        var rig = new Rig { Enabled = false };
        rig.Docked(Start);
        var plans = rig.Plans();

        plans.Poll(Start);
        plans.Want(System, Start);
        var answer = await plans.GetAsync(System, Start, TestContext.Current.CancellationToken);

        Assert.Null(answer);
        Assert.Empty(rig.Service.Asked);
        Assert.Empty(plans.Snapshot);
    }

    [Fact]
    public void TurningTheSwitchOffDropsThePlansAndTurningItOnFetchesAgain()
    {
        var rig = new Rig();
        rig.Docked(Start);
        var plans = rig.Plans();

        plans.Poll(Start);
        Assert.NotEmpty(plans.Snapshot);

        rig.Enabled = false;
        plans.Poll(Start + TimeSpan.FromMinutes(1));
        Assert.Empty(plans.Snapshot);

        rig.Enabled = true;
        plans.Poll(Start + TimeSpan.FromMinutes(2));

        Assert.Equal([System, System], rig.Service.Asked);
        Assert.NotEmpty(plans.Snapshot);
    }

    [Fact]
    public void WithNoServiceNothingIsFetched()
    {
        var rig = new Rig();
        rig.Docked(Start);
        var plans = new ColonisationPlans(null, () => true, () => rig.State, NullLogger<ColonisationPlans>.Instance);

        plans.Poll(Start);
        plans.Want(System, Start);

        Assert.Empty(plans.Snapshot);
    }
}
