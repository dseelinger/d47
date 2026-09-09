using D47.Core.Capabilities;
using D47.Core.Conversation;
using D47.Core.Logbook;
using D47.Core.Lore;
using D47.Core.Audio;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Conversation;

/// <summary>
/// Every call says how adventurous its sampler may be, and the creative ones do not say the same thing
/// as the mechanical ones.
/// </summary>
public class TheSamplerIsChosenPerCallClassTests
{
    /// <summary>The mechanical classes ask for no warmth at all.</summary>
    [Fact]
    public void NothingMechanicalAsksForWarmth()
    {
        LlmSampling[] mechanical =
        [
            LlmSampling.VoiceCasting, LlmSampling.Log, LlmSampling.Adventure, LlmSampling.Lore,
        ];

        Assert.All(mechanical, sampling => Assert.Equal(LlmSampling.Cold, sampling.Temperature));

        // And cold is genuinely cold rather than merely lower than warm, which is what a caller that re-asks
        // a validated answer is relying on.
        Assert.Equal(0.0, LlmSampling.Cold);
    }

    /// <summary>The two creative classes ask for warmth, in the band reported for character writing.</summary>
    [Fact]
    public void TheInCharacterClassesAskForWarmth()
    {
        Assert.Equal(LlmSampling.Warm, LlmSampling.Conversation.Temperature);
        Assert.Equal(LlmSampling.Warm, LlmSampling.InCharacter.Temperature);

        Assert.InRange(LlmSampling.Warm, 0.8, 1.0);
    }

    /// <summary>Saying nothing is a choice with a name, and only the key check makes it.</summary>
    [Fact]
    public void UnstatedIsAValueRatherThanAnAbsence()
    {
        Assert.Null(LlmSampling.Unstated.Temperature);
        Assert.NotEqual(LlmSampling.Unstated, LlmSampling.Conversation);
    }

    /// <summary>
    /// A line in a core's voice is warm without its caller having to say so — which is what makes the
    /// six flavour sites that were already in character correct by not changing.
    /// </summary>
    [Fact]
    public async Task AFlavourLineIsInCharacterWithoutBeingAsked()
    {
        var provider = FakeLlmProvider.Answering("Quiet out here.");

        await FlavourTurn.AskAsync(
            provider,
            model: null,
            persona: "You are Warden.",
            aboutMe: null,
            instruction: "Make one short remark.",
            gameState: null,
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(LlmSampling.InCharacter, provider.LastRequest!.Sampling);
    }

    /// <summary>Voice casting asks cold, through the same helper.</summary>
    [Fact]
    public async Task VoiceCastingAsksCold()
    {
        var provider = FakeLlmProvider.Answering("warden = af_heart");

        await VoicePairing.ChooseAsync(
            [
                new VoiceInfo("en-GB-SoniaNeural", "Sonia", "en-GB", "Female"),
                new VoiceInfo("en-GB-RyanNeural", "Ryan", "en-GB", "Male"),
            ],
            new Dictionary<string, string>(StringComparer.Ordinal),
            provider,
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(provider.LastRequest);
        Assert.Equal(LlmSampling.VoiceCasting, provider.LastRequest.Sampling);
        Assert.Equal(0.0, provider.LastRequest.Sampling.Temperature);
    }

    /// <summary>
    /// The Commander's log is cold on the request itself, which is the one call class that never goes
    /// through <see cref="FlavourTurn"/> and so could have been missed.
    /// </summary>
    [Fact]
    public void TheLogAsksCold()
    {
        var request = LogPrompt.Request(
            "claude-opus-5",
            new PromptAssembly
            {
                History = [new ConversationMessage(ConversationRole.User, "write it")],
            },
            LogLength.Brief);

        Assert.Equal(LlmSampling.Log, request.Sampling);
    }

    /// <summary>
    /// A lore lookup states its own sampling beside its own instruction, in Core, so that the choice is
    /// assertable against a value rather than against a running app — which is the same reason the
    /// instruction and the budget live there.
    /// </summary>
    [Fact]
    public void ALoreLookupStatesColdBesideItsInstruction()
    {
        Assert.Equal(LlmSampling.Lore, LoreLookup.Sampling);

        // And the helper honours what it is handed rather than forcing its own default on it, which is the
        // whole mechanism the three mechanical callers rely on.
        Assert.Equal(0.0, LoreLookup.Sampling.Temperature);
    }

    [Fact]
    public async Task ATurnTheCommanderAskedForIsWarm()
    {
        using var install = new TempInstall();

        var provider = FakeLlmProvider.Answering("Hyperspace is fine.");

        var registry = TestSurface.For(install).Registry;
        var availability = new LlmAvailabilityState(true);

        var loop = new TurnLoop(
            registry,
            new KeywordRouter(registry),
            availability,
            new SpendTracker(),
            PriceTable.Default,
            NullLogger<TurnLoop>.Instance,
            provider,
            clock: new InstantClock())
        {
            Retry = RetryPolicy.Default with { Attempts = 1 },
        };

        await foreach (var _ in loop.RunAsync(
            "compose a sonnet about hyperspace",
            cancellationToken: TestContext.Current.CancellationToken))
        {
        }

        Assert.NotNull(provider.LastRequest);
        Assert.Equal(LlmSampling.Conversation, provider.LastRequest.Sampling);
    }
}
