using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Listening;
using D47.Core.Persona;
using Xunit;
using static D47.Core.Tests.Conversation.TheShipAisNameReachesTheShipTests;

namespace D47.Core.Tests.Conversation;

/// <summary>In wake mode every wake phrase addresses the ship's AI, and answering someone on comms needs the comms key.</summary>
public class EveryWakePhraseAddressesTheShipTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    /// <summary>The gate and the turn loop as AppHost composes them: the loop's names include every wake phrase.</summary>
    private static (WakeWordGate Gate, TurnLoop Loop) Compose(string? spelled = null)
    {
        var phrases = ListeningWiring.WakePhrases(ListeningCapability.WakeMode, spelled, "Warden");
        var gate = new WakeWordGate { Phrases = phrases, Window = TimeSpan.FromSeconds(12) };
        var (loop, _, _, _) = Build(["Warden", ShipAiAddressing.CallSign, .. phrases]);
        loop.CommsSelected = () => OnComms.Captain;

        return (gate, loop);
    }

    private static async Task<List<TurnEvent>> SayAsync(WakeWordGate gate, TurnLoop loop, string said, DateTimeOffset at)
    {
        var decision = gate.Admit(said, at);

        Assert.Equal(WakeOutcome.Addressed, decision.Outcome);

        return await OnComms.AskAsync(loop, decision.Text);
    }

    [Fact]
    public async Task AfterTheBareNameAnUnnamedUtteranceReachesTheShipNotTheCaptain()
    {
        var (gate, loop) = Compose();

        await OnComms.SayAsync(loop, "how's our fuel?");

        Assert.Equal(WakeOutcome.Woken, gate.Admit("Warden", Now).Outcome);

        var followUp = await SayAsync(gate, loop, "How much tritium?", Now.AddSeconds(3));

        Assert.Empty(followUp.OfType<TurnEvent.Addressed>());
    }

    [Fact]
    public async Task AWakePhraseAfterACommsReplyRunsTheCommand()
    {
        var (gate, loop) = Compose();

        await OnComms.SayAsync(loop, "how's our fuel?");

        var events = await SayAsync(gate, loop, "Warden, request docking", Now.AddSeconds(8));

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.Equal(TurnRoute.ActionCommand, Result(events).Route);
    }

    [Fact]
    public void AnUnnamedUtteranceWithNoNameBeforeItIsIgnored()
    {
        var (gate, _) = Compose();

        Assert.Equal(WakeOutcome.Ignored, gate.Admit("How much tritium?", Now.AddSeconds(30)).Outcome);
    }

    [Theory]
    [InlineData("D47, request docking")]
    [InlineData("Computer, request docking")]
    public async Task EveryWakePhraseRunsTheCommandWithCustomWordsSet(string said)
    {
        var (gate, loop) = Compose(spelled: "Computer");

        var events = await SayAsync(gate, loop, said, Now);

        Assert.Equal(TurnRoute.ActionCommand, Result(events).Route);
    }
}
