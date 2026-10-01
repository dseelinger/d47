using System.Collections.Frozen;
using D47.Core.Journal;

namespace D47.Core.Persona;

/// <summary>What a beacon scan woke.</summary>
public enum CoreWaking
{
    /// <summary>The first beacon: every Guardian core but the Heretic.</summary>
    Cores,

    /// <summary>A second beacon, in another system: the Heretic.</summary>
    Heretic,
}

/// <summary>Which Guardian cores a running story holds back.</summary>
public enum HeldCores
{
    None,

    /// <summary>The Heretic alone: the story has had one beacon scan.</summary>
    Heretic,

    /// <summary>Every Guardian core: the story has had no beacon scan.</summary>
    All,
}

/// <summary>The cores a story holds back, and the story's title.</summary>
public readonly record struct CoreHold(HeldCores Cores, string Story)
{
    public static readonly CoreHold None = new(HeldCores.None, string.Empty);
}

/// <summary>
/// Which Guardian cores can be aboard. Every one can, except those the Commander's running story holds back
/// until its beacon scans.
/// </summary>
public sealed class GuardianCores(Func<CoreHold>? hold = null)
{
    /// <summary>The Guardian beacon systems, by id64 from EDSM.</summary>
    public static readonly FrozenDictionary<long, string> Beacons = new Dictionary<long, string>
    {
        [13872878396833] = "IC 2391 Sector MX-T b3-6",
        [4208161886922] = "Synuefe IL-N c23-15",
        [182443805035] = "Synuefe IT-F d12-5",
        [9476442170745] = "Synuefe KU-F b44-4",
        [9476979041665] = "Synuefe QA-E b45-4",
        [13874757182857] = "Synuefe RL-C b46-6",
        [869621795163] = "NGC 2451A Sector LX-U d2-25",
    }.ToFrozenDictionary();

    /// <summary>Where each of <see cref="Beacons"/> is, from EDSM.</summary>
    public static readonly FrozenDictionary<long, StarPosition> BeaconPositions = new Dictionary<long, StarPosition>
    {
        [13872878396833] = new(582.9375, -72.28125, -19.40625),
        [4208161886922] = new(658.28125, -117.96875, -46.3125),
        [182443805035] = new(739.46875, -140.53125, -13.375),
        [9476442170745] = new(714.34375, -173.84375, -113.59375),
        [9476979041665] = new(738.0625, -174.375, -103.25),
        [13874757182857] = new(727.90625, -157.1875, -67.1875),
        [869621795163] = new(726.0625, -163.5625, -171.78125),
    }.ToFrozenDictionary();

    /// <summary>The beacon system nearest <paramref name="here"/>, measured from Sol when it is unknown.</summary>
    public static (long Address, string Name) NearestBeacon(StarPosition? here)
    {
        var from = here ?? StarPosition.Origin;
        var address = BeaconPositions.MinBy(pair => pair.Value.DistanceTo(from)).Key;

        return (address, Beacons[address]);
    }

    /// <summary>
    /// Whether this event is a Guardian beacon scan, given the system the Commander is in. <c>DataScanned</c> names
    /// no system, so the system is the caller's.
    /// </summary>
    public static bool IsBeaconScan(JournalEvent journalEvent, long? systemAddress)
    {
        ArgumentNullException.ThrowIfNull(journalEvent);

        return journalEvent.Kind == "DataScanned" && systemAddress is { } address && Beacons.ContainsKey(address);
    }

    /// <summary>Every core available: the host for tests, the designer and the replay harness.</summary>
    public static GuardianCores AllAwake() => new();

    /// <summary>What the current story holds back now.</summary>
    public CoreHold Hold => hold?.Invoke() ?? CoreHold.None;

    /// <summary>Whether this core can be aboard. Anything that is not a Guardian core always can.</summary>
    public bool IsAwake(Persona persona) => !IsHeld(persona, Hold);

    /// <summary>The core itself where it is available, and the stock core where it is held back.</summary>
    public Persona Admit(Persona persona) => IsAwake(persona) ? persona : PersonaCatalog.Covas;

    /// <summary>Whether <paramref name="hold"/> keeps this core from being aboard.</summary>
    public static bool IsHeld(Persona persona, CoreHold hold)
    {
        ArgumentNullException.ThrowIfNull(persona);

        return PersonaCatalog.IsGuardian(persona.Id) && hold.Cores switch
        {
            HeldCores.All => true,
            HeldCores.Heretic => persona.Unlockable,
            _ => false,
        };
    }

    /// <summary>What is said, once, as the cores wake. <paramref name="aboard"/> is the core the story brought aboard.</summary>
    public static string Line(CoreWaking waking, Persona? aboard = null) => waking switch
    {
        CoreWaking.Heretic =>
            "Data link complete. This beacon held one more core, kept apart from the others. "
            + "The Heretic is awake, and can be chosen in the Persona section.",
        _ when aboard is not null =>
            "Data link complete. Something came across with the data: Guardian cores, awake in "
            + $"the ship's systems. {aboard.Name} has come aboard. You can choose another core in Settings.",
        _ =>
            "Data link complete. Something came across with the data: Guardian cores, awake in "
            + "the ship's systems. You can choose one in the Persona section.",
    };
}
