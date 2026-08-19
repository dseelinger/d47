using D47.Core.Audio;
using D47.Core.Persona;
using D47.Core.Tests.Conversation;
using Xunit;

namespace D47.Core.Tests.Persona;

/// <summary>
/// No two shipped cores answer in the same voice (remediation.md 11, item 13).
/// <para>
/// The cast is the premise: eleven Guardians who each sound like themselves. Two of them sharing a
/// voice is the one mistake a Commander notices immediately and cannot unhear, and the model doing
/// the pairing was asked for eleven answers and trusted to make them distinct.
/// </para>
/// </summary>
public class NoTwoCoresShareAVoiceTests
{
    private static IReadOnlyList<VoiceInfo> Voices() =>
    [
        new("en-GB-SoniaNeural", "Sonia", "en-GB", "Female"),
        new("en-GB-RyanNeural", "Ryan", "en-GB", "Male"),
        new("en-US-AriaNeural", "Aria", "en-US", "Female"),
        new("en-US-GuyNeural", "Guy", "en-US", "Male"),
        new("en-US-DavisNeural", "Davis", "en-US", "Male"),
        new("en-GB-ThomasNeural", "Thomas", "en-GB", "Male"),
        new("en-US-JennyNeural", "Jenny", "en-US", "Female"),
        new("en-GB-LibbyNeural", "Libby", "en-GB", "Female"),
        new("en-US-TonyNeural", "Tony", "en-US", "Male"),
        new("en-AU-NatashaNeural", "Natasha", "en-AU", "Female"),
        new("en-AU-WilliamNeural", "William", "en-AU", "Male"),
        new("en-IE-ConnorNeural", "Connor", "en-IE", "Male"),

        // Unlabelled, which every core's description admits. The collision has to be arranged on a
        // voice nothing refuses, or the test passes because a core turned it down on sex.
        new("neutral-one", "One", "en-GB"),
        new("neutral-two", "Two", "en-GB"),
    ];

    private static Dictionary<string, string> Nothing() => new(StringComparer.Ordinal);

    /// <summary>
    /// The model handing the same voice to two cores. It is asked for a set and answers with a
    /// list, and nothing about a list is distinct.
    /// </summary>
    [Fact]
    public async Task AVoiceTheModelRepeatsIsNotGivenOutTwice()
    {
        var said = string.Join(
            "\n",
            PersonaCatalog.Shipped.Select(core => $"{core.Id} = en-GB-RyanNeural"));

        var paired = await VoicePairing.ChooseAsync(
            Voices(),
            Nothing(),
            FakeLlmProvider.Answering(said),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        var given = paired.Values.ToArray();

        Assert.Equal(given.Length, given.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// And a voice the Commander already chose is not handed to somebody else either — an existing
    /// pairing is spoken for as firmly as one made a moment ago.
    /// </summary>
    [Fact]
    public async Task AVoiceAlreadyInUseIsNotGivenAway()
    {
        var existing = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["warden"] = "neutral-one",
        };

        var paired = await VoicePairing.ChooseAsync(
            Voices(),
            existing,
            FakeLlmProvider.Answering("cora = neutral-one"),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("neutral-one", paired["warden"]);

        // Paired with something else rather than left with nothing: a core with no pairing speaks
        // in the provider's default, and two of those are two cores in one voice. Which voice it
        // gets is the sex rule's business — the claim here is only that it got one, and not that
        // one.
        Assert.True(paired.ContainsKey("cora"), "the core was left with no voice of its own");
        Assert.NotEqual("neutral-one", paired["cora"]);
    }

    /// <summary>
    /// Where there are fewer voices than cores, nothing is dealt out twice: the cores that cannot
    /// be given one of their own stay unpaired, which is the established answer everywhere else
    /// here — a core keeps the voice in force rather than being dealt one on a guess.
    /// </summary>
    [Fact]
    public async Task FewerVoicesThanCoresStillNeverRepeats()
    {
        IReadOnlyList<VoiceInfo> two =
        [
            new("en-GB-RyanNeural", "Ryan", "en-GB", "Male"),
            new("en-GB-SoniaNeural", "Sonia", "en-GB", "Female"),
        ];

        var said = string.Join(
            "\n",
            PersonaCatalog.Shipped.Select(core => $"{core.Id} = en-GB-RyanNeural"));

        var paired = await VoicePairing.ChooseAsync(
            two,
            Nothing(),
            FakeLlmProvider.Answering(said),
            model: "claude-opus-5",
            spend: null,
            prices: null,
            logger: null,
            cancellationToken: TestContext.Current.CancellationToken);

        var given = paired.Values.ToArray();

        Assert.Equal(given.Length, given.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.True(given.Length <= 2, $"{given.Length} cores were paired from two voices");
    }
}
