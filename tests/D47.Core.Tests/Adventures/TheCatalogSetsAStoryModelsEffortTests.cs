using D47.Core.Adventures;
using D47.Core.Catalog;
using D47.Core.Conversation;
using D47.Core.Tests.Conversation;
using D47.Core.Tests.Stories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Adventures;

/// <summary>Story calls ask for the effort the catalog names for the chosen model, Medium when it names none, and the generator's own Effort overrides both.</summary>
[Trait("Category", "Integration")]
public sealed class TheCatalogSetsAStoryModelsEffortTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private static Func<string, string, ThinkingEffort?> Names(ThinkingEffort? effort) => (_, _) => effort;

    [Fact]
    public async Task AShortStoryAsksForTheEffortTheCatalogNames()
    {
        var provider = Scripted();

        var outcome = await Generator(provider, Names(ThinkingEffort.Low))
            .GenerateAsync(Short, Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.All(provider.Requests, request => Assert.Equal(ThinkingEffort.Low, request.Effort));
    }

    [Fact]
    public async Task AChapterAsksForTheEffortTheCatalogNames()
    {
        var provider = Scripted();
        
        var outcome = await Generator(provider, Names(ThinkingEffort.High))
            .GenerateAsync(PlannedChapterWorld.Ask(), Now, CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Refusal);
        Assert.All(provider.Requests, request => Assert.Equal(ThinkingEffort.High, request.Effort));
    }

    [Fact]
    public async Task AModelTheCatalogNamesNoEffortForGetsMedium()
    {
        var provider = Scripted();

        await Generator(provider, Names(null))
            .GenerateAsync(Short, Now, CancellationToken.None);

        Assert.All(provider.Requests, request => Assert.Equal(ThinkingEffort.Medium, request.Effort));
    }

    [Fact]
    public async Task TheGeneratorsOwnEffortOverridesTheCatalog()
    {
        var provider = Scripted();

        await Generator(provider, Names(ThinkingEffort.Low), ThinkingEffort.High)
            .GenerateAsync(Short, Now, CancellationToken.None);

        Assert.All(provider.Requests, request => Assert.Equal(ThinkingEffort.High, request.Effort));
    }

    [Fact]
    public async Task TheLookupIsMadeForTheProviderAndModelChosen()
    {
        var provider = Scripted();
        (string, string)? asked = null;

        await Generator(provider, (id, model) =>
            {
                asked = (id, model);
                return null;
            })
            .GenerateAsync(Short, Now, CancellationToken.None);

        Assert.Equal(("anthropic", "story-model"), asked);
    }

    [Theory]
    [InlineData("""{"storyEffort": "low"}""", ThinkingEffort.Low)]
    [InlineData("""{"storyEffort": "High"}""", ThinkingEffort.High)]
    [InlineData("""{"storyEffort": "enormous"}""", null)]
    [InlineData("""{"storyEffort": 3}""", null)]
    [InlineData("""{"storyEffort": null}""", null)]
    [InlineData("""{"images": true}""", null)]
    public void AnUnreadableStoryEffortIsNullAndTheRestOfTheCatalogStands(string traits, ThinkingEffort? expected)
    {
        var catalog = ModelCatalog.Parse(Catalog(traits));

        Assert.Equal(expected, catalog.TraitsFor("openai", "gpt-6-luna").StoryEffort);
        Assert.True(catalog.Knows("openai", "gpt-6-luna"));
        Assert.Equal(ModelCatalog.Embedded.DefaultFor("openai"), catalog.DefaultFor("openai"));
    }

    /// <summary>The shipped catalog with <paramref name="traits"/> as gpt-6-luna's traits.</summary>
    private static string Catalog(string traits)
    {
        var assembly = typeof(ModelCatalog).Assembly;
        using var reader = new StreamReader(assembly.GetManifestResourceStream("D47.Core.ModelCatalog")!);
        var json = reader.ReadToEnd();
        var from = json.IndexOf("\"id\": \"gpt-6-luna\"", StringComparison.Ordinal);
        var start = json.IndexOf("\"traits\": {", from, StringComparison.Ordinal);
        var end = json.IndexOf('}', start);

        return json[..start] + "\"traits\": " + traits + json[(end + 1)..];
    }

    private static readonly AdventureAsk Short = new(AdventureReach.Session, AdventureLength.Evening);

    private static RoundScriptedLlmProvider Scripted() => new(
        RoundScriptedLlmProvider.Saying(PlannedChapterWorld.ShortLines()),
        RoundScriptedLlmProvider.Saying(PlannedChapterWorld.NoObjections));

    private static AdventureGenerator Generator(ILlmProvider provider, Func<string, string, ThinkingEffort?> storyEffort, ThinkingEffort? effort = null)
    {
        var state = PlannedChapterWorld.State();
        var galaxy = PlannedChapterWorld.Roomy();

        return new AdventureGenerator(
            () => provider,
            () => "story-model",
            () => "You are the ship.",
            () => "core",
            () => null,
            () => state,
            () => galaxy,
            () => null,
            null,
            null,
            NullLogger.Instance,
            storyEffort)
        {
            Effort = effort,
        };
    }
}
