using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Persona;
using D47.Core.Stories;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// The Guardian chain and its leveller as a running filter give the same bytes however the input is cut, and
/// <see cref="GuardianVoice.Apply"/> is that filter over the whole clip.
/// </summary>
public class TheGuardianVoiceIsTheSameHoweverItArrivesTests
{
    private const double BasePitch = 165;

    private static readonly AudioClip Speech = PcmConverter.ToStandard(StandInVoice.Clip);

    private static readonly SpeechSettings DamagedCore = new()
    {
        GuardianVoice = new GuardianVoiceSettings
        {
            Effects = [.. GuardianPresets.EffectsFor(GuardianPresets.Find("damaged-core")!)],
        },
    };

    private static byte[] Whole(IPcmFilter filter, ReadOnlySpan<byte> pcm) => [.. filter.Push(pcm), .. filter.Finish()];

    /// <summary>Chunks of 1 to 4,999 bytes, odd sizes included, so samples are split across pushes.</summary>
    private static IEnumerable<int> RandomCuts(int length, int seed)
    {
        var random = new Random(seed);

        for (var at = 0; at < length;)
        {
            var size = Math.Min(length - at, random.Next(1, 5_000));
            yield return size;
            at += size;
        }
    }

    private static byte[] Chunked(IPcmFilter filter, byte[] pcm, int seed)
    {
        var output = new List<byte>();
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, seed))
        {
            output.AddRange(filter.Push(pcm.AsSpan(at, size)));
            at += size;
        }

        output.AddRange(filter.Finish());
        return [.. output];
    }

    /// <summary>Every built-in preset's chain, and the stock order with every effect ticked.</summary>
    public static TheoryData<string> Chains =>
    [
        .. GuardianPresets.Builtins.Where(preset => preset.TickedIds.Count > 0).Select(preset => string.Join(',', preset.TickedIds)),
        string.Join(',', GuardianVoice.Table.Select(effect => effect.Id)),
    ];

    [Theory]
    [MemberData(nameof(Chains))]
    public void AClipFedInRandomChunksIsByteIdenticalToTheClipFedWhole(string ids)
    {
        var effects = GuardianVoiceTests.Ticking(ids.Split(','));
        var pcm = Speech.Pcm.ToArray();

        Assert.Equal(
            Whole(GuardianVoice.Filter(effects, BasePitch), pcm),
            Chunked(GuardianVoice.Filter(effects, BasePitch), pcm, ids.Length));
    }

    [Theory]
    [MemberData(nameof(Chains))]
    public void ApplyIsTheFilterOverTheWholeClip(string ids)
    {
        var effects = GuardianVoiceTests.Ticking(ids.Split(','));

        Assert.Equal(
            Whole(GuardianVoice.Filter(effects, BasePitch), Speech.Pcm.Span),
            GuardianVoice.Apply(Speech, effects, BasePitch).Pcm.ToArray());
    }

    public static TheoryData<string, int> EachEffectAtBothEnds =>
    [
        .. GuardianVoice.Table.SelectMany(effect => new[] { GuardianVoice.LowestLevel, GuardianVoice.HighestLevel }
            .Select(level => (effect.Id, level))),
    ];

    [Theory]
    [MemberData(nameof(EachEffectAtBothEnds))]
    public void NoSamplePassesTheCeiling(string id, int level)
    {
        var effects = GuardianVoice.Defaults
            .Select(effect => effect.Id == id ? effect with { Ticked = true, Level = level } : effect)
            .ToList();
        var treated = GuardianVoiceTests.Samples(GuardianVoice.Apply(Speech, effects, BasePitch));

        Assert.True(treated.Max(Math.Abs) <= GuardianVoice.Ceiling, $"{id} at level {level} reaches {treated.Max(Math.Abs):F4}");
    }

    [Fact]
    public void AGuardianCoreHasARunningChainExactlyWhereItHasATreatment()
    {
        var none = new SpeechSettings
        {
            GuardianVoice = new GuardianVoiceSettings
            {
                Effects = [.. GuardianVoice.Defaults.Select(effect => effect with { Ticked = false })],
            },
        };

        foreach (var speech in new[] { DamagedCore, none })
        {
            Assert.Equal(
                GuardianVoice.ColourFor(speech, PersonaCatalog.Warden) is null,
                GuardianVoice.RunningColourFor(speech, PersonaCatalog.Warden) is null);
        }
    }

    [Fact]
    public void AGuardianCoresRunningChainEndsAsItsTreatmentWould()
    {
        var speech = DamagedCore;
        var colour = GuardianVoice.ColourFor(speech, PersonaCatalog.Warden)!;
        var running = GuardianVoice.RunningColourFor(speech, PersonaCatalog.Warden)!;

        Assert.Equal(colour(Speech).Pcm.ToArray(), Chunked(running(), Speech.Pcm.ToArray(), 788));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.5)]
    public void ACastMemberWithEffectsRunsWhatItsTreatmentApplies(double? link)
    {
        var pinned = new PinnedVoice("kokoro", "bm_george")
        {
            Link = link,
            Effects = [new StorySpeakerEffect("comb", 12), new StorySpeakerEffect("cylon", 1)],
        };

        Assert.Equal(
            CastVoice.Treatment(pinned)!(Speech).Pcm.ToArray(),
            Chunked(CastVoice.Running(pinned)!(), Speech.Pcm.ToArray(), 47));
    }

    [Fact]
    public async Task AnArrivingClipThroughTheChainEndsAsApplyWouldHaveIt()
    {
        var effects = GuardianVoiceTests.Ticking("comb", "ringMod", "glitch");
        var source = new ArrivingClip("line");
        var treated = source.Through(GuardianVoice.Filter(effects, BasePitch));
        var pcm = Speech.Pcm.ToArray();
        var at = 0;

        foreach (var size in RandomCuts(pcm.Length, 47))
        {
            source.Append(pcm.AsSpan(at, size));
            at += size;
        }

        Assert.False(treated.IsComplete);

        source.Complete();

        Assert.Equal(GuardianVoice.Apply(Speech, effects, BasePitch).Pcm.ToArray(), (await treated.Whole).Pcm.ToArray());
    }
}
