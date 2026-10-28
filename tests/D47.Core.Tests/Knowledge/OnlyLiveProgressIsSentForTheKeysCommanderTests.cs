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
            if (hasKey)
            {
                Keys.Add(CommanderSecret.Name(ColonisationCapability.KeySecretName, Fid));
            }

            Settings = new D47Settings
            {
                Knowledge = new KnowledgeSettings { ColonisationPlanner = reads, ColonisationProgress = writes },
            };

            Plans = new ColonisationPlans(new Service(), () => true, () => null, NullLogger<ColonisationPlans>.Instance)
            {
                Dispatch = work => _ = work(),
            };
            Plans.Want(System, Start);
            Plans.Poll(Start);

            Progress = new ColonisationProgress(
                name =>
                {
                    Built++;
                    Used.Add(name);
                    return Writers.TryGetValue(name, out var other) ? other : Writer;
                },
                Plans,
                () => Settings,
                Keys.Contains,
                (from, to) =>
                {
                    if (!Keys.Remove(from))
                    {
                        return false;
                    }

                    Keys.Add(to);
                    Moves.Add((from, to));
                    return true;
                },
                NullLogger<PlanWriteQueue>.Instance)
            {
                Dispatch = work => _ = work(),
            };
        }

        public Writer Writer { get; } = new();

        public ColonisationPlans Plans { get; }

        public ColonisationProgress Progress { get; }

        public D47Settings Settings { get; set; }

        public const string Fid = "F1";

        public HashSet<string> Keys { get; } = [];

        public Dictionary<string, Writer> Writers { get; } = [];

        public List<string> Used { get; } = [];

        public List<(string From, string To)> Moves { get; } = [];

        public int Built { get; private set; }

        public void Tick(
            IReadOnlyList<JournalEvent> arrived,
            bool priming = false,
            bool offDuty = false,
            string shown = "Tester",
            DateTimeOffset? at = null,
            string? fid = Fid) =>
            Progress.Tick(priming, offDuty, shown, fid, arrived, Known, at ?? Start);

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

    [Fact]
    public void TwoCommandersEachSendUnderTheirOwnKey()
    {
        var rig = new Rig();
        var other = new Writer { Commander = new PlanCommander(PlanWriteOutcome.Sent, "Alt") };
        var otherName = CommanderSecret.Name(ColonisationCapability.KeySecretName, "F2");
        rig.Keys.Add(otherName);
        rig.Writers[otherName] = other;
        rig.Prime();

        rig.Tick([Contribution()]);
        rig.Tick([], shown: "Alt", fid: "F2");
        rig.Tick([Contribution()], shown: "Alt", fid: "F2");
        rig.Tick([], shown: "Tester");
        rig.Tick([Contribution()], shown: "Tester");

        Assert.Equal(["contribute build-1 Tester steel=120", "contribute build-1 Tester steel=120"], rig.Writer.Calls);
        Assert.Equal(["contribute build-1 Alt steel=120"], other.Calls);
    }

    [Fact]
    public void ACommanderWithNoKeySendsNothingAndTheNoteSaysSo()
    {
        var rig = new Rig();
        rig.Prime();

        rig.Tick([Contribution()], shown: "Alt", fid: "F2");

        Assert.Empty(rig.Writer.Calls);
        Assert.Equal("No Raven Colonial key is set", rig.Progress.Note);
    }

    [Fact]
    public void ANewCommanderDropsHeldWritesAndAsksAgain()
    {
        var rig = new Rig();
        rig.Prime();
        rig.Writer.Hold = new TaskCompletionSource();
        rig.Tick([Contribution()]);
        rig.Tick([Contribution()]);

        rig.Keys.Add(CommanderSecret.Name(ColonisationCapability.KeySecretName, "F2"));
        rig.Tick([], shown: "Alt", fid: "F2");
        rig.Writer.Hold.SetResult();
        rig.Tick([], shown: "Alt", fid: "F2");
        rig.Tick([], shown: "Alt", fid: "F2");

        Assert.Equal(["contribute build-1 Tester steel=120"], rig.Writer.Calls);
        Assert.Equal(2, rig.Writer.Asked);
    }

    [Fact]
    public void AKeyThatBelongsToAnotherCommanderSendsNothingForTheShownOne()
    {
        var rig = new Rig();
        rig.Writer.Commander = new PlanCommander(PlanWriteOutcome.Sent, "Alt");
        rig.Prime();

        rig.Tick([Contribution()], shown: "Tester");

        Assert.Empty(rig.Writer.Calls);
        Assert.Equal("This key belongs to CMDR Alt, so nothing is sent for CMDR Tester", rig.Progress.Note);
    }

    [Fact]
    public void TheInstallationKeyMovesOnceToTheCommanderItBelongsTo()
    {
        var rig = new Rig(hasKey: false);
        rig.Keys.Add(ColonisationCapability.KeySecretName);
        rig.Prime();
        rig.Tick([Contribution()]);
        rig.Tick([Contribution()]);

        var own = CommanderSecret.Name(ColonisationCapability.KeySecretName, Rig.Fid);
        Assert.Equal([(ColonisationCapability.KeySecretName, own)], rig.Moves);
        Assert.Equal([own], rig.Keys);
        Assert.Equal(2, rig.Writer.Calls.Count);
        Assert.Equal(ColonisationCapability.KeySecretName, rig.Used[0]);
        Assert.Equal(own, rig.Used[^1]);
    }

    [Fact]
    public void TheInstallationKeyIsNotMovedWhileAnotherCommanderIsShown()
    {
        var rig = new Rig(hasKey: false);
        rig.Keys.Add(ColonisationCapability.KeySecretName);
        rig.Prime();

        rig.Tick([Contribution()], shown: "Alt", fid: "F2");
        rig.Tick([Contribution()], shown: "Alt", fid: "F2");

        Assert.Empty(rig.Moves);
        Assert.Empty(rig.Writer.Calls);
        Assert.Equal([ColonisationCapability.KeySecretName], rig.Keys);
        Assert.Equal("This key belongs to CMDR Tester, so nothing is sent for CMDR Alt", rig.Progress.Note);
    }
}
