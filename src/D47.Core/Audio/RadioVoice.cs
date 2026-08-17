namespace D47.Core.Audio;

/// <summary>
/// Speech put through a comms link, so a voice arriving from outside the ship sounds like it
/// arrived from outside the ship.
/// <para>
/// The reason this exists is that a voice on its own stopped carrying the distinction. Phase 11
/// gave every sender their own voice, which answers "who is talking" — but a station, a police
/// interceptor and the companion sitting beside the Commander were all rendered as one clean
/// close voice in the room. In a headset that reads as somebody standing behind you.
/// <b>Only the ship's AI and the crew are aboard</b>, and they are the only two roles that come
/// through untouched; see <see cref="IsOverTheAir"/>.
/// </para>
/// <para>
/// It lives in Core because it is arithmetic on bytes, exactly like <see cref="WavReader"/> and
/// the cue library: no device, no provider, and every property below is assertable from a
/// buffer. The sink never learns that a clip was treated.
/// </para>
/// <para>
/// <b>Deterministic, including the noise floor.</b> The hiss is drawn from a counter seeded by a
/// constant rather than from <see cref="Random"/>, so one clip in gives one clip out for ever —
/// which is what lets the replay harness produce identical audio twice, and what lets a test say
/// anything at all about the result.
/// </para>
/// </summary>
public static class RadioVoice
{
    /// <summary>
    /// Which roles arrive over a link rather than from the next seat.
    /// <para>
    /// Stated as the negative on purpose: the set that is <em>aboard</em> is small, closed and
    /// unlikely to grow, and a role added later is far more likely to be somebody transmitting
    /// than somebody in the cockpit. Written the other way round, a new role would quietly
    /// default to sounding like it was sitting beside the Commander.
    /// </para>
    /// </summary>
    public static bool IsOverTheAir(VoiceRole role) => role is not (VoiceRole.ShipAi or VoiceRole.Crew);

    /// <summary>
    /// The treatment for a role, or null where there is none. Handed to a speech pipeline as the
    /// colour its sentences are rendered in.
    /// </summary>
    public static Func<AudioClip, AudioClip>? Colours(VoiceRole role) =>
        IsOverTheAir(role) ? Apply : null;

    /// <summary>
    /// The bottom of the passband. 300 Hz is the low edge of the telephony band, and taking the
    /// chest out of a voice is most of what makes it sound like it came down a wire — the cue a
    /// listener reads is the missing bottom, not the added noise.
    /// </summary>
    private const double LowEdgeHz = 300;

    /// <summary>
    /// The top. 3.4 kHz is the other end of the same band; above it a voice starts sounding
    /// present again, which is the thing this is here to undo.
    /// </summary>
    private const double HighEdgeHz = 3_400;

    /// <summary>Butterworth. No resonant peak at either edge — a peak reads as a fault in the app.</summary>
    private const double Q = 0.70710678118654752;

    /// <summary>
    /// How hard the link is driven before it saturates. Enough to add the edge of a small speaker
    /// being pushed; not enough to make consonants tear, which stops being character and starts
    /// being unintelligible.
    /// </summary>
    private const double Drive = 1.9;

    /// <summary>
    /// How loud the static is under the words. Well down: while somebody is talking the message is
    /// the voice, and a floor competing with it is the thing that makes an effect tiring rather
    /// than atmospheric.
    /// <para>
    /// Added <em>after</em> the makeup gain rather than before the filters, which is the detail
    /// that keeps it steady. Ahead of the gain, a loud sentence and a quiet one would come back
    /// with different noise floors, and a reply split into three sentences is three clips — so the
    /// static would step up and down between them, which is a fault rather than a radio.
    /// </para>
    /// </summary>
    private const double HissUnderVoice = 0.022;

    /// <summary>
    /// And how loud it is once the words stop, on the bare carrier. Several times the floor under
    /// the voice, which is both what a real link does — nothing is masking it and the gain at the
    /// far end comes up to meet it — and the reason the tail is audible as a link at all rather
    /// than as a gap before the next thing d47 says.
    /// </summary>
    private const double HissOnTheOpenCarrier = 0.085;

    /// <summary>
    /// How long the floor takes to come up between the two. Not instant, because a step in a noise
    /// floor is heard as a click at the boundary; not slow, because the whole tail is 100 ms.
    /// </summary>
    private static readonly TimeSpan Swell = TimeSpan.FromMilliseconds(40);

    /// <summary>
    /// How long the link stays open after the last word, before it cuts.
    /// <para>
    /// This is the part that makes it a transmission rather than a filtered voice. A real link
    /// does not stop dead on the final consonant — it holds the carrier for a moment and then
    /// drops, and that gap is most of what a listener recognises.
    /// </para>
    /// <para>
    /// A fifth of a second, arrived at by listening. A tenth was audible but read as the clip
    /// simply ending; this is long enough to register as the link being held open, and still short
    /// enough that a station saying three things in a row does not gain a second of dead air.
    /// </para>
    /// </summary>
    private static readonly TimeSpan Tail = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// How long the very end is faded over. Not an effect — 3 ms is inaudible as a fade, and
    /// cutting a noise floor off mid-sample is an audible click. The link still reads as dropping
    /// abruptly, which is the point.
    /// </summary>
    private static readonly TimeSpan Cut = TimeSpan.FromMilliseconds(3);

    /// <summary>
    /// The same speech, over a link. The clip's name gains a suffix so a log line or a test
    /// failure says which of the two forms it is looking at.
    /// </summary>
    public static AudioClip Apply(AudioClip clip)
    {
        var pcm = clip.Pcm.Span;
        var samples = pcm.Length / 2;

        if (samples == 0)
        {
            return clip;
        }

        var channels = Math.Max(1, clip.Format.Channels);
        var rate = clip.Format.SampleRate > 0 ? clip.Format.SampleRate : AudioFormat.Standard.SampleRate;

        // One filter pair per channel, and a second pair for the static. Every clip in the app is
        // mono (AudioFormat.Standard), so in practice these are loops of one — but a stereo buffer
        // run through a single filter state interleaves two signals into one recursion, and that
        // is not a quiet bug.
        var voiceBand = Band(channels, rate);
        var staticBand = Band(channels, rate);

        var shaped = new double[samples];

        var wasSquared = 0.0;
        var isSquared = 0.0;
        var peak = 0.0;

        for (var index = 0; index < samples; index++)
        {
            var channel = index % channels;
            var sample = (short)(pcm[index * 2] | (pcm[(index * 2) + 1] << 8)) / 32768.0;

            wasSquared += sample * sample;

            sample = Math.Tanh(voiceBand.Low[channel].Next(voiceBand.High[channel].Next(sample)) * Drive);

            shaped[index] = sample;
            isSquared += sample * sample;
            peak = Math.Max(peak, Math.Abs(sample));
        }

        var makeup = Makeup(Math.Sqrt(wasSquared / samples), Math.Sqrt(isSquared / samples), peak);

        // The words, then the carrier still open, then the cut.
        var tail = (int)(Tail.TotalSeconds * rate) / channels * channels;
        var total = samples + tail;
        var fade = Math.Min(total, (int)(Cut.TotalSeconds * rate));

        var treated = new byte[total * 2];
        var noise = 0x9E3779B9u;
        var swell = Math.Max(1, (int)(Swell.TotalSeconds * rate));

        for (var index = 0; index < total; index++)
        {
            var channel = index % channels;

            noise = (noise * 1664525u) + 1013904223u;
            var white = ((noise >> 8) / 16777215.0 * 2) - 1;

            // The static goes through a band-pass of its own, so it is the same colour as the
            // voice arriving beside it. White noise over a telephone-band voice reads as a broken
            // sound card rather than as a link.
            //
            // Two levels, and the level is where the two requests met: down under the words so it
            // is not tiring, up on the bare carrier so the tail is heard as a link being held open
            // rather than as the clip having ended.
            var hiss = index < samples
                ? HissUnderVoice
                : HissUnderVoice + ((HissOnTheOpenCarrier - HissUnderVoice)
                                    * Math.Min(1, (double)(index - samples) / swell));

            var floor = staticBand.Low[channel].Next(staticBand.High[channel].Next(white)) * hiss;

            var sample = (index < samples ? shaped[index] * makeup : 0) + floor;

            if (index >= total - fade)
            {
                sample *= (double)(total - index) / fade;
            }

            var value = (short)Math.Clamp(Math.Round(sample * 32767.0), short.MinValue, short.MaxValue);

            treated[index * 2] = (byte)(value & 0xFF);
            treated[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
        }

        return clip with { Name = $"{clip.Name} (radio)", Pcm = treated };
    }

    /// <summary>
    /// A band-pass per channel, at the edges of the comms band. Two of these are built per clip —
    /// one for the voice and one for the static — because a filter carries state, and sharing one
    /// between two signals mixes them inside the recursion.
    /// </summary>
    private static (Biquad[] High, Biquad[] Low) Band(int channels, int rate)
    {
        var high = new Biquad[channels];
        var low = new Biquad[channels];

        for (var channel = 0; channel < channels; channel++)
        {
            high[channel] = Biquad.HighPass(LowEdgeHz, rate);
            low[channel] = Biquad.LowPass(HighEdgeHz, rate);
        }

        return (high, low);
    }

    /// <summary>
    /// The gain that puts the treated line back at the loudness it arrived with.
    /// <para>
    /// <b>This is the part that stops the effect being a volume change.</b> Both halves of the
    /// treatment move the level on their own — the band-pass throws away everything under 300 Hz,
    /// which for a voice is most of its power, and the saturator has a makeup gain of its own —
    /// and the Commander has exactly one speech volume for all of it. A radio line that arrives
    /// louder or quieter than the ship's AI reads as the mixer being wrong, and the Commander's
    /// fix for that is to turn re-voiced messages off.
    /// </para>
    /// <para>
    /// Bounded both ways, because matching the loudness of near-silence means multiplying the
    /// noise floor by whatever it takes. And it never clips: where matching the level would push
    /// a peak past full scale, the peak wins, since a line that distorts on its loudest syllable
    /// is a worse answer than one that is slightly quiet.
    /// </para>
    /// </summary>
    private static double Makeup(double was, double now, double peak)
    {
        if (was <= 0 || now <= 0)
        {
            return 1;
        }

        var gain = Math.Clamp(was / now, 0.25, 4.0);

        return peak * gain > 1 ? 1 / peak : gain;
    }

    /// <summary>
    /// One second-order section, direct form 1, with the usual cookbook coefficients. Small
    /// enough to keep here: two responses and four adds is not worth a dependency, and a package
    /// would have to clear the licence gate to buy nothing.
    /// </summary>
    private struct Biquad
    {
        private double _b0, _b1, _b2, _a1, _a2;
        private double _x1, _x2, _y1, _y2;

        public static Biquad LowPass(double frequency, int sampleRate)
        {
            var (cos, alpha) = Shape(frequency, sampleRate);
            var a0 = 1 + alpha;

            return new Biquad
            {
                _b0 = (1 - cos) / 2 / a0,
                _b1 = (1 - cos) / a0,
                _b2 = (1 - cos) / 2 / a0,
                _a1 = -2 * cos / a0,
                _a2 = (1 - alpha) / a0,
            };
        }

        public static Biquad HighPass(double frequency, int sampleRate)
        {
            var (cos, alpha) = Shape(frequency, sampleRate);
            var a0 = 1 + alpha;

            return new Biquad
            {
                _b0 = (1 + cos) / 2 / a0,
                _b1 = -(1 + cos) / a0,
                _b2 = (1 + cos) / 2 / a0,
                _a1 = -2 * cos / a0,
                _a2 = (1 - alpha) / a0,
            };
        }

        /// <summary>
        /// The two quantities both responses are built from. The corner is clamped below Nyquist
        /// because one at or above it produces coefficients that oscillate rather than filter,
        /// and a clip arriving at an unexpected sample rate has to sound treated rather than
        /// destroyed.
        /// </summary>
        private static (double Cos, double Alpha) Shape(double frequency, int sampleRate)
        {
            var rate = sampleRate > 0 ? sampleRate : AudioFormat.Standard.SampleRate;
            var w0 = 2 * Math.PI * Math.Clamp(frequency, 1, rate * 0.45) / rate;

            return (Math.Cos(w0), Math.Sin(w0) / (2 * Q));
        }

        public double Next(double x)
        {
            var y = (_b0 * x) + (_b1 * _x1) + (_b2 * _x2) - (_a1 * _y1) - (_a2 * _y2);

            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;

            return y;
        }
    }
}
