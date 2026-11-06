using D47.Core.Adventures;
using D47.Core.Catalog;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static D47.Core.Tests.Adventures.AdventureFixtures;

namespace D47.Core.Tests.Adventures;

/// <summary>A conversation model the catalog marks as not writing stories is replaced by the provider's default for chapters; other models write their own.</summary>
[Trait("Category", "Integration")]
public sealed class ABackgroundModelDoesNotWriteAStoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);

    private static async Task<IReadOnlyList<LlmRequest>> Written(string providerId, string conversationModel)
    {
        var provider = new RoundScriptedLlmProvider(
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.Spine),
            RoundScriptedLlmProvider.Saying(AdventureGeneratorTests.GoodBeats))
        {
            Id = providerId,
            DefaultModel = ModelCatalog.Embedded.DefaultFor(providerId)!,
        };
        var state = AdventureGeneratorTests.State(1, []);
        var generator = new AdventureGenerator(
            () => provider,
            () => conversationModel,
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => new AdventureGeneratorTests.Galaxy(),
            () => null,
            null,
            null,
            NullLogger.Instance,
            (id, model) => ModelCatalog.Embedded.TraitsFor(id, model).StoryEffort,
            null,
            null,
            (id, model) => ModelCatalog.Embedded.TraitsFor(id, model).WritesStories);
        var chapter = AdventureChapter.Of([LanternRoute(Accepted) with { Key = "first", Spine = new AdventureSpine { Premise = "First premise." } }], "first");

        var outcome = await generator.GenerateAsync(new AdventureAsk(Length: AdventureLength.Short, Chapter: chapter), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        return provider.Requests;
    }

    [Fact]
    public async Task LunaIsReplacedBySol()
    {
        var requests = await Written("openai", "gpt-6-luna");

        Assert.All(requests, request => Assert.Equal("gpt-6.1-sol", request.Model));
        Assert.All(requests, request => Assert.Equal(ModelCatalog.Embedded.TraitsFor("openai", "gpt-6.1-sol").StoryEffort ?? ThinkingEffort.Medium, request.Effort));
    }

    [Fact]
    public async Task HaikuIsReplacedBySonnet()
    {
        var requests = await Written("anthropic", "claude-haiku-5-5");

        Assert.All(requests, request => Assert.Equal("claude-sonnet-5-5", request.Model));
    }

    [Fact]
    public async Task AModelWithNoSuchMarkWritesItsOwnChapter()
    {
        var requests = await Written("openai", "gpt-6.1-sol");

        Assert.All(requests, request => Assert.Equal("gpt-6.1-sol", request.Model));
    }

    [Fact]
    public async Task AModelTheCatalogDoesNotKnowWritesItsOwnChapter()
    {
        var requests = await Written("openai", "my-local-model");

        Assert.All(requests, request => Assert.Equal("my-local-model", request.Model));
    }

    [Fact]
    public void TheShippedCatalogMarksLunaAndHaikuAsNotWritingStories()
    {
        Assert.False(ModelCatalog.Embedded.TraitsFor("openai", "gpt-6-luna").WritesStories);
        Assert.False(ModelCatalog.Embedded.TraitsFor("anthropic", "claude-haiku-5-5").WritesStories);
        Assert.True(ModelCatalog.Embedded.TraitsFor("openai", "gpt-6.1-sol").WritesStories);
        Assert.Null(ModelCatalog.Embedded.TraitsFor("openai", "gpt-6-luna").StoryEffort);
    }
}
