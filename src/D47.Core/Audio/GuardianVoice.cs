using D47.Core.Configuration;

namespace D47.Core.Audio;

/// <summary>One effect a Guardian core's voice can be given, and what its level sets.</summary>
public sealed record GuardianEffect
{
    /// <summary>The effect's key in settings: <c>speech.guardianVoice.&lt;id&gt;</c>.</summary>
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required string Help { get; init; }

    /// <summary>What the level sets, as the Commander reads it: "Depth", "Pitch", "Mix".</summary>
    public required string Parameter { get; init; }

    /// <summary>
    /// What <see cref="Value"/> is counted in: "%" for a share from 0 to 1, "st" for semitones, "×" for a
    /// multiplier.
    /// </summary>
    public required string Unit { get; init; }

    public required int DefaultLevel { get; init; }

    /// <summary>The parameter's value at a level from 1 to 20.</summary>
    public required Func<int, double> Value { get; init; }

    /// <summary>Starts the effect on a new clip from the parameter's value, the core's base pitch and the sample rate.</summary>
    internal Func<double, double, int, IGuardianStage> Start { get; init; } = null!;

    /// <summary>The id of an effect that, when ticked, skips this one.</summary>
    public string? SkippedBy { get; init; }
}

/// <summary>
/// Speech made to sound like a Guardian core. The ticked effects run in the order the Commander set, each at its
/// level, and the result is brought back to the dry clip's loudness.
/// </summary>
public static partial class GuardianVoice
{
    public const int LowestLevel = 1;

    public const int HighestLevel = 20;

    /// <summary>Every effect, in the default order.</summary>
    public static readonly IReadOnlyList<GuardianEffect> Table =
    [
        new GuardianEffect
        {
            Id = "stutter",
            Label = "Stutter",
            Help = "Word starts repeated two or three times, with phrases occasionally pitch-shifted; chance is how often a word start repeats.",
            Parameter = "Chance",
            Unit = "%",
            DefaultLevel = 7,
            Value = level => level / 20.0,
            Start = (chance, _, rate) => WholeClip(signal => Stutter(signal, chance, rate)),
        },
        new GuardianEffect
        {
            Id = "monotone",
            Label = "Monotone",
            Help = "Every voiced stretch moved to the core's base pitch; amount is how far of the way it moves.",
            Parameter = "Amount",
            Unit = "%",
            DefaultLevel = 20,
            Value = level => level / 20.0,
            Start = (amount, basePitchHz, rate) => WholeClip(signal => Monotone(signal, amount, basePitchHz, rate)),
        },
        new GuardianEffect
        {
            Id = "steppedPitch",
            Label = "Stepped pitch",
            Help = "Every voiced stretch moved to the nearest semitone, so the pitch jumps rather than glides; hold is the shortest step.",
            Parameter = "Hold",
            Unit = "ms",
            DefaultLevel = 12,
            Value = level => level * 10.0,
            Start = (holdMs, _, rate) => WholeClip(signal => SteppedPitch(signal, holdMs, rate)),
            SkippedBy = "monotone",
        },
        new GuardianEffect
        {
            Id = "cylon",
            Label = "Cylon",
            Help = "Channel vocoder onto a fixed-pitch carrier; depth is how much of the vocoded voice replaces the dry one.",
            Parameter = "Depth",
            Unit = "%",
            DefaultLevel = 20,
            Value = level => level / 20.0,
            Start = (depth, basePitchHz, rate) =>
                new DryWetStage(new CylonStage(basePitchHz * CarrierShare, rate), 1 - depth, depth),
        },
        new GuardianEffect
        {
            Id = "whisper",
            Label = "Whisper",
            Help = "Each frame's spectrum kept and its phase replaced with random values, so the voice is breath; mix is its share of the output.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 20,
            Value = level => level / 20.0,
            Start = (wet, _, rate) => WholeClip(signal => Run(new DryWetStage(new WhisperStage(rate), 1 - wet, wet), signal)),
        },
        new GuardianEffect
        {
            Id = "pitchDown",
            Label = "Pitch down",
            Help = "The voice lowered, duration kept; pitch is how many semitones.",
            Parameter = "Pitch",
            Unit = "st",
            DefaultLevel = 8,
            Value = level => -level / 2.0,
            Start = (semitones, _, rate) => new ShiftStage(Math.Pow(2, semitones / 12), rate),
        },
        new GuardianEffect
        {
            Id = "octaveDown",
            Label = "Octave-down layer",
            Help = "The line an octave lower, mixed under the dry voice; mix is how loud the layer is.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 12,
            Value = level => level / 20.0,
            Start = (mix, _, rate) => new DryWetStage(new ShiftStage(0.5, rate), 1, mix),
        },
        new GuardianEffect
        {
            Id = "chorus",
            Label = "Chorus",
            Help = "Three swept delayed copies mixed under the dry voice; depth is how loud the copies are.",
            Parameter = "Depth",
            Unit = "%",
            DefaultLevel = 16,
            Value = level => level / 20.0,
            Start = (depth, _, rate) => new ChorusStage(depth, rate),
        },
        new GuardianEffect
        {
            Id = "hive",
            Label = "Hive",
            Help = "Three pitch-shifted, delayed copies mixed under the dry voice; mix is how loud each copy is.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 12,
            Value = level => level / 20.0,
            Start = (gain, _, rate) => new HiveStage(gain, rate),
        },
        new GuardianEffect
        {
            Id = "flanger",
            Label = "Flanger",
            Help = "One delayed copy, its delay swept with feedback; rate is how fast the sweep runs.",
            Parameter = "Rate",
            Unit = "Hz",
            DefaultLevel = 5,
            Value = level => level / 20.0,
            Start = (hertz, _, rate) => new FlangerStage(hertz, rate),
        },
        new GuardianEffect
        {
            Id = "phaser",
            Label = "Phaser",
            Help = "Six allpass stages with their corner swept together; rate is how fast the sweep runs.",
            Parameter = "Rate",
            Unit = "Hz",
            DefaultLevel = 8,
            Value = level => level / 20.0,
            Start = (hertz, _, rate) => new PhaserStage(hertz, rate),
        },
        new GuardianEffect
        {
            Id = "wah",
            Label = "Wah",
            Help = "A band-pass whose centre follows the speech's loudness; mix is its share of the output.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 14,
            Value = level => level / 20.0,
            Start = (wet, _, rate) => new WahStage(wet, rate),
        },
        new GuardianEffect
        {
            Id = "comb",
            Label = "Metallic resonance",
            Help = "A 9 ms feedback comb filter; amount is its share of the output.",
            Parameter = "Amount",
            Unit = "%",
            DefaultLevel = 12,
            Value = level => level / 20.0,
            Start = (wet, _, rate) => new CombStage(wet, rate),
        },
        new GuardianEffect
        {
            Id = "ringMod",
            Label = "Ring modulation",
            Help = "A 45 Hz ring modulator blended with the dry voice; mix is its share of the output.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 7,
            Value = level => level / 20.0,
            Start = (wet, _, rate) => new RingModStage(wet, RingHz, rate),
        },
        new GuardianEffect
        {
            Id = "deepRingMod",
            Label = "Deep ring mod",
            Help = "A ring modulator with no dry signal, well below speech pitch; freq sets the carrier.",
            Parameter = "Freq",
            Unit = "Hz",
            DefaultLevel = 6,
            Value = level => 5.0 * level,
            Start = (hertz, _, rate) => new RingModStage(1, hertz, rate),
        },
        new GuardianEffect
        {
            Id = "tremolo",
            Label = "Tremolo",
            Help = "Amplitude modulation; rate is how fast the volume pulses.",
            Parameter = "Rate",
            Unit = "Hz",
            DefaultLevel = 12,
            Value = level => level / 2.0,
            Start = (hertz, _, rate) => new TremoloStage(hertz, rate),
        },
        new GuardianEffect
        {
            Id = "overdrive",
            Label = "Overdrive",
            Help = "Soft clipping through tanh; drive is the gain going into it.",
            Parameter = "Drive",
            Unit = "×",
            DefaultLevel = 8,
            Value = level => level / 2.0,
            Start = (drive, _, _) => new OverdriveStage(drive),
        },
        new GuardianEffect
        {
            Id = "bitcrusher",
            Label = "Bitcrusher",
            Help = "Sample-and-hold to 8 kHz, then bit reduction; bits is how many survive.",
            Parameter = "Bits",
            Unit = "bit",
            DefaultLevel = 16,
            Value = level => Math.Round(16 + ((2.0 - 16.0) * (level - 1) / 19.0)),
            Start = (bits, _, rate) => WholeClip(signal => Bitcrusher(signal, bits, rate)),
        },
        new GuardianEffect
        {
            Id = "glitch",
            Label = "Glitch",
            Help = "Short damaged stretches at irregular intervals; rate is how often they come.",
            Parameter = "Rate",
            Unit = "×",
            DefaultLevel = 10,
            Value = level => level / 10.0,
            Start = (often, _, rate) => new GlitchStage(often, rate),
        },
        new GuardianEffect
        {
            Id = "helmet",
            Label = "Helmet",
            Help = "The line put through a comms link at full strength, with a click at the start and end; mix is the treated share over dry.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 20,
            Value = level => level / 20.0,
            Start = (mix, _, rate) => new HelmetStage(mix, rate),
        },
        new GuardianEffect
        {
            Id = "hologram",
            Label = "Hologram",
            Help = "The line put through a comms link; signal is the link's strength, weaker links losing stretches of the voice under static.",
            Parameter = "Signal",
            Unit = "%",
            DefaultLevel = 6,
            Value = level => level / 20.0,
            Start = (strength, _, rate) => new RadioStage(strength, rate),
        },
        new GuardianEffect
        {
            Id = "reverseReverb",
            Label = "Reverse reverb",
            Help = "Reverb run backwards, so its tail swells into each word; mix is how loud the reverb is.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 9,
            Value = level => level / 20.0,
            Start = (wet, _, rate) => WholeClip(signal => ReverseReverb(signal, wet, rate)),
        },
        new GuardianEffect
        {
            Id = "shimmer",
            Label = "Shimmer",
            Help = "Reverb with an octave-up copy in its tail; shimmer is how loud the octave layer is.",
            Parameter = "Shimmer",
            Unit = "%",
            DefaultLevel = 10,
            Value = level => level / 20.0,
            Start = (layer, _, rate) => new ShimmerStage(layer, rate),
        },
        new GuardianEffect
        {
            Id = "reverb",
            Label = "Reverb",
            Help = "Schroeder reverb, adding a half-second tail; mix is how loud the reverb is.",
            Parameter = "Mix",
            Unit = "%",
            DefaultLevel = 9,
            Value = level => level / 20.0,
            Start = (wet, _, rate) => new ReverbStage(wet, rate),
        },
        new GuardianEffect
        {
            Id = "respirator",
            Label = "Respirator",
            Help = "A breath added after the sentence, synthesised rather than a bundled sound; breath is how long it runs.",
            Parameter = "Breath",
            Unit = "ms",
            DefaultLevel = 10,
            Value = level => level * 50.0,
            Start = (breathMs, _, rate) => new RespiratorStage(breathMs, rate),
        },
    ];

    private static readonly Dictionary<string, GuardianEffect> ById =
        Table.ToDictionary(effect => effect.Id, StringComparer.Ordinal);

    /// <summary>Every effect unticked, in the default order, at its default level.</summary>
    public static IReadOnlyList<GuardianVoiceEffect> Defaults { get; } =
        [.. Table.Select(effect => new GuardianVoiceEffect { Id = effect.Id, Level = effect.DefaultLevel })];

    /// <summary>The effect with this id, or null for an id this build does not know.</summary>
    public static GuardianEffect? Find(string id) => ById.GetValueOrDefault(id);

    /// <summary>The vocoder's carrier, as a share of the core's base pitch.</summary>
    private const double CarrierShare = 0.85;

    /// <summary>White noise in the carrier, relative to the sawtooth's amplitude.</summary>
    private const double CarrierNoise = 0.15;

    /// <summary>How far either side of a bin the speech envelope is averaged over.</summary>
    private const double EnvelopeHalfWidthHz = 280;

    /// <summary>(base ms, depth ms, rate Hz, phase) per copy.</summary>
    private static readonly (double Base, double Depth, double Rate, double Phase)[] ChorusCopies =
    [
        (12, 2.5, 0.31, 0),
        (19, 3.0, 0.47, 1.3),
        (27, 3.5, 0.23, 2.6),
    ];

    private const double CombDelayMs = 9;
    private const double CombFeedback = 0.7;

    private const double RingHz = 45;

    private const double FlangerMinMs = 1;
    private const double FlangerMaxMs = 5;
    private const double FlangerFeedback = 0.6;
    private const double FlangerDry = 0.7;
    private const double FlangerWet = 0.7;

    private const int PhaserStages = 6;
    private const double PhaserMinHz = 300;
    private const double PhaserMaxHz = 3_000;
    private const double PhaserFeedback = 0.5;
    private const double PhaserDry = 0.5;
    private const double PhaserWet = 0.5;

    private const double WahMinHz = 400;
    private const double WahMaxHz = 2_200;
    private const double WahQ = 4;
    private const double WahAttackSeconds = 0.010;
    private const double WahReleaseSeconds = 0.120;

    /// <summary>How often the band-pass centre is recomputed; more often than this buzzes rather than sweeps.</summary>
    private const double WahControlSeconds = 0.005;

    private const double TremoloDepth = 0.5;

    private const double OverdriveDry = 0.5;
    private const double OverdriveWet = 0.5;

    private const double BitcrusherSampleHz = 8_000;

    /// <summary>How long each of Helmet's two clicks runs.</summary>
    private const double HelmetClickMs = 12;

    /// <summary>The click's band-pass centre and width, chosen for a hard comms-key snap rather than speech.</summary>
    private const double HelmetClickHz = 1_800;

    private const double HelmetClickQ = 0.9;

    private const uint HelmetClickSeed = 0x85EBCA6Bu;

    /// <summary>Breath noise's band-pass centre and width, chosen for the hiss of air rather than a tone.</summary>
    private const double RespiratorHz = 700;

    private const double RespiratorQ = 0.5;

    private const uint RespiratorSeed = 0xC2B2AE35u;

    private const double RespiratorAmplitude = 0.6;

    private static readonly (double DelayMs, double Feedback)[] ReverbCombs =
    [
        (29.7, 0.88),
        (37.1, 0.87),
        (41.1, 0.87),
        (43.7, 0.86),
    ];

    private static readonly double[] ReverbAllpassMs = [5, 1.7];
    private const double ReverbAllpassGain = 0.7;
    private const double ReverbLowPassHz = 5_000;
    private const double ReverbDry = 0.75;
    private const double ReverbTailSeconds = 0.5;

    /// <summary>(semitones, delay ms) per copy.</summary>
    private static readonly (double Semitones, double DelayMs)[] HiveCopies =
    [
        (-5, 13),
        (-2, 23),
        (3, 37),
    ];

    private const uint WhisperSeed = 0x3C6EF372u;

    /// <summary>The reversed reverb's tail, added at the front of the clip; every line's first word waits this long.</summary>
    private const double ReverseTailSeconds = 0.3;

    /// <summary>The shimmer reverb's share of the output, fixed at Reverb's default mix.</summary>
    private const double ShimmerWet = 0.45;

    /// <summary>An onset window's RMS relative to the clip's peak, −30 dB, below which it counts as quiet.</summary>
    private const double StutterOnsetThreshold = 0.031_622_776_601_683_79;

    private const double StutterOnsetWindowMs = 10;
    private const double StutterOnsetMinQuietMs = 80;
    private const double StutterRepeatMinMs = 60;
    private const double StutterRepeatMaxMs = 120;
    private const double StutterShiftChance = 0.4;
    private const double StutterShiftMinSemitones = 2;
    private const double StutterShiftMaxSemitones = 5;
    private const double StutterCrossfadeMs = 10;
    private const uint StutterSeed = 0x1B873593u;

    private const double PitchFrameMs = 40;
    private const double PitchHopMs = 10;
    private const double PitchLowestHz = 60;
    private const double PitchHighestHz = 500;

    /// <summary>The rate the pitch is measured at: the clip is averaged down by a whole factor to about this.</summary>
    private const int PitchAnalysisHz = 12_000;

    /// <summary>The YIN threshold: a frame whose normalised difference never falls below it is unvoiced.</summary>
    private const double PitchVoicing = 0.2;

    /// <summary>A frame's RMS relative to the clip's peak, −40 dB, below which it is unvoiced.</summary>
    private const double PitchSilence = 0.01;

    private const double SemitoneReferenceHz = 440;

    /// <summary>The highest peak the levelled result may reach.</summary>
    internal const double Ceiling = 0.98;

    /// <summary>The phase-vocoder frame, as a duration; rounded to a power of two at the clip's rate.</summary>
    private const double FrameSeconds = 2048.0 / 48_000;

    /// <summary>
    /// The clip with the ticked effects applied in list order, each at its level; an id this build does not know
    /// is skipped. <paramref name="basePitchHz"/> is the core's fixed pitch, which the Cylon carrier and Monotone
    /// are derived from; it is not measured per clip.
    /// </summary>
    public static AudioClip Apply(AudioClip clip, IReadOnlyList<GuardianVoiceEffect> effects, double basePitchHz)
    {
        var channels = Math.Max(1, clip.Format.Channels);
        var frames = clip.Pcm.Length / (2 * channels);
        var chain = Ticked(effects);

        if (chain.Count == 0 || frames == 0)
        {
            return clip;
        }

        var filter = new GuardianFilter(clip.Format, chain, basePitchHz);
        var body = filter.Push(clip.Pcm.Span);
        var tail = filter.Finish();
        var pcm = new byte[body.Length + tail.Length];

        body.CopyTo(pcm, 0);
        tail.CopyTo(pcm, body.Length);

        return clip with
        {
            Name = $"{clip.Name} (guardian)",
            Pcm = pcm,
        };
    }

    /// <summary>
    /// Every effect in chain order: unknown and repeated ids dropped, a level outside 1 to 20 at its default, and
    /// a missing effect inserted unticked at its default level straight after the nearest effect that precedes
    /// it in the default order.
    /// </summary>
    public static IReadOnlyList<GuardianVoiceEffect> Effects(GuardianVoiceSettings settings)
    {
        if (settings.Effects is not { } stored)
        {
            return Defaults;
        }

        var effects = new List<GuardianVoiceEffect>(Table.Count);

        foreach (var effect in stored)
        {
            if (effect is null || Find(effect.Id) is not { } known || effects.Exists(e => e.Id == known.Id))
            {
                continue;
            }

            effects.Add(new GuardianVoiceEffect
            {
                Id = known.Id,
                Ticked = effect.Ticked,
                Level = effect.Level is >= LowestLevel and <= HighestLevel ? effect.Level : known.DefaultLevel,
            });
        }

        for (var index = 0; index < Table.Count; index++)
        {
            var missing = Table[index];

            if (effects.Exists(e => e.Id == missing.Id))
            {
                continue;
            }

            var at = 0;

            for (var before = index - 1; before >= 0; before--)
            {
                var found = effects.FindIndex(e => e.Id == Table[before].Id);

                if (found >= 0)
                {
                    at = found + 1;
                    break;
                }
            }

            effects.Insert(at, new GuardianVoiceEffect { Id = missing.Id, Level = missing.DefaultLevel });
        }

        return effects;
    }

    /// <summary>
    /// The Cylon carrier's base pitch for a core's <see cref="VoiceHint.Gender"/> — the median pitches
    /// measured for Edge Andrew and Ava (#224).
    /// </summary>
    public static double BasePitchHz(D47.Core.Persona.VoiceGender gender) => gender switch
    {
        D47.Core.Persona.VoiceGender.Male => 124,
        D47.Core.Persona.VoiceGender.Female => 209,
        _ => 165,
    };

    /// <summary>
    /// The treatment for this core: the COVAS reverb for a stock core when it is on, otherwise the ticked
    /// Guardian effects. Neither reaches the other kind of core.
    /// </summary>
    public static Func<AudioClip, AudioClip>? ColourFor(SpeechSettings speech, D47.Core.Persona.Persona core) =>
        core.Stock
            ? speech.CovasReverb ? CovasVoice.Apply : null
            : ColourFor(speech, core.VoiceHint.Gender);

    /// <summary><see cref="ColourFor(SpeechSettings, D47.Core.Persona.Persona)"/> as a running filter factory, null in the same cases.</summary>
    public static Func<IPcmFilter>? RunningColourFor(SpeechSettings speech, D47.Core.Persona.Persona core)
    {
        if (core.Stock)
        {
            return speech.CovasReverb ? CovasVoice.Filter : null;
        }

        var effects = Effects(speech.GuardianVoice);

        if (Ticked(effects).Count == 0)
        {
            return null;
        }

        var basePitch = BasePitchHz(core.VoiceHint.Gender);

        return () => Filter(effects, basePitch);
    }

    /// <summary>The ship AI's treatment for the settings in force, or null when no effect is ticked (#225).</summary>
    public static Func<AudioClip, AudioClip>? ColourFor(SpeechSettings speech, D47.Core.Persona.VoiceGender gender)
    {
        var effects = Effects(speech.GuardianVoice);

        if (Ticked(effects).Count == 0)
        {
            return null;
        }

        var basePitch = BasePitchHz(gender);

        return clip => Apply(clip, effects, basePitch);
    }

    /// <summary>
    /// The ticked effects this build knows, in list order, with their parameter values; an effect whose
    /// <see cref="GuardianEffect.SkippedBy"/> is ticked is left out.
    /// </summary>
    private static List<(GuardianEffect Effect, double Value)> Ticked(IReadOnlyList<GuardianVoiceEffect> effects)
    {
        var chain = new List<(GuardianEffect Effect, double Value)>();

        foreach (var effect in effects)
        {
            if (effect.Ticked && Find(effect.Id) is { } known
                && (known.SkippedBy is not { } skipper || !effects.Any(e => e.Ticked && e.Id == skipper)))
            {
                chain.Add((known, known.Value(effect.Level)));
            }
        }

        return chain;
    }

    /// <summary>The correction that removes the aliasing from a sawtooth's reset.</summary>
    private static double PolyBlep(double phase, double step)
    {
        if (phase < step)
        {
            var t = phase / step;
            return t + t - (t * t) - 1;
        }

        if (phase > 1 - step)
        {
            var t = (phase - 1) / step;
            return (t * t) + t + t + 1;
        }

        return 0;
    }

    /// <summary>
    /// A pitch shift by <paramref name="factor"/> that keeps the duration: a phase vocoder with identity phase
    /// locking compresses time by the factor, and resampling restores the length.
    /// </summary>
    private static double[] Shift(double[] signal, double factor, int rate) => Run(new ShiftStage(factor, rate), signal);

    /// <summary>The stage's whole output for the signal pushed as one chunk.</summary>
    private static double[] Run(IGuardianStage stage, double[] signal)
    {
        var output = new List<double>(signal.Length);
        stage.Push(signal, output);
        stage.Finish(output);
        return [.. output];
    }

    /// <summary>
    /// Advances the synthesis phase of each spectral peak by its measured frequency, and sets every other bin's
    /// phase relative to the nearest peak.
    /// </summary>
    private static void Lock(
        double[] magnitude,
        double[] phase,
        double[] previous,
        double[] synthesis,
        List<int> peaks,
        int frame,
        int analysisHop,
        int synthesisHop)
    {
        var bins = frame / 2;
        peaks.Clear();

        for (var bin = 1; bin < bins; bin++)
        {
            if (magnitude[bin] > magnitude[bin - 1] && magnitude[bin] >= magnitude[bin + 1])
            {
                peaks.Add(bin);
            }
        }

        if (peaks.Count == 0)
        {
            Array.Copy(phase, synthesis, bins + 1);
            return;
        }

        foreach (var peak in peaks)
        {
            var expected = 2 * Math.PI * peak / frame;
            var deviation = Wrap(phase[peak] - previous[peak] - (expected * analysisHop));
            synthesis[peak] = Wrap(synthesis[peak] + ((expected + (deviation / analysisHop)) * synthesisHop));
        }

        var nearest = 0;

        for (var bin = 0; bin <= bins; bin++)
        {
            while (nearest + 1 < peaks.Count && bin > (peaks[nearest] + peaks[nearest + 1]) / 2)
            {
                nearest++;
            }

            var peak = peaks[nearest];

            if (bin != peak)
            {
                synthesis[bin] = synthesis[peak] + phase[bin] - phase[peak];
            }
        }
    }

    /// <summary>
    /// A short band-passed noise burst: loudest at the first sample and fading out when
    /// <paramref name="rising"/> is false, or the reverse when it is true.
    /// </summary>
    private static double[] Click(int rate, bool rising)
    {
        var length = Samples(HelmetClickMs, rate);
        var noise = new Noise(HelmetClickSeed);
        var filter = new Biquad();
        filter.SetBandPass(HelmetClickHz, HelmetClickQ, rate);
        var output = new double[length];

        for (var index = 0; index < length; index++)
        {
            var filtered = filter.Next(noise.Next(-1, 1));
            var envelope = rising ? (index + 1.0) / length : 1.0 - ((double)index / length);
            output[index] = filtered * envelope;
        }

        return output;
    }

    /// <summary>Band-passed noise under a rise-and-fall envelope, so it reads as an inhale and exhale.</summary>
    private static double[] Breath(double milliseconds, int rate)
    {
        var length = Samples(milliseconds, rate);
        var noise = new Noise(RespiratorSeed);
        var filter = new Biquad();
        filter.SetBandPass(RespiratorHz, RespiratorQ, rate);
        var output = new double[length];

        for (var index = 0; index < length; index++)
        {
            var filtered = filter.Next(noise.Next(-1, 1));
            var t = length > 1 ? (double)index / (length - 1) : 0;
            var envelope = Math.Sin(Math.PI * t);
            output[index] = filtered * envelope * RespiratorAmplitude;
        }

        return output;
    }

    /// <summary>
    /// Sample-and-hold to <see cref="BitcrusherSampleHz"/>, then a mid-tread quantiser at <paramref name="bits"/>
    /// bits either side of zero, scaled to the clip's peak so a quiet clip keeps its levels and silence stays
    /// silent.
    /// </summary>
    private static double[] Bitcrusher(double[] signal, double bits, int rate)
    {
        var peak = signal.Length == 0 ? 0 : signal.Max(Math.Abs);
        var output = new double[signal.Length];

        if (peak == 0)
        {
            return output;
        }

        var half = (Math.Pow(2, Math.Round(bits) - 1) - 1) / peak;
        var step = Math.Max(1, (int)Math.Round(rate / BitcrusherSampleHz));
        var held = 0.0;

        for (var index = 0; index < signal.Length; index++)
        {
            if (index % step == 0)
            {
                held = signal[index];
            }

            output[index] = Math.Clamp(Math.Round(held * half) / half, -1, 1);
        }

        return output;
    }

    /// <summary>
    /// Repeats each word's start with a seeded chance, and shifts whole phrases with another. Onsets split the
    /// clip into phrases; the schedule comes from a fixed seed, so the same clip stutters the same way every
    /// time. Lengthens the clip by the repeats it adds.
    /// </summary>
    private static double[] Stutter(double[] signal, double chance, int rate)
    {
        var onsets = Onsets(signal, rate);

        if (onsets.Count == 0)
        {
            return (double[])signal.Clone();
        }

        var random = new Noise(StutterSeed ^ (uint)Math.Round(chance * 1_000_000));
        var crossfade = Samples(StutterCrossfadeMs, rate);
        var frame = FrameLength(rate);
        var segments = new List<double[]>();

        if (onsets[0] > 0)
        {
            segments.Add(signal[..onsets[0]]);
        }

        for (var index = 0; index < onsets.Count; index++)
        {
            var start = onsets[index];
            var end = index + 1 < onsets.Count ? onsets[index + 1] : signal.Length;
            var phrase = signal[start..end];

            if (phrase.Length >= frame && random.Next(0, 1) < StutterShiftChance)
            {
                var semitones = random.Next(StutterShiftMinSemitones, StutterShiftMaxSemitones);

                if (random.Next(0, 1) < 0.5)
                {
                    semitones = -semitones;
                }

                phrase = Shift(phrase, Math.Pow(2, semitones / 12), rate);
            }

            if (random.Next(0, 1) < chance)
            {
                phrase = Repeat(phrase, ref random, crossfade, rate);
            }

            segments.Add(phrase);
        }

        return Join(segments, crossfade);
    }

    /// <summary>The first 60–120 ms of <paramref name="phrase"/> played two or three times before it continues.</summary>
    private static double[] Repeat(double[] phrase, ref Noise random, int crossfade, int rate)
    {
        var burstMs = random.Next(StutterRepeatMinMs, StutterRepeatMaxMs);
        var burstLength = Math.Min(phrase.Length, (int)Math.Round(burstMs / 1000 * rate));

        if (burstLength == 0)
        {
            return phrase;
        }

        var repeats = random.Next(0, 1) < 0.5 ? 2 : 3;
        var burst = phrase[..burstLength];
        var pieces = new List<double[]>();

        for (var copy = 0; copy < repeats; copy++)
        {
            pieces.Add(burst);
        }

        pieces.Add(phrase[burstLength..]);

        return Join(pieces, crossfade);
    }

    /// <summary>
    /// Onset sample indices: a 10 ms window's RMS rising to within 30 dB of the clip's peak after at least
    /// 80 ms below it. The clip's first sound is an onset.
    /// </summary>
    private static List<int> Onsets(double[] signal, int rate)
    {
        var window = Samples(StutterOnsetWindowMs, rate);
        var peak = 0.0;

        foreach (var sample in signal)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        var threshold = peak * StutterOnsetThreshold;
        var onsets = new List<int>();
        var quietMs = double.PositiveInfinity;
        var above = false;

        for (var start = 0; start < signal.Length; start += window)
        {
            var length = Math.Min(window, signal.Length - start);
            var loud = Rms(signal.AsSpan(start, length)) >= threshold;

            if (loud)
            {
                if (!above && quietMs >= StutterOnsetMinQuietMs)
                {
                    onsets.Add(start);
                }

                above = true;
                quietMs = 0;
            }
            else
            {
                above = false;
                quietMs += 1_000.0 * length / rate;
            }
        }

        return onsets;
    }

    /// <summary>The RMS of a stretch of signal.</summary>
    private static double Rms(ReadOnlySpan<double> signal)
    {
        var squared = 0.0;

        foreach (var sample in signal)
        {
            squared += sample * sample;
        }

        return signal.Length == 0 ? 0 : Math.Sqrt(squared / signal.Length);
    }

    /// <summary>Concatenates the segments, each join a <paramref name="crossfade"/>-sample linear crossfade.</summary>
    private static double[] Join(List<double[]> segments, int crossfade)
    {
        if (segments.Count == 1)
        {
            return segments[0];
        }

        var total = segments.Sum(segment => segment.Length);

        for (var index = 1; index < segments.Count; index++)
        {
            total -= Math.Min(crossfade, Math.Min(segments[index - 1].Length, segments[index].Length));
        }

        var output = new double[total];
        Array.Copy(segments[0], output, segments[0].Length);
        var at = segments[0].Length;

        for (var index = 1; index < segments.Count; index++)
        {
            var next = segments[index];
            var fade = Math.Min(crossfade, Math.Min(at, next.Length));
            var overlapStart = at - fade;

            for (var sample = 0; sample < fade; sample++)
            {
                var t = (sample + 1.0) / (fade + 1);
                output[overlapStart + sample] = (output[overlapStart + sample] * (1 - t)) + (next[sample] * t);
            }

            var remain = next.Length - fade;
            Array.Copy(next, fade, output, at, remain);
            at += remain;
        }

        return output;
    }

    /// <summary>Each voiced frame's pitch moved <paramref name="amount"/> of the way to <paramref name="basePitchHz"/>, on a log scale.</summary>
    private static double[] Monotone(double[] signal, double amount, double basePitchHz, int rate)
    {
        var track = PitchTrack(signal, rate);
        var pitch = PerSample(track, signal.Length, rate);
        var target = new double[signal.Length];

        for (var index = 0; index < signal.Length; index++)
        {
            target[index] = pitch[index] > 0 ? pitch[index] * Math.Pow(basePitchHz / pitch[index], amount) : 0;
        }

        return Resynthesise(signal, track, pitch, target, rate);
    }

    /// <summary>
    /// Each voiced frame's pitch moved to the nearest equal-tempered semitone, a new semitone taken only once the
    /// current one has been held for <paramref name="holdMs"/>.
    /// </summary>
    private static double[] SteppedPitch(double[] signal, double holdMs, int rate)
    {
        var track = PitchTrack(signal, rate);
        var pitch = PerSample(track, signal.Length, rate);
        var notes = new double[track.Length];
        var holdFrames = (int)Math.Ceiling(holdMs / PitchHopMs);
        int? current = null;
        var held = 0;

        for (var frame = 0; frame < track.Length; frame++)
        {
            if (track[frame] > 0)
            {
                var nearest = (int)Math.Round(12 * Math.Log2(track[frame] / SemitoneReferenceHz));

                if (current is null || (nearest != current && held >= holdFrames))
                {
                    current = nearest;
                    held = 0;
                }

                notes[frame] = SemitoneReferenceHz * Math.Pow(2, current.Value / 12.0);
            }

            held++;
        }

        var hop = Samples(PitchHopMs, rate);
        var target = new double[signal.Length];

        for (var index = 0; index < signal.Length; index++)
        {
            if (pitch[index] <= 0)
            {
                continue;
            }

            var below = Math.Min(track.Length - 1, index / hop);
            var above = Math.Min(track.Length - 1, below + 1);
            var (near, far) = index - (below * hop) < hop / 2 ? (below, above) : (above, below);
            target[index] = notes[near] > 0 ? notes[near] : notes[far];
        }

        return Resynthesise(signal, track, pitch, target, rate);
    }

    /// <summary>
    /// The pitch of each 40 ms frame by YIN on the clip averaged down to about 12 kHz, frame k centred on k × 10 ms,
    /// or 0 where the frame is quiet or aperiodic.
    /// </summary>
    private static double[] PitchTrack(double[] signal, int rate)
    {
        var track = new double[(signal.Length / Samples(PitchHopMs, rate)) + 1];
        var factor = Math.Max(1, rate / PitchAnalysisHz);
        var analysisRate = (double)rate / factor;
        var decimated = new double[signal.Length / factor];

        for (var index = 0; index < decimated.Length; index++)
        {
            var total = 0.0;

            for (var offset = 0; offset < factor; offset++)
            {
                total += signal[(index * factor) + offset];
            }

            decimated[index] = total / factor;
        }

        signal = decimated;

        var frame = (int)Math.Round(PitchFrameMs / 1000 * analysisRate);
        var minLag = Math.Max(2, (int)(analysisRate / PitchHighestHz));
        var maxLag = (int)Math.Ceiling(analysisRate / PitchLowestHz);
        var width = Math.Max(1, frame - maxLag - 1);
        var normalised = new double[maxLag + 2];
        var peak = 0.0;

        foreach (var sample in signal)
        {
            peak = Math.Max(peak, Math.Abs(sample));
        }

        for (var k = 0; k < track.Length; k++)
        {
            var start = (int)Math.Round(k * PitchHopMs / 1000 * analysisRate) - (frame / 2);
            var squared = 0.0;

            for (var index = 0; index < frame; index++)
            {
                var sample = Sample(signal, start + index);
                squared += sample * sample;
            }

            if (peak == 0 || Math.Sqrt(squared / frame) < peak * PitchSilence)
            {
                continue;
            }

            var running = 0.0;
            normalised[0] = 1;

            for (var lag = 1; lag <= maxLag + 1; lag++)
            {
                var difference = 0.0;

                for (var index = 0; index < width; index++)
                {
                    var delta = Sample(signal, start + index) - Sample(signal, start + index + lag);
                    difference += delta * delta;
                }

                running += difference;
                normalised[lag] = running > 0 ? difference * lag / running : 1;
            }

            var found = 0;

            for (var lag = minLag; lag <= maxLag; lag++)
            {
                if (normalised[lag] < PitchVoicing)
                {
                    while (lag < maxLag && normalised[lag + 1] < normalised[lag])
                    {
                        lag++;
                    }

                    found = lag;
                    break;
                }
            }

            if (found == 0)
            {
                continue;
            }

            var before = normalised[found - 1];
            var after = normalised[found + 1];
            var curve = before - (2 * normalised[found]) + after;

            track[k] = analysisRate / (found + (curve > 0 ? (before - after) / (2 * curve) : 0));
        }

        return track;
    }

    /// <summary>
    /// The frame track as a pitch per sample: linear between two voiced frames, the voiced one's pitch beside an
    /// unvoiced frame, and 0 between two unvoiced ones.
    /// </summary>
    private static double[] PerSample(double[] track, int length, int rate)
    {
        var hop = Samples(PitchHopMs, rate);
        var pitch = new double[length];

        for (var index = 0; index < length; index++)
        {
            var k = Math.Min(track.Length - 1, index / hop);
            var left = track[k];
            var right = track[Math.Min(track.Length - 1, k + 1)];
            var fraction = (double)(index - (k * hop)) / hop;

            pitch[index] = (left > 0, right > 0) switch
            {
                (true, true) => left + ((right - left) * fraction),
                (true, false) => left,
                (false, true) => right,
                _ => 0,
            };
        }

        return pitch;
    }

    /// <summary>
    /// TD-PSOLA: two-period Hann grains cut one analysis period apart at <paramref name="pitch"/> and laid one
    /// synthesis period apart at <paramref name="target"/>, keeping the length. The result is blended with the dry
    /// signal by how voiced the neighbouring frames are, so unvoiced stretches pass unchanged.
    /// </summary>
    private static double[] Resynthesise(double[] signal, double[] track, double[] pitch, double[] target, int rate)
    {
        var length = signal.Length;
        var hop = Samples(PitchHopMs, rate);
        var marks = new List<int>();

        for (var position = 0.0; position < length;)
        {
            marks.Add((int)position);
            position += pitch[(int)position] > 0 ? rate / pitch[(int)position] : hop;
        }

        var sum = new double[length];
        var weight = new double[length];
        var mark = 0;

        for (var time = 0.0; time < length;)
        {
            var at = (int)Math.Round(time);

            while (mark + 1 < marks.Count && Math.Abs(marks[mark + 1] - time) <= Math.Abs(marks[mark] - time))
            {
                mark++;
            }

            var centre = marks[mark];
            var half = Math.Max(1, (int)Math.Round(pitch[centre] > 0 ? rate / pitch[centre] : hop));

            for (var offset = -half; offset < half; offset++)
            {
                var source = centre + offset;
                var destination = at + offset;

                if (source >= 0 && source < length && destination >= 0 && destination < length)
                {
                    var window = 0.5 + (0.5 * Math.Cos(Math.PI * offset / half));
                    sum[destination] += window * signal[source];
                    weight[destination] += window;
                }
            }

            var place = Math.Min(length - 1, at);
            time += target[place] > 0 ? rate / target[place] : pitch[place] > 0 ? rate / pitch[place] : hop;
        }

        var output = new double[length];

        for (var index = 0; index < length; index++)
        {
            var k = Math.Min(track.Length - 1, index / hop);
            var fraction = (double)(index - (k * hop)) / hop;
            var voiced = ((track[k] > 0 ? 1 : 0) * (1 - fraction))
                + ((track[Math.Min(track.Length - 1, k + 1)] > 0 ? 1 : 0) * fraction);
            var moved = weight[index] > 1e-9 ? sum[index] / weight[index] : 0;

            output[index] = (voiced * moved) + ((1 - voiced) * signal[index]);
        }

        return output;
    }

    /// <summary>The sample at <paramref name="index"/>, zero outside the signal.</summary>
    private static double Sample(double[] signal, int index) =>
        index >= 0 && index < signal.Length ? signal[index] : 0;

    /// <summary>
    /// Reverb on the clip reversed, reversed back, so the tail comes before each sound and swells into it. The
    /// clip gains <see cref="ReverseTailSeconds"/> at the front, faded in from zero.
    /// </summary>
    private static double[] ReverseReverb(double[] signal, double mix, int rate)
    {
        var tail = (int)Math.Round(ReverseTailSeconds * rate);
        var total = signal.Length + tail;
        var reversed = (double[])signal.Clone();
        Array.Reverse(reversed);

        var low = ReverbWet(reversed, total, rate);
        var output = new double[total];

        for (var index = 0; index < total; index++)
        {
            var source = total - 1 - index;
            var wet = mix * low[source];

            if (source >= signal.Length)
            {
                wet *= (double)(total - 1 - source) / tail;
            }

            output[index] = (ReverbDry * (index >= tail ? signal[index - tail] : 0)) + wet;
        }

        return output;
    }

    /// <summary>
    /// The low-passed wet signal of four parallel combs averaged and two allpasses in series, run for
    /// <paramref name="total"/> samples and not faded.
    /// </summary>
    private static double[] ReverbWet(double[] signal, int total, int rate)
    {
        var tank = new ReverbTank(rate);
        var wet = new double[total];

        for (var index = 0; index < total; index++)
        {
            wet[index] = tank.Next(index < signal.Length ? signal[index] : 0);
        }

        return wet;
    }

    private static double[] Mix(double[] first, double firstGain, double[] second, double secondGain)
    {
        var output = new double[first.Length];

        for (var index = 0; index < first.Length; index++)
        {
            output[index] = (firstGain * first[index]) + (secondGain * second[index]);
        }

        return output;
    }

    internal static double[][] Decode(ReadOnlySpan<byte> pcm, int channels, int frames)
    {
        var decoded = new double[channels][];

        for (var channel = 0; channel < channels; channel++)
        {
            decoded[channel] = new double[frames];
        }

        for (var index = 0; index < frames * channels; index++)
        {
            decoded[index % channels][index / channels] =
                (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;
        }

        return decoded;
    }

    internal static byte[] Encode(double[][] channels, double gain)
    {
        var frames = channels[0].Length;
        var pcm = new byte[frames * channels.Length * 2];

        for (var index = 0; index < frames * channels.Length; index++)
        {
            var sample = channels[index % channels.Length][index / channels.Length] * gain;
            var value = (short)Math.Clamp(Math.Round(sample * 32767.0), short.MinValue, short.MaxValue);

            pcm[index * 2] = (byte)(value & 0xFF);
            pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return pcm;
    }

    /// <summary>The sum of the squared Hann window at a quarter-frame hop.</summary>
    private const double OverlapGain = 1.5;

    /// <summary>The power of two nearest <see cref="FrameSeconds"/> at this rate.</summary>
    private static int FrameLength(int rate) =>
        1 << Math.Max(4, (int)Math.Round(Math.Log2(FrameSeconds * rate)));

    internal static int Samples(double milliseconds, int rate) =>
        Math.Max(1, (int)Math.Round(milliseconds / 1000 * rate));

    /// <summary>Periodic Hann.</summary>
    private static double[] Hann(int length)
    {
        var window = new double[length];

        for (var index = 0; index < length; index++)
        {
            window[index] = 0.5 - (0.5 * Math.Cos(2 * Math.PI * index / length));
        }

        return window;
    }

    /// <summary>A linearly interpolated read, zero outside the signal.</summary>
    private static double Read(double[] signal, double position)
    {
        if (position < 0 || position > signal.Length - 1)
        {
            return 0;
        }

        var below = (int)position;

        if (below >= signal.Length - 1)
        {
            return signal[^1];
        }

        var fraction = position - below;

        return (signal[below] * (1 - fraction)) + (signal[below + 1] * fraction);
    }

    /// <summary>A moving average of <paramref name="source"/> over ±<paramref name="halfWidth"/> bins.</summary>
    private static void Smooth(double[] source, double[] target, int halfWidth)
    {
        var running = 0.0;
        var from = 0;
        var to = -1;

        for (var bin = 0; bin < source.Length; bin++)
        {
            while (to < Math.Min(source.Length - 1, bin + halfWidth))
            {
                running += source[++to];
            }

            while (from < bin - halfWidth)
            {
                running -= source[from++];
            }

            target[bin] = Math.Max(0, running) / (to - from + 1);
        }
    }

    /// <summary>Fills the negative-frequency half from the positive one, so the inverse transform is real.</summary>
    private static void Mirror(double[] re, double[] im)
    {
        var length = re.Length;

        for (var bin = 1; bin < length / 2; bin++)
        {
            re[length - bin] = re[bin];
            im[length - bin] = -im[bin];
        }

        im[0] = 0;
        im[length / 2] = 0;
    }

    private static double Wrap(double angle) => angle - (2 * Math.PI * Math.Round(angle / (2 * Math.PI)));

    /// <summary>In-place radix-2 FFT; the length must be a power of two. The inverse is scaled by 1/N.</summary>
    private static void Fourier(double[] re, double[] im, bool inverse)
    {
        var length = re.Length;

        for (int index = 1, swap = 0; index < length; index++)
        {
            var bit = length >> 1;

            for (; (swap & bit) != 0; bit >>= 1)
            {
                swap ^= bit;
            }

            swap ^= bit;

            if (index < swap)
            {
                (re[index], re[swap]) = (re[swap], re[index]);
                (im[index], im[swap]) = (im[swap], im[index]);
            }
        }

        for (var size = 2; size <= length; size <<= 1)
        {
            var angle = (inverse ? 2 : -2) * Math.PI / size;
            var stepRe = Math.Cos(angle);
            var stepIm = Math.Sin(angle);
            var half = size / 2;

            for (var start = 0; start < length; start += size)
            {
                var turnRe = 1.0;
                var turnIm = 0.0;

                for (var offset = 0; offset < half; offset++)
                {
                    var a = start + offset;
                    var b = a + half;
                    var productRe = (re[b] * turnRe) - (im[b] * turnIm);
                    var productIm = (re[b] * turnIm) + (im[b] * turnRe);

                    re[b] = re[a] - productRe;
                    im[b] = im[a] - productIm;
                    re[a] += productRe;
                    im[a] += productIm;

                    (turnRe, turnIm) = ((turnRe * stepRe) - (turnIm * stepIm), (turnRe * stepIm) + (turnIm * stepRe));
                }
            }
        }

        if (inverse)
        {
            for (var index = 0; index < length; index++)
            {
                re[index] /= length;
                im[index] /= length;
            }
        }
    }

    /// <summary>A second-order band-pass section, direct form 1, recomputed for a new centre each sample.</summary>
    private struct Biquad
    {
        private double _b0, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        /// <summary>Cookbook constant-skirt-gain band-pass coefficients for a centre frequency and Q.</summary>
        public void SetBandPass(double frequency, double q, int rate)
        {
            var actualRate = rate > 0 ? rate : AudioFormat.Standard.SampleRate;
            var w0 = 2 * Math.PI * Math.Clamp(frequency, 1, actualRate * 0.45) / actualRate;
            var alpha = Math.Sin(w0) / (2 * q);
            var a0 = 1 + alpha;

            _b0 = alpha / a0;
            _b2 = -alpha / a0;
            _a1 = -2 * Math.Cos(w0) / a0;
            _a2 = (1 - alpha) / a0;
        }

        public double Next(double x)
        {
            var y = (_b0 * x) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);

            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;

            return y;
        }
    }

    /// <summary>A linear congruential generator, so every draw is the same from one call to the next.</summary>
    private struct Noise(uint seed)
    {
        private uint _state = seed;

        public double Next(double low, double high)
        {
            _state = (_state * 1664525u) + 1013904223u;
            return low + ((high - low) * ((_state >> 8) / 16777215.0));
        }
    }
}
