using D47.Core.Audio;
using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// Flying in a rival Power's space (Phase 15, "Warn that you are exposed in a rival Power's
/// territory").
/// </summary>
public sealed class RivalTerritoryCallout : ICallout
{
    public string Id => "rival-territory";

    /// <summary>How long an attack keeps this quiet after the last sign of it.</summary>
    public TimeSpan DangerHold { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Long enough that circling a station does not repeat it, and keyed per system so moving into a
    /// different rival Power's space says so immediately.
    /// </summary>
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);

    /// <summary>The local day the full explanation last played, and how to remember a new one.</summary>
    public Func<string?>? LastExplainedDay { get; set; }

    public Action<string>? RememberExplainedDay { get; set; }

    private string? _sessionExplainedDay;

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

        // Normal space and nowhere else.
        var exposed =
            location is { Mode: FlightMode.Normal }
            && state!.Pledge.IsRival(location.ControllingPower);

        var entered = exposed && !_wasExposed;
        _wasExposed = exposed;

        // Folded during priming and announced never: the backlog is where the Commander already is, and
        // reading it out at startup would announce a condition they entered an hour ago.
        if (!entered || context.IsPriming)
        {
            yield break;
        }

        if (_quietUntil is { } quiet && context.Now < quiet)
        {
            yield break;
        }

        var power = location!.ControllingPower!;

        // The Commander's local day, off the injected clock — no Core component reads one.
        var today = context.Now.LocalDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var full = !string.Equals(LastExplainedDay is { } last ? last() : _sessionExplainedDay, today, StringComparison.Ordinal);

        if (full)
        {
            if (RememberExplainedDay is { } remember)
            {
                remember(today);
            }
            else
            {
                _sessionExplainedDay = today;
            }
        }

        var text = full
            ? $"{SaidName(power)} controls this system, and you fly for {SaidName(state!.Pledge.Power!)}. You are exposed here."
            : "Hostile territory. Be on guard.";

        yield return new Announcement($"territory.rival.{location.StarSystem}", text)
        {
            Cue = AlertCue.RivalTerritory,
            Cooldown = Cooldown,
        };
    }

    /// <summary>"A.</summary>
    private static string SaidName(string power) =>
        power.Length > 3 && char.IsAsciiLetterUpper(power[0]) && power[1] == '.' && power[2] == ' '
            ? power[3..]
            : power;

    /// <summary>Whether something is already shooting, or about to.</summary>
    private static bool IsUnderThreat(CalloutContext context)
    {
        var status = context.Status;

        if (status.IsKnown && status.InShip)
        {
            // Elite's own in-danger flag, plus the two conditions that mean something has already reached the
            // Commander.
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

                // The other half of this phase.
                case "ReceiveText" when AnnouncedAttackCallout.Read(journalEvent) is not null:
                    return true;
            }
        }

        return false;
    }
}
