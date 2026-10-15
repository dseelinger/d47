using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Listening;
using D47.Core.Persona;
using Xunit;
using static D47.Core.Tests.Conversation.TheShipAisNameReachesTheShipTests;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// In wake mode every wake phrase addresses the ship's AI, and another speaker's reply opens the window so the
/// Commander can answer them without one (#1181).
/// </summary>
public class AnotherSpeakerCanBeAnsweredWithoutAWakePhraseTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    /// <summary>The gate and the turn loop as AppHost composes them: the loop's names include every wake phrase.</summary>
    private static (WakeWordGate Gate, TurnLoop Loop, CaptainLine Captain) Compose(string? spelled = null)
    {
        var phrases = ListeningWiring.WakePhrases(ListeningCapability.WakeMode, spelled, "Warden");
        var gate = new WakeWordGate { Phrases = phrases, Window = TimeSpan.FromSeconds(12) };
        var (loop, captain, _, _) = Build(["Warden", ShipAiAddressing.CallSign, .. phrases]);

        return (gate, loop, captain);
    }

    private static async Task<List<TurnEvent>> SayAsync(WakeWordGate gate, TurnLoop loop, string said, DateTimeOffset at)
    {
        var decision = gate.Admit(said, at);

        Assert.Equal(WakeOutcome.Addressed, decision.Outcome);

        return await RunAsync(loop, decision.Text);
    }

    [Fact]
    public async Task TheCaptainIsAnsweredInsideTheWindowAfterTheirReply()
    {
        var (gate, loop, captain) = Compose();

        var asked = await SayAsync(gate, loop, "Warden, captain, how's our fuel?", Now);

        Assert.Equal(VoiceRole.CarrierCaptain, Assert.Single(asked.OfType<TurnEvent.Addressed>()).Role);

        // The captain's reply has finished playing.
        gate.Open(Now.AddSeconds(5));

        var followUp = await SayAsync(gate, loop, "How much tritium?", Now.AddSeconds(8));

        Assert.Equal(VoiceRole.CarrierCaptain, Assert.Single(followUp.OfType<TurnEvent.Addressed>()).Role);
        Assert.True(captain.IsOpen);
    }

    [Fact]
    public async Task AWakePhraseInsideTheWindowGoesToTheShip()
    {
        var (gate, loop, captain) = Compose();

        await SayAsync(gate, loop, "Warden, captain, how's our fuel?", Now);
        gate.Open(Now.AddSeconds(5));

        var events = await SayAsync(gate, loop, "Warden, request docking", Now.AddSeconds(8));

        Assert.Empty(events.OfType<TurnEvent.Addressed>());
        Assert.False(captain.IsOpen);
        Assert.Equal(TurnRoute.ActionCommand, Result(events).Route);
    }

    [Fact]
    public void OnceTheWindowLapsesAnUnnamedReplyIsIgnored()
    {
        var (gate, _, _) = Compose();

        gate.Open(Now);

        Assert.Equal(WakeOutcome.Ignored, gate.Admit("How much tritium?", Now.AddSeconds(30)).Outcome);
    }

    [Theory]
    [InlineData("D47, request docking")]
    [InlineData("Computer, request docking")]
    public async Task EveryWakePhraseRunsTheCommandWithCustomWordsSet(string said)
    {
        var (gate, loop, _) = Compose(spelled: "Computer");

        var events = await SayAsync(gate, loop, said, Now);

        Assert.Equal(TurnRoute.ActionCommand, Result(events).Route);
    }

    [Fact]
    public async Task AWakePhraseAloneClosesTheLines()
    {
        var (gate, loop, captain) = Compose();

        await SayAsync(gate, loop, "Warden, captain, how's our fuel?", Now);
        Assert.True(captain.IsOpen);

        // What the listener does on a bare wake phrase, which never reaches the turn loop.
        Assert.Equal(WakeOutcome.Woken, gate.Admit("Warden", Now.AddSeconds(20)).Outcome);
        loop.CloseLines();

        var next = await SayAsync(gate, loop, "how much cargo", Now.AddSeconds(22));

        Assert.False(captain.IsOpen);
        Assert.Empty(next.OfType<TurnEvent.Addressed>());
    }
}
