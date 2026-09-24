using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using D47.Core.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A model that refuses a prompt as larger than its context is asked again with the short tool list (#423).</summary>
public class ASmallContextGetsTheShortToolListTests
{
    /// <summary>A question no model-free route answers.</summary>
    private const string Question = "what is my elite rank in combat";

    private static readonly string[] ShortList =
    [
        "get_capabilities",
        "get_location",
        "get_ship",
        "get_materials",
        "get_session_summary",
        "distance_between",
        "plot_route",
        "find_nearest_station",
        "get_engineer_progress",
        "find_material",
        "get_checklist",
        "get_goals",
        "stop_speaking",
        "cancel_turn",
        "list_settings",
        "get_setting",
        "set_setting",
    ];

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
    public void TheCompactListIsTheSeventeenAlwaysLoadedTools()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;

        var compact = ToolSurface.Compact(registry, ControlContext.None, actionsEnabled: false).Tools;

        Assert.Equal(ShortList.Order(StringComparer.Ordinal), compact.Select(tool => tool.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(compact, tool => tool.Deferred);
    }

    [Fact]
    public async Task AnOverflowIsAskedAgainWithTheShortListAndAnswered()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var provider = new OverflowingProvider(refuseRequests: [0]);

        var result = await RunAsync(Build(registry, provider), Question);

        Assert.Equal(TurnOutcome.Answered, result.Outcome);
        Assert.Equal("Elite.", result.Text);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal(ToolSurface.ForMode(registry, ControlContext.None, actionsEnabled: false).Tools, provider.Requests[0].Prompt.Tools);
        Assert.Equal(ToolSurface.Compact(registry, ControlContext.None, actionsEnabled: false).Tools, provider.Requests[1].Prompt.Tools);
    }

    [Fact]
    public async Task TheNextTurnSendsTheShortListFirst()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var provider = new OverflowingProvider(refuseRequests: [0]);
        var loop = Build(registry, provider);

        await RunAsync(loop, Question);
        var second = await RunAsync(loop, Question);

        Assert.Equal(TurnOutcome.Answered, second.Outcome);
        Assert.Equal(3, provider.Requests.Count);
        Assert.Equal(ToolSurface.Compact(registry, ControlContext.None, actionsEnabled: false).Tools, provider.Requests[2].Prompt.Tools);
    }

    [Fact]
    public async Task RefusedWithTheShortListAndWithoutTheEarlierTurnsItFailsAfterThreeRequests()
    {
        using var install = new TempInstall();
        var registry = TestSurface.For(install).Registry;
        var provider = new OverflowingProvider(refuseRequests: [1, 2, 3]);
        var loop = Build(registry, provider);

        await RunAsync(loop, Question);
        var result = await RunAsync(loop, Question);

        Assert.Equal(TurnOutcome.Failed, result.Outcome);
        Assert.StartsWith("I couldn't reach the model just then.", result.Text, StringComparison.Ordinal);

        // One request for the first turn, three for the second.
        Assert.Equal(4, provider.Requests.Count);
        Assert.Equal(ToolSurface.Compact(registry, ControlContext.None, actionsEnabled: false).Tools, provider.Requests[2].Prompt.Tools);
        Assert.True(provider.Requests[2].Prompt.History.Count > 1);
        Assert.Single(provider.Requests[3].Prompt.History);
    }

    [Fact]
    public void TheModelRowSaysHowManyToolsTheSmallContextGets()
    {
        using var install = new TempInstall();
        var local = new D47Settings { Llm = new LlmSettings { Provider = LlmProviderCatalog.OpenAiCompatibleId } };

        Assert.Equal(
            "This model's context is 8,192 tokens, so D47 offers it 17 of 82 tools. "
            + "Set its context length to 16,384 or more in your server for all of them.",
            ModelRow(install, () => ConversationCapability.ContextNote(8192, 17, 82)).Note!(local));

        Assert.Null(ModelRow(install, () => null).Note!(local));
        Assert.Null(ModelRow(install, () => "unused").Note!(new D47Settings()));
    }

    private static SettingRow ModelRow(TempInstall install, Func<string?> note)
    {
        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        var settings = new SettingsService(
            store,
            new SecretStore(install.Paths, new ReversibleProtector(), NullLogger<SecretStore>.Instance),
            store.Load(),
            NullLogger<SettingsService>.Instance);

        return ConversationCapability
            .Create(
                settings,
                new LlmAvailabilityState(providerConfigured: false),
                new SpendTracker(),
                new TurnCancellation(NullLogger<TurnCancellation>.Instance),
                () => { },
                contextNote: note)
            .Settings
            .Single(row => row.Key == ConversationCapability.ModelKey);
    }

    /// <summary>Refuses the requests it is told to as larger than an 8,192-token context, learning that size as a Chat Completions endpoint does.</summary>
    private sealed class OverflowingProvider(int[] refuseRequests) : ILlmProvider
    {
        private int? _context;

        public List<LlmRequest> Requests { get; } = [];

        public string Id => LlmProviderCatalog.OpenAiCompatibleId;

        public string DisplayName => "Overflowing";

        public string DefaultModel => "local";

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = false,
            SupportsOperatorSystemMessages = false,
            MinimumCacheablePrefixTokens = 0,
            SupportsToolCalls = true,
            ContextTokens = _context,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var index = Requests.Count;
            Requests.Add(request);
            await Task.CompletedTask;

            if (refuseRequests.Contains(index))
            {
                _context = 8192;
                yield return new LlmStreamEvent.Failed("The model's context is 8,192 tokens.", Transient: false)
                {
                    ContextExceeded = true,
                };
                yield break;
            }

            yield return new LlmStreamEvent.TextDelta("Elite.");
            yield return new LlmStreamEvent.Completed(LlmUsage.None, LlmStopReason.Completed);
        }
    }
}
