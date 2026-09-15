using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using D47.Core.Input;
using D47.Core.Journal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A provider that searches its tools is sent every tool in every mode; any other is sent the mode's list.</summary>
public class TheToolsAreSearchedWhereTheProviderCanTests
{
    /// <summary>A question no model-free route answers.</summary>
    private const string Question = "what is my elite rank in combat";

    private static TurnLoop Build(CapabilityRegistry registry, ILlmProvider provider)
    {
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(true),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock());

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };
        return loop;
    }

    private static async Task<TurnResult> RunAsync(TurnLoop loop, string input)
    {
        TurnResult? result = null;

        await foreach (var turnEvent in loop.RunAsync(input, cancellationToken: TestContext.Current.CancellationToken))
        {
            if (turnEvent is TurnEvent.Completed completed)
            {
                result = completed.Result;
            }
        }

        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task ASearchingProviderIsSentTheSameToolsOnFootInTheSrvAndWithKeyPressesOff()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Elite."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            ToolSearch = true,
        };

        var loop = Build(registry, provider);
        var sent = new List<IReadOnlyList<ToolAdvertisement>>();

        foreach (var (context, actions) in new[]
                 {
                     (ControlContext.OnFoot, true),
                     (ControlContext.Srv, true),
                     (ControlContext.Supercruise, false),
                 })
        {
            loop.ToolContext = () => context;
            loop.ActionsEnabled = () => actions;

            await RunAsync(loop, Question);
            sent.Add(provider.LastRequest!.Prompt.Tools);
        }

        var searchable = ToolSurface.Searchable(registry).Tools;

        Assert.Contains(searchable, tool => tool.Deferred);
        Assert.All(sent, tools => Assert.Equal(searchable, tools));
    }

    [Fact]
    public async Task AProviderThatCannotSearchIsSentTheModesList()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Elite."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
        };

        var loop = Build(registry, provider);
        loop.ToolContext = () => ControlContext.Srv;
        loop.ActionsEnabled = () => true;

        await RunAsync(loop, Question);

        var tools = provider.LastRequest!.Prompt.Tools;

        Assert.Equal(ToolSurface.ForMode(registry, ControlContext.Srv, actionsEnabled: true).Tools, tools);
        Assert.DoesNotContain(tools, tool => tool.Deferred);
    }

    [Fact]
    public async Task ARefusedSearchIsAskedAgainWithTheModesListAndSoIsEveryLaterTurn()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var provider = new DemotingProvider();

        var loop = Build(registry, provider);
        loop.ToolContext = () => ControlContext.OnFoot;
        loop.ActionsEnabled = () => true;

        var first = await RunAsync(loop, Question);
        var second = await RunAsync(loop, Question);

        Assert.Equal(TurnOutcome.Answered, first.Outcome);
        Assert.Equal("Elite.", first.Text);
        Assert.Equal(TurnOutcome.Answered, second.Outcome);

        var modeTools = ToolSurface.ForMode(registry, ControlContext.OnFoot, actionsEnabled: true).Tools;

        Assert.Equal(3, provider.Requests.Count);
        Assert.Equal(ToolSurface.Searchable(registry).Tools, provider.Requests[0].Prompt.Tools);
        Assert.Equal(modeTools, provider.Requests[1].Prompt.Tools);
        Assert.False(provider.Requests[1].Prompt.ToolsSearchable);
        Assert.Equal(modeTools, provider.Requests[2].Prompt.Tools);
    }

    [Fact]
    public async Task ControlSrvCalledByTheModelOnFootIsRefusedWithTheModeAndPressesNothing()
    {
        var input = new RecordingGameInput();
        var registry = CapabilityRegistry.Build(ActionCapabilities.All(new ActionSurface
        {
            Binds = () => new EliteBinds
            {
                PresetName = "Test",
                SourceFile = "Test.binds",
                Bindings = [new EliteBinding("ToggleBuggyTurretButton", "Primary", "Keyboard", "Key_T")],
            },
            Status = () => new GameStatus { Flags2 = (uint)StatusFlags2.OnFoot, ReadAt = DateTimeOffset.UnixEpoch },
            Input = input,
            Enabled = () => true,
        }));

        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Calling("c1", "control_srv", """{"action":"srv_turret"}"""),
            RoundScriptedLlmProvider.Saying("That does nothing on foot."))
        {
            ToolSearch = true,
        };

        var loop = Build(registry, provider);
        loop.ToolContext = () => ControlContext.OnFoot;
        loop.ActionsEnabled = () => true;

        await RunAsync(loop, Question);

        Assert.Contains(provider.Requests[0].Prompt.Tools, tool => tool.Name == "control_srv");

        var result = Assert.IsType<ConversationContent.ToolResult>(
            Assert.Single(provider.Requests[1].Prompt.History[^1].Content));

        Assert.True(result.IsError);
        Assert.Contains("on foot", result.Content, StringComparison.Ordinal);
        Assert.Empty(input.Steps);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheSearchSentenceIsInTheGuardrailsOnlyWhenTheToolsAreSearchable(bool searchable)
    {
        using var install = new TempInstall();
        var provider = new FakeLlmProvider(
            new LlmStreamEvent.TextDelta("Elite."),
            new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed))
        {
            ToolCalls = true,
            ToolSearch = searchable,
        };

        await RunAsync(Build(TestSurface.For(install).Registry, provider), Question);

        var block = provider.LastRequest!.Prompt.RenderCachedSystemBlock();

        Assert.StartsWith(Guardrails.Text, block, StringComparison.Ordinal);
        Assert.Equal(searchable, block.Contains(Guardrails.SearchFirst, StringComparison.Ordinal));
    }

    /// <summary>Refuses tool search once, withdrawing it from its capabilities as the Anthropic provider does, then answers.</summary>
    private sealed class DemotingProvider : ILlmProvider
    {
        private const string Refusal = "Tool search was refused.";

        private bool _searches = true;

        public List<LlmRequest> Requests { get; } = [];

        public string Id => "anthropic";

        public string DisplayName => "Demoting";

        public string DefaultModel => "claude-opus-5";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = true,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 512,
            SupportsToolCalls = true,
            SupportsToolSearch = _searches,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await Task.CompletedTask;

            if (_searches)
            {
                _searches = false;
                yield return new LlmStreamEvent.Failed(Refusal, Transient: false);
                yield break;
            }

            yield return new LlmStreamEvent.TextDelta("Elite.");
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        }
    }
}
