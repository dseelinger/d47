using D47.Core.Audio;
using D47.Core.Conversation;
using D47.Core.Journal;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>The carrier's tower answers every comms turn while the CARRIER channel selects it.</summary>
public class TheTowerAnswersOnTheCarrierChannelTests
{
    private const string TowerBrief = "tower control for the Commander's fleet carrier";

    private const string Notes = "dry, and tired of cargo haulers";

    private static readonly CarrierState Owned = new()
    {
        CallSign = "BNH-T2F",
        Name = "Sacred Fire",
        StarSystem = "Wolf 359",
    };

    private static (TurnLoop Loop, TowerLine Line) Build(
        TestSurface surface,
        ILlmProvider provider,
        double? lightYears = 100,
        CarrierState? carrier = null,
        string? notes = null)
    {
        var line = new TowerLine(
            () => new CommanderGameState(new CommanderIdentity("F1", "Tester")) { Carrier = carrier ?? Owned },
            () => "Sol",
            () => DateTimeOffset.UnixEpoch,
            (_, _, _) => Task.FromResult(lightYears),
            notes: () => notes);

        var loop = new TurnLoop(
            surface.Registry,
            surface.Router,
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = "You are the ship's AI aboard this vessel.",
            ShipAiNames = () => ["Warden"],
            CommsSelected = () => OnComms.Tower,
        };

        loop.Lines.Add(line);

        return (loop, line);
    }

    [Fact]
    public async Task ACommsTurnIsAnsweredInTheTowersOwnVoiceWithItsOwnBrief()
    {
        var provider = FakeLlmProvider.Answering("Pad four, Commander.");
        var (loop, _) = Build(TestSurface.For(new MemoryInstall()), provider);

        var events = await OnComms.SayAsync(loop, "request landing clearance");

        Assert.Equal(VoiceRole.TowerControl, Assert.Single(events.OfType<TurnEvent.Addressed>()).Role);
        Assert.Contains(TowerBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryCommsTurnIsTheTowersWhileSelected()
    {
        var provider = FakeLlmProvider.Answering("Pad four, Commander.");
        var (loop, _) = Build(TestSurface.For(new MemoryInstall()), provider);

        await OnComms.SayAsync(loop, "request landing clearance");
        var next = await OnComms.SayAsync(loop, "and how much fuel have you got");

        Assert.Equal(VoiceRole.TowerControl, Assert.Single(next.OfType<TurnEvent.Addressed>()).Role);
        Assert.Equal(3, provider.LastRequest!.Prompt.History.Count);
    }

    [Fact]
    public async Task TheCaptainsKeyIsNotTheTowers()
    {
        var (_, line) = Build(TestSurface.For(new MemoryInstall()), FakeLlmProvider.Answering("Aye."));

        Assert.IsType<LineDecision.NotMine>(
            await line.RouteAsync(OnComms.Captain, "status", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheTowerGetsFainterWithDistance()
    {
        async Task<Speaker> SpeakerAt(double lightYears)
        {
            var (_, line) = Build(TestSurface.For(new MemoryInstall()), FakeLlmProvider.Answering("Aye."), lightYears);
            var taken = Assert.IsType<LineDecision.Taken>(
                await line.RouteAsync(OnComms.Tower, "status", TestContext.Current.CancellationToken));

            return taken.Speaker;
        }

        var near = await SpeakerAt(100);
        var far = await SpeakerAt(375);

        Assert.Equal(VoiceRole.TowerControl, near.Role);
        Assert.Equal(1, near.Signal);
        Assert.InRange(far.Signal, 0.01, 0.99);
    }

    [Fact]
    public async Task TheTowerKnowsTheCarrierReportAsLiveState()
    {
        var (_, line) = Build(TestSurface.For(new MemoryInstall()), FakeLlmProvider.Answering("Aye."));
        var taken = Assert.IsType<LineDecision.Taken>(
            await line.RouteAsync(OnComms.Tower, "status", TestContext.Current.CancellationToken));

        Assert.Contains("BNH-T2F", taken.Speaker.LiveState!(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BeyondRangeTheTowerCannotBeHeardAndNoModelIsAsked()
    {
        var provider = FakeLlmProvider.Answering("Pad four, Commander.");
        var (loop, _) = Build(TestSurface.For(new MemoryInstall()), provider, 600);

        var events = await OnComms.SayAsync(loop, "request landing clearance");

        var result = Assert.Single(events.OfType<TurnEvent.Completed>()).Result;

        Assert.Equal(TurnRoute.CarrierOutOfRange, result.Route);
        Assert.Contains("600", result.Text, StringComparison.Ordinal);
        Assert.Contains("Tower control", result.Text, StringComparison.Ordinal);
        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task WithNoCarrierOwnedNoOneIsOnTheChannel()
    {
        var provider = FakeLlmProvider.Answering("Aye.");
        var (loop, _) = Build(TestSurface.For(new MemoryInstall()), provider, carrier: CarrierState.None);

        var events = await OnComms.SayAsync(loop, "status");

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.NoOneOnComms, Assert.Single(events.OfType<TurnEvent.Completed>()).Result.Route);
    }

    [Fact]
    public async Task TheTowerCarriesTheCommandersNotesInItsBrief()
    {
        var provider = FakeLlmProvider.Answering("Pad four, Commander.");
        var (loop, _) = Build(TestSurface.For(new MemoryInstall()), provider, notes: Notes);

        await OnComms.SayAsync(loop, "request landing clearance");

        Assert.Contains(Notes, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTowerBriefWithoutNotesNamesNoneAndTheCaptainBriefCarriesThem()
    {
        Assert.DoesNotContain("what you are like", TowerLine.Brief(Owned), StringComparison.Ordinal);
        Assert.Contains(Notes, TowerLine.Brief(Owned, Notes), StringComparison.Ordinal);
        Assert.Contains(Notes, CaptainLine.Brief(Owned, notes: Notes), StringComparison.Ordinal);
        Assert.DoesNotContain("what you are like", CaptainLine.Brief(Owned), StringComparison.Ordinal);
    }
}
