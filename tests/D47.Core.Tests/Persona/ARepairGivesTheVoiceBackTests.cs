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

    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new(George, "George", "british", "male"),
        new(Matilda, "Matilda", "american", "female"),
        new(Callum, "Callum", "british", "male"),
    ];

    private static Task<VoicePairing.VoiceRepair> RepairAsync(
        IReadOnlyDictionary<string, string> before,
        IReadOnlyDictionary<string, string> after,
        D47.Core.Conversation.ILlmProvider? provider = null,
        IReadOnlyList<VoiceInfo>? voices = null) =>
        VoicePairing.WithReplacementsAsync(
            before,
            after,
            voices ?? Voices(),
            provider,
            model: provider is null ? null : "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

    [Fact]
    public async Task ACoreTheRepairStripsIsGivenAVoiceRatherThanNone()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["analyst-prime"] = Matilda };
        var after = VoicePairing.WithoutMiscastVoices(before, Voices());

        // The repair really did take it off, or this test proves nothing.
        Assert.False(after.ContainsKey("analyst-prime"));

        var repair = await RepairAsync(before, after);

        Assert.Contains(repair.Voices["analyst-prime"], new[] { George, Callum });
        Assert.True(repair.Complete);
    }

    [Fact]
    public async Task AModelsChoiceIsWrittenDownToo()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };
        var after = VoicePairing.WithoutMiscastVoices(before, Voices());

        Assert.False(after.ContainsKey("cora"));

        var repair = await RepairAsync(before, after, FakeLlmProvider.Answering($"cora = {Matilda}"));

        Assert.Equal(Matilda, repair.Voices["cora"]);
        Assert.True(repair.Complete);
    }

    /// <summary>A list with no voice that fits leaves the core where it was, and the repair unfinished.</summary>
    [Fact]
    public async Task ACoreNothingCanBeChosenForKeepsWhatItHad()
    {
        IReadOnlyList<VoiceInfo> men = [new(George, "George", "british", "male"), new(Callum, "Callum", "british", "male")];
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["cora"] = George };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, men), voices: men);

        Assert.Equal(George, repair.Voices["cora"]);
        Assert.False(repair.Complete);
    }

    /// <summary>Two cores repaired in one pass must not both be handed the same voice.</summary>
    [Fact]
    public async Task TwoCoresRepairedTogetherDoNotEndUpSharingAVoice()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["analyst-prime"] = Matilda,
            ["cora"] = George,
        };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        Assert.Equal(Matilda, repair.Voices["cora"]);
        Assert.Contains(repair.Voices["analyst-prime"], new[] { George, Callum });
    }

    /// <summary>A repair that changed nothing answers the same instance and asks nothing of the model.</summary>
    [Fact]
    public async Task ARepairThatChangedNothingIsLeftAlone()
    {
        var before = new Dictionary<string, string>(StringComparer.Ordinal) { ["warden"] = Matilda };
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
            ["analyst-prime"] = Matilda,
        };

        var repair = await RepairAsync(before, VoicePairing.WithoutMiscastVoices(before, Voices()));

        Assert.Equal(Matilda, repair.Voices["warden"]);
    }
}
