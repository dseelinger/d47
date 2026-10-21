using System.Text.RegularExpressions;
using D47.Core.Conversation;

namespace D47.Core.CaptainsLog;

/// <summary>What <see cref="CaptainsLogPhrase.Read"/> made of an utterance.</summary>
public abstract record CaptainsLogReading
{
    private CaptainsLogReading()
    {
    }

    /// <summary>An entry to save, in the words as heard.</summary>
    public sealed record Entry(string Text) : CaptainsLogReading;

    /// <summary>The opening phrase with nothing after it.</summary>
    public sealed record Empty : CaptainsLogReading;

    /// <summary>Entries to read back: <paramref name="Span"/> is last, today, yesterday, week, session or null for all.</summary>
    public sealed record Find(string? Span, string? System, string? Words) : CaptainsLogReading;

    /// <summary>How many entries there are.</summary>
    public sealed record Count : CaptainsLogReading;

    /// <summary>The newest entry, to delete.</summary>
    public sealed record DeleteLast : CaptainsLogReading;

    /// <summary>Every entry, to write to a file.</summary>
    public sealed record ExportAll : CaptainsLogReading;
}

/// <summary>The model-free grammar for "captain's log, …" and "log entry, …".</summary>
public static partial class CaptainsLogPhrase
{
    /// <summary>Said when the opening phrase arrives with no entry; the next utterance is the entry.</summary>
    public const string GoAhead = "Go ahead.";

    /// <summary>Said when an entry is empty.</summary>
    public const string NoEntry = "Say the entry after 'captain's log'.";

    /// <summary>The utterance read as a log entry or a request about the entries, or null when it is neither.</summary>
    public static CaptainsLogReading? Read(string? input)
    {
        var said = SpokenOpeners.Strip(Whitespace().Replace((input ?? string.Empty).Replace('’', '\''), " ").Trim());

        if (ReadBack(said) is { } readBack)
        {
            return readBack;
        }

        if (Opening().Match(said) is not { Success: true } match)
        {
            return null;
        }

        var text = said[match.Length..].TrimStart(',', ':', ' ').TrimEnd();

        return text.Length == 0 ? new CaptainsLogReading.Empty() : new CaptainsLogReading.Entry(text);
    }

    private static CaptainsLogReading? ReadBack(string said)
    {
        var plain = said.TrimEnd('.', '?', '!', ' ');

        if (DeleteLastPattern().IsMatch(plain))
        {
            return new CaptainsLogReading.DeleteLast();
        }

        if (ExportPattern().IsMatch(plain))
        {
            return new CaptainsLogReading.ExportAll();
        }

        if (CountPattern().IsMatch(plain))
        {
            return new CaptainsLogReading.Count();
        }

        if (LastPattern().IsMatch(plain))
        {
            return new CaptainsLogReading.Find("last", null, null);
        }

        if (SpanPattern().Match(plain) is { Success: true } span)
        {
            var named = span.Groups["span"].Value.ToLowerInvariant().Replace("this ", string.Empty, StringComparison.Ordinal);

            return new CaptainsLogReading.Find(named.Replace("'s", string.Empty, StringComparison.Ordinal), null, null);
        }

        if (FilterPattern().Match(plain) is { Success: true } filter)
        {
            var value = filter.Groups["value"].Value.Trim();

            return value.Length == 0
                ? null
                : filter.Groups["kind"].Value.ToLowerInvariant() == "about"
                    ? new CaptainsLogReading.Find(null, null, value)
                    : new CaptainsLogReading.Find(null, value, null);
        }

        return null;
    }

    private const string Entries = @"(?:captain'?s?'? )?log entr(?:y|ies)\b";

    [GeneratedRegex(@"^(?:delete|remove) (?:my|the) last " + Entries, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeleteLastPattern();

    [GeneratedRegex(@"^export (?:my|the|all my|all the) " + Entries + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExportPattern();

    [GeneratedRegex(@"^how many " + Entries, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CountPattern();

    [GeneratedRegex(@"^read (?:me )?(?:my|the) last " + Entries + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LastPattern();

    [GeneratedRegex(
        @"^read (?:me )?(?<span>today'?s|yesterday'?s|this week'?s|this session'?s) " + Entries + "$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SpanPattern();

    [GeneratedRegex(
        @"^(?:read (?:me )?)?(?:captain'?s?'? )?log entries (?<kind>from|in|about) (?<value>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FilterPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>"captain's log" and its transcriptions, or "log entry"; "captain's log entries" is not one.</summary>
    [GeneratedRegex(
        @"^(?:(?:captain'?s?|captains'?) log\b(?! entries\b)(?: entry\b)?|log entry\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Opening();
}
