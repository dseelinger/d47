using D47.Core.Capabilities.Builtin;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The line under the microphone indicator (list.md Phase 13).
/// <para>
/// It answers one question — what would open the gate right now — and it is the only place the
/// pre-roll is ever explained to a Commander. It lived in <c>AppHost</c>, so the only way to
/// check any of these sentences was to launch the app and put the microphone into each state by
/// hand (list.md Phase 19).
/// </para>
/// </summary>
public class TheMicrophoneSaysWhatOpensItTests
{
    [Fact]
    public void ArmedInWakeModeNamesThePhraseTheCommanderSetTests()
    {
        var line = MicrophoneNarration.For(
            MicrophoneState.Armed, ListeningCapability.WakeMode, ["computer"], gesture: null, preRollMilliseconds: 500);

        Assert.Equal("say computer and D47 will listen", line);
    }

    /// <summary>
    /// Wake mode with no phrase resolved yet. Still a usable instruction rather than a blank:
    /// the row is unset, which means the ship's AI's name, and the Commander knows what they
    /// called it.
    /// </summary>
    [Fact]
    public void ArmedInWakeModeWithNoPhraseStillSaysSomethingUsable()
    {
        var line = MicrophoneNarration.For(
            MicrophoneState.Armed, ListeningCapability.WakeMode, [], gesture: null, preRollMilliseconds: 500);

        Assert.Equal("say D47's name and it will listen", line);
    }

    /// <summary>The first phrase, not all of them — a hint rather than a list.</summary>
    [Fact]
    public void OnlyTheFirstPhraseIsNamed()
    {
        var line = MicrophoneNarration.For(
            MicrophoneState.Armed,
            ListeningCapability.WakeMode,
            ["computer", "d47", "hey"],
            gesture: null,
            preRollMilliseconds: 500);

        Assert.DoesNotContain("d47", line, StringComparison.Ordinal);
        Assert.DoesNotContain("hey", line, StringComparison.Ordinal);
    }

    [Fact]
    public void ArmedInContinuousModeSaysItOpensByItself()
    {
        var line = MicrophoneNarration.For(
            MicrophoneState.Armed,
            ListeningCapability.ContinuousMode,
            [],
            gesture: null,
            preRollMilliseconds: 500);

        Assert.Equal("D47 opens the microphone by itself when it hears you start", line);
    }

    /// <summary>
    /// The pre-roll sentence, which is the one that matters most: audio is already being
    /// captured, and this is the only thing that says how briefly it is kept. A Commander who
    /// does not read this reads an armed microphone as d47 recording them.
    /// </summary>
    [Fact]
    public void IdleWithAKeyBoundExplainsThePreRollAndTheGesture()
    {
        var line = MicrophoneNarration.For(
            MicrophoneState.Idle,
            ListeningCapability.HoldMode,
            [],
            gesture: "Caps Lock",
            preRollMilliseconds: 350);

        Assert.Equal("audio is discarded within 350 ms unless you hold Caps Lock", line);
    }

    /// <summary>Toggle is pressed rather than held, and the sentence has to agree with the mode.</summary>
    [Fact]
    public void ToggleModeSaysPressRatherThanHold()
    {
        var line = MicrophoneNarration.For(
            MicrophoneState.Idle,
            ListeningCapability.ToggleMode,
            [],
            gesture: "Caps Lock",
            preRollMilliseconds: 350);

        Assert.Equal("audio is discarded within 350 ms unless you press Caps Lock", line);
    }

    /// <summary>
    /// Nothing bound, so there is no gesture to name and the line is empty rather than a sentence
    /// ending in nothing.
    /// </summary>
    [Fact]
    public void IdleWithNoKeyBoundSaysNothing()
    {
        var line = MicrophoneNarration.For(
            MicrophoneState.Idle,
            ListeningCapability.HoldMode,
            [],
            gesture: null,
            preRollMilliseconds: 350);

        Assert.Equal(string.Empty, line);
    }

    /// <summary>
    /// Every state has an answer, and none of them throws. The indicator is on both surfaces and
    /// a state nobody wrote a line for must be a blank rather than a crash in the panel.
    /// </summary>
    [Theory]
    [InlineData(MicrophoneState.Off)]
    [InlineData(MicrophoneState.Open)]
    public void TheRemainingStatesAreBlankRatherThanBroken(MicrophoneState state)
    {
        var line = MicrophoneNarration.For(
            state, ListeningCapability.HoldMode, [], gesture: "Caps Lock", preRollMilliseconds: 350);

        Assert.Equal(string.Empty, line);
    }
}
