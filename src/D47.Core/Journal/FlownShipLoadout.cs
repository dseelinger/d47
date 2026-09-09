using System.Globalization;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// Asking what is fitted on the ship the Commander is flying, with the remembered loadout behind the
/// live one and a warning where neither can answer (#337).
/// </summary>
public static class FlownShipLoadout
{
    /// <summary>
    /// Whether a module family is fitted on the ship being flown, from the live loadout where there is
    /// one and the remembered loadout otherwise — and null, logged once a session, where there is still
    /// no evidence either way.
    /// </summary>
    /// <param name="asking">
    /// The journal event that led to the question — <c>FSDJump</c>, <c>Location</c> — so a warning says
    /// what d47 was about to speak about rather than only which callout was running.
    /// </param>
    /// <param name="logger">
    /// Null where the caller has none composed, which is the case under the designer and in tests that
    /// are not about the warning.
    /// </param>
    public static bool? Fitted(
        this CommanderGameState state,
        string family,
        string asking,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(state);

        var answer = state.FlownShip.Fitted(family);

        if (answer is null)
        {
            Silent(state, family, asking, logger);
        }

        return answer;
    }

    /// <summary>
    /// The arrival that led to the question — <c>FSDJump</c>, <c>Location</c>, <c>CarrierJump</c> — or
    /// <paramref name="otherwise"/> on the ticks that carry none, which is most of them.
    /// </summary>
    public static string Asked(IReadOnlyList<JournalEvent> events, string otherwise)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events.LastOrDefault(journalEvent =>
            journalEvent.Kind is "FSDJump" or "Location" or "CarrierJump")?.Kind ?? otherwise;
    }

    /// <summary>
    /// Records that nothing — the journal, nor the ships file — can say what is fitted on the ship
    /// being flown.
    /// </summary>
    private static void Silent(
        CommanderGameState state,
        string family,
        string asking,
        ILogger? logger)
    {
        if (logger is null || !state.OweLoadoutSilenceWarning())
        {
            return;
        }

        var shipId = state.FlownShip.ShipId ?? state.Ship.ShipId;

        logger.LogWarning(
            "No loadout for ship {ShipId}: {Asking} asked whether {Family} is fitted and neither "
            + "the journal nor the remembered loadouts can say. Nothing will be said about it.",
            shipId?.ToString(CultureInfo.InvariantCulture) ?? "unknown",
            asking,
            family);
    }
}
