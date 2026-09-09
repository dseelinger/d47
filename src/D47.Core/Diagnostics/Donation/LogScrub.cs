using System.Globalization;
using System.Text.RegularExpressions;
using D47.Core.Audio;

namespace D47.Core.Diagnostics.Donation;

/// <summary>Whose words a log entry carries.</summary>
public enum LogVoice
{
    /// <summary>d47's own: announcements, tool calls, errors, timings, what it said.</summary>
    D47,

    /// <summary>The Commander's own speech, written down as it was heard.</summary>
    Commander,

    /// <summary>Another player's or an NPC's words, arriving in-game and re-voiced.</summary>
    InGame,
}

/// <summary>One entry of d47's human-readable log: when, whose words, and the whole of it.</summary>
/// <param name="At">When the sink wrote it.</param>
/// <param name="Voice">Whose words, decided by <see cref="LogScrub.Parse"/>.</param>
/// <param name="Text">The entry as written, continuation lines included.</param>
public sealed record LogEntry(DateTimeOffset At, LogVoice Voice, string Text);

/// <summary>
/// The d47 half of an incident excerpt, and it rides the opposite rule to the journal half (#160).
/// </summary>
public static partial class LogScrub
{
    /// <summary>What a withheld sentence is replaced by, in the shape the log line had.</summary>
    public const string Withheld = "[withheld]";

    /// <summary>
    /// The speakers that are d47 rather than somebody in the game: its own name, and the roles it casts
    /// voices for.
    /// </summary>
    private static readonly HashSet<string> OwnVoices =
        new(Enum.GetNames<VoiceRole>().Append("D47"), StringComparer.OrdinalIgnoreCase);

    /// <summary>The two lines that write down what the Commander said.</summary>
    private static readonly string[] CommanderSpeech =
    [
        "Heard: ",
        "Not addressed to me: ",
    ];

    /// <summary>A re-voiced in-game message on its way to being spoken.</summary>
    private const string InGameCallout = "Callout message.";

    /// <summary>Splits the file into entries.</summary>
    /// <param name="day">Which day this file holds, from its name.</param>
    /// <param name="zone">The zone the sink wrote in.</param>
    public static IReadOnlyList<LogEntry> Parse(string log, DateOnly day, TimeZoneInfo zone)
    {
        var entries = new List<LogEntry>();
        var text = new System.Text.StringBuilder();
        DateTimeOffset at = default;
        var voice = LogVoice.D47;
        var open = false;

        void Close()
        {
            if (open)
            {
                entries.Add(new LogEntry(at, voice, text.ToString()));
            }

            text.Clear();
        }

        foreach (var line in log.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');

            if (Head().Match(trimmed) is { Success: true } head)
            {
                Close();

                var clock = TimeOnly.ParseExact(head.Groups["at"].Value, "HH:mm:ss", CultureInfo.InvariantCulture);
                var local = day.ToDateTime(clock, DateTimeKind.Unspecified);

                // The hour that happens twice when the clocks go back is resolved to the first of the two,
                // which is what ConvertTimeToUtc does with an ambiguous local time.
                at = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, zone), TimeSpan.Zero);
                voice = VoiceOf(head.Groups["message"].Value);
                open = true;
                text.Append(trimmed);
                continue;
            }

            if (open)
            {
                text.Append('\n').Append(trimmed);
            }
        }

        Close();
        return entries;
    }

    /// <summary>
    /// One entry, ready to travel: whichever words are withheld replaced, then the pseudonyms and
    /// whatever else the host asked to be substituted applied over the rest.
    /// </summary>
    /// <param name="also">Literal substitutions the host supplies, longest first.</param>
    public static string Redact(
        LogEntry entry,
        Pseudonyms names,
        IReadOnlyList<KeyValuePair<string, string>>? also = null)
    {
        var text = entry.Voice == LogVoice.InGame ? Silence(entry.Text) : entry.Text;

        foreach (var (real, stand) in names.Replacements)
        {
            text = text.Replace(real, stand, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var (real, stand) in also ?? [])
        {
            if (real is { Length: > 0 })
            {
                text = text.Replace(real, stand, StringComparison.OrdinalIgnoreCase);
            }
        }

        return text;
    }

    /// <summary>
    /// Drops the sentence off an in-game line and keeps everything left of it — the timestamp, the
    /// component, and who it was from.
    /// </summary>
    private static string Silence(string text)
    {
        var newline = text.IndexOf('\n');
        var first = newline < 0 ? text : text[..newline];

        if (Head().Match(first) is not { Success: true } head)
        {
            return text;
        }

        var start = head.Groups["message"].Index;
        var message = text[start..];

        if (Said().Match(message) is { Success: true } said)
        {
            return text[..start] + said.Groups["who"].Value + " said: " + Withheld;
        }

        if (!message.StartsWith(InGameCallout, StringComparison.Ordinal))
        {
            return text;
        }

        var colon = message.IndexOf(": ", InGameCallout.Length, StringComparison.Ordinal);

        return colon < 0 ? text : text[..start] + message[..(colon + 2)] + Withheld;
    }

    private static LogVoice VoiceOf(string message)
    {
        foreach (var prefix in CommanderSpeech)
        {
            if (message.StartsWith(prefix, StringComparison.Ordinal))
            {
                return LogVoice.Commander;
            }
        }

        if (message.StartsWith(InGameCallout, StringComparison.Ordinal))
        {
            return LogVoice.InGame;
        }

        return Said().Match(message) is { Success: true } said
               && !OwnVoices.Contains(said.Groups["who"].Value)
            ? LogVoice.InGame
            : LogVoice.D47;
    }

    /// <summary>
    /// The template <c>LoggingSetup</c> writes: the bracketed time and level, the component, then the
    /// message.
    /// </summary>
    [GeneratedRegex(@"^\[(?<at>\d{2}:\d{2}:\d{2}) [A-Z]{3}\] [^:\r\n]*: (?<message>.*)$")]
    private static partial Regex Head();

    /// <summary>
    /// <c>&lt;speaker&gt; said: </c>, anchored at the start of the message — which is where
    /// <c>SpeechPipeline</c> writes it, and the reason this is not a search.
    /// </summary>
    [GeneratedRegex(@"^(?<who>[^:\r\n]{1,80}?) said: ")]
    private static partial Regex Said();
}
