using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Listening;
using Xunit;

namespace D47.Core.Tests.Listening;

/// <summary>
/// The composition root's listening wiring, decided from settings alone (list.md Phase 19,
/// "Give the composition root a test harness").
/// <para>
/// <c>AppHost.ApplyListeningSettings</c> is a hundred and twenty lines of device handles, native
/// model loads and a keyboard hook, with two decisions threaded through it: what happens to the
/// speech model, and whether the microphone is opened at all. Neither needs any of that
/// hardware, and neither could be reached by a test while it was in there.
/// </para>
/// <para>
/// The root still opens the device and loads the model. What was lifted is <em>whether</em>.
/// </para>
/// </summary>
public class TheRootDecidesWhatToListenWithTests
{
    private static ListeningSettings With(string model, bool gpu = false) =>
        new() { Model = model, UseGpu = gpu };

    [Fact]
    public void AModelOnDiskIsLoadedFromWhereItIs()
    {
        var plan = ListeningWiring.PlanModel(With("small.en"), new FakeModelStore("small.en"));

        Assert.Equal(SpeechModelAction.Load, plan.Action);
        Assert.Equal("small.en", plan.Model?.Id);
        Assert.NotNull(plan.Path);
    }

    /// <summary>
    /// A fresh install: the shipped model is selected and none of it is on disk. This is the
    /// decision behind "d47 can hear you after one download it starts itself" — a plan that
    /// answered Unload here would leave a push-to-talk key capturing audio nothing can read.
    /// </summary>
    [Fact]
    public void AFreshInstallFetchesTheModelItShipsWithSelected()
    {
        var shipped = new D47Settings().Listening;

        var plan = ListeningWiring.PlanModel(shipped, new FakeModelStore());

        Assert.Equal(SpeechModelAction.Fetch, plan.Action);
        Assert.Equal(shipped.Model, plan.Model?.Id);
    }

    /// <summary>
    /// Choosing no model is a decision rather than a gap, so nothing is fetched and whatever is
    /// held is released. A name from a settings file this build no longer ships is the same
    /// answer — it releases rather than fetching something nobody asked for.
    /// </summary>
    [Theory]
    [InlineData(WhisperModels.NoneId)]
    [InlineData("")]
    [InlineData("not-a-model")]
    public void NothingSelectedReleasesWhateverIsLoaded(string selected)
    {
        Assert.Equal(SpeechModelAction.Unload, ListeningWiring.PlanModel(With(selected), new FakeModelStore()).Action);
    }

    /// <summary>
    /// The GPU flag rides along with the plan rather than being read separately at the load site,
    /// so a model loaded on the CPU because two reads of settings disagreed cannot happen.
    /// </summary>
    [Fact]
    public void TheGpuChoiceTravelsWithThePlan()
    {
        Assert.True(ListeningWiring.PlanModel(With("small.en", gpu: true), new FakeModelStore("small.en")).UseGpu);
        Assert.False(ListeningWiring.PlanModel(With("small.en"), new FakeModelStore("small.en")).UseGpu);
    }

    /// <summary>
    /// A key bound opens the microphone whatever the mode, because push-to-talk is the mode that
    /// has one.
    /// </summary>
    [Fact]
    public void AKeyBoundOpensTheMicrophone()
    {
        Assert.True(ListeningWiring.NeedsMicrophone(ListeningCapability.HoldMode, keyBound: true));
    }

    /// <summary>
    /// No key and nothing that opens the gate by itself means no microphone. d47 holding an input
    /// device it will never read from is exactly the surprise the unset default exists to avoid —
    /// and it is what the Commander sees in Windows' privacy indicator.
    /// </summary>
    [Fact]
    public void NoKeyAndNoHandsFreeModeLeavesTheDeviceClosed()
    {
        Assert.False(ListeningWiring.NeedsMicrophone(ListeningCapability.HoldMode, keyBound: false));
        Assert.False(ListeningWiring.NeedsMicrophone(ListeningCapability.ToggleMode, keyBound: false));
    }

    /// <summary>Hands-free with no key bound is a legitimate configuration and does open it.</summary>
    [Theory]
    [InlineData(ListeningCapability.ContinuousMode)]
    [InlineData(ListeningCapability.WakeMode)]
    public void AHandsFreeModeOpensItWithNoKeyAtAll(string mode)
    {
        Assert.True(ListeningWiring.NeedsMicrophone(mode, keyBound: false));
    }

    /// <summary>
    /// Outside wake-word mode the policy holds nothing, which is what makes it inert: with no
    /// phrases the gate admits everything, and admitting everything is what continuous listening
    /// means. A phrase left behind here would silently start filtering it.
    /// </summary>
    [Theory]
    [InlineData(ListeningCapability.HoldMode)]
    [InlineData(ListeningCapability.ContinuousMode)]
    [InlineData(ListeningCapability.ToggleMode)]
    [InlineData(null)]
    public void NothingIsAWakeWordOutsideWakeWordMode(string? mode)
    {
        Assert.Empty(ListeningWiring.WakePhrases(mode, "d47, computer", "Aurora"));
    }

    /// <summary>
    /// An unset row means the name the Commander gave their ship's AI — which changes when the
    /// core does, and is why the name is passed in rather than fixed.
    /// </summary>
    [Fact]
    public void AnUnsetRowAnswersToTheShipsName()
    {
        Assert.Equal(["Aurora"], ListeningWiring.WakePhrases(ListeningCapability.WakeMode, null, "Aurora"));
        Assert.Equal(["Aurora"], ListeningWiring.WakePhrases(ListeningCapability.WakeMode, "", "Aurora"));
    }

    [Fact]
    public void SpelledPhrasesAreSplitAndTrimmed()
    {
        var phrases = ListeningWiring.WakePhrases(ListeningCapability.WakeMode, " d47 , computer ,, hey ", "Aurora");

        Assert.Equal(["d47", "computer", "hey"], phrases);
    }
}
