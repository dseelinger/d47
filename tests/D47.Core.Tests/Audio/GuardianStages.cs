using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Starts a Guardian stage by id and feeds it whole or in random chunks.</summary>
internal static class GuardianStages
{
    private const double BasePitch = 200;

    private const int Rate = 48_000;

    public static TheoryData<string, int> EachLevel(params string[] ids) => [.. (
        from id in ids
        from level in new[] { GuardianVoice.LowestLevel, GuardianVoice.Find(id)!.DefaultLevel, GuardianVoice.HighestLevel }
        select (id, level)).Distinct()];

    public static IGuardianStage Start(string id, int level, double basePitch, int rate)
    {
        var effect = GuardianVoice.Find(id)!;

        return effect.Start(effect.Value(level), basePitch, rate);
    }

    /// <summary>The output of one push of <paramref name="dry"/>, and of pushes cut at random up to <paramref name="maxChunk"/> samples.</summary>
    public static (List<double> Whole, List<double> Chunked) WholeAndChunked(string id, int level, double[] dry, int seed, int maxChunk)
    {
        var whole = new List<double>();
        var once = Start(id, level, BasePitch, Rate);
        once.Push(dry, whole);
        once.Finish(whole);

        var chunked = new List<double>();
        var piecewise = Start(id, level, BasePitch, Rate);
        var random = new Random(seed);

        for (var at = 0; at < dry.Length;)
        {
            var size = Math.Min(dry.Length - at, random.Next(0, maxChunk));
            piecewise.Push(dry.AsSpan(at, size), chunked);
            at += size;
        }

        piecewise.Finish(chunked);
        return (whole, chunked);
    }
}
