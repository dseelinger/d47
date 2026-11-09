namespace D47.Core.Journal;

/// <summary>Follows whether the Commander is in the Nomad, from the journal alone.</summary>
public sealed class NomadTracker
{
    private const string NomadVessel = "lander01";

    private long? _id;

    public bool Aboard { get; private set; }

    public void Observe(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "LaunchVessel" when journalEvent.LaunchesSrv:
                Aboard = journalEvent.String("VesselType") == NomadVessel;
                _id = Aboard ? journalEvent.Long("ID") : null;
                break;

            case "LaunchSRV" or "DockSRV" or "SRVDestroyed" or "Died" or "LoadGame":
                Aboard = false;
                _id = null;
                break;

            case "Embark" when journalEvent.Bool("SRV"):
                Aboard = Aboard && journalEvent.Long("ID") == _id;
                break;

            case "Embark":
                Aboard = false;
                _id = null;
                break;
        }
    }
}
