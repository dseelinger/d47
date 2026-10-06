using D47.Core.Logbook;

namespace D47.Core.Callouts;

/// <summary>One line after the opening line, recapping the last complete session.</summary>
public sealed class RecapCallout : ICallout
{
    public const string KeyPrefix = "recap.";

    public const string Key = KeyPrefix + "last-session";

    /// <summary>How long after the first live tick the line waits; longer than the opening line's.</summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(12);

    /// <summary>How long after the first live tick a line still being prepared is given up on.</summary>
    public TimeSpan GiveUp { get; set; } = TimeSpan.FromMinutes(1);

    private readonly object _gate = new();
    private DateTimeOffset _firstLiveTick;
    private bool _said;
    private bool _ready;
    private string? _line;

    public string Id => "recap";

    /// <summary>Whether the core aboard is a stock core; the line falls due and is not said.</summary>
    public Func<bool> StockCoreAboard { get; set; } = () => false;

    /// <summary>
    /// Asked once, on the first live tick, to prepare the line for the session that started before the
    /// instant given. Must not block: the caller answers later through <see cref="Supply"/>.
    /// </summary>
    public Action<DateTimeOffset>? Prepare { get; set; }

    /// <summary>The prepared line, or null for nothing to say. Safe from any thread.</summary>
    public void Supply(string? line)
    {
        lock (_gate)
        {
            _line = line;
            _ready = true;
        }
    }

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || _said)
        {
            yield break;
        }

        if (_firstLiveTick == default)
        {
            _firstLiveTick = context.Now;
            Prepare?.Invoke(context.State?.Session.StartedAt ?? context.Now);
            yield break;
        }

        var waited = context.Now - _firstLiveTick;

        if (waited < Settle)
        {
            yield break;
        }

        string? line;

        lock (_gate)
        {
            if (!_ready && waited < GiveUp)
            {
                yield break;
            }

            line = _line;
        }

        _said = true;

        if (string.IsNullOrWhiteSpace(line) || StockCoreAboard())
        {
            yield break;
        }

        yield return new Announcement(Key, line)
        {
            Urgency = CalloutUrgency.Routine,

            // Said once per launch; the flag above is the cooldown.
            Cooldown = TimeSpan.Zero,
        };
    }

    /// <summary>
    /// The plain line for a digest of the last session — where it ended, the ship, and its most notable
    /// mishap or progress — or null when it holds nothing beyond its opening and closing.
    /// </summary>
    public static string? Compose(LogDigest digest)
    {
        ArgumentNullException.ThrowIfNull(digest);

        if (!digest.Facts.Any(fact => fact.Kind is not (LogFactKind.Opening or LogFactKind.Closing)))
        {
            return null;
        }

        var closing = digest.Facts.LastOrDefault(fact => fact.Kind == LogFactKind.Closing);
        var notable = digest.Facts
            .Where(fact => !fact.Aggregate && fact.Kind is LogFactKind.Mishap or LogFactKind.Progress)
            .OrderBy(fact => fact.Kind == LogFactKind.Mishap ? 0 : 1)
            .ThenBy(fact => fact.At)
            .FirstOrDefault();

        var parts = new List<string>();

        if (closing is not null)
        {
            parts.Add($"you {Clause(closing.Statement)}" + (digest.Ship is { } ship ? $" in {ship}" : string.Empty));
        }
        else if (digest.Ship is { } ship)
        {
            parts.Add($"you flew {ship}");
        }

        if (notable is not null)
        {
            parts.Add($"{(parts.Count > 0 ? "and earlier you " : "you ")}{Clause(notable.Statement)}");
        }

        return parts.Count == 0 ? null : $"Last session {string.Join(", ", parts)}.";
    }

    /// <summary>
    /// A fact's statement as a clause after "you": first letter lowered, "Was" made "were", final full
    /// stop dropped.
    /// </summary>
    private static string Clause(string statement)
    {
        var text = statement.Trim().TrimEnd('.');

        if (text.StartsWith("Was ", StringComparison.Ordinal))
        {
            return "were " + text[4..];
        }

        return text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];
    }
}
