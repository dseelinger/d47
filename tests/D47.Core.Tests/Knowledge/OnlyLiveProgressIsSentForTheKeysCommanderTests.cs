using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Journal;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Knowledge;

public class OnlyLiveProgressIsSentForTheKeysCommanderTests
{
    private const long MarketId = 3960010001;
    private const string System = "Hyades Sector AB-C d1";

    private static readonly DateTimeOffset Start = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private sealed class Writer : IColonisationPlanWriter
    {
        public List<string> Calls { get; } = [];

        public PlanCommander Commander { get; set; } = new(PlanWriteOutcome.Sent, "Tester");

        public int Asked { get; private set; }

        public Task<PlanCommander> CommanderAsync(CancellationToken cancellationToken)
        {
            Asked++;
            return Task.FromResult(Commander);
        }

        public Task<PlanWriteResult> NeedsAsync(string buildId, IReadOnlyDictionary<string, int> remaining, int maxNeed, CancellationToken cancellationToken) =>
            Record($"needs {buildId} steel={remaining.GetValueOrDefault("steel")}");

        public Task<PlanWriteResult> ContributeAsync(string buildId, string commander, IReadOnlyDictionary<string, int> delivered, CancellationToken cancellationToken) =>
            Record($"contribute {buildId} {commander} steel={delivered.GetValueOrDefault("steel")}");

        public Task<PlanWriteResult> CompleteAsync(string buildId, CancellationToken cancellationToken) =>
            Record($"complete {buildId}");

        public Task<PlanWriteResult> CarrierRegisteredAsync(long marketId, CancellationToken cancellationToken) =>
            Record($"carrier {marketId}");

        public Task<PlanWriteResult> CarrierMovedAsync(long marketId, IReadOnlyDictionary<string, int> moved, CancellationToken cancellationToken) =>
            Record($"carrier moved {marketId}");

        public TaskCompletionSource? Hold { get; set; }

        private async Task<PlanWriteResult> Record(string call)
        {
            Calls.Add(call);

            if (Hold is { } hold)
            {
                await hold.Task;
            }

            return new PlanWriteResult(PlanWriteOutcome.Sent);
        }
    }

    private sealed class Service : IColonisationPlanService
    {
        public Task<SystemPlan?> SystemAsync(string systemName, CancellationToken cancellationToken) =>
            Task.FromResult<SystemPlan?>(new SystemPlan(
                System,
                [new PlannedSite("s1", "Port", PlanStatus.Build) { MarketId = MarketId, BuildId = "build-1" }],
                Start));
    }

    private sealed class Rig
    {
        public Rig(bool reads = true, bool writes = true, bool hasKey = true)
        {
            Settings = new D47Settings
            {
                Knowledge = new KnowledgeSettings { ColonisationPlanner = reads, ColonisationProgress = writes },
            };
            HasKey = hasKey;

            Plans = new ColonisationPlans(new Service(), () => true, () => null, NullLogger<ColonisationPlans>.Instance)
            {
                Dispatch = work => _ = work(),
            };
            Plans.Want(System, Start);
            Plans.Poll(Start);

            Progress = new ColonisationProgress(
                () =>
                {
                    Built++;
                    return Writer;
                },
                Plans,
                () => Settings,
                () => HasKey,
                NullLogger<PlanWriteQueue>.Instance)
            {
                Dispatch = work => _ = work(),
            };
        }

        public Writer Writer { get; } = new();

        public ColonisationPlans Plans { get; }

        public ColonisationProgress Progress { get; }

        public D47Settings Settings { get; set; }

        public bool HasKey { get; set; }

        public int Built { get; private set; }

        public void Tick(
            IReadOnlyList<JournalEvent> arrived,
            bool priming = false,
            bool offDuty = false,
            string shown = "Tester",
            DateTimeOffset? at = null) =>
            Progress.Tick(priming, offDuty, shown, arrived, Known, at ?? Start);

        /// <summary>The startup tick, which also asks whose key it is.</summary>
        public void Prime() => Tick([], priming: true);
    }

    private static JournalEvent Event(string json) =>
        JournalEvent.TryParse(json, NullLogger.Instance, out var parsed)
            ? parsed!
            : throw new InvalidOperationException("Fixture JSON did not parse.");

    private static JournalEvent Depot() =>
        Event(
            $$"""{"timestamp":"2026-10-21T12:00:00Z","event":"ColonisationConstructionDepot","MarketID":{{MarketId}},"ConstructionProgress":0.1,"ConstructionComplete":false,"ConstructionFailed":false,"ResourcesRequired":[{"Name":"$Steel_name;","Name_Localised":"Steel","RequiredAmount":1000,"ProvidedAmount":400,"Payment":100}]}""");

    private static JournalEvent Contribution() =>
        Event(
            $$"""{"timestamp":"2026-10-21T12:05:00Z","event":"ColonisationContribution","MarketID":{{MarketId}},"Contributions":[{"Name":"$Steel_name;","Name_Localised":"Steel","Amount":120}]}""");

    private static string? Known(long marketId) => marketId == MarketId ? System : null;

    [Fact]
    public void ALiveDeliveryIsSentUnderTheKeysCommanderName()
    {
        var rig = new Rig();
        rig.Prime();

        rig.Tick([Contribution()], shown: "TESTER");

        Assert.Equal(["contribute build-1 Tester steel=120"], rig.Writer.Calls);
        Assert.Equal("Sending as CMDR Tester", rig.Progress.Note);
    }

    [Fact]
    public void ThePrimingTickSendsNothing()
    {
        var rig = new Rig();
        rig.Prime();

        rig.Tick([Depot(), Contribution()], priming: true);

        Assert.Empty(rig.Writer.Calls);
    }

    [Fact]
    public void AnOffDutyTickSendsNothing()
    {
        var rig = new Rig();
        rig.Prime();

        rig.Tick([Depot(), Contribution()], offDuty: true);

        Assert.Empty(rig.Writer.Calls);
    }

    [Fact]
    public void AnotherCommanderShownSendsNothingAndTheNoteSaysWhy()
    {
        var rig = new Rig();
        rig.Prime();

        rig.Tick([Depot(), Contribution()], shown: "Alt");

        Assert.Empty(rig.Writer.Calls);
        Assert.Equal("This key belongs to CMDR Tester, so nothing is sent for CMDR Alt", rig.Progress.Note);
    }

    [Fact]
    public void WithTheSwitchOffNothingIsBuiltOrSent()
    {
        var rig = new Rig(writes: false);
        rig.Prime();

        rig.Tick([Depot(), Contribution()]);

        Assert.Equal(0, rig.Built);
        Assert.Equal(0, rig.Writer.Asked);
        Assert.Empty(rig.Writer.Calls);
        Assert.Null(rig.Progress.Note);
    }

    [Fact]
    public void WithReadsOffNothingIsBuiltOrSent()
    {
        var rig = new Rig(reads: false);
        rig.Prime();

        rig.Tick([Depot(), Contribution()]);

        Assert.Equal(0, rig.Built);
        Assert.Empty(rig.Writer.Calls);
    }

    [Fact]
    public void WithReadsOffTheWriteRowIsDisabled()
    {
        var row = ColonisationCapability.Create(() => null).Settings
            .Single(setting => setting.Key == ColonisationCapability.ProgressKey);

        Assert.True(row.DisabledFor(new D47Settings()));
        Assert.False(row.DisabledFor(new D47Settings { Knowledge = new KnowledgeSettings { ColonisationPlanner = true } }));
        Assert.True(row.Protected);
    }

    [Fact]
    public void WithNoKeyThePlannerIsNotAsked()
    {
        var rig = new Rig(hasKey: false);
        rig.Prime();

        rig.Tick([Contribution()]);

        Assert.Equal(0, rig.Writer.Asked);
        Assert.Empty(rig.Writer.Calls);
        Assert.Equal("No Raven Colonial key is set", rig.Progress.Note);
    }

    [Fact]
    public void ARefusedKeyIsSaidAndNothingIsSent()
    {
        var rig = new Rig();
        rig.Writer.Commander = new PlanCommander(PlanWriteOutcome.KeyRefused, null);
        rig.Prime();

        rig.Tick([Contribution()]);

        Assert.Empty(rig.Writer.Calls);
        Assert.Equal("Raven Colonial refused the key", rig.Progress.Note);
    }

    [Fact]
    public void ANewKeyIsAskedAboutAgain()
    {
        var rig = new Rig();
        rig.Prime();
        rig.Writer.Commander = new PlanCommander(PlanWriteOutcome.Sent, "Alt");

        rig.Progress.KeyChanged();
        rig.Tick([]);
        rig.Tick([Contribution()], shown: "Alt");

        Assert.Equal(2, rig.Writer.Asked);
        Assert.Equal(["contribute build-1 Alt steel=120"], rig.Writer.Calls);
    }

    [Fact]
    public void ANewKeyDropsWritesHeldUnderTheOldKeysName()
    {
        var rig = new Rig();
        rig.Prime();
        rig.Writer.Hold = new TaskCompletionSource();
        rig.Tick([Contribution()]);
        rig.Tick([Contribution()]);

        rig.Writer.Commander = new PlanCommander(PlanWriteOutcome.Sent, "Alt");
        rig.Progress.KeyChanged();
        rig.Tick([]);
        rig.Writer.Hold.SetResult();
        rig.Tick([]);
        rig.Tick([]);

        Assert.Equal(["contribute build-1 Tester steel=120"], rig.Writer.Calls);
    }

    [Fact]
    public void TurningTheSwitchBackOnAsksAgain()
    {
        var rig = new Rig();
        rig.Prime();
        var on = rig.Settings;

        rig.Settings = new D47Settings();
        rig.Tick([]);
        rig.Settings = on;
        rig.Tick([]);

        Assert.Equal(2, rig.Writer.Asked);
    }

    [Fact]
    public void TheFailureTimeIsWrittenInUtcWhateverTheClocksOffset()
    {
        var rig = new Rig();
        rig.Writer.Commander = new PlanCommander(PlanWriteOutcome.Failed, null);
        var local = new DateTimeOffset(2026, 10, 21, 8, 0, 0, TimeSpan.FromHours(-4));
        rig.Tick([], priming: true, at: local);

        rig.Tick([], at: local);

        Assert.Equal("Could not ask Raven Colonial whose key this is at 12:00 UTC", rig.Progress.Note);
    }

    [Fact]
    public void AFailedLookupIsAskedAgainAfterAMinute()
    {
        var rig = new Rig();
        rig.Writer.Commander = new PlanCommander(PlanWriteOutcome.Failed, null);
        rig.Prime();

        rig.Tick([]);
        Assert.Equal("Could not ask Raven Colonial whose key this is at 12:00 UTC", rig.Progress.Note);

        rig.Writer.Commander = new PlanCommander(PlanWriteOutcome.Sent, "Tester");
        rig.Tick([], at: Start + ColonisationProgress.AskAgainAfter);
        rig.Tick([Contribution()], at: Start + ColonisationProgress.AskAgainAfter);

        Assert.Equal(2, rig.Writer.Asked);
        Assert.Equal(["contribute build-1 Tester steel=120"], rig.Writer.Calls);
    }
}
