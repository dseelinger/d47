using System.Text;

namespace D47.Core.Audio;

/// <summary>How clearly a comms link carries over distance, and what a weak one does to the words.</summary>
public static class LinkSignal
{
    /// <summary>The distance within which the link is clear.</summary>
    public const double ClearLightYears = 250;

    /// <summary>The distance at which nothing carries.</summary>
    public const double LostLightYears = 500;

    /// <summary>Stands in for the words a weak link loses.</summary>
    public const string Lost = "…";

    /// <summary>1 at or under <see cref="ClearLightYears"/>, falling to 0 at <see cref="LostLightYears"/>; 1 when the distance is unknown.</summary>
    public static double Strength(double? lightYears) => lightYears switch
    {
        null or double.NaN or <= ClearLightYears => 1,
        >= LostLightYears => 0,
        { } away => 0.5 * (1 + Math.Cos(Math.PI * (away - ClearLightYears) / (LostLightYears - ClearLightYears))),
    };

    /// <summary>The share of words a link at this strength loses.</summary>
    public static double LossRate(double strength) => (1 - Math.Clamp(strength, 0, 1)) * 0.6;

    /// <summary>A thinner for one turn's text, seeded from <paramref name="seed"/> so the same turn loses the same words.</summary>
    public static Thinner Thin(double strength, string seed) => new(strength, Seed(seed));

    /// <summary>FNV-1a, which unlike <see cref="string.GetHashCode()"/> is the same in every process.</summary>
    private static uint Seed(string text)
    {
        var hash = 2166136261u;

        foreach (var c in text)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash == 0 ? 1 : hash;
    }

    /// <summary>
    /// Drops words from streamed text, replacing each run of dropped words with <see cref="Lost"/>. Text is held
    /// back until a word ends, so a word split across two deltas is kept or dropped whole.
    /// </summary>
    public sealed class Thinner
    {
        private readonly double _loss;
        private readonly StringBuilder _word = new();
        private readonly StringBuilder _space = new();
        private uint _state;
        private bool _lastDropped;

        internal Thinner(double strength, uint seed)
        {
            _loss = LossRate(strength);
            _state = seed;
        }

        /// <summary>The text that can be released so far, which may be empty.</summary>
        public string Push(string text)
        {
            var released = new StringBuilder();

            foreach (var c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    EndWord(released);
                    _space.Append(c);
                }
                else
                {
                    _word.Append(c);
                }
            }

            return released.ToString();
        }

        /// <summary>Whatever is still held back. Call when the text stops.</summary>
        public string Flush()
        {
            var released = new StringBuilder();

            EndWord(released);
            released.Append(_space);
            _space.Clear();

            return released.ToString();
        }

        private void EndWord(StringBuilder released)
        {
            if (_word.Length == 0)
            {
                return;
            }

            if (Drops())
            {
                // A run of dropped words, and the spaces between them, is one mark.
                if (!_lastDropped)
                {
                    released.Append(_space).Append(Lost);
                }

                _lastDropped = true;
            }
            else
            {
                released.Append(_space).Append(_word);
                _lastDropped = false;
            }

            _space.Clear();
            _word.Clear();
        }

        private bool Drops()
        {
            if (_loss <= 0)
            {
                return false;
            }

            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;

            return _state / (double)uint.MaxValue < _loss;
        }
    }
}
