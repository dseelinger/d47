using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// "Never gated behind a turn completing" (Phase 5, "Shut up").
/// <para>
/// Every surface must gate ordinary input on a turn being in flight, or a second question
/// tramples the first. That gate is precisely what a silence command must not be behind — and
/// the moment it is wanted is the moment the gate is closed, so the failure is invisible in any
/// test that is not looking for it. These are looking for it.
/// </para>
/// </summary>
public class InterruptingCommandTests
{
    private static KeywordRouter Router(TempInstall install, Action? onSilence = null)
    {
        CapabilityRegistry? built = null;

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths,
            new FakeVerbosityControl(),
            new D47.Core.Journal.GameStateStore(),
            TestSurface.For(install).Settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-test",
            TestSurface.SilentSpeech(onSilence),
            TestSurface.NoFleet(),
            SpokenNamesSurface.Inert,
            new TurnCancellation(NullLogger<TurnCancellation>.Instance),
            new CalloutEngine(NullLogger<CalloutEngine>.Instance),
            () => built!,
            TestSurface.SilentListening(),
            TestSurface.NoHeadset(),
            ActionSurface.Inert,
            () => "No autonomous actions in a test.",
            NavigationSurface.Inert,
            new D47.Core.Actions.MacroStore(
                Path.Combine(install.Paths.Data, "macros.json"),
                NullLogger<D47.Core.Actions.MacroStore>.Instance),
            new D47.Core.Persona.PersonaHost(),
            TestSurface.Checklists(install.Paths)));

        built = registry;

        return new KeywordRouter(registry);
    }

    /// <summary>
    /// Typed input arrives however it arrives. Case is handled by matching case-insensitively
    /// rather than by lowercasing the string, and surrounding whitespace and punctuation by
    /// word-boundary matching — so nothing has to remember to normalise before asking.
    /// </summary>
    [Theory]
    [InlineData("STOP")]
    [InlineData("Stop")]
    [InlineData("sToP")]
    [InlineData("stop.")]
    [InlineData("stop?")]
    [InlineData("   stop   ")]
    [InlineData("...stop...")]
    [InlineData("\tstop\n")]
    [InlineData("\"stop\"")]
    public void SilenceIsRecognisedWhateverTheCaseOrPunctuation(string said)
    {
        using var install = new TempInstall();

        Assert.NotNull(Router(install).MatchInterrupting(said));
    }

    /// <summary>
    /// The other half of word-boundary matching, and the reason it is not a substring search:
    /// "stop" inside a longer word is not the command.
    /// </summary>
    [Theory]
    [InlineData("stopping")]
    [InlineData("unstoppable")]
    [InlineData("nonstop")]
    public void AWordMerelyContainingStopIsNotTheCommand(string said)
    {
        using var install = new TempInstall();

        Assert.Null(Router(install).MatchInterrupting(said));
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("Stop!")]
    [InlineData("stop it")]
    [InlineData("enough")]
    [InlineData("quiet")]
    [InlineData("shut up")]
    [InlineData("be quiet")]
    [InlineData("stop talking")]
    [InlineData("stop speaking")]
    [InlineData("d47, shut up please")]
    public void EveryWayOfAskingForSilenceMayInterrupt(string said)
    {
        using var install = new TempInstall();

        var match = Router(install).MatchInterrupting(said);

        Assert.NotNull(match);
        Assert.Equal("stop_speaking", match.ToolName);
    }

    /// <summary>
    /// The narrowness is the point. If ordinary questions could interrupt, the gate every
    /// surface needs would be meaningless and a second question would trample the first.
    /// </summary>
    [Theory]
    [InlineData("where am I")]
    [InlineData("what system is this")]
    [InlineData("status")]
    [InlineData("what can you do")]
    public void AnOrdinaryQuestionMayNot(string said)
    {
        using var install = new TempInstall();

        Assert.Null(Router(install).MatchInterrupting(said));
    }

    /// <summary>
    /// The price of a one-syllable interrupt: "stop" is a bare common verb, and a Commander-named
    /// macro (Phase 10) may legitimately contain it. It stays out of the general vocabulary
    /// entirely, so nothing routes it to silence on the ordinary path — the surface only consults
    /// the interrupt list while there is something to interrupt.
    /// </summary>
    [Theory]
    [InlineData("stop")]
    [InlineData("stop the mining run")]
    [InlineData("stop recording that macro")]
    public void BareStopIsNotAGeneralCommand(string said)
    {
        using var install = new TempInstall();

        Assert.Null(Router(install).Match(said));
    }

    /// <summary>
    /// And the phrases that can only ever mean silence stay on the ordinary path too, so they
    /// work when d47 is idle — asking a quiet d47 to be quiet should not be an error.
    /// </summary>
    [Theory]
    [InlineData("shut up")]
    [InlineData("stop talking")]
    public void AnUnambiguousSilencePhraseStillWorksWhenIdle(string said)
    {
        using var install = new TempInstall();

        var match = Router(install).Match(said);

        Assert.NotNull(match);
        Assert.Equal("stop_speaking", match.ToolName);
    }

    [Fact]
    public void NothingMatchesEmptyInput()
    {
        using var install = new TempInstall();

        Assert.Null(Router(install).MatchInterrupting("   "));
    }

    /// <summary>
    /// Interrupting is declared on the tool, not decided by name at the call site. A tool
    /// earning it should be a deliberate edit here, not a surprise — this test is the reason
    /// cancel_turn could not be added quietly.
    /// <para>
    /// Two, and they are different strengths: stop_speaking stops the mouth, cancel_turn ends
    /// the work. Anything that wants to join them has to justify interrupting a turn in flight.
    /// </para>
    /// </summary>
    [Fact]
    public void OnlyTheTwoInterruptsShip()
    {
        using var install = new TempInstall();

        CapabilityRegistry? built = null;

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths,
            new FakeVerbosityControl(),
            new D47.Core.Journal.GameStateStore(),
            TestSurface.For(install).Settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-test",
            TestSurface.SilentSpeech(),
            TestSurface.NoFleet(),
            SpokenNamesSurface.Inert,
            new TurnCancellation(NullLogger<TurnCancellation>.Instance),
            new CalloutEngine(NullLogger<CalloutEngine>.Instance),
            () => built!,
            TestSurface.SilentListening(),
            TestSurface.NoHeadset(),
            ActionSurface.Inert,
            () => "No autonomous actions in a test.",
            NavigationSurface.Inert,
            new D47.Core.Actions.MacroStore(
                Path.Combine(install.Paths.Data, "macros.json"),
                NullLogger<D47.Core.Actions.MacroStore>.Instance),
            new D47.Core.Persona.PersonaHost(),
            TestSurface.Checklists(install.Paths)));

        built = registry;

        var interrupting =
            (from capability in registry.All
             from tool in capability.Descriptor.Tools
             where tool.Interrupting
             select tool.Name).ToArray();

        Assert.Equal(["cancel_turn", "stop_speaking"], interrupting.OrderBy(name => name, StringComparer.Ordinal));
    }

    /// <summary>
    /// End to end at the layer that matters: the phrase reaches the arbiter and the queue is
    /// actually cleared, with no model and no turn involved.
    /// </summary>
    [Fact]
    public async Task TheCommandReachesTheArbiterAndSilencesIt()
    {
        using var install = new TempInstall();
        var silenced = 0;

        CapabilityRegistry? built = null;

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths,
            new FakeVerbosityControl(),
            new D47.Core.Journal.GameStateStore(),
            TestSurface.For(install).Settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-test",
            TestSurface.SilentSpeech(() => silenced++),
            TestSurface.NoFleet(),
            SpokenNamesSurface.Inert,
            new TurnCancellation(NullLogger<TurnCancellation>.Instance),
            new CalloutEngine(NullLogger<CalloutEngine>.Instance),
            () => built!,
            TestSurface.SilentListening(),
            TestSurface.NoHeadset(),
            ActionSurface.Inert,
            () => "No autonomous actions in a test.",
            NavigationSurface.Inert,
            new D47.Core.Actions.MacroStore(
                Path.Combine(install.Paths.Data, "macros.json"),
                NullLogger<D47.Core.Actions.MacroStore>.Instance),
            new D47.Core.Persona.PersonaHost(),
            TestSurface.Checklists(install.Paths)));

        built = registry;

        var match = new KeywordRouter(registry).MatchInterrupting("shut up");
        Assert.NotNull(match);

        var result = await registry.InvokeAsync(
            match.ToolName, ToolArguments.Empty, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(1, silenced);
    }

    /// <summary>
    /// The tool is reachable with no language model configured at all — the router is the
    /// model-free path, and a stop that needed the model would be gated behind the thing it is
    /// interrupting (architecture.md §7).
    /// </summary>
    [Fact]
    public void SilenceNeedsNoModel()
    {
        using var install = new TempInstall();
        var availability = new LlmAvailabilityState(providerConfigured: false);

        Assert.False(availability.CanAttemptModelTurn);
        Assert.NotNull(Router(install).MatchInterrupting("shut up"));
    }
}
