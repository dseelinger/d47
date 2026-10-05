using D47.Core.Audio;
using D47.Core.Configuration;
using D47.Core.Persona;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.Core.Tests.Audio;

public class ACovasVoiceHasALightReverbTests
{
    private const int Rate = 48_000;
    private const int BurstSamples = Rate / 2;
    private const double Amplitude = 0.25;

    private static readonly GuardianVoiceSettings CylonTicked = new()
    {
        Effects = [.. GuardianVoice.Defaults.Select(effect => effect with { Ticked = effect.Id == "cylon" })],
    };

    private static AudioClip Burst()
    {
        var pcm = new byte[BurstSamples * 2];

        for (var index = 0; index < BurstSamples; index++)
        {
            var value = (short)Math.Round(Math.Sin(2 * Math.PI * 1_000 * index / Rate) * Amplitude * short.MaxValue);
            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip("burst", pcm, AudioFormat.Standard);
    }

    private static double[] Samples(AudioClip clip)
    {
        var span = clip.Pcm.Span;
        var samples = new double[span.Length / 2];

        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(span[index * 2] | (span[(index * 2) + 1] << 8)) / 32768.0;
        }

        return samples;
    }

    private static double Rms(double[] samples, int from, int count)
    {
        var sum = 0.0;

        for (var index = from; index < from + count && index < samples.Length; index++)
        {
            sum += samples[index] * samples[index];
        }

        return Math.Sqrt(sum / count);
    }

    private static double Db(double ratio) => 20 * Math.Log10(Math.Max(ratio, 1e-12));

    private static AudioClip Covas(SpeechSettings speech)
    {
        var colour = GuardianVoice.ColourFor(speech, PersonaCatalog.Covas);
        Assert.NotNull(colour);

        return colour(Burst());
    }

    [Fact]
    public void AfterTheBurstItFallsTwentyDecibelsWithin300MsAndSixtyWithin650Ms()
    {
        var input = Samples(Burst());
        var output = Samples(Covas(new SpeechSettings()));
        var level = Rms(input, 0, BurstSamples);
        var window = Rate / 100;

        Assert.InRange(Db(Rms(output, BurstSamples + (Rate * 300 / 1_000), window) / level), double.MinValue, -20);

        for (var from = BurstSamples + (Rate * 650 / 1_000); from < output.Length; from += window)
        {
            Assert.InRange(Db(Rms(output, from, window) / level), double.MinValue, -60);
        }
    }

    [Fact]
    public void WithTheBoxUntickedCovasIsUntreated() =>
        Assert.Null(GuardianVoice.ColourFor(new SpeechSettings { CovasReverb = false }, PersonaCatalog.Covas));

    [Fact]
    public void ATickedGuardianEffectDoesNotReachCovas() =>
        Assert.Equal(
            Covas(new SpeechSettings()).Pcm.ToArray(),
            Covas(new SpeechSettings { GuardianVoice = CylonTicked }).Pcm.ToArray());

    [Fact]
    public void AGuardianCoreIsTheSameWhetherTheBoxIsTickedOrNot()
    {
        Assert.Null(GuardianVoice.ColourFor(new SpeechSettings { CovasReverb = true }, PersonaCatalog.Warden));
        Assert.Null(GuardianVoice.ColourFor(new SpeechSettings { CovasReverb = false }, PersonaCatalog.Warden));

        var ticked = GuardianVoice.ColourFor(
            new SpeechSettings { CovasReverb = true, GuardianVoice = CylonTicked }, PersonaCatalog.Warden);
        var unticked = GuardianVoice.ColourFor(
            new SpeechSettings { CovasReverb = false, GuardianVoice = CylonTicked }, PersonaCatalog.Warden);

        Assert.NotNull(ticked);
        Assert.NotNull(unticked);
        Assert.Equal(ticked(Burst()).Pcm.ToArray(), unticked(Burst()).Pcm.ToArray());
    }

    [Fact]
    public void ASettingsFileWithoutTheKeyLoadsWithTheBoxTicked()
    {
        using var install = new TempInstall();
        File.WriteAllText(install.Paths.SettingsFile, """{ "schemaVersion": 1, "speech": { "cuesEnabled": false } }""");

        var store = new SettingsStore(install.Paths, NullLogger<SettingsStore>.Instance);

        Assert.True(store.Load().Speech.CovasReverb);
    }
}
