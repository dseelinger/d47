using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>#787: the effects that read no sample after the one they emit run on a clip as it arrives.</summary>
public class TwelveGuardianEffectsRunAsTheClipArrivesTests
{
    private const double BasePitch = 200;

    private static readonly AudioClip Line = GuardianVoiceTests.Voiced(150, seconds: 2.0);

    public static TheoryData<string, int> EachStreamingEffectAtEachLevel => [.. (
        from id in new[]
        {
            "chorus", "flanger", "phaser", "wah", "comb", "ringMod", "deepRingMod", "tremolo", "overdrive", "glitch",
            "respirator", "reverb",
        }
        from level in new[] { GuardianVoice.LowestLevel, GuardianVoice.Find(id)!.DefaultLevel, GuardianVoice.HighestLevel }
        select (id, level)).Distinct()];

    private static IGuardianStage Start(string id, int level)
    {
        var effect = GuardianVoice.Find(id)!;

        return effect.Start(effect.Value(level), BasePitch, Line.Format.SampleRate);
    }

    [Theory]
    [MemberData(nameof(EachStreamingEffectAtEachLevel))]
    public void FedInRandomChunksTheOutputMatchesOneChunk(string id, int level)
    {
        var dry = GuardianVoiceTests.Samples(Line);

        var whole = new List<double>();
        var once = Start(id, level);
        once.Push(dry, whole);
        once.Finish(whole);

        var chunked = new List<double>();
        var piecewise = Start(id, level);
        var random = new Random(787 + level);

        for (var at = 0; at < dry.Length;)
        {
            var size = Math.Min(dry.Length - at, random.Next(0, 2_000));
            piecewise.Push(dry.AsSpan(at, size), chunked);
            at += size;
        }

        piecewise.Finish(chunked);

        Assert.Equal(whole, chunked);
    }

    [Theory]
    [MemberData(nameof(EachStreamingEffectAtEachLevel))]
    public void EverySamplePushedIsEmittedBeforeTheNextArrives(string id, int level)
    {
        var dry = GuardianVoiceTests.Samples(Line);
        var output = new List<double>();
        var stage = Start(id, level);
        var random = new Random(787 + level);

        for (var at = 0; at < dry.Length;)
        {
            var size = Math.Min(dry.Length - at, random.Next(1, 2_000));
            stage.Push(dry.AsSpan(at, size), output);
            at += size;

            Assert.Equal(at, output.Count);
        }
    }
}
