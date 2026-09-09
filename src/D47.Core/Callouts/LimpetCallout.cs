using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// A reminder to buy limpets, when docking somewhere that sells them with a big hold and few aboard
/// (asked for 2026-08-21).
/// </summary>
public sealed class LimpetCallout : ICallout
{
    public string Id => "limpets";

    public const string Key = "limpets.low";

    /// <summary>Elite's own name for a limpet.</summary>
    public const string Limpet = "drones";

    /// <summary>The service that gates Advanced Maintenance, and therefore limpets.</summary>
    public const string Service = "rearm";

    /// <summary>
    /// The smallest hold worth reminding about, in tonnes, and the limpet threshold as a percentage of
    /// that hold's capacity — the Commander's ruling, 2026-08-21.
    /// </summary>
    public Func<int> Floor { get; set; } = () => 64;

    public Func<int> Percent { get; set; } = () => 5;

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.State is not { } state || context.IsPriming)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind != "Docked" || !Sells(journalEvent))
            {
                continue;
            }

            if (state.Ship.CargoCapacity is not { } capacity || capacity <= Floor())
            {
                continue;
            }

            // Not merely empty: the hold has to have been read at all.
            if (!state.Hold.IsKnown || !state.Hold.IsShip)
            {
                continue;
            }

            var held = state.Hold.Of(Limpet);

            // Percent of capacity, stated as a multiplication so no rounding is invented: held×100 against
            // capacity×percent is the same comparison with none of the division.
            if (held * 100 >= capacity * Percent())
            {
                continue;
            }

            yield return new Announcement(Key, Said(held, capacity, Silencing(capacity)))
            {
                // Long, because it is about a place rather than a moment, and a Commander who docks twice in
                // ten minutes has not forgotten.
                Cooldown = TimeSpan.FromMinutes(20),
            };
        }
    }

    private static bool Sells(JournalEvent journalEvent) =>
        journalEvent.Items("StationServices")
            .Any(service => string.Equals(service.GetString(), Service, StringComparison.OrdinalIgnoreCase));

    /// <summary>The number of limpets that silences this callout (#140).</summary>
    private int Silencing(int capacity) => ((capacity * Percent()) + 99) / 100;

    /// <summary>The line says the number, which is the whole of #140.</summary>
    private static string Said(int held, int capacity, int silencing) =>
        held == 0
            ? $"No limpets aboard, and this station sells them. You have {Tonnes(capacity)} to fill. "
              + $"Buy {silencing} and I'll stop asking."
            : $"{Tonnes(held)} of limpets against {Tonnes(capacity)} of hold. This station sells them. "
              + $"{silencing} aboard silences me — {silencing - held} more.";

    private static string Tonnes(int count) => count == 1 ? "1 tonne" : $"{count} tonnes";
}
