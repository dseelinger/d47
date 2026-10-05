using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>On arrival in a system, the largest unpaid fine or bounty owed to a faction present there (#639).</summary>
public sealed class OutstandingCrimesCallout(OutstandingCrimes crimes) : ICallout
{
    public string Id => "outstanding-crimes";

    public const string KeyPrefix = "outstanding-crimes.";

    public static readonly TimeSpan Cooldown = TimeSpan.FromHours(1);

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        if (context.IsPriming || !crimes.HistoryFolded)
        {
            yield break;
        }

        foreach (var journalEvent in context.Events)
        {
            if (journalEvent.Kind is not ("FSDJump" or "Location"))
            {
                continue;
            }

            var present = journalEvent.Items("Factions")
                .Select(faction => faction.String("Name"))
                .OfType<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (present.Count == 0)
            {
                continue;
            }

            var ship = crimes.FlownShip ?? context.State?.Ship.ShipId;

            if (crimes.Owed(context.State?.Identity.FrontierId, ship)
                    .FirstOrDefault(debt => present.Contains(debt.Faction)) is not { } debt)
            {
                continue;
            }

            var kind = (debt.Fines > 0, debt.Bounties > 0) switch
            {
                (true, true) => "fines and bounties",
                (true, false) => "fines",
                _ => "bounties",
            };

            var where = debt.ShipId is null ? "on foot" : "on this ship";
            var system = journalEvent.Long("SystemAddress")?.ToString(System.Globalization.CultureInfo.InvariantCulture)
                         ?? journalEvent.String("StarSystem")
                         ?? string.Empty;

            yield return new Announcement(
                KeyPrefix + system,
                $"You owe {debt.Faction} {SpokenCredits.Band(debt.Total)} credits in {kind} {where}.")
            {
                Cooldown = Cooldown,
            };
        }
    }
}
