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
}

/// <summary>The model-free grammar for "captain's log, …" and "log entry, …".</summary>
public static partial class CaptainsLogPhrase
{
    /// <summary>Said when the opening phrase arrives with no entry.</summary>
    public const string NoEntry = "Say the entry after 'captain's log'.";

    /// <summary>The utterance read as a log entry, or null when it does not open with the phrase.</summary>
    public static CaptainsLogReading? Read(string? input)
    {
        var said = SpokenOpeners.Strip(Whitespace().Replace((input ?? string.Empty).Replace('’', '\''), " ").Trim());

        if (Opening().Match(said) is not { Success: true } match)
        {
            return null;
        }

        var text = said[match.Length..].TrimStart(',', ':', ' ').TrimEnd();

        return text.Length == 0 ? new CaptainsLogReading.Empty() : new CaptainsLogReading.Entry(text);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>"captain's log" and its transcriptions, or "log entry"; "captain's log entries" is not one.</summary>
    [GeneratedRegex(
        @"^(?:(?:captain'?s?|captains'?) log\b(?! entries\b)(?: entry\b)?|log entry\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Opening();
}
