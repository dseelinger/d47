using D47.Core.Callouts;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// While the Commander is away, a call nobody started is answered as away and never reaches a provider that is not on
/// this machine; a call the Commander started, or one already under way, is sent.
/// </summary>
public sealed class UnpromptedCallsAreNotSentWhileAwayTests
{
    private static Task<FlavourReply> Ask(ILlmProvider provider, SpendTracker spend, bool underway = false) =>
        FlavourTurn.AskForAsync(
            provider,
            model: null,
            persona: null,
            aboutMe: null,
            "Say something.",
            gameState: null,
            spend,
            PriceTable.Default,
            logger: null,
            purpose: SpendPurposes.RewordedCallout,
            TestContext.Current.CancellationToken,
            underway: underway);

    private static FakeLlmProvider Answering() =>
        FakeLlmProvider.Answering("All quiet out here.", new LlmUsage(100, 20, 0, 0));

    [Fact]
    public async Task AnUnpromptedCallToTheCloudIsNotSentAndCostsNothing()
    {
        var inner = Answering();
        var spend = new SpendTracker();

        var reply = await Ask(AwayGate.Wrap(inner, () => true), spend);

        Assert.Equal(FlavourMiss.Away, reply.Miss);
        Assert.Null(reply.Line);
        Assert.Equal(0, inner.CallCount);
        Assert.Equal(0, spend.TurnCount);
    }

    [Fact]
    public async Task AnUnpromptedCallIsSentWhileTheCommanderIsThere()
    {
        var inner = Answering();
        var spend = new SpendTracker();

        var reply = await Ask(AwayGate.Wrap(inner, () => false), spend);

        Assert.Equal("All quiet out here.", reply.Line);
        Assert.Equal(1, inner.CallCount);
        Assert.Equal(1, spend.TurnCount);
    }

    [Fact]
    public async Task AModelOnThisMachineIsStillAsked()
    {
        var inner = new OnThisMachine(Answering());

        var reply = await Ask(AwayGate.Wrap(inner, () => true), new SpendTracker());

        Assert.Equal("All quiet out here.", reply.Line);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task ACallAlreadyUnderWayIsSent()
    {
        var inner = Answering();

        var reply = await Ask(AwayGate.Wrap(inner, () => true), new SpendTracker(), underway: true);

        Assert.Equal("All quiet out here.", reply.Line);
        Assert.Equal(1, inner.CallCount);
        Assert.False(inner.LastRequest!.Background);
    }

    [Fact]
    public async Task AFlavourLineIsBackgroundUnlessUnderWay()
    {
        var inner = Answering();

        await Ask(inner, new SpendTracker());

        Assert.True(inner.LastRequest!.Background);
    }

    [Fact]
    public async Task ARequestThatIsNotBackgroundIsSent()
    {
        var inner = Answering();
        var gate = AwayGate.Wrap(inner, () => true);
        var request = new LlmRequest
        {
            Model = "claude-opus-5",
            Effort = ThinkingEffort.Low,
            Sampling = LlmSampling.InCharacter,
            Prompt = new PromptAssembly { History = [new ConversationMessage(ConversationRole.User, "What is my jump range?")] },
        };

        var events = new List<LlmStreamEvent>();

        await foreach (var streamEvent in gate.StreamAsync(request, TestContext.Current.CancellationToken))
        {
            events.Add(streamEvent);
        }

        Assert.Equal(1, inner.CallCount);
        Assert.Contains(events, streamEvent => streamEvent is LlmStreamEvent.Completed);
    }

    [Fact]
    public async Task TheGateIsReadAtEachCall()
    {
        var inner = Answering();
        var away = true;
        var gate = AwayGate.Wrap(inner, () => away);

        Assert.Equal(FlavourMiss.Away, (await Ask(gate, new SpendTracker())).Miss);

        away = false;

        Assert.Equal("All quiet out here.", (await Ask(gate, new SpendTracker())).Line);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public void TheGateReportsWhatItWraps()
    {
        var inner = new OnThisMachine(new FakeLlmProvider { Id = "openai-compatible", DefaultModel = "qwen3-8b", WebSearch = false });
        var gate = AwayGate.Wrap(inner, () => true);

        Assert.Equal("openai-compatible", gate.Id);
        Assert.Equal("qwen3-8b", gate.DefaultModel);
        Assert.True(gate.RunsOnThisMachine);
        Assert.False(gate.CapabilitiesFor("qwen3-8b").SupportsWebSearch);
        Assert.False(AwayGate.Wrap(Answering(), () => true).RunsOnThisMachine);
    }

    [Fact]
    public async Task TheGateLoadsModelsExactlyWhenWhatItWrapsDoes()
    {
        var loader = new Loader(Answering());

        Assert.IsAssignableFrom<IModelLoader>(AwayGate.Wrap(loader, () => true));
        Assert.IsNotAssignableFrom<IModelLoader>(AwayGate.Wrap(Answering(), () => true));

        var reload = await ((IModelLoader)AwayGate.Wrap(loader, () => true))
            .ReloadAsync("qwen3-8b", 32768, TestContext.Current.CancellationToken);

        Assert.True(reload.Loaded);
        Assert.Equal(("qwen3-8b", 32768), loader.Reloaded);
    }

    [Fact]
    public async Task ARewordedCalloutRefusedWhileAwayIsSpokenAsWritten()
    {
        const string Authored = "Good evening, Commander. Good to have you back.";
        var inner = Answering();
        var gate = AwayGate.Wrap(inner, () => true);
        var asked = 0;

        var said = await new Rewording(new RewordChance(new Random(1)), null).VaryAsync(
            new Announcement(ContinuityCallout.Key, Authored),
            hasModel: true,
            personalityEnabled: true,
            rewordPercent: 100,
            () => ShipFacts.Unknown,
            "Ash",
            (brief, instruction, token) =>
            {
                asked++;
                return FlavourTurn.AskForAsync(
                    gate, null, null, null, instruction, null, new SpendTracker(), PriceTable.Default, null, SpendPurposes.RewordedCallout, token);
            });

        Assert.Equal(Authored, said?.Text);
        Assert.Equal(1, asked);
        Assert.Equal(0, inner.CallCount);
    }

    /// <summary>A provider pointed at this machine.</summary>
    private sealed class OnThisMachine(FakeLlmProvider inner) : ILlmProvider
    {
        public int Calls => inner.CallCount;

        public string Id => inner.Id;

        public bool RunsOnThisMachine => true;

        public string DefaultModel => inner.DefaultModel;

        public LlmProviderCapabilities CapabilitiesFor(string model) => inner.CapabilitiesFor(model);

        public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken) =>
            inner.StreamAsync(request, cancellationToken);
    }

    /// <summary>A provider whose server reloads models.</summary>
    private sealed class Loader(FakeLlmProvider inner) : ILlmProvider, IModelLoader
    {
        public (string Model, int Tokens)? Reloaded { get; private set; }

        public string Id => inner.Id;

        public string DefaultModel => inner.DefaultModel;

        public LlmProviderCapabilities CapabilitiesFor(string model) => inner.CapabilitiesFor(model);

        public IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken cancellationToken) =>
            inner.StreamAsync(request, cancellationToken);

        public Task<LoadedContext?> LoadedContextAsync(string model, CancellationToken cancellationToken) =>
            Task.FromResult<LoadedContext?>(new LoadedContext(8192, 32768, 1));

        public Task<ContextReload> ReloadAsync(string model, int tokens, CancellationToken cancellationToken)
        {
            Reloaded = (model, tokens);
            return Task.FromResult(new ContextReload(true, null));
        }
    }
}
