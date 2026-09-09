using D47.Core.Capabilities.Builtin;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>What an open prompt says while it waits on a spoken value.</summary>
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
    /// Clearing the key is a legitimate configuration — it is how a Commander asks d47 never to listen.
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
