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

    /// <summary>
    /// Another player's or an NPC's words, arriving in-game and re-voiced. Nobody in this
    /// conversation can consent for them.
    /// </summary>
    InGame,
}

/// <summary>One entry of d47's human-readable log: when, whose words, and the whole of it.</summary>
/// <param name="At">
/// The time of day the sink wrote, in local time. There is no date in the file — it rolls daily
/// and the name carries it — which is why this is a <see cref="TimeOnly"/> and the window that
/// selects on it is wrap-aware.
/// </param>
/// <param name="Voice">Whose words, decided by <see cref="LogScrub.Parse"/>.</param>
/// <param name="Text">
/// The entry as written, continuation lines included. An exception renders across several lines
/// with no timestamp on the ones after the first, and a stack trace cut in half is worse evidence
/// than no stack trace.
/// </param>
public sealed record LogEntry(TimeOnly At, LogVoice Voice, string Text);

/// <summary>
/// The d47 half of an incident excerpt, and it rides <b>the opposite rule</b> to the journal half
/// (<a href="https://github.com/dseelinger/d47/issues/160">#160</a>).
/// <para>
/// A journal is a schema, so it gets a field list. A log is free text: d47's replies can carry
/// memory-derived personal content and no enumeration of fields can reach into a sentence. So the
/// show step is the control here — the Commander reads the excerpt as prose and decides — and this
/// class does only the three things that are mechanical rather than editorial.
/// </para>
/// <list type="number">
/// <item>
/// <b>d47's own lines travel.</b> Announcements, tool calls, <c>said:</c>, errors and timings are
/// what the report is evidence <em>of</em>.
/// </item>
/// <item>
/// <b>The Commander's speech is held back unless they say otherwise.</b> Sometimes the exact words
/// are the bug — a mishearing is reproduced by what was misheard — and that trade is theirs to
/// make, per incident. The default is out.
/// </item>
/// <item>
/// <b>Somebody else's words never travel</b>, and there is no switch for it. A re-voiced in-game
/// message is another player's sentence, which is the same rule the journal half applies to
/// <c>ReceiveText</c> — a donor cannot consent on another player's behalf. The line's shape stays
/// so the report can still say a message arrived and when.
/// </item>
/// </list>
/// <para>
/// <b>The pseudonyms cross over from the journal half.</b> Nothing else would make them worth
/// having: a scrubbed <c>LoadGame</c> three lines above <c>Settings now read for Commander JOHN
/// DEPARAGON (F735466)</c> has protected nothing. This is a substitution over free text rather
/// than a field rule, which is why <see cref="Pseudonyms.Replacements"/> is ordered longest-first.
/// </para>
/// </summary>
public static partial class LogScrub
{
    /// <summary>What a withheld sentence is replaced by, in the shape the log line had.</summary>
    public const string Withheld = "[withheld]";

    /// <summary>
    /// The speakers that are d47 rather than somebody in the game: its own name, and the roles it
    /// casts voices for. Anything else on a <c>… said:</c> line is a station, an NPC or another
    /// Commander, whose sentence arrived over the air.
    /// </summary>
    private static readonly HashSet<string> OwnVoices =
        new(Enum.GetNames<VoiceRole>().Append("D47"), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The two lines that write down what the Commander said. Matched on the prefix the logging
    /// call writes, so a reworded message is a match that stops rather than one that silently
    /// starts passing speech through — which is why the report prints the count either way.
    /// </summary>
    private static readonly string[] CommanderSpeech =
    [
        "Heard: ",
        "Not addressed to me: ",
    ];

    /// <summary>
    /// A re-voiced in-game message on its way to being spoken. <c>message.</c> is the key prefix
    /// <c>IncomingMessages</c> builds, so this is d47's own classification read back rather than a
    /// second opinion about what a line is.
    /// </summary>
    private const string InGameCallout = "Callout message.";

    /// <summary>
    /// Splits the file into entries. A line that does not open with a timestamp belongs to the
    /// entry above it; anything before the first timestamp is dropped, which is the fragment
    /// <c>LogTail</c> leaves when it reads a window off the end of a large file.
    /// </summary>
    public static IReadOnlyList<LogEntry> Parse(string log)
    {
        var entries = new List<LogEntry>();
        var text = new System.Text.StringBuilder();
        TimeOnly at = default;
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

                at = TimeOnly.ParseExact(head.Groups["at"].Value, "HH:mm:ss", CultureInfo.InvariantCulture);
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
    /// <param name="also">
    /// Literal substitutions the host supplies, longest first. It is where the Windows account
    /// name goes: the log names it on every path it prints, dozens of times in a startup, and a
    /// show step that has to catch each one by eye is a show step that will miss one.
    /// </param>
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
    /// component, and who it was from. That the message arrived, and when, is d47's half of the
    /// story and is often the whole defect; the words are somebody else's.
    /// <para>
    /// It finds the message the same way <see cref="Parse"/> did, rather than searching the whole
    /// entry: the component before it is a type name and the sentence after it may contain
    /// anything, including the word this is looking for.
    /// </para>
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
    /// The template <c>LoggingSetup</c> writes: the bracketed time and level, the component, then
    /// the message. The component cannot contain <c>": "</c> — it is a type name — so the first
    /// one after the bracket is the separator and everything past it is the message, colons and
    /// all.
    /// </summary>
    [GeneratedRegex(@"^\[(?<at>\d{2}:\d{2}:\d{2}) [A-Z]{3}\] [^:\r\n]*: (?<message>.*)$")]
    private static partial Regex Head();

    /// <summary>
    /// <c>&lt;speaker&gt; said: </c>, <b>anchored</b> at the start of the message — which is where
    /// <c>SpeechPipeline</c> writes it, and the reason this is not a search. Unanchored it would
    /// also fire on an announcement that happens to quote somebody, and classify one of d47's own
    /// lines as another player's.
    /// </summary>
    [GeneratedRegex(@"^(?<who>[^:\r\n]{1,80}?) said: ")]
    private static partial Regex Said();
}
