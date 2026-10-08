using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A learned phrase is a pattern expanded into wordings, and matching stays an exact lookup (#537).</summary>
public class APhraseCanBeTaughtAsAPatternTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-phrase-patterns").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly DateTimeOffset At = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    private string PhrasesPath => Path.Combine(_root, "phrases.json");

    private LearnedPhrasesStore Store() => new(PhrasesPath, NullLogger<LearnedPhrasesStore>.Instance);

    private static PhraseBook Book(LearnedPhrasesStore store)
    {
        var registry = CapabilityRegistry.Build([LearnedPhrasesCapability.Create(store, () => "F1")]);

        DynamicCommand Command(string phrase) =>
            new(phrase, LearnedPhrasesCapability.Id, LearnedPhrasesCapability.ForgetTool, new Dictionary<string, string>());

        return PhraseBook.From(registry, [Command("drop the wheels"), Command("raise the wheels")]);
    }

    private static CapabilityRegistry Registry(LearnedPhrasesStore store) =>
        CapabilityRegistry.Build([LearnedPhrasesCapability.Create(store, () => "F1", () => Book(store))]);

    private static Task<ToolResult> Add(
        CapabilityRegistry registry, string pattern, string phrase, ToolCaller caller = ToolCaller.Commander) =>
        registry.InvokeAsync(
            LearnedPhrasesCapability.AddTool,
            new ToolArguments(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["pattern"] = pattern, ["phrase"] = phrase }),
            TestContext.Current.CancellationToken,
            caller: caller);

    [Fact]
    public void TwoGroupsMakeFourWordingsAndEachRunsThePhrase()
    {
        Assert.True(PhrasePattern.TryExpand("[boost|get clear] and [jump|engage]", out var wordings, out _));
        Assert.Equal(4, wordings.Count);

        var store = Store();
        store.Learn("F1", "[boost|get clear] and [jump|engage]", "jump out", At);

        foreach (var wording in wordings)
        {
            Assert.Equal("jump out", store.PhraseFor("F1", wording));
        }
    }

    [Fact]
    public void AnEmptyAlternativeMakesAGroupOptional()
    {
        var store = Store();
        store.Learn("F1", "[please|] drop the wheels", "drop the wheels", At);

        Assert.Equal("drop the wheels", store.PhraseFor("F1", "drop the wheels"));
        Assert.Equal("drop the wheels", store.PhraseFor("F1", "please drop the wheels"));
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("[a|b", "never closed")]
    [InlineData("a]", "no '['")]
    [InlineData("[a|[b|c]]", "nested")]
    public void ABrokenPatternIsRefusedWithTheProblemNamed(string pattern, string problem)
    {
        Assert.False(PhrasePattern.TryExpand(pattern, out _, out var error));
        Assert.Contains(problem, error);
    }

    [Fact]
    public void AHundredAndOneWordingsAreRefused()
    {
        static string Group(int count) => "[" + string.Join('|', Enumerable.Range(0, count).Select(i => $"w{i}")) + "]";

        Assert.False(PhrasePattern.TryExpand(Group(101), out _, out var error));
        Assert.Contains("100", error);
        Assert.True(PhrasePattern.TryExpand(Group(100), out _, out _));
    }

    [Fact]
    public async Task AWordingEqualToAPhraseInTheBookIsRefusedNamingIt()
    {
        var result = await Add(Registry(Store()), "[please|] raise the wheels", "drop the wheels");

        Assert.True(result.IsError);
        Assert.Contains("raise the wheels", result.Content);
    }

    [Fact]
    public void AClashWithTheBookReportsItsCapability()
    {
        var store = Store();

        var clash = store.FindClash("F1", ["raise the wheels"], "drop the wheels", Book(store));

        Assert.NotNull(clash);
        Assert.Equal(PhraseClashKind.BookPhrase, clash.Kind);
        Assert.Equal("raise the wheels", clash.Wording);
        Assert.Equal("raise the wheels", clash.StandsFor);
        Assert.Equal(LearnedPhrasesCapability.Id, clash.CapabilityId);
        Assert.Null(clash.Pattern);
    }

    [Fact]
    public void AWordingThatDiffersFromTheBookOnlyByTheClashes()
    {
        var store = Store();

        var clash = store.FindClash("F1", ["raise wheels"], "drop the wheels", Book(store));

        Assert.NotNull(clash);
        Assert.Equal(PhraseClashKind.BookPhrase, clash.Kind);
        Assert.Equal("raise the wheels", clash.StandsFor);
    }

    [Fact]
    public void AClashWithTheCommandersOwnPhraseReportsItsPattern()
    {
        var store = Store();
        store.Learn("F1", "[gear|wheels] down", "raise the wheels", At);

        var clash = store.FindClash("F1", ["gear down"], "drop the wheels", Book(store));

        Assert.NotNull(clash);
        Assert.Equal(PhraseClashKind.OwnPhrase, clash.Kind);
        Assert.Equal("[gear|wheels] down", clash.Pattern);
        Assert.Equal("raise the wheels", clash.StandsFor);
        Assert.Null(clash.CapabilityId);
    }

    [Fact]
    public async Task APatternAlreadyTaughtForThePhraseChangesNothing()
    {
        var store = Store();
        var registry = Registry(store);

        Assert.False((await Add(registry, "[gear|wheels] down", "drop the wheels")).IsError);

        var again = await Add(registry, "[gear|wheels] down", "drop the wheels");

        Assert.False(again.IsError);
        Assert.Contains("already taught", again.Content);
        Assert.Single(store.For("F1"));
    }

    [Fact]
    public async Task APhraseNotInTheBookIsRefused()
    {
        var result = await Add(Registry(Store()), "gear down", "no such phrase");

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task TheModelCallingAddIsRefusedAndTheCommanderStoresThePhrase()
    {
        var store = Store();
        var registry = Registry(store);

        Assert.True((await Add(registry, "gear down", "drop the wheels", ToolCaller.Model)).IsError);
        Assert.Null(store.PhraseFor("F1", "gear down"));

        Assert.False((await Add(registry, "gear down", "drop the wheels")).IsError);
        Assert.Equal("drop the wheels", store.PhraseFor("F1", "gear down"));
    }

    [Fact]
    public void APhrasesFileWrittenBeforePatternsStillLoadsAndMatches()
    {
        File.WriteAllText(
            PhrasesPath,
            """
            {"commanders":[{"frontierId":"F1","phrases":[
              {"said":"set focus on elite","phrase":"set focus to elite","learnedAt":"2026-09-13T12:00:00+00:00"}]}]}
            """);

        var store = Store();
        store.Load();

        Assert.Equal("set focus to elite", store.PhraseFor("F1", "set focus on elite"));
        Assert.Single(store.For("F1"));
    }

    [Fact]
    public void APatternSurvivesAReloadAsOneEntryWithAllItsWordings()
    {
        Store().Learn("F1", "[gear|wheels] down", "drop the wheels", At);

        var reloaded = Store();
        reloaded.Load();

        Assert.Single(reloaded.For("F1"));
        Assert.Equal("[gear|wheels] down", reloaded.For("F1")[0].Said);
        Assert.Equal("drop the wheels", reloaded.PhraseFor("F1", "wheels down"));
    }

    [Fact]
    public void ForgettingAPatternRemovesAllItsWordingsAndNothingElse()
    {
        var store = Store();
        store.Learn("F1", "[gear|wheels] down", "drop the wheels", At);
        store.Learn("F1", "lower the gear", "drop the wheels", At);

        Assert.True(store.Forget("F1", "[gear|wheels] down"));

        Assert.Null(store.PhraseFor("F1", "gear down"));
        Assert.Null(store.PhraseFor("F1", "wheels down"));
        Assert.Equal("drop the wheels", store.PhraseFor("F1", "lower the gear"));
    }

    [Fact]
    public void ANearMissCannotBecomeAPatternByAccident()
    {
        var literal = PhrasePattern.Literal("a [b|c] d");

        Assert.DoesNotContain('[', literal);
        Assert.DoesNotContain(']', literal);
        Assert.DoesNotContain('|', literal);
    }
}
