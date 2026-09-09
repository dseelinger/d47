using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>Voices from outside the ship, arriving over a link rather than from the next seat.</summary>
public class RadioVoiceTests
{
    /// <summary>A tone at one frequency, at half scale.</summary>
    private static AudioClip Tone(double hertz, double seconds = 0.5, double amplitude = 0.5)
    {
        var format = AudioFormat.Standard;
        var samples = (int)(format.SampleRate * seconds);
        var pcm = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * hertz * index / format.SampleRate)
                                * amplitude * short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip($"{hertz:0} Hz", pcm, format);
    }

    /// <summary>Several tones at once, each at the same strength.</summary>
    private static AudioClip Chord(double amplitude, params int[] hertz)
    {
        var format = AudioFormat.Standard;
        var samples = format.SampleRate / 2;
        var pcm = new byte[samples * 2];

        for (var index = 0; index < samples; index++)
        {
            var sum = hertz.Sum(f => Math.Sin(2 * Math.PI * f * index / format.SampleRate));
            var value = (short)Math.Clamp(sum * amplitude * short.MaxValue, short.MinValue, short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return new AudioClip("chord", pcm, format);
    }

    /// <summary>
    /// How much of one frequency is in a clip, relative to how much 1 kHz is in it — the middle of a
    /// voice being the thing everything else is judged against here.
    /// </summary>
    private static double Share(AudioClip clip, int hertz) => Magnitude(clip, hertz) / Magnitude(clip, 1_000);

    /// <summary>Goertzel.</summary>
    private static double Magnitude(AudioClip clip, int hertz)
    {
        var pcm = clip.Pcm.Span;
        var samples = pcm.Length / 2;
        var coefficient = 2 * Math.Cos(2 * Math.PI * hertz / clip.Format.SampleRate);

        var previous = 0.0;
        var older = 0.0;

        for (var index = 0; index < samples; index++)
        {
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
            var current = sample + (coefficient * previous) - older;

            older = previous;
            previous = current;
        }

        return Math.Sqrt(Math.Max(
            0, (previous * previous) + (older * older) - (coefficient * previous * older)));
    }

    /// <summary>How long the carrier is held open after the last word, in samples at 48 kHz.</summary>
    private const int TailSamples = 9_600;

    /// <summary>
    /// The bare carrier at the end of a treated line, measured after the floor has finished swelling
    /// and before the 3 ms cut — so neither the last syllable nor either ramp is in it.
    /// </summary>
    private static AudioClip Tail(AudioClip treated)
    {
        var total = treated.Pcm.Length / 2;
        var from = total - TailSamples + 2_400;

        return treated with { Pcm = treated.Pcm.Slice(from * 2, (total - 480 - from) * 2) };
    }

    /// <summary>The floor under the words, taken from the middle of the voice's own stretch.</summary>
    private static AudioClip UnderTheVoice(AudioClip treated)
    {
        var voice = (treated.Pcm.Length / 2) - TailSamples;

        return treated with { Pcm = treated.Pcm.Slice(voice / 4 * 2, voice / 2 * 2) };
    }

    private static double Rms(AudioClip clip)
    {
        var pcm = clip.Pcm.Span;
        var samples = pcm.Length / 2;

        if (samples == 0)
        {
            return 0;
        }

        var squared = 0.0;

        for (var index = 0; index < samples; index++)
        {
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
            squared += sample * sample;
        }

        return Math.Sqrt(squared / samples);
    }

    [Fact]
    public void OnlyTheShipAndItsCrewSoundLikeTheyAreInTheRoom()
    {
        // The requirement in one assertion.
        Assert.False(RadioVoice.IsOverTheAir(VoiceRole.ShipAi));
        Assert.False(RadioVoice.IsOverTheAir(VoiceRole.Crew));

        Assert.True(RadioVoice.IsOverTheAir(VoiceRole.Comms));
        Assert.True(RadioVoice.IsOverTheAir(VoiceRole.CarrierCaptain));
        Assert.True(RadioVoice.IsOverTheAir(VoiceRole.TowerControl));

        // And the two that stay in the room get no treatment at all, rather than a treatment that happens to
        // be inaudible.
        Assert.Null(RadioVoice.Colours(VoiceRole.ShipAi));
        Assert.NotNull(RadioVoice.Colours(VoiceRole.Comms));
    }

    [Fact]
    public void TheChestOfTheVoiceIsGoneAndTheMiddleOfItIsNot()
    {
        // The band-pass is the whole illusion: what a listener reads as "down a wire" is the missing bottom.
        var chord = Chord(0.3, 100, 1_000, 9_000);
        var treated = RadioVoice.Apply(chord);

        // The three tones go in at the same strength, so each share is 1.0 to begin with and whatever it is
        // afterwards is what the filters did.
        Assert.Equal(1.0, Share(chord, 100), precision: 1);
        Assert.Equal(1.0, Share(chord, 9_000), precision: 1);

        Assert.True(Share(treated, 100) < 0.2, $"100 Hz came through at {Share(treated, 100):P0}");
        Assert.True(Share(treated, 9_000) < 0.2, $"9 kHz came through at {Share(treated, 9_000):P0}");
    }

    [Fact]
    public void EveryTransmissionArrivesAtTheSameLoudnessWhicheverVoiceSentIt()
    {
        // A 26 dB spread going in: quieter than any real voice, and up at full scale.
        var levels = new[] { 0.05, 0.1, 0.2, 0.4, 0.7, 1.0 }
            .Select(amplitude => Rms(UnderTheVoice(RadioVoice.Apply(Tone(1_000, 1.0, amplitude)))))
            .ToArray();

        var spread = 20 * Math.Log10(levels.Max() / levels.Min());

        Assert.True(spread < 1.0, $"{spread:F1} dB of it survived, and the point was that none should");
    }

    /// <summary>
    /// And the level it lands on is the one a voice already had, so this is a levelling and not a
    /// volume change.
    /// </summary>
    [Fact]
    public void ATypicalLineIsNoLouderOrQuieterThanOneInTheRoom()
    {
        // 0.1414 peak is an RMS of 0.10, which is the -20 dBFS a real treated line was measured at.
        foreach (var hertz in new[] { 700, 1_200, 2_000 })
        {
            var before = Rms(Tone(hertz, 1.0, 0.1414));
            var after = Rms(UnderTheVoice(RadioVoice.Apply(Tone(hertz, 1.0, 0.1414))));

            Assert.True(
                after > before * 0.7 && after < before * 1.3,
                $"{hertz} Hz came out at {after / before:P0} of the level it went in at");
        }
    }

    [Fact]
    public void TheLoudestPossibleLineDoesNotDistort()
    {
        // Saturation is the point, clipping is not.
        var loud = RadioVoice.Apply(Tone(1_000, amplitude: 1.0));
        var pcm = loud.Pcm.Span;

        var atTheRail = 0;

        for (var index = 0; index < pcm.Length / 2; index++)
        {
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8));

            if (Math.Abs((int)sample) >= short.MaxValue - 1)
            {
                atTheRail++;
            }
        }

        // A handful of samples at the rail is a sine touching its own peak.
        Assert.True(atTheRail < pcm.Length / 2 / 100, $"{atTheRail} samples were pinned at full scale");
    }

    [Fact]
    public void TheSameClipTreatedTwiceIsTheSameBytes()
    {
        // Including the hiss, which is why it comes from a counter rather than from Random.
        var first = RadioVoice.Apply(Tone(800));
        var second = RadioVoice.Apply(Tone(800));

        Assert.Equal(first.Pcm.ToArray(), second.Pcm.ToArray());
    }

    /// <summary>The static on the open carrier is audible and is still the background.</summary>
    [Fact]
    public void TheStaticIsAudibleAndIsStillTheBackground()
    {
        var floor = Rms(Tail(RadioVoice.Apply(Tone(1_000, seconds: 1.0, amplitude: 0.4))));
        var dbfs = 20 * Math.Log10(floor);

        Assert.True(dbfs > -40, $"the static is at {dbfs:F0} dBFS, which is not heard as a link");
        Assert.True(dbfs < -28, $"the static is at {dbfs:F0} dBFS, which is heard instead of the voice");
    }

    /// <summary>And it comes up when the words stop.</summary>
    [Fact]
    public void TheStaticComesUpWhenTheVoiceStops()
    {
        // A silent line, so both stretches are floor and nothing else and the two are directly comparable.
        var silence = new AudioClip("silence", new byte[48_000 * 2], AudioFormat.Standard);
        var treated = RadioVoice.Apply(silence);

        var swelled = 20 * Math.Log10(Rms(Tail(treated)) / Rms(UnderTheVoice(treated)));

        Assert.True(swelled > 6, $"the carrier only came up {swelled:F0} dB, which is not heard as a swell");
        Assert.True(swelled < 18, $"the carrier came up {swelled:F0} dB, which is heard as a burst of noise");
    }

    /// <summary>And the level of it does not depend on how loud the line was.</summary>
    [Fact]
    public void TheStaticIsTheSameLevelWhateverTheLineWasDoing()
    {
        var loud = Rms(Tail(RadioVoice.Apply(Tone(1_000, amplitude: 0.9))));
        var quiet = Rms(Tail(RadioVoice.Apply(Tone(1_000, amplitude: 0.05))));

        Assert.Equal(loud, quiet, tolerance: loud * 0.05);
    }

    /// <summary>The carrier stays open for a fifth of a second after the last word and then drops.</summary>
    [Fact]
    public void TheLinkStaysOpenAfterTheLastWordAndThenCuts()
    {
        var line = Tone(1_000, seconds: 0.5);
        var treated = RadioVoice.Apply(line);

        // A fifth of a second longer than what went in, at 48 kHz.
        Assert.Equal(line.Pcm.Length + (TailSamples * 2), treated.Pcm.Length);

        // Carrying static, not silence.
        Assert.True(Rms(Tail(treated)) > 0.001, "the carrier is not open");

        // And it ends at zero rather than mid-sample.
        var pcm = treated.Pcm.Span;
        var last = (short)(pcm[^2] | (pcm[^1] << 8));

        Assert.True(Math.Abs((int)last) < 64, $"the link cut off at {last}, which clicks");
    }

    [Fact]
    public void AClipWithNothingInItIsHandedBackUntouched()
    {
        // The provider can return an empty buffer, and the arithmetic below divides by the sample count.
        var empty = new AudioClip("nothing", ReadOnlyMemory<byte>.Empty, AudioFormat.Standard);

        Assert.Same(empty.Name, RadioVoice.Apply(empty).Name);
        Assert.Equal(0, RadioVoice.Apply(empty).Pcm.Length);
    }

    [Fact]
    public void AStereoClipIsNotTwoSignalsThroughOneFilter()
    {
        // Every clip in the app is mono today.
        var format = new AudioFormat(48_000, 2);
        var samples = 24_000;
        var pcm = new byte[samples * 2 * 2];

        for (var frame = 0; frame < samples; frame++)
        {
            // A tone in the passband on the left, silence on the right.
            var value = (short)(Math.Sin(2 * Math.PI * 1_000 * frame / 48_000.0) * 0.5 * short.MaxValue);

            pcm[frame * 4] = (byte)(value & 0xFF);
            pcm[(frame * 4) + 1] = (byte)((value >> 8) & 0xFF);
        }

        var treated = RadioVoice.Apply(new AudioClip("stereo", pcm, format));
        var span = treated.Pcm.Span;

        var right = 0.0;

        for (var frame = 0; frame < samples; frame++)
        {
            right = Math.Max(right, Math.Abs((short)(span[(frame * 4) + 2] | (span[(frame * 4) + 3] << 8)) / 32768.0));
        }

        // The silent channel comes back as the noise floor and nothing else.
        Assert.True(right < 0.05, $"the silent channel came back at {right:F3}");
    }
}
