using System.Text.RegularExpressions;

namespace D47.Core.Reminders;

/// <summary>What <see cref="JournalReminderPhrase.Read"/> made of an utterance.</summary>
public abstract record JournalReminderReading
{
    private JournalReminderReading()
    {
    }

    /// <summary>A reminder to store.</summary>
    public sealed record Set(string Sentence, JournalTrigger Trigger, string? Argument) : JournalReminderReading;

    /// <summary>A reminder to cancel, named by words from its sentence.</summary>
    public sealed record Cancel(string Words) : JournalReminderReading;

    /// <summary>A request for a reminder this grammar cannot set, and the reply that says so.</summary>
    public sealed record Declined(string Reply) : JournalReminderReading;
}

/// <summary>The model-free grammar for journal reminders: "remind me to &lt;sentence&gt; &lt;trigger&gt;" and its inverse.</summary>
public static partial class JournalReminderPhrase
{
    /// <summary>Said when "remind me to" arrives with no trigger the journal can fire.</summary>
    public const string NoTrigger =
        "I can only remind you at a moment in the game: when you next dock, when you dock at a station, "
        + "when you arrive in a system, when you're back at your carrier, when your hold is empty or full, "
        + "when a material is full, or next session.";

    /// <summary>Said for a time expression in a run without timers and alarms.</summary>
    public const string NoClock =
        "Reminders follow the game rather than the clock. Ask me for one when you next dock, when you arrive "
        + "in a system, or next session.";

    private static readonly (JournalTrigger Trigger, string Pattern)[] Triggers =
    [
        (JournalTrigger.OwnCarrier,
            @"(?:when|(?:the )?next time) i(?:'m| am)?(?: next)? (?:back (?:at|on|aboard|to)|at|on|aboard|dock (?:at|on)|get (?:back )?to|reach|arrive at|land on) (?:my|our) (?:fleet )?carrier"),
        (JournalTrigger.DockingAt,
            @"(?:when i(?: next)? dock|(?:the )?next time i dock) (?:at|on|with) (?<arg>.+?)"),
        (JournalTrigger.NextDocking,
            @"(?:when i(?: next)? dock|(?:the )?next time i dock)"),
        (JournalTrigger.HoldEmpty,
            @"when (?:my|the) (?:cargo hold|hold|cargo) is empty"),
        (JournalTrigger.HoldFull,
            @"when (?:my|the) (?:cargo hold|hold|cargo) is full"),
        (JournalTrigger.ArrivalIn,
            @"(?:when i(?: next)?|(?:the )?next time i) (?:get to|arrive in|arrive at|reach) (?<arg>.+?)"),
        (JournalTrigger.MaterialFull,
            @"when (?:my |the )?(?<arg>[a-z][a-z' -]*?) (?:is|are) full"),
        (JournalTrigger.NextSession,
            @"(?:(?:at |in )?(?:the start of )?(?:my |the )?next session|(?:the )?next time i (?:play|log in|log on)|when i next (?:play|log in|log on))"),
    ];

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    /// <summary>"remind me (to) &lt;sentence&gt; &lt;trigger&gt;", one per trigger, in the order they are tried.</summary>
    private static readonly (JournalTrigger Trigger, Regex Regex)[] After =
    [
        .. Triggers.Select(trigger => (trigger.Trigger, new Regex(
            $@"^remind me (?:(?<to>to|about|of) )?(?<sentence>.+?),? {trigger.Pattern}$", Options))),
    ];

    /// <summary>"&lt;trigger&gt;, remind me (to) &lt;sentence&gt;".</summary>
    private static readonly (JournalTrigger Trigger, Regex Regex)[] Before =
    [
        .. Triggers.Select(trigger => (trigger.Trigger, new Regex(
            $@"^{trigger.Pattern},? remind me (?:(?<to>to|about|of) )?(?<sentence>.+)$", Options))),
    ];

    /// <summary>The utterance read as a journal reminder, or null when it is not one this grammar takes.</summary>
    /// <param name="timersRegistered">Whether timers and alarms are registered, which leave a time expression to the model.</param>
    public static JournalReminderReading? Read(string? input, bool timersRegistered)
    {
        var said = Normalise(input);

        if (said.Length == 0)
        {
            return null;
        }

        if (CancelPattern().Match(said) is { Success: true } cancel)
        {
            return new JournalReminderReading.Cancel(Clean(cancel.Groups["words"].Value));
        }

        foreach (var (trigger, regex) in After.Concat(Before))
        {
            if (regex.Match(said) is not { Success: true } match)
            {
                continue;
            }

            var sentence = Sentence(match);

            if (sentence.Length == 0)
            {
                continue;
            }

            var argument = match.Groups["arg"] is { Success: true } arg ? Argument(trigger, arg.Value) : null;

            if (JournalReminder.NeedsArgument(trigger) && string.IsNullOrEmpty(argument))
            {
                continue;
            }

            return new JournalReminderReading.Set(sentence, trigger, argument);
        }

        if (!AskedPattern().IsMatch(said))
        {
            return null;
        }

        if (TimePattern().IsMatch(said))
        {
            return timersRegistered ? null : new JournalReminderReading.Declined(NoClock);
        }

        return new JournalReminderReading.Declined(NoTrigger);
    }

    /// <summary>One line, typographic apostrophes straightened, no leading courtesy and no closing punctuation.</summary>
    private static string Normalise(string? input)
    {
        var said = Whitespace().Replace((input ?? string.Empty).Replace('’', '\''), " ").Trim();
        said = said.TrimEnd('.', '!', '?', ' ');

        return Courtesy().Replace(said, string.Empty).Trim();
    }

    private static string Sentence(Match match)
    {
        var sentence = Clean(match.Groups["sentence"].Value);
        var to = match.Groups["to"].Value.ToLowerInvariant();

        return to is "about" or "of" && sentence.Length > 0 ? $"{to} {sentence}" : sentence;
    }

    private static string? Argument(JournalTrigger trigger, string value)
    {
        var argument = Clean(value);

        if (trigger == JournalTrigger.ArrivalIn)
        {
            argument = SystemWords().Replace(argument, string.Empty).Trim();
        }

        return argument.Length == 0 ? null : argument;
    }

    private static string Clean(string value) => value.Trim().Trim(',', ';', ':', ' ');

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^(?:please,? |hey,? |ok,? |okay,? )|,? please$", Options)]
    private static partial Regex Courtesy();

    [GeneratedRegex(@"^(?:the )|(?: system)$", Options)]
    private static partial Regex SystemWords();

    [GeneratedRegex(
        @"^(?:(?:cancel|forget|delete|remove|drop|clear) (?:the |my |that )?(?:journal )?reminder (?:to|about|for|of)|(?:don't|do not) remind me (?:to|about|of)) (?<words>.+)$",
        Options)]
    private static partial Regex CancelPattern();

    /// <summary>"remind me to", which asks for a reminder; "remind me what…" asks a question and is left alone.</summary>
    [GeneratedRegex(@"(?:^|, )remind me (?:to|in|at|after|tomorrow|tonight)\b", Options)]
    private static partial Regex AskedPattern();

    [GeneratedRegex(
        @"\b(?:(?:in|after|for) (?:\S+ ){0,3}(?:seconds?|minutes?|mins?|hours?|hrs?)|at \d{1,2}(?::\d{2})?|at (?:one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|noon|midnight)|o'clock|tomorrow|tonight|this (?:morning|afternoon|evening))\b",
        Options)]
    private static partial Regex TimePattern();
}
