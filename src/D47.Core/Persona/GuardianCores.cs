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

    /// <summary>A story picked with a beacon scan from an earlier story on record: its core comes aboard at the pick.</summary>
    Remembered,
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

/// <summary>The cores a story holds back, the story's title, and whether it opened with its first beacon scan behind it.</summary>
public readonly record struct CoreHold(HeldCores Cores, string Story, bool Narrated = false)
{
    public static readonly CoreHold None = new(HeldCores.None, string.Empty);
}

/// <summary>
/// Which Guardian cores can be aboard. Every one can, except those the Commander's running story holds back
/// until its beacon scans.
/// </summary>
public sealed class GuardianCores(Func<CoreHold>? hold = null)
{
    /// <summary>
    /// The Guardian beacon systems, by id64. The first seven are from EDSM; the rest are the systems with a
    /// <c>Guardian Beacon</c> signal in the daily signal index (<c>signals.tsv.gz</c>).
    /// </summary>
    public static readonly FrozenDictionary<long, string> Beacons = new Dictionary<long, string>
    {
        [13872878396833] = "IC 2391 Sector MX-T b3-6",
        [4208161886922] = "Synuefe IL-N c23-15",
        [182443805035] = "Synuefe IT-F d12-5",
        [9476442170745] = "Synuefe KU-F b44-4",
        [9476979041665] = "Synuefe QA-E b45-4",
        [13874757182857] = "Synuefe RL-C b46-6",
        [869621795163] = "NGC 2451A Sector LX-U d2-25",
        [84926173882] = "HIP 36823",
        [4482838500042] = "IC 2391 Sector CQ-Y c16",
        [20469679662481] = "IC 2391 Sector FG-X b1-9",
        [16072170022289] = "IC 2391 Sector HG-X b1-7",
        [5307807765170] = "NGC 2451A Sector RT-R c4-19",
        [909761221298] = "NGC 2451A Sector TO-R c4-3",
        [319849220451] = "Synuefe AS-H d11-9",
        [13874220508537] = "Synuefe BO-G b44-6",
        [4168173472099] = "Synuefe CS-H d11-121",
        [27067823302025] = "Synuefe DK-D b46-12",
        [9705787167426] = "Synuefe DK-P c22-35",
        [9705787134658] = "Synuefe FF-P c22-35",
        [11675197252993] = "Synuefe FU-E b45-5",
        [6132374311618] = "Synuefe HA-P c22-22",
        [1184437801674] = "Synuefe HL-N c23-4",
        [9476173931905] = "Synuefe HP-E b45-4",
        [11676539364729] = "Synuefe IJ-G b44-5",
        [9476979107201] = "Synuefe OF-E b45-4",
        [11676270732673] = "Synuefe RA-E b45-5",
        [3893262010715] = "Synuefe WL-J d10-113",
        [1694255515995] = "Synuefe ZG-J d10-49",
    }.ToFrozenDictionary();

    /// <summary>Where each of <see cref="Beacons"/> is, from EDSM and the signal index.</summary>
    public static readonly FrozenDictionary<long, StarPosition> BeaconPositions = new Dictionary<long, StarPosition>
    {
        [13872878396833] = new(582.9375, -72.28125, -19.40625),
        [4208161886922] = new(658.28125, -117.96875, -46.3125),
        [182443805035] = new(739.46875, -140.53125, -13.375),
        [9476442170745] = new(714.34375, -173.84375, -113.59375),
        [9476979041665] = new(738.0625, -174.375, -103.25),
        [13874757182857] = new(727.90625, -157.1875, -67.1875),
        [869621795163] = new(726.0625, -163.5625, -171.78125),
        [84926173882] = new(640.4375, -143.90625, -118.3125),
        [4482838500042] = new(559.875, -87.15625, -33.15625),
        [20469679662481] = new(563.75, -100.03125, -59.3125),
        [16072170022289] = new(604.5625, -101.78125, -58.65625),
        [5307807765170] = new(753.03125, -84.03125, -153.75),
        [909761221298] = new(739.46875, -127.875, -156.1875),
        [319849220451] = new(610.3125, -65.125, -101.84375),
        [13874220508537] = new(687.3125, -96.75, -112.125),
        [4168173472099] = new(746.09375, -100.78125, -86.625),
        [27067823302025] = new(636.3125, -53.65625, -68.8125),
        [9705787167426] = new(711.46875, -102.6875, -89.53125),
        [9705787134658] = new(700.03125, -129.5, -96.375),
        [11675197252993] = new(682.28125, -102.59375, -104.5625),
        [6132374311618] = new(701.5, -168.125, -104.40625),
        [1184437801674] = new(649.9375, -114.78125, -57.0625),
        [9476173931905] = new(678.59375, -118.75, -94.78125),
        [11676539364729] = new(790.0625, -124.8125, -120.40625),
        [9476979107201] = new(751.0625, -157.71875, -86.09375),
        [11676270732673] = new(755.53125, -169.125, -88.28125),
        [3893262010715] = new(645.0625, -95.46875, -142.4375),
        [1694255515995] = new(723.8125, -156.71875, -152.3125),
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
            + "The Heretic is awake, and can be chosen in the Core and character section.",
        CoreWaking.Remembered when aboard is not null =>
            "The Guardian cores from your last beacon scan are still in the ship's systems. "
            + $"{aboard.Name} has come aboard for this story. You can choose another core in Settings.",
        CoreWaking.Remembered =>
            "The Guardian cores from your last beacon scan are still in the ship's systems. "
            + "You can choose one in the Core and character section.",
        _ when aboard is not null =>
            "Data link complete. Something came across with the data: Guardian cores, awake in "
            + $"the ship's systems. {aboard.Name} has come aboard. You can choose another core in Settings.",
        _ =>
            "Data link complete. Something came across with the data: Guardian cores, awake in "
            + "the ship's systems. You can choose one in the Core and character section.",
    };
}
