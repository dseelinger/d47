using D47.Core.Adventures;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>
/// A story call the endpoint refuses as larger than the model's context gives the Commander the prompt size and the
/// context to set, and is not written again.
/// </summary>
public sealed class AStoryPromptTooBigForTheContextSaysSoTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ARefusalNamingThePromptSizeAsksForTheContextItNeeds()
    {
        var outcome = await Generate(new Refusing(new LlmStreamEvent.Failed("Too big.", Transient: false) { ContextExceeded = true, PromptTokens = 20016 }));

        Assert.NotNull(outcome.ContextMessage);
        Assert.Contains("20,016 tokens", outcome.ContextMessage);
        Assert.Contains("28,672", outcome.ContextMessage);
        Assert.Equal(outcome.ContextMessage, outcome.Message);
        Assert.False(outcome.Redraftable);
    }

    [Fact]
    public async Task ARefusalWithoutAPromptSizeAsksForTheFloor()
    {
        var outcome = await Generate(new Refusing(new LlmStreamEvent.Failed("Too big.", Transient: false) { ContextExceeded = true }));

        Assert.Contains("larger than the model's context", outcome.ContextMessage);
        Assert.Contains("16,384", outcome.ContextMessage);
    }

    [Fact]
    public async Task AnOverflowIsNotWrittenAgain()
    {
        var provider = new Refusing(new LlmStreamEvent.Failed("Too big.", Transient: false) { ContextExceeded = true, PromptTokens = 20016 });

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), TimeProvider.System)
            .DraftAsync(new AdventureAsk(Length: AdventureLength.Short), Now, _ => { }, CancellationToken.None);

        Assert.Equal(1, provider.Calls);
        Assert.NotNull(outcome.ContextMessage);
    }

    [Fact]
    public async Task AFailureThatIsNotAnOverflowIsHandledAsBefore()
    {
        var outcome = await Generate(new Refusing(new LlmStreamEvent.Failed("Server error.", Transient: false)));

        Assert.Null(outcome.ContextMessage);
        Assert.True(outcome.Redraftable);
    }

    private static Task<AdventureOutcome> Generate(ILlmProvider provider) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), TimeProvider.System)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

    private sealed class Refusing(LlmStreamEvent.Failed failure) : ILlmProvider
    {
        public int Calls { get; private set; }

        public string Id => "anthropic";

        public string DisplayName => "Refusing";

        public string DefaultModel => "claude-opus-5";

        public bool RunsOnThisMachine => true;

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 0,
        };

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            await Task.CompletedTask;
            yield return failure;
        }
    }
}
