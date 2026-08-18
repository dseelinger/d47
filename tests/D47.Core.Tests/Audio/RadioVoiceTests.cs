using D47.Core.Audio;
using Xunit;

namespace D47.Core.Tests.Audio;

/// <summary>
/// Voices from outside the ship, arriving over a link rather than from the next seat
/// (docs/plans/change-requests.md item 13).
/// </summary>
public class RadioVoiceTests
{
    /// <summary>
    /// A tone at one frequency, at half scale. Enough to say what the filters do without needing
    /// speech: a band-pass either passes a sine or it does not.
    /// </summary>
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

    /// <summary>
    /// Several tones at once, each at the same strength. The whole point of measuring a chord
    /// rather than one tone at a time is that a chord carries the filter's shape in one buffer,
    /// where a single tone only carries its level — and the level is deliberately put back.
    /// </summary>
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
    /// How much of one frequency is in a clip, relative to how much 1 kHz is in it — the middle
    /// of a voice being the thing everything else is judged against here.
    /// </summary>
    private static double Share(AudioClip clip, int hertz) => Magnitude(clip, hertz) / Magnitude(clip, 1_000);

    /// <summary>
    /// Goertzel. One bin of a DFT for the cost of a loop, which is all this needs — and no
    /// package, so nothing new has to clear the licence gate to measure a filter.
    /// </summary>
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
    /// The bare carrier at the end of a treated line, measured after the floor has finished
    /// swelling and before the 3 ms cut — so neither the last syllable nor either ramp is in it.
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
        // The requirement in one assertion. Everything else in this file is about how good the
        // link sounds; this is about who gets one, and it is the part that would be wrong in a
        // way the Commander notices immediately.
        Assert.False(RadioVoice.IsOverTheAir(VoiceRole.ShipAi));
        Assert.False(RadioVoice.IsOverTheAir(VoiceRole.Crew));

        Assert.True(RadioVoice.IsOverTheAir(VoiceRole.Comms));
        Assert.True(RadioVoice.IsOverTheAir(VoiceRole.CarrierCaptain));
        Assert.True(RadioVoice.IsOverTheAir(VoiceRole.TowerControl));

        // And the two that stay in the room get no treatment at all, rather than a treatment
        // that happens to be inaudible.
        Assert.Null(RadioVoice.Colours(VoiceRole.ShipAi));
        Assert.NotNull(RadioVoice.Colours(VoiceRole.Comms));
    }

    [Fact]
    public void TheChestOfTheVoiceIsGoneAndTheMiddleOfItIsNot()
    {
        // The band-pass is the whole illusion: what a listener reads as "down a wire" is the
        // missing bottom. A test that only asserted "the bytes changed" would pass for a filter
        // that removed the wrong end.
        //
        // Three tones at once, and each measured against the one in the middle, because the
        // treatment deliberately puts the overall level back where it found it — so one tone at a
        // time measures the loudness match rather than the filter, and a band-pass that did
        // nothing at all would pass that. What is asserted here is the shape.
        var chord = Chord(0.3, 100, 1_000, 9_000);
        var treated = RadioVoice.Apply(chord);

        // The three tones go in at the same strength, so each share is 1.0 to begin with and
        // whatever it is afterwards is what the filters did.
        Assert.Equal(1.0, Share(chord, 100), precision: 1);
        Assert.Equal(1.0, Share(chord, 9_000), precision: 1);

        Assert.True(Share(treated, 100) < 0.2, $"100 Hz came through at {Share(treated, 100):P0}");
        Assert.True(Share(treated, 9_000) < 0.2, $"9 kHz came through at {Share(treated, 9_000):P0}");
    }

    /// <summary>
    /// Every transmission arrives at the same loudness, whichever voice sent it.
    /// <para>
    /// <b>This replaced an assertion that said the opposite</b>, and the reason is worth keeping.
    /// The old rule was that a treated line came back at the loudness it arrived at, so that an
    /// effect could not read as a volume change. That is a good rule for one line and the wrong
    /// one across many: it faithfully preserves whatever level spread the speech provider
    /// produced, so a voice that is simply hotter than its neighbours arrives hotter — reported
    /// as "certain voices just sound more loud/present". Matching each line to itself cannot fix a
    /// difference that is between lines, so the treatment now brings all of them to one level, the
    /// way a receiver's AGC does.
    /// </para>
    /// <para>
    /// Measured on the voice rather than on the whole clip, because the clip has a fifth of a
    /// second of carrier on the end of it and that dilutes a whole-clip RMS by a factor that
    /// depends on how long the line was.
    /// </para>
    /// </summary>
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
    /// <para>
    /// There is one speech volume and it is the Commander's; a radio line that arrived louder or
    /// quieter than the ship's AI would read as the mixer being wrong, and the Commander's fix for
    /// that is to turn re-voiced messages off rather than to work out which of the two moved. The
    /// target is the level real Edge Neural output was measured at, which is what keeps the
    /// average voice exactly where it was and collapses only the spread around it.
    /// </para>
    /// <para>
    /// A band of tones rather than one, because the level has to hold across a voice's range and
    /// not just at the frequency that happened to get chosen.
    /// </para>
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
        // Saturation is the point, clipping is not. A full-scale input is the case where the
        // makeup gain and the drive would compound, and the sound of that is a torn syllable
        // rather than a radio.
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

        // A handful of samples at the rail is a sine touching its own peak. Thousands is a
        // waveform with its top cut off.
        Assert.True(atTheRail < pcm.Length / 2 / 100, $"{atTheRail} samples were pinned at full scale");
    }

    [Fact]
    public void TheSameClipTreatedTwiceIsTheSameBytes()
    {
        // Including the hiss, which is why it comes from a counter rather than from Random. A
        // replay that produced different audio each run could not be compared against anything,
        // and no test in this file could assert a number.
        var first = RadioVoice.Apply(Tone(800));
        var second = RadioVoice.Apply(Tone(800));

        Assert.Equal(first.Pcm.ToArray(), second.Pcm.ToArray());
    }

    /// <summary>
    /// The static on the open carrier is audible and is still the background.
    /// <para>
    /// The bounds are an absolute level rather than a ratio, because the level is fixed and the
    /// voice's is not — a treated line comes back at whatever loudness it arrived at, so a ratio
    /// measured against a test tone says more about the tone than about the static.
    /// </para>
    /// <para>
    /// <b>Set by listening, over two passes.</b> Against real Edge Neural output the tail lands
    /// about 13 dB under the spoken line, which was called right. The pass before it was 6 dB
    /// quieter and was reported as the clip simply ending — so the lower bound here is not a round
    /// number, it is the level that failed.
    /// </para>
    /// </summary>
    [Fact]
    public void TheStaticIsAudibleAndIsStillTheBackground()
    {
        var floor = Rms(Tail(RadioVoice.Apply(Tone(1_000, seconds: 1.0, amplitude: 0.4))));
        var dbfs = 20 * Math.Log10(floor);

        Assert.True(dbfs > -40, $"the static is at {dbfs:F0} dBFS, which is not heard as a link");
        Assert.True(dbfs < -28, $"the static is at {dbfs:F0} dBFS, which is heard instead of the voice");
    }

    /// <summary>
    /// And it comes up when the words stop.
    /// <para>
    /// Two levels rather than one, which is both what a real link does — nothing is masking the
    /// floor any more and the far end's gain rises to meet it — and what the two halves of the
    /// request asked for: quieter under the voice, louder in the gap after it.
    /// </para>
    /// </summary>
    [Fact]
    public void TheStaticComesUpWhenTheVoiceStops()
    {
        // A silent line, so both stretches are floor and nothing else and the two are directly
        // comparable. Under a real voice the same two levels are there and one of them is masked.
        var silence = new AudioClip("silence", new byte[48_000 * 2], AudioFormat.Standard);
        var treated = RadioVoice.Apply(silence);

        var swelled = 20 * Math.Log10(Rms(Tail(treated)) / Rms(UnderTheVoice(treated)));

        Assert.True(swelled > 6, $"the carrier only came up {swelled:F0} dB, which is not heard as a swell");
        Assert.True(swelled < 18, $"the carrier came up {swelled:F0} dB, which is heard as a burst of noise");
    }

    /// <summary>
    /// And the level of it does not depend on how loud the line was.
    /// <para>
    /// A reply is split into one clip per sentence, so a floor that tracked the makeup gain would
    /// step up and down between sentences of the same transmission. That is the artefact that
    /// makes an effect read as broken rather than as atmosphere, and it is why the static is added
    /// after the gain rather than before the filters.
    /// </para>
    /// </summary>
    [Fact]
    public void TheStaticIsTheSameLevelWhateverTheLineWasDoing()
    {
        var loud = Rms(Tail(RadioVoice.Apply(Tone(1_000, amplitude: 0.9))));
        var quiet = Rms(Tail(RadioVoice.Apply(Tone(1_000, amplitude: 0.05))));

        Assert.Equal(loud, quiet, tolerance: loud * 0.05);
    }

    /// <summary>
    /// The carrier stays open for a fifth of a second after the last word and then drops. This is
    /// the part that makes it a transmission rather than a filtered voice — a link does not stop
    /// dead on the final consonant.
    /// </summary>
    [Fact]
    public void TheLinkStaysOpenAfterTheLastWordAndThenCuts()
    {
        var line = Tone(1_000, seconds: 0.5);
        var treated = RadioVoice.Apply(line);

        // A fifth of a second longer than what went in, at 48 kHz.
        Assert.Equal(line.Pcm.Length + (TailSamples * 2), treated.Pcm.Length);

        // Carrying static, not silence.
        Assert.True(Rms(Tail(treated)) > 0.001, "the carrier is not open");

        // And it ends at zero rather than mid-sample. A noise floor cut off at full swing is an
        // audible click, which reads as a defect in the audio path.
        var pcm = treated.Pcm.Span;
        var last = (short)(pcm[^2] | (pcm[^1] << 8));

        Assert.True(Math.Abs((int)last) < 64, $"the link cut off at {last}, which clicks");
    }

    [Fact]
    public void AClipWithNothingInItIsHandedBackUntouched()
    {
        // The provider can return an empty buffer, and the arithmetic below divides by the
        // sample count.
        var empty = new AudioClip("nothing", ReadOnlyMemory<byte>.Empty, AudioFormat.Standard);

        Assert.Same(empty.Name, RadioVoice.Apply(empty).Name);
        Assert.Equal(0, RadioVoice.Apply(empty).Pcm.Length);
    }

    [Fact]
    public void AStereoClipIsNotTwoSignalsThroughOneFilter()
    {
        // Every clip in the app is mono today. One filter state fed an interleaved buffer does
        // not filter it, it recurses two signals into each other, and the result is loud rather
        // than subtly wrong — so the case is asserted before something starts producing stereo.
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
