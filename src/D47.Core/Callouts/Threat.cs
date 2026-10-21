using D47.Core.Journal;

namespace D47.Core.Callouts;

public static class Threat
{
    /// <summary>Whether something is already shooting, or about to.</summary>
    public static bool IsUnder(CalloutContext context)
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
