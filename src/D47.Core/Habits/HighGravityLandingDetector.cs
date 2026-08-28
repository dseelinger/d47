using D47.Core.Journal;

namespace D47.Core.Habits;

/// <summary>
/// Landing somewhere heavy (Phase 32) — and <b>the detector that ships having found
/// nothing</b>.
/// <para>
/// The Commander named high-gravity landings as something they get wrong. Across 914 journals: 477
/// of 532 touchdowns under half a g, seven between half and one, and <b>none at all above one</b>.
/// There is no habit here, and there is no honest way to manufacture one.
/// </para>
/// <para>
/// <b>So it ships anyway, and says so.</b> Item 2's other half — "a new Commander gets silence and
/// an explanation, not a confident habit derived from a fortnight" — has no natural test case in a
/// thirteen-month corpus where everything else has hundreds of samples. This is that case, for
/// real: the detector runs, does the join, and reports that it has not seen the Commander land
/// anywhere heavy enough to say. A Commander told "nothing to report" when the truth is "not enough
/// of you yet" has been told something untrue about themselves, which is the specific failure the
/// item says makes them stop believing the true ones.
/// </para>
/// <para>
/// <b>The gravity join is resolved at the end rather than as it goes.</b> A body's surface gravity
/// arrives in a <c>Scan</c>, and nothing guarantees that scan precedes the landing on it —
/// discovery and return visits are months apart in this corpus. Landings are collected and the
/// gravity is attached once the whole corpus has been read, which is the one thing
/// <see cref="HabitDetector.Resolve"/> exists for.
/// </para>
/// </summary>
public sealed class HighGravityLandingDetector : HabitDetector
{
    /// <summary>Earth's, in the units Elite reports. What <c>SurfaceGravity</c> is divided by.</summary>
    public const double Standard = 9.80665;

    /// <summary>Where "heavy" starts. One g is where a Commander starts noticing the ground arrive.</summary>
    public const double Heavy = 1.0;

    private readonly Dictionary<string, double> _gravity = new(StringComparer.Ordinal);
    private readonly List<(DateTimeOffset At, string Body)> _landings = [];

    private double _heaviest;

    public override string Key => "high-gravity";

    public override string Subject => "landing on heavy worlds";

    protected override string Observation => "You have put a ship down somewhere over one g";

    protected override HabitOccasion Occasion => HabitOccasion.TouchingDown;

    protected override string Denominator => "landings on bodies you had scanned";

    public override void Observe(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "Scan":
                if (journalEvent.Raw.String("BodyName") is { } scanned &&
                    journalEvent.Raw.Double("SurfaceGravity") is { } gravity)
                {
                    _gravity[scanned] = gravity / Standard;
                }

                return;

            case "Touchdown":
                // Body on the modern event, BodyName on the older ones. Both spellings are in this
                // corpus, which is the join scan_join.py exists to catch.
                if ((journalEvent.Raw.String("Body") ?? journalEvent.Raw.String("BodyName")) is { } landed)
                {
                    _landings.Add((journalEvent.Timestamp, landed));
                }

                return;

            default:
                return;
        }
    }

    protected override IReadOnlyList<DateTimeOffset> Resolve()
    {
        var heavy = new List<DateTimeOffset>();
        var judged = 0;

        foreach (var (at, body) in _landings)
        {
            if (!_gravity.TryGetValue(body, out var gravity))
            {
                // An unscanned body cannot be judged, so it is not a chance to have got this wrong
                // either. Counting it in the denominator would dilute a rate with unknowns.
                continue;
            }

            judged++;
            _heaviest = Math.Max(_heaviest, gravity);

            if (gravity >= Heavy)
            {
                heavy.Add(at);
            }
        }

        // Assigned rather than incremented, so concluding twice from one instance cannot count the
        // same landing twice — the one place in the phase where the count is not accumulated as it
        // goes.
        Opportunities = judged;

        return heavy;
    }

    protected override string Declined(HabitEvidence evidence)
    {
        if (evidence.Journals < HabitFloor.Journals || evidence.Opportunities < HabitFloor.Opportunities)
        {
            return base.Declined(evidence);
        }

        return evidence.Occurrences == 0
            ? $"you have not landed anywhere over one g — the heaviest of your {evidence.Opportunities} "
                + $"landings was {_heaviest:0.00} g"
            : base.Declined(evidence);
    }
}
