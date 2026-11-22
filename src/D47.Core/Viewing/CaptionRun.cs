using D47.Core.Vr;

namespace D47.Core.Viewing;

/// <summary>
/// Captions written from a video's soundtrack: the cues so far, how far they reach, and whether the picture has to wait
/// for them. Stretches are <see cref="Stretch"/> long and overlap by <see cref="Overlap"/>; a cue is kept once it ends
/// before the overlap, and the next stretch starts where the first cue not kept starts. One writer uses it at a time.
/// </summary>
public sealed class CaptionRun
{
    public static readonly TimeSpan Stretch = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan Overlap = TimeSpan.FromSeconds(5);

    /// <summary>How far ahead of the picture writing goes before it waits.</summary>
    public static readonly TimeSpan MostAhead = TimeSpan.FromSeconds(60);

    /// <summary>How near the end of the captions the picture comes before it waits for them.</summary>
    public static readonly TimeSpan WaitWithin = TimeSpan.FromSeconds(1);

    /// <summary>How far past the picture the captions reach before a waiting picture plays on.</summary>
    public static readonly TimeSpan PlayOnAhead = TimeSpan.FromSeconds(10);

    /// <summary>How far outside the written captions the picture moves before writing starts again from it.</summary>
    public static readonly TimeSpan Jump = TimeSpan.FromSeconds(3);

    /// <summary>The least a stretch moves writing on, so a stretch of one long cue cannot hold it in place.</summary>
    private static readonly TimeSpan LeastProgress = TimeSpan.FromSeconds(10);

    private IReadOnlyList<SubtitleCue> _cues = [];

    public CaptionRun(TimeSpan from)
    {
        From = from < TimeSpan.Zero ? TimeSpan.Zero : from;
        WrittenTo = From;
    }

    /// <summary>The cues written, ordered by start; a new list after every change.</summary>
    public IReadOnlyList<SubtitleCue> Cues => _cues;

    /// <summary>Where writing last started.</summary>
    public TimeSpan From { get; private set; }

    /// <summary>How far the captions reach from <see cref="From"/>, and where the next stretch starts.</summary>
    public TimeSpan WrittenTo { get; private set; }

    /// <summary>Whether the captions reach the end of the soundtrack.</summary>
    public bool Finished { get; private set; }

    /// <summary>Whether the picture at <paramref name="position"/> is outside the written captions, so writing starts again from it.</summary>
    public bool Jumped(TimeSpan position) =>
        position < From - Jump || (!Finished && position > WrittenTo + Jump);

    /// <summary>Whether writing is far enough ahead of the picture to wait.</summary>
    public bool Ahead(TimeSpan position) => Finished || WrittenTo - position >= MostAhead;

    /// <summary>Whether the picture has come near the end of the captions.</summary>
    public bool Behind(TimeSpan position) => !Finished && position >= WrittenTo - WaitWithin;

    /// <summary>Whether a picture waiting for the captions can play on.</summary>
    public bool CaughtUp(TimeSpan position) => Finished || WrittenTo - position >= PlayOnAhead;

    /// <summary>Starts writing again at <paramref name="at"/>, dropping the cues that end after it.</summary>
    public void Restart(TimeSpan at)
    {
        From = at < TimeSpan.Zero ? TimeSpan.Zero : at;
        WrittenTo = From;
        Finished = false;
        _cues = [.. _cues.Where(cue => cue.End <= From)];
    }

    /// <summary>
    /// Keeps the cues of a stretch that starts at <paramref name="start"/> and lasts <paramref name="heard"/>, and moves
    /// <see cref="WrittenTo"/> to where the next stretch starts.
    /// </summary>
    /// <param name="cues">The stretch's cues, timed from its start.</param>
    /// <param name="ended">Whether the soundtrack ends with this stretch, which keeps every cue.</param>
    public void Accept(TimeSpan start, TimeSpan heard, IReadOnlyList<SubtitleCue> cues, bool ended)
    {
        ArgumentNullException.ThrowIfNull(cues);

        var end = start + heard;
        var placed = cues
            .Where(cue => cue.End > cue.Start && cue.Text.Length > 0)
            .Select(cue => cue with { Start = start + cue.Start, End = start + cue.End })
            .Where(cue => cue.Start >= WrittenTo && cue.Start < end)
            .OrderBy(cue => cue.Start)
            .ToList();

        TimeSpan next;
        List<SubtitleCue> kept;

        if (ended || heard <= Overlap)
        {
            kept = placed;
            next = end;
        }
        else
        {
            var cutoff = end - Overlap;
            kept = [.. placed.TakeWhile(cue => cue.End <= cutoff)];
            next = placed.Count > kept.Count ? placed[kept.Count].Start : cutoff;

            if (next < start + LeastProgress)
            {
                kept = [.. placed.Where(cue => cue.Start < cutoff)];
                next = kept.Count > 0 ? Max(cutoff, kept[^1].End) : cutoff;
            }
        }

        _cues = [.. _cues.Concat(kept).OrderBy(cue => cue.Start)];
        WrittenTo = Max(WrittenTo, Min(next, end));
        Finished = ended;
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}
