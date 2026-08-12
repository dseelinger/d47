using D47.Core.Audio;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// "Never gated behind a turn completing" (list.md Phase 5, "Shut up").
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
        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths,
            new FakeVerbosityControl(),
            new D47.Core.Journal.GameStateStore(),
            TestSurface.For(install).Settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-test",
            TestSurface.SilentSpeech(onSilence)));

        return new KeywordRouter(registry);
    }

    [Theory]
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

    [Fact]
    public void NothingMatchesEmptyInput()
    {
        using var install = new TempInstall();

        Assert.Null(Router(install).MatchInterrupting("   "));
    }

    /// <summary>
    /// Interrupting is declared on the tool, not decided by name at the call site. A second
    /// tool earning it should be a deliberate edit here, not a surprise.
    /// </summary>
    [Fact]
    public void StopSpeakingIsTheOnlyInterruptingToolThatShips()
    {
        using var install = new TempInstall();

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths,
            new FakeVerbosityControl(),
            new D47.Core.Journal.GameStateStore(),
            TestSurface.For(install).Settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-test",
            TestSurface.SilentSpeech()));

        var interrupting =
            (from capability in registry.All
             from tool in capability.Descriptor.Tools
             where tool.Interrupting
             select tool.Name).ToArray();

        Assert.Equal(["stop_speaking"], interrupting);
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

        var registry = CapabilityRegistry.Build(BuiltinCapabilities.All(
            install.Paths,
            new FakeVerbosityControl(),
            new D47.Core.Journal.GameStateStore(),
            TestSurface.For(install).Settings,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            "1.0.0-test",
            TestSurface.SilentSpeech(() => silenced++)));

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
