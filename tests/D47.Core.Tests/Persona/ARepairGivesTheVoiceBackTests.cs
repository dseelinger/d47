using D47.Core.Audio;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>A voice repair has to give a voice back to every core it takes one off.</summary>
public class ARepairGivesTheVoiceBackTests
{
    private const string George = "JBFqnCBsd6RMkjVDRZzb";
    private const string Matilda = "XrExE9yKIg1WjnnlVkGX";
    private const string Callum = "N2lVS1w4EtoT3dr4eOWO";

    private const string Eleven = TtsProviderCatalog.ElevenLabsId;

    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new(George, "George", "british", "male"),
        new(Matilda, "Matilda", "american", "female"),
        new(Callum, "Callum", "british", "male"),
    ];

    private static Task<VoicePairing.VoiceRepair> RepairAsync(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        D47.Core.Conversation.ILlmProvider? provider = null) =>
        VoicePairing.WithReplacementsAsync(
            before,
            after,
            Voices(),
            provider,
            model: provider is null ? null : "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            ttsProvider: Eleven,
            cancellationToken: TestContext.Current.CancellationToken);

    /// <summary>The fault, at its smallest.</summary>
    [Fact]
    public async Task ACoreTheRepairStripsIsGivenAVoiceRatherThanNone()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = Matilda };
        var after = VoicePairing.WithoutMiscastVoices(before, Voices());

        // The repair really did take it off, or this test proves nothing.
        Assert.False(after.ContainsKey("warden"));

        var repair = await RepairAsync(before, after);

        Assert.True(repair.Voices.ContainsKey("warden"));
        Assert.Equal(George, repair.Voices["warden"]);
    }

    /// <summary>
    /// No model at all, which is the common case: a named default is a lookup rather than a judgement,
    /// so the repair still completes without anything to ask.
    /// </summary>
    [Fact]
    public async Task TheRepairFinishesWithNoModelWhenTheAnswerIsANamedDefault()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = Matilda };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        Assert.True(repair.Complete);
    }

    [Fact]
    public async Task AModelsChoiceIsWrittenDownToo()
    {
        // Cora is written female and was holding a man's voice.
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };
        var after = VoicePairing.WithoutMiscastVoices(before, Voices());

        Assert.False(after.ContainsKey("cora"));

        var repair = await RepairAsync(before, after, FakeLlmProvider.Answering($"cora = {Matilda}"));

        Assert.Equal(Matilda, repair.Voices["cora"]);
        Assert.True(repair.Complete);
    }

    [Fact]
    public async Task ACoreNothingCanBeChosenForKeepsWhatItHad()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };

        // No model and no named default for Cora, so ChooseOneAsync answers null.
        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        Assert.Equal(George, repair.Voices["cora"]);
    }

    /// <summary>And the repair says it did not finish, so the caller leaves its mark off.</summary>
    [Fact]
    public async Task TheRepairIsNotMarkedDoneWhenACoreWasLeftBehind()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        Assert.False(repair.Complete);
    }

    /// <summary>Two cores repaired in one pass must not both be handed the same voice.</summary>
    [Fact]
    public async Task TwoCoresRepairedTogetherDoNotEndUpSharingAVoice()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = Matilda,
            ["sentinel"] = Matilda,
        };

        var after = VoicePairing.WithoutMiscastVoices(before, Voices());
        var repair = await RepairAsync(before, after, FakeLlmProvider.Answering($"sentinel = {Callum}"));

        // Both are still paired — asserted before the distinctness, because two cores holding nothing are
        // also trivially not sharing anything, which is exactly what the broken version did.
        Assert.Equal(George, repair.Voices["warden"]);
        Assert.Equal(Callum, repair.Voices["sentinel"]);

        var assigned = repair.Voices.Values.ToArray();

        Assert.Equal(assigned.Length, assigned.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>A repair that changed nothing answers the same instance and asks nothing of the model.</summary>
    [Fact]
    public async Task ARepairThatChangedNothingIsLeftAlone()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = George };
        var after = VoicePairing.WithoutMiscastVoices(before, Voices());

        Assert.Same(before, after);

        var repair = await RepairAsync(before, after);

        Assert.Same(before, repair.Voices);
        Assert.True(repair.Complete);
    }

    /// <summary>Cores the repair did not touch come through untouched.</summary>
    [Fact]
    public async Task PairingsTheRepairDidNotTouchSurviveIt()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = Matilda,
            ["cora"] = Matilda,
        };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        // Cora is written female and Matilda is a woman's voice, so that pairing was never in question.
        Assert.Equal(Matilda, repair.Voices["cora"]);
    }
}
