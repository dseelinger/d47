using D47.Core.Adventures;
using D47.Core.Conversation;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>On a server that reports a model's loaded context, a story write brings it to 16,384 tokens first.</summary>
public sealed class TheStoryModelIsReloadedBeforeWritingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null, 0, 0, AdventureOutcome.ContextStep.Nothing)]
    [InlineData(8192, 131072, 2, AdventureOutcome.ContextStep.Nothing)]
    [InlineData(8192, 8192, 1, AdventureOutcome.ContextStep.Nothing)]
    [InlineData(null, 131072, 0, AdventureOutcome.ContextStep.Load)]
    [InlineData(8192, 131072, 1, AdventureOutcome.ContextStep.Reload)]
    [InlineData(16384, 131072, 1, AdventureOutcome.ContextStep.Nothing)]
    [InlineData(32768, 131072, 1, AdventureOutcome.ContextStep.Nothing)]
    public void EachReadHasItsOwnStep(int? tokens, int maximum, int instances, AdventureOutcome.ContextStep expected) =>
        Assert.Equal(expected, AdventureOutcome.StepFor(new LoadedContext(tokens, maximum, instances)));

    [Fact]
    public void ANullReadIsLeftAlone() =>
        Assert.Equal(AdventureOutcome.ContextStep.Nothing, AdventureOutcome.StepFor(null));

    [Fact]
    public void TheFloorOfTheContextNeededIsTheSameNumber() =>
        Assert.Equal(AdventureOutcome.StoryContextTokens, AdventureOutcome.ContextNeeded(null));

    [Fact]
    public async Task AModelLoadedBelowTheFloorIsReloadedAndTheLineShowsDuringIt()
    {
        var progress = new StoryProgress(TimeProvider.System);
        var provider = new Loader(new LoadedContext(8192, 131072, 1), progress);

        await Generate(provider, progress);

        Assert.Equal([16384], provider.Reloads);
        Assert.Equal("Reloading story-model with room for a story.", provider.HintDuringReload);
        Assert.DoesNotContain("room for a story", progress.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AModelThatIsNotLoadedIsLoadedAtTheFloor()
    {
        var progress = new StoryProgress(TimeProvider.System);
        var provider = new Loader(new LoadedContext(null, 131072, 0), progress);

        await Generate(provider, progress);

        Assert.Equal([16384], provider.Reloads);
        Assert.Equal("Loading story-model with room for a story.", provider.HintDuringReload);
    }

    [Theory]
    [InlineData(16384, 131072, 1)]
    [InlineData(32768, 131072, 1)]
    [InlineData(8192, 131072, 2)]
    [InlineData(8192, 8192, 1)]
    public async Task OtherReadsReloadNothingAndShowNoLine(int tokens, int maximum, int instances)
    {
        var progress = new StoryProgress(TimeProvider.System);
        var provider = new Loader(new LoadedContext(tokens, maximum, instances), progress);

        await Generate(provider, progress);

        Assert.Empty(provider.Reloads);
        Assert.Null(provider.HintDuringReload);
        Assert.True(provider.Streams > 0);
    }

    [Fact]
    public async Task ANullReadReloadsNothing()
    {
        var provider = new Loader(null, null);

        await Generate(provider, null);

        Assert.Empty(provider.Reloads);
        Assert.True(provider.Streams > 0);
    }

    [Fact]
    public async Task AFailedReloadStopsTheWriteBeforeAnyStoryCall()
    {
        var provider = new Loader(new LoadedContext(8192, 131072, 1), null) { Reload = new ContextReload(false, "LM Studio said no.") };

        var outcome = await Generate(provider, null);

        Assert.Equal(0, provider.Streams);
        Assert.Null(outcome.Draft);
        Assert.True(outcome.Unanswered);
        Assert.False(outcome.Redraftable);
        Assert.StartsWith("d47 could not load story-model with 16,384 tokens of context. Set the context to at least 16,384 tokens", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedReloadLogsTheServersReason()
    {
        var provider = new Loader(new LoadedContext(8192, 131072, 1), null) { Reload = new ContextReload(false, "LM Studio said no.") };
        var logger = new RecordingLogger();

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), logger, TimeProvider.System)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short), Now, CancellationToken.None);

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("LM Studio said no.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AFailedReloadIsNotWrittenAgainByAShortStory()
    {
        var provider = new Loader(new LoadedContext(8192, 131072, 1), null) { Reload = new ContextReload(false, "no") };

        var outcome = await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), TimeProvider.System)
            .DraftAsync(new AdventureAsk(Length: AdventureLength.Short), Now, _ => { }, CancellationToken.None);

        Assert.Equal(1, provider.LoadedReads);
        Assert.Equal(0, provider.Streams);
        Assert.NotNull(outcome.ContextMessage);
    }

    [Fact]
    public async Task ARevisionChecksTheContextToo()
    {
        var provider = new Loader(new LoadedContext(8192, 131072, 1), null);
        var draft = new Adventure { Key = "k", Name = "Name", Source = AdventureSource.Generated, Written = Now };

        await AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), TimeProvider.System)
            .ReviseAsync(draft, new AdventureAsk(Length: AdventureLength.Short), [], "Shorter.", Now, CancellationToken.None);

        Assert.Equal([16384], provider.Reloads);
    }

    private static Task<AdventureOutcome> Generate(Loader provider, StoryProgress? progress) =>
        AdventureGeneratorTests.Generator(provider, new AdventureGeneratorTests.Galaxy(), new RecordingLogger(), TimeProvider.System)
            .GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Progress: progress), Now, CancellationToken.None);

    private sealed class Loader(LoadedContext? read, StoryProgress? progress) : ILlmProvider, IModelLoader
    {
        public List<int> Reloads { get; } = [];

        public int Streams { get; private set; }

        public int LoadedReads { get; private set; }

        public string? HintDuringReload { get; private set; }

        public ContextReload Reload { get; init; } = new(true, null);

        public string Id => "anthropic";

        public string DisplayName => "Loader";

        public string DefaultModel => "story-model";

        public bool RunsOnThisMachine => true;

        public LlmProviderCapabilities CapabilitiesFor(string model) => new()
        {
            SupportsPromptCaching = false,
            SupportsThinkingEffort = true,
            SupportsOperatorSystemMessages = true,
            MinimumCacheablePrefixTokens = 0,
        };

        public Task<LoadedContext?> LoadedContextAsync(string model, CancellationToken cancellationToken)
        {
            LoadedReads++;
            return Task.FromResult(read);
        }

        public Task<ContextReload> ReloadAsync(string model, int tokens, CancellationToken cancellationToken)
        {
            Reloads.Add(tokens);
            HintDuringReload = progress?.Hint;
            return Task.FromResult(Reload);
        }

        public async IAsyncEnumerable<LlmStreamEvent> StreamAsync(
            LlmRequest request,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Streams++;
            await Task.CompletedTask;
            yield return new LlmStreamEvent.Failed("Server error.", Transient: false);
        }
    }
}
