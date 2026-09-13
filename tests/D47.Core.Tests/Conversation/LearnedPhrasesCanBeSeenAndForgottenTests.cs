using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A learned phrase is listed and forgettable, so a mishearing accepted once does not stay (#171).</summary>
public class LearnedPhrasesCanBeSeenAndForgottenTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-learned-phrases").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly DateTimeOffset At = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private LearnedPhrasesStore Store() =>
        new(Path.Combine(_root, "phrases.json"), NullLogger<LearnedPhrasesStore>.Instance);

    private static CapabilityRegistry Registry(LearnedPhrasesStore store, string? frontierId = "F1") =>
        CapabilityRegistry.Build([LearnedPhrasesCapability.Create(store, () => frontierId ?? string.Empty)]);

    [Fact]
    public async Task ForgettingAnEntryRemovesItFromTheStore()
    {
        var store = Store();
        store.Learn("F1", "set focus on elite", "set focus to elite", At);

        var registry = Registry(store);

        var result = await registry.InvokeAsync(
            LearnedPhrasesCapability.ForgetTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = "set focus on elite" }),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Null(store.PhraseFor("F1", "set focus on elite"));
    }

    [Fact]
    public async Task ForgettingSomethingNotLearnedIsRefused()
    {
        var store = Store();
        var registry = Registry(store);

        var result = await registry.InvokeAsync(
            LearnedPhrasesCapability.ForgetTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = "never said this" }),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task TheModelCallingForgetIsRefused()
    {
        var store = Store();
        store.Learn("F1", "set focus on elite", "set focus to elite", At);

        var registry = Registry(store);

        var result = await registry.InvokeAsync(
            LearnedPhrasesCapability.ForgetTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["said"] = "set focus on elite" }),
            TestContext.Current.CancellationToken,
            caller: ToolCaller.Model);

        Assert.True(result.IsError);
        Assert.NotNull(store.PhraseFor("F1", "set focus on elite"));
    }

    [Fact]
    public void SayingForgetRunsTheToolThroughTheRouter()
    {
        var store = Store();
        store.Learn("F1", "set focus on elite", "set focus to elite", At);

        var registry = Registry(store);
        var router = new KeywordRouter(registry, () => LearnedPhrasesCapability.Phrases(store, () => "F1"));

        var match = router.MatchToolCommand("forget 'set focus on elite'");

        Assert.NotNull(match);
        Assert.Equal(LearnedPhrasesCapability.ForgetTool, match.ToolName);
        Assert.Equal("set focus on elite", match.Arguments.Values["said"]);
    }

    [Fact]
    public void ThePhrasesAreScopedToTheFlyingCommander()
    {
        var store = Store();
        store.Learn("F1", "set focus on elite", "set focus to elite", At);

        Assert.Empty(LearnedPhrasesCapability.Phrases(store, () => "F2"));
        Assert.Single(LearnedPhrasesCapability.Phrases(store, () => "F1"));
    }
}
