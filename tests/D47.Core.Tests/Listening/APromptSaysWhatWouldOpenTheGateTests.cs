using D47.Core.Capabilities.Builtin;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// What an open prompt says while it waits on a spoken value (remediation.md 10, item 12).
/// <para>
/// It said "Say it — I am listening." in every mode, and a comment argued that this was worded
/// for both — that under push-to-talk the microphone is shut until the key is held, and the
/// panel's own microphone row says so a few centimetres away. A Commander read it and reported
/// it as untrue, which is the end of that argument: two controls that have to be reconciled to
/// find out whether one of them is lying is not a design, and the prompt is the one being
/// looked at.
/// </para>
/// <para>
/// So only the one mode that is actually listening says so.
/// </para>
/// </summary>
public class APromptSaysWhatWouldOpenTheGateTests
{
    [Fact]
    public void HoldNamesTheKeyToHold()
    {
        Assert.Equal(
            "Hold [ and say it.",
            MicrophoneNarration.Prompt(ListeningCapability.HoldMode, [], "["));
    }

    [Fact]
    public void ToggleAsksForAPressRatherThanAHold()
    {
        Assert.Equal(
            "Press [ and say it.",
            MicrophoneNarration.Prompt(ListeningCapability.ToggleMode, [], "["));
    }

    /// <summary>The defect, stated as an assertion: the gate is shut, so nothing claims otherwise.</summary>
    [Theory]
    [InlineData(ListeningCapability.HoldMode)]
    [InlineData(ListeningCapability.ToggleMode)]
    public void NothingClaimsToBeListeningWhileTheGateIsShut(string mode)
    {
        var said = MicrophoneNarration.Prompt(mode, [], "RightShift");

        Assert.DoesNotContain("listening", said, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>And the one mode that is listening is the one that says it.</summary>
    [Fact]
    public void ContinuousIsTheOneThatSaysItIsListening()
    {
        Assert.Equal(
            "Say it — I am listening.",
            MicrophoneNarration.Prompt(ListeningCapability.ContinuousMode, [], gesture: null));
    }

    [Fact]
    public void WakeModeNamesWhatToSayFirst()
    {
        Assert.Equal(
            "Say computer, then say it.",
            MicrophoneNarration.Prompt(ListeningCapability.WakeMode, ["computer"], gesture: null));
    }

    [Fact]
    public void WakeModeWithNoPhraseStillSaysSomethingUsable()
    {
        Assert.Equal(
            "Say D47's name, then say it.",
            MicrophoneNarration.Prompt(ListeningCapability.WakeMode, [], gesture: null));
    }

    /// <summary>
    /// Clearing the key is a legitimate configuration — it is how a Commander asks d47 never to
    /// listen. A prompt in that state is not a worse version of the hold one; there is no way to
    /// speak here at all, so it says the only thing that works.
    /// </summary>
    [Theory]
    [InlineData(ListeningCapability.HoldMode)]
    [InlineData(ListeningCapability.ToggleMode)]
    public void NoKeyBoundSendsTheCommanderToTheKeyboard(string mode)
    {
        Assert.Equal(
            "No push-to-talk key is bound. Type it instead.",
            MicrophoneNarration.Prompt(mode, [], gesture: null));
    }
}
