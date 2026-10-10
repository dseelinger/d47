using D47.App;
using D47.Core.Audio;
using Xunit;

namespace D47.App.Tests;

public class TheStartupAnnouncementSkipsWhatSetupCoveredTests
{
    [Fact]
    public void AMissingModelIsNotAnnouncedAfterTheWizardWasShown()
    {
        Assert.Null(StartupProblems.Say(false, setupShown: true, canAttemptModelTurn: false, "No Anthropic API key is stored."));
    }

    [Fact]
    public void AMissingModelIsAnnouncedWhenNoWizardWasShown()
    {
        var problem = StartupProblems.Say(false, setupShown: false, canAttemptModelTurn: false, "No Anthropic API key is stored.");

        Assert.Equal(LoopState.Unsure, problem?.State);
        Assert.Contains("I have no language model right now. No Anthropic API key is stored.", problem?.Text);
    }

    [Fact]
    public void SettingsThatDidNotLoadAreAnnouncedEvenAfterTheWizard()
    {
        var problem = StartupProblems.Say(true, setupShown: true, canAttemptModelTurn: false, "No key.");

        Assert.Equal(LoopState.Failed, problem?.State);
        Assert.Equal("My settings did not load cleanly.", problem?.Text);
    }

    [Fact]
    public void AUsableModelSaysNothing()
    {
        Assert.Null(StartupProblems.Say(false, setupShown: false, canAttemptModelTurn: true, null));
    }
}
