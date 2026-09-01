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
/// Every call says how adventurous its sampler may be, and the creative ones do not say the same
/// thing as the mechanical ones
/// (<a href="https://github.com/dseelinger/d47/issues/98">#98</a>).
/// <para>
/// <b>Before this there was no sampling field anywhere in d47</b>, so an ambient remark in a
/// core's voice and a question about which voice to cast took the same unchosen default — one
/// that varies by provider and by model. That is a confound underneath every judgement anybody
/// makes about how a core sounds, and it cuts both ways: the mechanical calls were getting
/// whatever warmth the endpoint felt like.
/// </para>
/// <para>
/// <b>The half most likely to be broken later is the mechanical one</b>, which is why it is
/// pinned here rather than left to the class table: somebody tuning the ambient lines has every
/// reason to reach for <see cref="LlmSampling.Warm"/> and no reason to think about voice casting.
/// </para>
/// </summary>
public class TheSamplerIsChosenPerCallClassTests
{
    /// <summary>
    /// <b>The mechanical classes ask for no warmth at all.</b> The table is the subject here —
    /// the sites that use it are driven below — because a value quietly nudged up is the change
    /// that would pass every one of those.
    /// </summary>
    [Fact]
    public void NothingMechanicalAsksForWarmth()
    {
        LlmSampling[] mechanical =
        [
            LlmSampling.VoiceCasting, LlmSampling.Log, LlmSampling.Adventure, LlmSampling.Lore,
        ];

        Assert.All(mechanical, sampling => Assert.Equal(LlmSampling.Cold, sampling.Temperature));

        // And cold is genuinely cold rather than merely lower than warm, which is what a caller
        // that re-asks a validated answer is relying on.
        Assert.Equal(0.0, LlmSampling.Cold);
    }

    /// <summary>
    /// <b>The two creative classes ask for warmth, in the band reported for character writing.</b>
    /// Bounded on both sides: below about 0.7 characters read flat, which is the complaint #98
    /// exists to make checkable, and above about 1.2 they stop being coherent.
    /// </summary>
    [Fact]
    public void TheInCharacterClassesAskForWarmth()
    {
        Assert.Equal(LlmSampling.Warm, LlmSampling.Conversation.Temperature);
        Assert.Equal(LlmSampling.Warm, LlmSampling.InCharacter.Temperature);

        Assert.InRange(LlmSampling.Warm, 0.8, 1.0);
    }

    /// <summary>
    /// <b>Saying nothing is a choice with a name, and only the key check makes it.</b> That call
    /// asks one token in order to learn whether a key works, against a gateway that may validate
    /// fields d47 has never met — and a rejected field there reads as a rejected key.
    /// </summary>
    [Fact]
    public void UnstatedIsAValueRatherThanAnAbsence()
    {
        Assert.Null(LlmSampling.Unstated.Temperature);
        Assert.NotEqual(LlmSampling.Unstated, LlmSampling.Conversation);
    }

    /// <summary>
    /// A line in a core's voice is warm without its caller having to say so — which is what makes
    /// the six flavour sites that were already in character correct by not changing.
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

    /// <summary>
    /// <b>Voice casting asks cold, through the same helper.</b> The same argument the missing
    /// persona block already made, made to the sampler: this is a mechanical question about d47's
    /// own configuration, never spoken aloud, and parsed line by line afterwards.
    /// </summary>
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
    /// The Commander's log is cold on the request itself, which is the one call class that never
    /// goes through <see cref="FlavourTurn"/> and so could have been missed.
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
    /// A lore lookup states its own sampling beside its own instruction, in Core, so that the
    /// choice is assertable against a value rather than against a running app — which is the same
    /// reason the instruction and the budget live there.
    /// </summary>
    [Fact]
    public void ALoreLookupStatesColdBesideItsInstruction()
    {
        Assert.Equal(LlmSampling.Lore, LoreLookup.Sampling);

        // And the helper honours what it is handed rather than forcing its own default on it,
        // which is the whole mechanism the three mechanical callers rely on.
        Assert.Equal(0.0, LoreLookup.Sampling.Temperature);
    }

    /// <summary>
    /// <b>A turn the Commander asked for is warm, on every round.</b> A turn is one core talking,
    /// and the tools it may call are what make it factual — not the sampler.
    /// </summary>
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
