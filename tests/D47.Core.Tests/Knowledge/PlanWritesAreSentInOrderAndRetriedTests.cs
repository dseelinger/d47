using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class PlanWritesAreSentInOrderAndRetriedTests
{
    private const long MarketId = 3960010001;
    private const string System = "Hyades Sector AB-C d1";

    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class Writer : IColonisationPlanWriter
    {
        public List<string> Calls { get; } = [];

        public Queue<PlanWriteOutcome> Outcomes { get; } = new();

        public TaskCompletionSource? Hold { get; set; }

        private async Task<PlanWriteResult> Record(string call)
        {
            Calls.Add(call);

            if (Hold is { } hold)
            {
                await hold.Task;
            }

            return new PlanWriteResult(Outcomes.Count > 0 ? Outcomes.Dequeue() : PlanWriteOutcome.Sent);
        }

        public Task<PlanCommander> CommanderAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new PlanCommander(PlanWriteOutcome.Sent, "Tester"));

        public Task<PlanWriteResult> NeedsAsync(string buildId, IReadOnlyDictionary<string, int> remaining, int maxNeed, CancellationToken cancellationToken) =>
            Record($"needs {buildId} steel={remaining.GetValueOrDefault("steel")}");

        public Task<PlanWriteResult> ContributeAsync(string buildId, string commander, IReadOnlyDictionary<string, int> delivered, CancellationToken cancellationToken) =>
            Record($"contribute {buildId} {commander} steel={delivered.GetValueOrDefault("steel")}");

        public Task<PlanWriteResult> CompleteAsync(string buildId, CancellationToken cancellationToken) =>
            Record($"complete {buildId}");
    }

    private sealed class Service : IColonisationPlanService
    {
        public List<string> Asked { get; } = [];

        public SystemPlan? Plan { get; set; }

        public TaskCompletionSource<SystemPlan?>? Pending { get; set; }

        public Task<SystemPlan?> SystemAsync(string systemName, CancellationToken cancellationToken)
        {
            Asked.Add(systemName);
            return Pending?.Task ?? Task.FromResult(Plan);
        }
    }

    private sealed class Rig
    {
        public Rig()
        {
            Plans = new ColonisationPlans(Service, () => true, () => null, NullLogger<ColonisationPlans>.Instance)
            {
                Dispatch = work => _ = work(),
            };
            Queue = new PlanWriteQueue(Writer, Plans, NullLogger<PlanWriteQueue>.Instance)
            {
                Dispatch = work => _ = work(),
            };
        }

        public Writer Writer { get; } = new();

        public Service Service { get; } = new();

        public ColonisationPlans Plans { get; }

        public PlanWriteQueue Queue { get; }

        public void PlanHolds(PlanStatus status = PlanStatus.Build, Dictionary<string, int>? needed = null)
        {
            Service.Plan = new SystemPlan(
                System,
                [new PlannedSite("s1", "Port", status) { MarketId = MarketId, BuildId = "build-1", Needed = needed ?? [] }],
                Start);
            Plans.Want(System, Start);
        }

        public void Tick(DateTimeOffset at) => Queue.Poll(at);
    }

    private static NeedsWrite Needs(int steel, DateTimeOffset at) =>
        new(MarketId, System, at, new Dictionary<string, int> { ["steel"] = steel }, 1000);

    private static ContributionWrite Delivered(int steel, DateTimeOffset at) =>
        new(MarketId, System, at, new Dictionary<string, int> { ["steel"] = steel });

    [Fact]
    public void AddAndPollReturnWithoutAwaitingTheWriter()
    {
        var rig = new Rig();
        rig.Writer.Hold = new TaskCompletionSource();
        rig.PlanHolds();

        rig.Queue.Add([Needs(600, Start)], "Tester", Start);
        rig.Tick(Start);

        Assert.Equal(["needs build-1 steel=600"], rig.Writer.Calls);
    }

    [Fact]
    public void AWriteWithNoMatchingProjectIsHeldAskedForAndSentWhenThePlanArrives()
    {
        var rig = new Rig();
        rig.Service.Pending = new TaskCompletionSource<SystemPlan?>();

        rig.Queue.Add([Needs(600, Start)], "Tester", Start);
        rig.Tick(Start.AddMinutes(1));

        Assert.Empty(rig.Writer.Calls);
        Assert.Contains(System, rig.Service.Asked);

        rig.Service.Pending.SetResult(new SystemPlan(
            System,
            [new PlannedSite("s1", "Port", PlanStatus.Build) { MarketId = MarketId, BuildId = "build-1" }],
            Start));
        rig.Tick(Start.AddMinutes(2));

        Assert.Equal(["needs build-1 steel=600"], rig.Writer.Calls);
    }

    [Fact]
    public void AWriteHeldForTenMinutesIsDropped()
    {
        var rig = new Rig();

        rig.Queue.Add([Needs(600, Start)], "Tester", Start);
        rig.Tick(Start.AddMinutes(10));
        rig.PlanHolds();
        rig.Tick(Start.AddMinutes(11));

        Assert.Empty(rig.Writer.Calls);
    }

    [Fact]
    public void AFailedContributionIsNotSentAgain()
    {
        var rig = new Rig();
        rig.PlanHolds();
        rig.Writer.Outcomes.Enqueue(PlanWriteOutcome.Failed);

        rig.Queue.Add([Delivered(50, Start)], "Tester", Start);
        rig.Tick(Start);

        for (var minute = 1; minute <= 30; minute++)
        {
            rig.Tick(Start.AddMinutes(minute));
        }

        Assert.Equal(["contribute build-1 Tester steel=50"], rig.Writer.Calls);
        Assert.NotNull(rig.Queue.Status.LastFailure);
    }

    [Fact]
    public void AFailedSnapshotIsResentAtOneFiveAndFifteenMinutesWithTheLatestFigure()
    {
        var rig = new Rig();
        rig.PlanHolds();
        rig.Writer.Outcomes.Enqueue(PlanWriteOutcome.Failed);
        rig.Writer.Outcomes.Enqueue(PlanWriteOutcome.Failed);
        rig.Writer.Outcomes.Enqueue(PlanWriteOutcome.Failed);
        rig.Writer.Outcomes.Enqueue(PlanWriteOutcome.Failed);

        rig.Queue.Add([Needs(600, Start)], "Tester", Start);
        rig.Tick(Start);
        rig.Queue.Add([Needs(550, Start)], "Tester", Start);

        rig.Tick(Start.AddSeconds(59));
        Assert.Single(rig.Writer.Calls);

        rig.Tick(Start.AddMinutes(1));
        Assert.Equal("needs build-1 steel=550", rig.Writer.Calls[^1]);
        Assert.Equal(2, rig.Writer.Calls.Count);

        rig.Tick(Start.AddMinutes(1));
        rig.Tick(Start.AddMinutes(5));
        Assert.Equal(2, rig.Writer.Calls.Count);

        rig.Tick(Start.AddMinutes(6));
        Assert.Equal(3, rig.Writer.Calls.Count);

        rig.Tick(Start.AddMinutes(6));
        rig.Tick(Start.AddMinutes(20));
        Assert.Equal(3, rig.Writer.Calls.Count);

        rig.Tick(Start.AddMinutes(21));
        Assert.Equal(4, rig.Writer.Calls.Count);

        rig.Tick(Start.AddMinutes(21));
        rig.Tick(Start.AddHours(2));
        Assert.Equal(4, rig.Writer.Calls.Count);
    }

    [Fact]
    public void AContributionAndTheSnapshotAfterItReachTheWriterInOrder()
    {
        var rig = new Rig();
        rig.PlanHolds();

        rig.Queue.Add([Delivered(50, Start), Needs(550, Start)], "Tester", Start);
        rig.Tick(Start);
        rig.Tick(Start);

        Assert.Equal(["contribute build-1 Tester steel=50", "needs build-1 steel=550"], rig.Writer.Calls);
    }

    [Fact]
    public void AnUnchangedSnapshotIsNotSent()
    {
        var rig = new Rig();
        rig.PlanHolds();

        rig.Queue.Add([Needs(600, Start)], "Tester", Start);
        rig.Tick(Start);
        rig.Queue.Add([Needs(600, Start.AddMinutes(1))], "Tester", Start.AddMinutes(1));
        rig.Tick(Start.AddMinutes(1));

        Assert.Single(rig.Writer.Calls);
    }

    [Fact]
    public void ASnapshotEqualToThePlansNeedsIsNotSentFirst()
    {
        var rig = new Rig();
        rig.PlanHolds(needed: new Dictionary<string, int> { ["steel"] = 600 });

        rig.Queue.Add([Needs(600, Start)], "Tester", Start);
        rig.Tick(Start);

        Assert.Empty(rig.Writer.Calls);
    }

    [Fact]
    public void ACompletionForACompletePlannedSiteIsNotSent()
    {
        var rig = new Rig();
        rig.PlanHolds(PlanStatus.Complete);

        rig.Queue.Add([new CompleteWrite(MarketId, System, Start)], "Tester", Start);
        rig.Tick(Start);

        Assert.Empty(rig.Writer.Calls);
    }

    [Fact]
    public void ASiteNotOnThePlannerStopsFurtherWritesToIt()
    {
        var rig = new Rig();
        rig.PlanHolds();
        rig.Writer.Outcomes.Enqueue(PlanWriteOutcome.NotOnPlanner);

        rig.Queue.Add([Delivered(50, Start)], "Tester", Start);
        rig.Tick(Start);
        rig.Queue.Add([Needs(500, Start), Delivered(10, Start)], "Tester", Start.AddMinutes(1));
        rig.Tick(Start.AddMinutes(1));

        Assert.Single(rig.Writer.Calls);
    }

    [Fact]
    public void ARefusedKeyStopsEveryWriteUntilReset()
    {
        var rig = new Rig();
        rig.PlanHolds();
        rig.Writer.Outcomes.Enqueue(PlanWriteOutcome.KeyRefused);

        rig.Queue.Add([Delivered(50, Start)], "Tester", Start);
        rig.Tick(Start);
        rig.Queue.Add([Needs(500, Start)], "Tester", Start.AddMinutes(1));
        rig.Tick(Start.AddMinutes(1));

        Assert.Single(rig.Writer.Calls);
        Assert.True(rig.Queue.Status.KeyRefused);

        rig.Queue.Reset();
        rig.Queue.Add([Needs(500, Start)], "Tester", Start.AddMinutes(2));
        rig.Tick(Start.AddMinutes(2));

        Assert.Equal(2, rig.Writer.Calls.Count);
        Assert.False(rig.Queue.Status.KeyRefused);
    }
}
