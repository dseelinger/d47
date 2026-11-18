namespace D47.Core.Journal;

/// <summary>The one rule for what a journal event puts the Commander in.</summary>
public static class CombatVehicles
{
    /// <summary>The vehicle the event puts the Commander in, or null when it changes nothing.</summary>
    public static CombatVehicle? After(JournalEvent journalEvent)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind switch
        {
            "Disembark" or "DropshipDeploy" => CombatVehicle.OnFoot,
            "Embark" when journalEvent.Bool("Taxi") => null,
            "Embark" => journalEvent.Bool("SRV") ? CombatVehicle.Srv : CombatVehicle.Ship,
            "LaunchSRV" or "LaunchVessel" when journalEvent.LaunchesSrv => CombatVehicle.Srv,
            "DockSRV" => CombatVehicle.Ship,
            "LoadGame" => IsSuit(journalEvent.String("Ship")) ? CombatVehicle.OnFoot : CombatVehicle.Ship,
            "Location" when journalEvent.Bool("OnFoot") => CombatVehicle.OnFoot,
            "Location" when journalEvent.Bool("InSRV") => CombatVehicle.Srv,
            _ => null,
        };
    }

    /// <summary>Whether a LoadGame or Loadout ship name is a suit rather than a hull.</summary>
    public static bool IsSuit(string? ship) => ship?.Contains("Suit", StringComparison.OrdinalIgnoreCase) == true;
}
