using D47.Core.Configuration;

namespace D47.Core.Audio;

public static partial class GuardianVoice
{
    /// <summary>Dry input the leveller measures before it releases anything.</summary>
    private const double LevellerWarmupMs = 100;

    /// <summary>The time constant the leveller's gain follows its target with.</summary>
    private const double LevellerSmoothingMs = 100;

    /// <summary>How far ahead the limiter sees a peak coming.</summary>
    private const double LimiterLookAheadMs = 5;

    /// <summary>The time constant the limiter recovers with after a peak.</summary>
    private const double LimiterReleaseMs = 50;

    /// <summary>The ticked effects as a running filter on <see cref="AudioFormat.Standard"/> PCM.</summary>
    public static IPcmFilter Filter(IReadOnlyList<GuardianVoiceEffect> effects, double basePitchHz) =>
        new GuardianFilter(AudioFormat.Standard, Ticked(effects), basePitchHz);

    /// <summary>Interleaved 16-bit PCM through one effect chain a channel, then the leveller.</summary>
    private sealed class GuardianFilter : IPcmFilter
    {
        private readonly int _channels;
        private readonly ChainStage[] _chains;
        private readonly Leveller _leveller;
        private byte[] _held = [];
        private bool _finished;

        public GuardianFilter(AudioFormat format, List<(GuardianEffect Effect, double Value)> chain, double basePitchHz)
        {
            var rate = format.SampleRate > 0 ? format.SampleRate : AudioFormat.Standard.SampleRate;

            _channels = Math.Max(1, format.Channels);
            _chains =
            [
                .. Enumerable.Range(0, _channels)
                    .Select(_ => new ChainStage([.. chain.Select(link => link.Effect.Start(link.Value, basePitchHz, rate))])),
            ];
            _leveller = new Leveller(_channels, rate, _chains[0].Delay);
        }

        public byte[] Push(ReadOnlySpan<byte> pcm)
        {
            if (_finished)
            {
                throw new InvalidOperationException("The Guardian chain has already been finished.");
            }

            var input = pcm;

            if (_held.Length > 0)
            {
                var joined = new byte[_held.Length + pcm.Length];

                _held.CopyTo(joined, 0);
                pcm.CopyTo(joined.AsSpan(_held.Length));
                input = joined;
            }

            var frames = input.Length / (2 * _channels);
            var dry = Decode(input, _channels, frames);

            _held = input[(frames * 2 * _channels)..].ToArray();
            _leveller.AddDry(dry, frames);

            for (var channel = 0; channel < _channels; channel++)
            {
                var treated = new List<double>();

                _chains[channel].Push(dry[channel], treated);
                _leveller.AddTreated(channel, treated);
            }

            return Write(_leveller.Drain(finished: false));
        }

        public byte[] Finish()
        {
            if (_finished)
            {
                return [];
            }

            _finished = true;

            for (var channel = 0; channel < _channels; channel++)
            {
                var treated = new List<double>();

                _chains[channel].Finish(treated);
                _leveller.AddTreated(channel, treated);
            }

            return Write(_leveller.Drain(finished: true));
        }

        private byte[] Write(List<double> interleaved)
        {
            var pcm = new byte[interleaved.Count * 2];

            for (var index = 0; index < interleaved.Count; index++)
            {
                var value = (short)Math.Clamp(Math.Round(interleaved[index] * 32767.0), short.MinValue, short.MaxValue);

                pcm[index * 2] = (byte)(value & 0xFF);
                pcm[(index * 2) + 1] = (byte)((value >> 8) & 0xFF);
            }

            return pcm;
        }
    }

    /// <summary>
    /// Brings the treated signal to the dry signal's loudness as it arrives, then limits it to <see cref="Ceiling"/>.
    /// Treated frame <c>n</c> is levelled by the treated energy over frames 0 to <c>n</c> and the dry energy over
    /// frames 0 to <c>n</c> less the chain's <see cref="IGuardianStage.Delay"/>, summed across channels; frames before the warm-up are held and given the gain measured at its end. Frames released by
    /// <see cref="Drain"/> once finished get the gain over the whole dry input and the treated frames beside it, so the
    /// output does not depend on how either arrived.
    /// </summary>
    private sealed class Leveller
    {
        private readonly int _channels;
        private readonly int _warmup;
        private readonly double _smoothing;
        private readonly int _lookAhead;
        private readonly double _release;
        private readonly Queue<double>[] _treated;
        private readonly Queue<double> _dryEnergy = new();
        private readonly List<double[]> _warming = [];
        private readonly Queue<(double[] Frame, double Room)> _limiting = new();

        private long _index;
        private double _drySquared;
        private double _treatedSquared;
        private double _target = 1;
        private double _gain = 1;
        private bool _warm;
        private bool _whole;
        private double _limit = 1;
        private int _peaksAhead;

        public Leveller(int channels, int rate, int delay)
        {
            _channels = channels;

            for (var frame = 0; frame < delay; frame++)
            {
                _dryEnergy.Enqueue(0);
            }

            _warmup = Samples(LevellerWarmupMs, rate);
            _smoothing = 1 - Math.Exp(-1 / (LevellerSmoothingMs / 1000 * rate));
            _lookAhead = Samples(LimiterLookAheadMs, rate);
            _release = 1 - Math.Exp(-1 / (LimiterReleaseMs / 1000 * rate));
            _treated = [.. Enumerable.Range(0, channels).Select(_ => new Queue<double>())];
        }

        public void AddDry(double[][] dry, int frames)
        {
            for (var frame = 0; frame < frames; frame++)
            {
                var energy = 0.0;

                for (var channel = 0; channel < _channels; channel++)
                {
                    energy += dry[channel][frame] * dry[channel][frame];
                }

                _dryEnergy.Enqueue(energy);
            }
        }

        public void AddTreated(int channel, List<double> samples)
        {
            foreach (var sample in samples)
            {
                _treated[channel].Enqueue(sample);
            }
        }

        /// <summary>
        /// Every interleaved frame now settled. Once <paramref name="finished"/>, the dry input and every chain have
        /// ended: a channel that ran short is padded with silence and the limiter is emptied.
        /// </summary>
        public List<double> Drain(bool finished)
        {
            var output = new List<double>();

            if (finished && !_whole)
            {
                _whole = true;
                _target = WholeRatio();
                Warm(output);
            }

            while (Ready(finished))
            {
                var frame = new double[_channels];
                var energy = 0.0;

                for (var channel = 0; channel < _channels; channel++)
                {
                    frame[channel] = _treated[channel].TryDequeue(out var sample) ? sample : 0;
                    energy += frame[channel] * frame[channel];
                }

                if (!_whole && _dryEnergy.TryDequeue(out var dry))
                {
                    _drySquared += dry;
                    _treatedSquared += energy;
                    _target = Ratio(_drySquared, _treatedSquared);
                }

                _index++;

                if (_warm)
                {
                    _gain += _smoothing * (_target - _gain);
                    Level(frame, output);
                    continue;
                }

                _warming.Add(frame);

                if (_index >= _warmup)
                {
                    Warm(output);
                }
            }

            if (finished)
            {
                while (_limiting.Count > 0)
                {
                    Release(output);
                }
            }

            return output;
        }

        private static double Ratio(double drySquared, double treatedSquared) =>
            drySquared > 0 && treatedSquared > 0 ? Math.Sqrt(drySquared / treatedSquared) : 1;

        /// <summary>The ratio over every dry frame and the treated frames up to the dry input's length.</summary>
        private double WholeRatio()
        {
            var drySquared = _drySquared + _dryEnergy.Sum();
            var treatedSquared = _treatedSquared;

            foreach (var channel in _treated)
            {
                foreach (var sample in channel.Take(_dryEnergy.Count))
                {
                    treatedSquared += sample * sample;
                }
            }

            return Ratio(drySquared, treatedSquared);
        }

        /// <summary>Starts the gain at the target and releases the frames held for the warm-up at it.</summary>
        private void Warm(List<double> output)
        {
            if (_warm)
            {
                return;
            }

            _warm = true;
            _gain = _target;

            foreach (var frame in _warming)
            {
                Level(frame, output);
            }

            _warming.Clear();
        }

        /// <summary>The frame at the current gain, into the limiter.</summary>
        private void Level(double[] frame, List<double> output)
        {
            var peak = 0.0;

            for (var channel = 0; channel < _channels; channel++)
            {
                frame[channel] *= _gain;
                peak = Math.Max(peak, Math.Abs(frame[channel]));
            }

            Limit(frame, peak > Ceiling ? Ceiling / peak : 1, output);
        }

        /// <summary>Whether the next treated frame can be levelled: every channel has it, and so does the dry input.</summary>
        private bool Ready(bool finished)
        {
            if (finished)
            {
                return _treated.Any(channel => channel.Count > 0);
            }

            return _dryEnergy.Count > 0 && _treated.All(channel => channel.Count > 0);
        }

        /// <summary>Queues a levelled frame needing gain <paramref name="room"/> or less, and releases what the look-ahead has passed.</summary>
        private void Limit(double[] frame, double room, List<double> output)
        {
            _limiting.Enqueue((frame, room));

            if (room < 1)
            {
                _peaksAhead++;
            }

            if (_limiting.Count > _lookAhead)
            {
                Release(output);
            }
        }

        /// <summary>
        /// The oldest queued frame at a gain that ramps down linearly to each peak ahead and recovers at the release
        /// rate after it; no frame's gain exceeds its own room.
        /// </summary>
        private void Release(List<double> output)
        {
            var reach = _lookAhead + 1.0;
            var attack = 1.0;

            if (_peaksAhead > 0)
            {
                var distance = 0;

                foreach (var (_, room) in _limiting)
                {
                    if (room < 1)
                    {
                        attack = Math.Min(attack, room + ((1 - room) * distance / reach));
                    }

                    distance++;
                }
            }

            _limit = Math.Min(attack, _limit + (_release * (1 - _limit)));

            var (frame, own) = _limiting.Dequeue();

            if (own < 1)
            {
                _peaksAhead--;
            }

            foreach (var sample in frame)
            {
                output.Add(sample * _limit);
            }
        }
    }
}
