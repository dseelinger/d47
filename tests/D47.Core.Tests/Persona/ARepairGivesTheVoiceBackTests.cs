using D47.Core.Audio;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// A voice repair has to give a voice back to every core it takes one off (list.md Phase 11,
/// #33).
/// <para>
/// Both repairs work by <em>removing</em> a pairing, and a core with no pairing has no voice. On
/// Edge it falls back to the ship AI's, so two cores share one; on ElevenLabs there is nothing to
/// fall back to and the provider refuses the sentence outright.
/// </para>
/// <para>
/// This lived in <c>AppHost.WithReplacementsAsync</c>, where no test could reach it, and it was
/// wrong: the replacement was chosen, logged as <c>"{Persona} takes {Voice} instead"</c>, and
/// never written to the map. Three of the tests below fail against that version —
/// <see cref="ACoreTheRepairStripsIsGivenAVoiceRatherThanNone"/>,
/// <see cref="AModelsChoiceIsWrittenDownToo"/> and
/// <see cref="TwoCoresRepairedTogetherDoNotEndUpSharingAVoice"/>. The rest pin the properties
/// that were already right, so the next change to this has all of them to answer to
/// (list.md Phase 19, "Give the composition root a test harness").
/// </para>
/// <para>
/// What made the fault permanent rather than merely wrong is that the stripped core was reported
/// as a <em>completed</em> repair: the caller wrote its "this repair has run" mark, and the pass
/// never ran again. See <see cref="TheRepairIsNotMarkedDoneWhenACoreWasLeftBehind"/> for the
/// half of that which was always correct.
/// </para>
/// </summary>
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

    /// <summary>
    /// The fault, at its smallest. Warden is written male and was holding a woman's voice, so the
    /// gender repair drops the pairing — and the whole point of the pass is that Warden comes out
    /// of it holding George, its named default, rather than holding nothing.
    /// </summary>
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
    /// No model at all, which is the common case: a named default is a lookup rather than a
    /// judgement, so the repair still completes without anything to ask.
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
        // Cora is written female and was holding a man's voice. Nothing names a default for her,
        // so this is the path that goes to the model — and the answer still has to land.
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };
        var after = VoicePairing.WithoutMiscastVoices(before, Voices());

        Assert.False(after.ContainsKey("cora"));

        var repair = await RepairAsync(before, after, FakeLlmProvider.Answering($"cora = {Matilda}"));

        Assert.Equal(Matilda, repair.Voices["cora"]);
        Assert.True(repair.Complete);
    }

    /// <summary>
    /// Nothing could be chosen, so the core keeps what it had. Two cores sharing a voice, or one
    /// speaking in the wrong gender, are both smaller faults than one that cannot speak at all.
    /// </summary>
    [Fact]
    public async Task ACoreNothingCanBeChosenForKeepsWhatItHad()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };

        // No model and no named default for Cora, so ChooseOneAsync answers null.
        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        Assert.Equal(George, repair.Voices["cora"]);
    }

    /// <summary>
    /// And the repair says it did not finish, so the caller leaves its mark off. This is the half
    /// that made the original fault permanent rather than merely wrong: the stripped core was
    /// reported as a completed repair, the flag was written, and the pass never ran again.
    /// </summary>
    [Fact]
    public async Task TheRepairIsNotMarkedDoneWhenACoreWasLeftBehind()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        Assert.False(repair.Complete);
    }

    /// <summary>
    /// Two cores repaired in one pass must not both be handed the same voice. The replacement has
    /// to be recorded as it is chosen, because what has already been taken is what the next
    /// choice is filtered against — so dropping it on the floor offers it again.
    /// </summary>
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

        // Both are still paired — asserted before the distinctness, because two cores holding
        // nothing are also trivially not sharing anything, which is exactly what the broken
        // version did.
        Assert.Equal(George, repair.Voices["warden"]);
        Assert.Equal(Callum, repair.Voices["sentinel"]);

        var assigned = repair.Voices.Values.ToArray();

        Assert.Equal(assigned.Length, assigned.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// A repair that changed nothing answers the same instance and asks nothing of the model. The
    /// pass runs on a launch where every pairing is already right far more often than not.
    /// </summary>
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

    /// <summary>
    /// Cores the repair did not touch come through untouched. The map that is written is the
    /// whole set, not just the repaired part, so anything dropped here is a pairing lost.
    /// </summary>
    [Fact]
    public async Task PairingsTheRepairDidNotTouchSurviveIt()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = Matilda,
            ["cora"] = Matilda,
        };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        // Cora is written female and Matilda is a woman's voice, so that pairing was never in
        // question.
        Assert.Equal(Matilda, repair.Voices["cora"]);
    }
}
