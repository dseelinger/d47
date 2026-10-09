using D47.Core.Storage;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Conversation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>"Teach a phrase" asks for the wording, then the phrase it runs, then a yes, and stores it (#539).</summary>
public class APhraseCanBeTaughtByVoiceTests
{
    private readonly MemoryFileSystem _files = new();

    private readonly string _root = Path.Combine(Path.GetTempPath(), "d47-teach-by-voice");

    private readonly List<string> _pressed = [];

    private LearnedPhrasesStore Store() =>
        new(Path.Combine(_root, "phrases.json"), _files, NullLogger<LearnedPhrasesStore>.Instance);

    private ToolDefinition Key(string name, string phrase) => new()
    {
        Name = name,
        Description = "Presses a key.",
        Commands = [new ToolCommandPhrase(phrase, new Dictionary<string, string>())],
        Handler = (_, _) =>
        {
            _pressed.Add(name);
            return Task.FromResult(ToolResult.Ok($"Pressed {name}."));
        },
    };

    private TurnLoop Build(LearnedPhrasesStore store, string? commander = "F1")
    {
        KeywordRouter? router = null;

        var registry = CapabilityRegistry.Build(
        [
            new CapabilityDescriptor
            {
                Id = "keys",
                Group = "Test",
                Name = "Keys",
                Summary = "Records what was pressed.",
                Tools = [Key("drop_wheels", "drop the wheels"), Key("gear_down", "gear down")],
            },
            LearnedPhrasesCapability.Create(store, () => commander ?? string.Empty, () => router!.Book),
        ]);

        router = new KeywordRouter(registry);

        var loop = new TurnLoop(
            registry,
            router,
            new LlmAvailabilityState(providerConfigured: false),
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            clock: new InstantClock())
        {
            CommanderId = () => commander,
            LearnedPhraseFor = utterance => commander is null ? null : store.PhraseFor(commander, utterance),
        };

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
    public async Task TheWholeExchangeStoresThePhraseAndTheWordingThenRunsIt()
    {
        var store = Store();
        var loop = Build(store);

        Assert.Equal("What do you want to say?", (await RunAsync(loop, "teach a phrase")).Text);
        Assert.Equal("What should it do?", (await RunAsync(loop, "wheels out")).Text);
        Assert.Equal("'wheels out' will do 'drop the wheels'. Keep it?", (await RunAsync(loop, "drop the wheels")).Text);

        var kept = await RunAsync(loop, "yes");

        Assert.Equal(TurnOutcome.Answered, kept.Outcome);
        Assert.Empty(_pressed);
        Assert.Equal("drop the wheels", store.PhraseFor("F1", "wheels out"));

        await RunAsync(loop, "wheels out");

        Assert.Equal(["drop_wheels"], _pressed);
    }

    [Fact]
    public async Task NewPhraseStartsItToo()
    {
        var loop = Build(Store());

        Assert.Equal("What do you want to say?", (await RunAsync(loop, "new phrase")).Text);
    }

    [Fact]
    public async Task ACapturedWordingOfGearDownPressesNoKey()
    {
        var loop = Build(Store());

        await RunAsync(loop, "teach a phrase");
        var captured = await RunAsync(loop, "gear down");

        Assert.Equal("What should it do?", captured.Text);
        Assert.Empty(_pressed);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("the second one")]
    public async Task AWordingThatAnswersAnOfferIsNotTaken(string wording)
    {
        var loop = Build(Store());

        await RunAsync(loop, "teach a phrase");

        Assert.Equal(
            $"'{wording}' answers questions, so it cannot be taught. What do you want to say?",
            (await RunAsync(loop, wording)).Text);
        Assert.Equal("What should it do?", (await RunAsync(loop, "wheels out")).Text);
    }

    [Fact]
    public async Task APhraseCanBePickedFromTheDidYouMean()
    {
        var store = Store();
        var loop = Build(store);

        await RunAsync(loop, "teach a phrase");
        await RunAsync(loop, "wheels out");

        Assert.Equal("Did you mean 'drop the wheels'?", (await RunAsync(loop, "drop the wheel")).Text);
        Assert.Equal("'wheels out' will do 'drop the wheels'. Keep it?", (await RunAsync(loop, "yes")).Text);

        await RunAsync(loop, "yes");

        Assert.Empty(_pressed);
        Assert.Equal("drop the wheels", store.PhraseFor("F1", "wheels out"));
    }

    [Fact]
    public async Task AReplyThatReachesNoPhraseEndsTheExchangeAndLearnsNothing()
    {
        var store = Store();
        var loop = Build(store);

        await RunAsync(loop, "teach a phrase");
        await RunAsync(loop, "wheels out");

        var missed = await RunAsync(loop, "xyzzy plugh");

        Assert.Equal("No phrase matches that.", missed.Text);
        Assert.Empty(store.For("F1"));

        await RunAsync(loop, "drop the wheels");

        Assert.Equal(["drop_wheels"], _pressed);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task CancelAtAnyStepLearnsNothing(int step)
    {
        var store = Store();
        var loop = Build(store);

        string[] before = ["teach a phrase", "wheels out", "drop the wheels"];

        foreach (var reply in before.Take(step - 1))
        {
            await RunAsync(loop, reply);
        }

        Assert.Equal("Dropped.", (await RunAsync(loop, "cancel")).Text);
        Assert.Empty(store.For("F1"));

        await RunAsync(loop, "drop the wheels");

        Assert.Equal(["drop_wheels"], _pressed);
    }

    [Fact]
    public async Task AClashIsRefusedWithAddPhrasesMessage()
    {
        var store = Store();
        var loop = Build(store);

        await RunAsync(loop, "teach a phrase");
        await RunAsync(loop, "gear down");
        await RunAsync(loop, "drop the wheels");

        var refused = await RunAsync(loop, "yes");

        Assert.Equal(TurnOutcome.Failed, refused.Outcome);
        Assert.Equal("\"gear down\" is already the phrase \"gear down\" in keys.", refused.Text);
        Assert.Empty(store.For("F1"));
        Assert.Empty(_pressed);
    }

    [Fact]
    public async Task WithNobodyFlyingItDoesNotStart()
    {
        var loop = Build(Store(), commander: null);

        var refused = await RunAsync(loop, "teach a phrase");

        Assert.Equal("Nobody is flying, so there is nobody to teach a phrase to.", refused.Text);

        await RunAsync(loop, "drop the wheels");

        Assert.Equal(["drop_wheels"], _pressed);
    }
}
