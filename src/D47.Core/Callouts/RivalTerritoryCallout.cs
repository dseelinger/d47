using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Flying in a rival Power's space (list.md Phase 15, "Warn that you are exposed in a rival Power's
/// territory").
/// <para>
/// <b>A standing condition rather than an event.</b> It announces on the edge into the condition
/// and is then silent for as long as it holds — the Commander is told once that where they are is
/// hostile, not reminded of it every ten seconds until they leave.
/// </para>
/// <para>
/// <b>Not on arrival, deliberately.</b> Arriving is a supercruise state and nothing can reach you
/// there: across every Power security contact in the 912-journal corpus, 0% happened in supercruise
/// against 67% in normal space. So the condition requires <see cref="FlightMode.Normal"/>, and the
/// edge is dropping out of supercruise or undocking rather than the jump itself.
/// </para>
/// <para>
/// <b>What this cannot be.</b> The thing actually wanted is a warning when a Power Security ship
/// appears in the contacts panel, and no third-party app can see that — across the 221 event types
/// Elite has ever written there is no contact, spawn or proximity event, and every signal about
/// another ship requires it to have already acted.
/// <c>$PowersSecurity_OnAttackStart*</c> is real and names the Power, but was measured at a median
/// of one second before the shot, which is confirmation rather than warning. This is the reachable
/// half, and it says what it knows — that you are exposed — rather than implying something is near.
/// See docs/spikes/journal-corpus-warnings.md.
/// </para>
/// </summary>
public sealed class RivalTerritoryCallout : ICallout
{
    public string Id => "rival-territory";

    /// <summary>
    /// How long an attack keeps this quiet after the last sign of it.
    /// <para>
    /// "Be careful, you are in enemy territory" arriving as a pirate opens fire is worse than
    /// silence, so anything on the danger path outranks this. A minute is long enough to cover the
    /// gap between a shot landing and the next one, and the suppression <em>drops</em> the warning
    /// rather than deferring it — a territory warning delivered after the fight is over is not a
    /// warning either.
    /// </para>
    /// </summary>
    public TimeSpan DangerHold { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Long enough that circling a station does not repeat it, and keyed per system so moving into
    /// a <em>different</em> rival Power's space says so immediately.
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);

    private bool _wasExposed;
    private DateTimeOffset? _quietUntil;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (IsUnderThreat(context))
        {
            _quietUntil = context.Now + DangerHold;
        }

        var state = context.State;
        var location = state?.Location;

        // Normal space and nowhere else. Docked, landed, on foot and in supercruise are all
        // places a Power's security cannot open fire on the Commander, and a warning that fires
        // in them is a warning about nothing.
        var exposed =
            location is { Mode: FlightMode.Normal }
            && state!.Pledge.IsRival(location.ControllingPower);

        var entered = exposed && !_wasExposed;
        _wasExposed = exposed;

        // Folded during priming and announced never: the backlog is where the Commander already
        // is, and reading it out at startup would announce a condition they entered an hour ago.
        if (!entered || context.IsPriming)
        {
            yield break;
        }

        if (_quietUntil is { } quiet && context.Now < quiet)
        {
            yield break;
        }

        yield return new Announcement(
            $"territory.rival.{location!.StarSystem}",
            $"{location.ControllingPower} controls this system, and you fly for "
            + $"{state!.Pledge.Power}. You are exposed here.")
        {
            Cue = AlertCue.RivalTerritory,
            Cooldown = Cooldown,
        };
    }

    /// <summary>
    /// Whether something is already shooting, or about to. Read from the same two sources the
    /// danger warnings are — Status.json for the conditions, the journal for the transitions —
    /// plus the announced-attack allowlist, so "on the danger path" means one thing rather than
    /// two things that have to agree.
    /// </summary>
    private static bool IsUnderThreat(CalloutContext context)
    {
        var status = context.Status;

        if (status.IsKnown && status.InShip)
        {
            // Elite's own in-danger flag, plus the two conditions that mean something has already
            // reached the Commander. Shields down is not proof of an attack on its own — they can
            // be knocked out by a star — but this is deciding whether to stay quiet, and staying
            // quiet is the cheap mistake.
            if (status.Has(StatusFlags.InDanger)
                || status.Has(StatusFlags.BeingInterdicted)
                || !status.ShieldsUp)
            {
                return true;
            }
        }

        foreach (var journalEvent in context.Events)
        {
            switch (journalEvent.Kind)
            {
                case "UnderAttack" or "HullDamage" or "Interdicted" or "Died":
                    return true;

                case "ShieldState" when !journalEvent.Bool("ShieldsUp"):
                    return true;

                // The other half of this phase. An announced attack has not landed yet, which is
                // exactly the moment a remark about territory would be worst.
                case "ReceiveText" when AnnouncedAttackCallout.Read(journalEvent) is not null:
                    return true;
            }
        }

        return false;
    }
}
