using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>A run from a "did you mean" offer asks once whether to remember the Commander's own wording.</summary>
public class ACommandersWordingIsLearnedOnAYesTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("d47-phrases").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static readonly DateTimeOffset At = new(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);

    private sealed class Spy
    {
        public List<string> Ran { get; } = [];

        public CapabilityRegistry Registry(string phrase = "run the first thing", bool guarded = false) =>
            CapabilityRegistry.Build(
            [
                new CapabilityDescriptor
                {
                    Id = "spy",
                    Group = "Test",
                    Name = "Spy",
                    Summary = "Records what ran.",
                    Tools = [Tool("first_thing", phrase, guarded)],
                },
            ]);

        private ToolDefinition Tool(string name, string phrase, bool guarded) => new()
        {
            Name = name,
            Description = "Records that it ran.",
            Protected = guarded,
            Commands = [new ToolCommandPhrase(phrase, new Dictionary<string, string>())],
            Handler = (_, _) =>
            {
                Ran.Add(name);
                return Task.FromResult(ToolResult.Ok($"Ran {name}."));
            },
        };
    }

    private LearnedPhrasesStore Store() =>
        new(Path.Combine(_root, "phrases.json"), NullLogger<LearnedPhrasesStore>.Instance);

    private static TurnLoop Build(CapabilityRegistry registry, LearnedPhrasesStore? store = null, string? commander = "F1")
    {
        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            clock: new InstantClock())
        {
            CommanderId = () => commander,
        };

        loop.Retry = RetryPolicy.Default with { Attempts = 1 };

        if (store is not null)
        {
            loop.LearnedPhraseFor = utterance => commander is null ? null : store.PhraseFor(commander, utterance);
            loop.LearnPhrase = (said, phrase) =>
            {
                if (commander is not null)
                {
                    store.Learn(commander, said, phrase, At);
                }
            };
        }

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
    public async Task AnAcceptedOfferRunsThenAsksOnceAndYesWritesTheEntry()
    {
        var spy = new Spy();
        var store = Store();
        var loop = Build(spy.Registry(), store);

        var offered = await RunAsync(loop, "run the first think");
        Assert.Equal(TurnRoute.Offer, offered.Route);

        var ran = await RunAsync(loop, "yes");
        Assert.Equal(["first_thing"], spy.Ran);
        Assert.Contains(
            "Want me to remember 'run the first think' as another way to say that?",
            ran.Text,
            StringComparison.Ordinal);

        var learned = await RunAsync(loop, "yes");
        Assert.Equal("Learned.", learned.Text);

        var reading = Store();
        reading.Load();
        Assert.Equal("run the first thing", reading.PhraseFor("F1", "run the first think"));
    }

    [Fact]
    public async Task AfterARestartTheSameUtteranceRunsWithNoOffer()
    {
        var spy = new Spy();
        var writer = Store();
        writer.Learn("F1", "run the first think", "run the first thing", At);

        var reloaded = Store();
        reloaded.Load();

        var loop = Build(spy.Registry(), reloaded);

        var ran = await RunAsync(loop, "run the first think");

        Assert.NotEqual(TurnRoute.Offer, ran.Route);
        Assert.Equal(["first_thing"], spy.Ran);
    }

    [Fact]
    public async Task NoWritesNothingAndTheNextTurnRoutesNormally()
    {
        var spy = new Spy();
        var store = Store();
        var loop = Build(spy.Registry(), store);

        await RunAsync(loop, "run the first think");
        var ran = await RunAsync(loop, "yes");
        Assert.Contains("Want me to remember", ran.Text, StringComparison.Ordinal);

        var dropped = await RunAsync(loop, "no");
        Assert.Equal("Dropped.", dropped.Text);

        var reading = Store();
        reading.Load();
        Assert.Null(reading.PhraseFor("F1", "run the first think"));

        var next = await RunAsync(loop, "run the second thing entirely");
        Assert.NotEqual(TurnRoute.Offer, next.Route);
    }

    [Fact]
    public async Task ADeclinedUtteranceIsNotAskedAboutAgainThisSession()
    {
        var spy = new Spy();
        var store = Store();
        var loop = Build(spy.Registry(), store);

        await RunAsync(loop, "run the first think");
        await RunAsync(loop, "yes");
        await RunAsync(loop, "no");

        await RunAsync(loop, "run the first think");
        var ran = await RunAsync(loop, "yes");

        Assert.Equal(["first_thing", "first_thing"], spy.Ran);
        Assert.DoesNotContain("Want me to remember", ran.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUtteranceEqualToAnExistingPhraseIsNotOfferedForLearning()
    {
        var spy = new Spy();
        var store = Store();
        var loop = Build(spy.Registry(), store);

        loop.Offers.Open(new Offer(
        [
            new OfferChoice(
                "run the first thing",
                new OfferTarget.RoutePhrase("run the first thing", Guarded: false, Said: "run the first thing")),
        ]));

        var ran = await RunAsync(loop, "yes");

        Assert.Equal(["first_thing"], spy.Ran);
        Assert.DoesNotContain("Want me to remember", ran.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUtteranceAlreadyLearnedForAnotherPhraseIsNotOfferedAgain()
    {
        var spy = new Spy();
        var store = Store();
        store.Learn("F1", "run the first think", "some other phrase", At);
        var loop = Build(spy.Registry(), store);

        loop.Offers.Open(new Offer(
        [
            new OfferChoice(
                "run the first thing",
                new OfferTarget.RoutePhrase("run the first thing", Guarded: false, Said: "run the first think")),
        ]));

        var ran = await RunAsync(loop, "yes");

        Assert.Equal(["first_thing"], spy.Ran);
        Assert.DoesNotContain("Want me to remember", ran.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALearnedAliasOfAGuardedPhraseRunsExactlyLikeTheDeclaredPhrase()
    {
        var spy = new Spy();
        var writer = Store();
        writer.Learn("F1", "run the first think", "run the first thing", At);

        var reloaded = Store();
        reloaded.Load();

        var loop = Build(spy.Registry(guarded: true), reloaded);

        var ran = await RunAsync(loop, "run the first think");

        Assert.Equal(["first_thing"], spy.Ran);
        Assert.NotEqual(TurnRoute.Offer, ran.Route);
    }

    [Fact]
    public async Task AnEntryWhosePhraseHasLeftTheVocabularyIsIgnored()
    {
        var spy = new Spy();
        var writer = Store();
        writer.Learn("F1", "run the first think", "run the vanished thing", At);

        var reloaded = Store();
        reloaded.Load();

        var loop = Build(spy.Registry(), reloaded);

        var result = await RunAsync(loop, "run the first think");

        Assert.Empty(spy.Ran);
        Assert.Equal(TurnRoute.Offer, result.Route);
    }

    [Fact]
    public async Task NoCommanderKnownIsNeverAskedToLearn()
    {
        var spy = new Spy();
        var store = Store();
        var loop = Build(spy.Registry(), store, commander: null);

        await RunAsync(loop, "run the first think");
        var ran = await RunAsync(loop, "yes");

        Assert.Equal(["first_thing"], spy.Ran);
        Assert.DoesNotContain("Want me to remember", ran.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APhraseRunDirectlyWithNoOfferIsNeverAskedAboutLearning()
    {
        var spy = new Spy();
        var store = Store();
        var loop = Build(spy.Registry(), store);

        var ran = await RunAsync(loop, "run the first thing");

        Assert.Equal(["first_thing"], spy.Ran);
        Assert.DoesNotContain("Want me to remember", ran.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AOneWordUtteranceIsNeverAskedAboutLearning()
    {
        var spy = new Spy();
        var store = Store();
        var loop = Build(spy.Registry("run", guarded: true), store);

        await RunAsync(loop, "run");
        var ran = await RunAsync(loop, "yes");

        Assert.Equal(["first_thing"], spy.Ran);
        Assert.DoesNotContain("Want me to remember", ran.Text, StringComparison.Ordinal);
    }
}
