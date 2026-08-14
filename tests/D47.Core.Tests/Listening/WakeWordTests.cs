using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// Wake-word gating (list.md Phase 13), which decides about words rather than about audio — so
/// every one of these is a transcript in and a decision out, with no model and no microphone.
/// </summary>
public class WakeWordTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("3311-01-01T00:00:00Z");

    private static WakeWordGate Gate(params string[] phrases) => new() { Phrases = phrases };

    [Fact]
    public void SomethingSaidToSomebodyElseIsDropped()
    {
        var gate = Gate("D47");

        // The common case in this mode by a wide margin, and the reason the mode is bearable at
        // all: a room where people talk is not a room full of commands.
        Assert.Equal(
            WakeOutcome.Ignored,
            gate.Admit("I was in Shinrarta last night and the pad fees were criminal", Now).Outcome);
    }

    [Fact]
    public void TheNameIsTakenOffWhatIsPassedOn()
    {
        var gate = Gate("D47");

        var decision = gate.Admit("D47, how much fuel do I have?", Now);

        // The turn should receive the request, not the vocative. "D47, how much fuel" reaching
        // the router is a phrase that matches nothing it declares.
        Assert.Equal(WakeOutcome.Addressed, decision.Outcome);
        Assert.Equal("how much fuel do I have?", decision.Text);
    }

    /// <summary>
    /// The reason this matches a flattened form rather than the string. Whisper writes a short
    /// alphanumeric name however it happens to hear it, and every one of these is a real spelling
    /// it produces for the same two syllables.
    /// </summary>
    [Theory]
    [InlineData("D47, gear up")]
    [InlineData("D 47 gear up")]
    [InlineData("d-47: gear up")]
    [InlineData("D47. Gear up.")]
    [InlineData("  d47 gear up")]
    public void TheNameIsRecognisedHoweverTheModelSpeltIt(string transcript)
    {
        var decision = Gate("D47").Admit(transcript, Now);

        Assert.Equal(WakeOutcome.Addressed, decision.Outcome);
        Assert.Contains("gear up", decision.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheNameHasToBeNearTheFrontRatherThanAnywhereAtAll()
    {
        var gate = Gate("D47");

        // Talking about d47 is not talking to it. Without the bound, every sentence mentioning
        // the ship's AI becomes a command with the front half cut off.
        Assert.Equal(
            WakeOutcome.Ignored,
            gate.Admit("I was telling Sarah how much I like flying with D47", Now).Outcome);
    }

    [Fact]
    public void TheNameOnItsOwnWakesRatherThanAsks()
    {
        var gate = Gate("D47");

        var decision = gate.Admit("D47?", Now);

        // Passing an empty request to the turn loop would produce an answer to nothing. The
        // right response to being called by name is to say yes.
        Assert.Equal(WakeOutcome.Woken, decision.Outcome);
        Assert.Equal(string.Empty, decision.Text);
    }

    [Fact]
    public void WhatFollowsBeingCalledByNameNeedsNoSecondName()
    {
        var gate = Gate("D47");

        gate.Admit("D47", Now);

        // How a person addresses anybody: the name, an acknowledgement, then the thing. Making
        // the Commander say the name again into a companion that has just confirmed it is
        // listening is the interaction nobody wants.
        var decision = gate.Admit("how much fuel do I have?", Now.AddSeconds(3));

        Assert.Equal(WakeOutcome.Addressed, decision.Outcome);
        Assert.Equal("how much fuel do I have?", decision.Text);
    }

    [Fact]
    public void TheWindowIsOneFollowUpRatherThanAnOpenMicrophone()
    {
        var gate = Gate("D47");

        gate.Admit("D47", Now);
        gate.Admit("how much fuel do I have?", Now.AddSeconds(1));

        // The follow-up shuts the window. Otherwise being called by name once would leave d47
        // taking everything said in the room for the next twelve seconds.
        Assert.Equal(
            WakeOutcome.Ignored,
            gate.Admit("anyway, as I was saying", Now.AddSeconds(2)).Outcome);
    }

    [Fact]
    public void TheWindowRunsOut()
    {
        var gate = Gate("D47");
        gate.Window = TimeSpan.FromSeconds(12);

        gate.Admit("D47", Now);

        Assert.Equal(
            WakeOutcome.Ignored,
            gate.Admit("how much fuel do I have?", Now.AddSeconds(30)).Outcome);
    }

    [Fact]
    public void AWindowOfZeroMeansTheNameAndTheRequestArriveTogether()
    {
        var gate = Gate("D47");
        gate.Window = TimeSpan.Zero;

        gate.Admit("D47", Now);

        Assert.False(gate.IsAwake(Now));
        Assert.Equal(WakeOutcome.Ignored, gate.Admit("gear up", Now).Outcome);
    }

    [Fact]
    public void EveryNameItAnswersToIsANameItAnswersTo()
    {
        // The Commander adds spellings when the model keeps hearing the name as something else.
        var gate = Gate("D47", "Directive", "detour seven");

        Assert.Equal(WakeOutcome.Addressed, gate.Admit("Directive, gear up", Now).Outcome);
        Assert.Equal(WakeOutcome.Addressed, gate.Admit("Detour seven, gear up", Now).Outcome);
    }

    [Fact]
    public void WithNoNameConfiguredEverythingIsAddressed()
    {
        var gate = new WakeWordGate();

        // Which is what continuous listening without a wake word means. The policy is inert
        // rather than the callers having to know which mode is in force.
        var decision = gate.Admit("how much fuel do I have?", Now);

        Assert.Equal(WakeOutcome.Addressed, decision.Outcome);
        Assert.Equal("how much fuel do I have?", decision.Text);
    }

    [Fact]
    public void RenamingTheShipsAiRenamesTheWakeWord()
    {
        var gate = Gate("D47");

        Assert.Equal(WakeOutcome.Addressed, gate.Admit("D47 gear up", Now).Outcome);

        // The name follows the core, so the wake word has to as well — a Commander who switched
        // core and found the new one deaf to its own name would have no way to work out why.
        gate.Phrases = ["Athena"];

        Assert.Equal(WakeOutcome.Ignored, gate.Admit("D47 gear up", Now).Outcome);
        Assert.Equal(WakeOutcome.Addressed, gate.Admit("Athena, gear up", Now).Outcome);
    }

    [Fact]
    public void AnEmptyOrBlankPhraseIsNotAWakeWord()
    {
        // A settings row cleared to whitespace must not turn into a phrase that matches the
        // start of every sentence ever spoken.
        var gate = Gate("   ", "D47");

        Assert.Equal(WakeOutcome.Ignored, gate.Admit("nothing to do with anything", Now).Outcome);
        Assert.Equal(WakeOutcome.Addressed, gate.Admit("D47 gear up", Now).Outcome);
    }
}
