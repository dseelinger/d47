namespace D47.Core.Journal;

/// <summary>Follows whether the Commander is in the Nomad, from the journal alone.</summary>
public sealed class NomadTracker
{
    private const string NomadVessel = "lander01";

    private long? _id;

    private bool _inSrv;

    private long? _srvId;

    public bool Aboard { get; private set; }

    public void Observe(JournalEvent journalEvent)
    {
        if (journalEvent.InSrvAfter is { } inSrv)
        {
            _inSrv = inSrv;
        }

        switch (journalEvent.Kind)
        {
            case "LaunchVessel" when journalEvent.LaunchesSrv:
                Aboard = journalEvent.String("VesselType") == NomadVessel;
                _srvId = journalEvent.Long("ID");
                _id = Aboard ? _srvId : null;
                break;

            case "LaunchSRV" or "DockSRV" or "SRVDestroyed" or "Died" or "LoadGame":
                Aboard = false;
                _id = null;
                _srvId = journalEvent.Kind == "LaunchSRV" ? journalEvent.Long("ID") : null;
                break;

            case "Embark" when journalEvent.Bool("SRV"):
                _srvId = journalEvent.Long("ID");
                Aboard = Aboard && _srvId == _id;
                break;

            case "Embark":
                Aboard = false;
                _id = null;
                _srvId = null;
                break;

            // Only the Nomad flies. In an SRV these events are the ship's, and say PlayerControlled false.
            case "Liftoff" or "Touchdown" when _inSrv && journalEvent.Bool("PlayerControlled"):
                Aboard = true;
                _id = _srvId;
                break;
        }
    }
}
