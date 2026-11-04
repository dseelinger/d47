namespace D47.Core.Callouts;

/// <summary>What to do with a canned NPC line, given how often its speaker has already said it.</summary>
internal enum RepeatVerdict
{
    Speak,
    Quip,
    Mute,
}

/// <summary>
/// Counts each speaker's canned lines on journal time. A line is spoken twice, the third time the speaker
/// quips (once per speaker), and from then on the line is muted. A speaker's counts are cleared after
/// <see cref="Quiet"/> without a canned line, and on <see cref="Clear"/>.
/// </summary>
internal sealed class RepeatedLines
{
    public static readonly TimeSpan Quiet = TimeSpan.FromMinutes(5);

    public const int SpokenTimes = 2;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, Speaker> _speakers = new(StringComparer.Ordinal);

    public RepeatVerdict Count(string speaker, string line, DateTimeOffset at)
    {
        lock (_lock)
        {
            if (_speakers.TryGetValue(speaker, out var held) && at - held.Last >= Quiet)
            {
                _speakers.Remove(speaker);
                held = null;
            }

            held ??= _speakers[speaker] = new Speaker();
            held.Last = at;

            var times = held.Counts.GetValueOrDefault(line) + 1;
            held.Counts[line] = times;

            if (times <= SpokenTimes)
            {
                return RepeatVerdict.Speak;
            }

            if (times == SpokenTimes + 1 && !held.Quipped)
            {
                held.Quipped = true;
                return RepeatVerdict.Quip;
            }

            return RepeatVerdict.Mute;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _speakers.Clear();
        }
    }

    private sealed class Speaker
    {
        public DateTimeOffset Last { get; set; }

        public Dictionary<string, int> Counts { get; } = new(StringComparer.Ordinal);

        public bool Quipped { get; set; }
    }
}
