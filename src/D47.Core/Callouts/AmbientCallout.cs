using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A remark, now and then, about where the Commander actually is (list.md Phase 11, "Ambient
/// Voice").
/// <para>
/// Everything else in Phase 8 speaks because something happened. This speaks because nothing
/// has, which makes the timing the whole of the design. Three rules keep it from being noise:
/// it waits out an interval, it waits for the situation to have settled rather than firing on
/// the transition, and it never repeats a situation twice running.
/// </para>
/// <para>
/// Silent while personality is off. The checklist puts "no ambient remarks" in that item's own
/// acceptance criteria, which makes this the one callout the personality switch reaches.
/// </para>
/// <para>
/// Emits the stock line. The app replaces it with a model-written one in the core's own voice
/// when there is a model, the same way the carrier lines are varied — which is why the
/// situation travels on the announcement rather than only the text.
/// </para>
/// </summary>
public sealed class AmbientCallout : ICallout
{
    public string Id => "ambient";

    public const string KeyPrefix = "ambient.";

    /// <summary>Off means no unprompted remarks, whatever else is enabled.</summary>
    public Func<bool> Enabled { get; set; } = () => true;

    /// <summary>
    /// The shortest gap between two remarks. A Commander who wants a talkative companion turns
    /// it down; one who wants a quiet one turns it up. Zero silences it entirely.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long a situation has to hold before it is worth remarking on. Without this, a remark
    /// about being docked arrives as the Commander is lifting off — Status.json flips several
    /// times a minute during an approach, and the ambient line is the slowest thing in the app.
    /// </summary>
    public TimeSpan Settle { get; set; } = TimeSpan.FromSeconds(90);

    private AmbientSituation _situation = AmbientSituation.None;
    private AmbientSituation _lastSpoken = AmbientSituation.None;
    private DateTimeOffset _situationSince;
    private DateTimeOffset _lastSpokenAt;
    private int _picks;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var situation = AmbientLines.Situate(context.Status);

        if (situation != _situation)
        {
            _situation = situation;
            _situationSince = context.Now;
        }

        // Priming folds the backlog. An ambient remark from an hour ago is not ambient.
        if (context.IsPriming || !Enabled() || Interval <= TimeSpan.Zero || situation == AmbientSituation.None)
        {
            yield break;
        }

        // The first tick of a session has _lastSpokenAt at default, which is far enough in the
        // past that the interval passes immediately — so it is seeded here rather than special
        // cased, and the Commander gets silence for one interval after launch rather than a
        // remark while they are still reading the panel.
        if (_lastSpokenAt == default)
        {
            _lastSpokenAt = context.Now;
            yield break;
        }

        if (context.Now - _situationSince < Settle || context.Now - _lastSpokenAt < Interval)
        {
            yield break;
        }

        // Never the same situation twice running. Two remarks about being in supercruise, an
        // interval apart, is the point at which ambient stops sounding like company.
        if (situation == _lastSpoken)
        {
            yield break;
        }

        if (AmbientLines.Pick(situation, _picks) is not { } line)
        {
            yield break;
        }

        _picks++;
        _lastSpoken = situation;
        _lastSpokenAt = context.Now;

        yield return new Announcement($"{KeyPrefix}{situation}".ToLowerInvariant(), line)
        {
            // Routine, and it never silences anything. An ambient remark that talked over a
            // fuel warning would be the worst possible trade.
            Urgency = CalloutUrgency.Routine,
            Cooldown = Interval,
        };
    }

    /// <summary>
    /// What the last emitted line was about, so the app can ask the model for one in character.
    /// Read straight after <see cref="Examine"/> yields, which is the only moment it is
    /// meaningful.
    /// </summary>
    public AmbientSituation LastSituation => _lastSpoken;
}
