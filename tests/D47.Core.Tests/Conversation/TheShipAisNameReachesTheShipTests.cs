using D47.Core.Actions;
using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// An utterance opening with the ship AI's name or "D47" closes every line, and what follows is routed with the name
/// taken off (#1179).
/// </summary>
public class TheShipAisNameReachesTheShipTests
{
    private const string ShipAiBrief = "You are the ship's AI aboard this vessel.";

    private const string CaptainBrief = "captain of the Commander's fleet carrier";

    private sealed class Requested : IDockingWatch
    {
        public Task<bool?> ConfirmAsync(CancellationToken cancellationToken) => Task.FromResult<bool?>(true);
    }

    private static readonly GameStatus Approaching = new()
    {
        Flags = StatusFlags.InMainShip,
        Destination = new StatusDestination(7780433924826, 12, "Patterson Enterprise"),
        ReadAt = DateTimeOffset.UnixEpoch,
    };

    private static CapabilityRegistry Commands(RecordingGameInput input) =>
        CapabilityRegistry.Build(ActionCapabilities.All(
            new ActionSurface
            {
                Binds = () => new EliteBinds
                {
                    PresetName = "Test",
                    SourceFile = "Test.binds",
                    Bindings =
                    [
                        new EliteBinding("FocusCommsPanel", "Primary", "Keyboard", "Key_3"),
                        new EliteBinding("FocusLeftPanel", "Primary", "Keyboard", "Key_1"),
                        new EliteBinding("CycleNextPanel", "Primary", "Keyboard", "Key_N"),
                        new EliteBinding("UI_Right", "Primary", "Keyboard", "Key_D"),
                        new EliteBinding("UI_Select", "Primary", "Keyboard", "Key_Z"),
                    ],
                },
                Status = () => Approaching,
                Input = input,
                Enabled = () => true,
            },
            new ShipCommandSurface
            {
                Enabled = _ => true,
                AwaitLeftPanel = (_, _) => Task.FromResult<bool?>(true),
                AwaitUndocked = _ => Task.FromResult<bool?>(true),
                NextStatus = _ => Task.FromResult(Approaching),
                Now = () => DateTimeOffset.UnixEpoch,
                WatchDockingRequest = () => new Requested(),
            }));

    private static (TurnLoop Loop, CaptainLine Captain, FakeLlmProvider Provider, RecordingGameInput Input) Build()
    {
        var input = new RecordingGameInput();
        var registry = Commands(input);
        var provider = FakeLlmProvider.Answering("Aye, Commander.");

        var captain = new CaptainLine(
            () => new CarrierState { CallSign = "BNH-T2F", Name = "Sacred Fire", StarSystem = "Sol" },
            () => "Sol");

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider)
        {
            Persona = ShipAiBrief,
            ShipAiNames = () => ["Cora", ShipAiAddressing.CallSign],
        };

        loop.Lines.Add(captain);

        return (loop, captain, provider, input);
    }

    private static async Task<List<TurnEvent>> RunAsync(TurnLoop loop, string input)
    {
        List<TurnEvent> events = [];

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    private static TurnResult Result(IEnumerable<TurnEvent> events) =>
        Assert.Single(events.OfType<TurnEvent.Completed>()).Result;

    private static string Text(ConversationMessage message) =>
        string.Join(' ', message.Content.OfType<ConversationContent.Text>().Select(part => part.Value));

    [Theory]
    [InlineData("Cora, request docking")]
    [InlineData("Cora, take us in")]
    [InlineData("D47, request docking")]
    [InlineData("D-47, request docking")]
    [InlineData("d47 request docking")]
    public async Task ACommandAfterTheNameRunsWithTheCaptainsLineOpen(string utterance)
    {
        var (loop, captain, provider, input) = Build();

        await RunAsync(loop, "Captain, how much fuel have we got");
        Assert.True(captain.IsOpen);

        var events = await RunAsync(loop, utterance);
        var result = Result(events);

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.False(captain.IsOpen);
        Assert.Equal(TurnRoute.ActionCommand, result.Route);
        Assert.DoesNotContain("Did you mean", result.Text, StringComparison.Ordinal);
        Assert.Contains(input.Steps, step => step.Kind == InputStepKind.KeyDown);
        Assert.Equal(1, provider.CallCount);

        Assert.NotEqual("Learned.", Result(await RunAsync(loop, "yes")).Text);
    }

    [Fact]
    public async Task TheCaptainNamedAfterTheShipAiAnswers()
    {
        var (loop, _, provider, _) = Build();

        var events = await RunAsync(loop, "Cora, captain, how's our fuel?");

        Assert.Equal(VoiceRole.CarrierCaptain, Assert.Single(events.OfType<TurnEvent.Addressed>()).Role);
        Assert.Contains(CaptainBrief, provider.LastRequest!.Prompt.Persona, StringComparison.Ordinal);
        Assert.Equal("how's our fuel?", Text(Assert.Single(provider.LastRequest.Prompt.History)));
    }

    [Fact]
    public async Task TheNameAloneClosesTheLineForTheNextUtterance()
    {
        var (loop, captain, provider, _) = Build();

        await RunAsync(loop, "Captain, how much fuel have we got");

        var named = await RunAsync(loop, "Cora");

        Assert.Empty(named.OfType<TurnEvent.Addressed>());
        Assert.False(captain.IsOpen);

        var next = await RunAsync(loop, "and how much cargo");

        Assert.Empty(next.OfType<TurnEvent.Addressed>());
        Assert.Equal(ShipAiBrief, provider.LastRequest!.Prompt.Persona);
    }
}
