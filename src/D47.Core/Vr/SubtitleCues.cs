using System.Globalization;
using System.Net;
using System.Text;

namespace D47.Core.Vr;

/// <summary>One timed line of a subtitle file: shown from <see cref="Start"/> until just before <see cref="End"/>.</summary>
/// <param name="Text">The cue's lines joined with newlines, with styling tags removed.</param>
public sealed record SubtitleCue(TimeSpan Start, TimeSpan End, string Text);

/// <summary>Reads SRT and WebVTT subtitle files into cues.</summary>
public static class SubtitleCues
{
    private const string Arrow = "-->";

    /// <summary>
    /// The cues in <paramref name="text"/>, an SRT or WebVTT file, ordered by start. Blocks that are not cues, cues
    /// whose times do not parse, and cues that end at or before their start are skipped.
    /// </summary>
    public static IReadOnlyList<SubtitleCue> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var lines = text.TrimStart('﻿').Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var cues = new List<SubtitleCue>();
        var block = new List<string>();

        foreach (var line in lines)
        {
            if (line.Trim().Length == 0)
            {
                Read(block, cues);
                block.Clear();
                continue;
            }

            block.Add(line);
        }

        Read(block, cues);

        return [.. cues.OrderBy(cue => cue.Start)];
    }

    /// <summary>
    /// The cue covering <paramref name="position"/>, or null. Where cues overlap, the one that started latest; where two
    /// start together, the later in the list.
    /// </summary>
    public static SubtitleCue? At(IReadOnlyList<SubtitleCue> cues, TimeSpan position)
    {
        ArgumentNullException.ThrowIfNull(cues);

        SubtitleCue? found = null;

        for (var i = 0; i < cues.Count; i++)
        {
            var cue = cues[i];

            if (cue.Start <= position && position < cue.End && (found is null || cue.Start >= found.Start))
            {
                found = cue;
            }
        }

        return found;
    }

    private static void Read(List<string> block, List<SubtitleCue> cues)
    {
        if (block.Count == 0)
        {
            return;
        }

        // The timing line is first, or second after an SRT index or a WebVTT identifier. A header, NOTE, STYLE or
        // REGION block has none.
        var timing = block[0].Contains(Arrow, StringComparison.Ordinal) ? 0
            : block.Count > 1 && block[1].Contains(Arrow, StringComparison.Ordinal) ? 1
            : -1;

        if (timing < 0 || block[0].Split(' ', 2)[0] is "NOTE" or "STYLE" or "REGION" or "WEBVTT")
        {
            return;
        }

        var parts = block[timing].Split(Arrow, 2);
        var right = parts[1].Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);

        if (right.Length == 0
            || !TryTime(parts[0].Trim(), out var start)
            || !TryTime(right[0], out var end)
            || end <= start)
        {
            return;
        }

        var text = string.Join(
            '\n',
            block.Skip(timing + 1).Select(Clean).Where(line => line.Length > 0));

        if (text.Length > 0)
        {
            cues.Add(new SubtitleCue(start, end, text));
        }
    }

    /// <summary>
    /// <c>hh:mm:ss,fff</c>, <c>hh:mm:ss.fff</c> or <c>mm:ss.fff</c>, with any number of hour digits and of fraction
    /// digits.
    /// </summary>
    private static bool TryTime(string value, out TimeSpan time)
    {
        time = default;

        var fields = value.Replace(',', '.').Split(':');

        if (fields.Length is < 2 or > 3)
        {
            return false;
        }

        var hours = 0L;

        if (fields.Length == 3 && !long.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out hours))
        {
            return false;
        }

        var seconds = fields[^1].Split('.');

        if (!long.TryParse(fields[^2], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || seconds.Length > 2
            || !long.TryParse(seconds[0], NumberStyles.None, CultureInfo.InvariantCulture, out var whole))
        {
            return false;
        }

        var milliseconds = 0L;

        if (seconds.Length == 2)
        {
            var fraction = seconds[1];

            if (fraction.Length == 0
                || !long.TryParse(
                    fraction.Length > 3 ? fraction[..3] : fraction.PadRight(3, '0'),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out milliseconds))
            {
                return false;
            }
        }

        if (minutes > 59 || whole > 59 || hours > 999)
        {
            return false;
        }

        time = TimeSpan.FromMilliseconds((((hours * 60) + minutes) * 60 * 1000) + (whole * 1000) + milliseconds);
        return true;
    }

    /// <summary>One text line without its <c>&lt;…&gt;</c> tags or <c>{\…}</c> overrides, entities decoded.</summary>
    private static string Clean(string line)
    {
        var kept = new StringBuilder(line.Length);

        for (var i = 0; i < line.Length; i++)
        {
            var close = line[i] == '<' ? line.IndexOf('>', i + 1)
                : line[i] == '{' && i + 1 < line.Length && line[i + 1] == '\\' ? line.IndexOf('}', i + 2)
                : -1;

            if (close > i)
            {
                i = close;
                continue;
            }

            kept.Append(line[i]);
        }

        return WebUtility.HtmlDecode(kept.ToString()).Replace(' ', ' ').Trim();
    }
}
