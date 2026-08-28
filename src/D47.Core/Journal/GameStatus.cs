using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// The <c>Flags</c> bitfield Elite writes into Status.json. Only the bits d47 acts on are
/// named; the rest stay in <see cref="GameStatus.Flags"/> for a later phase to reach.
/// </summary>
[Flags]
public enum StatusFlags : uint
{
    None = 0,
    Docked = 1u << 0,
    Landed = 1u << 1,
    LandingGearDown = 1u << 2,
    ShieldsUp = 1u << 3,
    Supercruise = 1u << 4,
    FlightAssistOff = 1u << 5,
    HardpointsDeployed = 1u << 6,
    InWing = 1u << 7,
    LightsOn = 1u << 8,
    CargoScoopDeployed = 1u << 9,
    SilentRunning = 1u << 10,
    ScoopingFuel = 1u << 11,
    SrvHandbrake = 1u << 12,
    SrvTurretView = 1u << 13,
    SrvDriveAssist = 1u << 15,
    FsdMassLocked = 1u << 16,
    FsdCharging = 1u << 17,
    FsdCooldown = 1u << 18,

    /// <summary>Elite sets this below 25% of the main tank.</summary>
    LowFuel = 1u << 19,

    /// <summary>Elite sets this above 100% heat.</summary>
    Overheating = 1u << 20,

    HasLatLong = 1u << 21,
    InDanger = 1u << 22,
    BeingInterdicted = 1u << 23,
    InMainShip = 1u << 24,
    InFighter = 1u << 25,
    InSrv = 1u << 26,

    /// <summary>Analysis mode rather than combat mode. The scanners only fire in this one.</summary>
    AnalysisMode = 1u << 27,

    NightVision = 1u << 28,
    FsdJump = 1u << 30,
    SrvHighBeam = 1u << 31,
}

/// <summary>
/// Odyssey's second bitfield. Only <see cref="OnFoot"/> is named: it is the bit that decides
/// whether the Commander is in a ship at all, which the first bitfield cannot answer — it
/// reports <see cref="StatusFlags.InMainShip"/> for a Commander standing next to their ship.
/// </summary>
[Flags]
public enum StatusFlags2 : uint
{
    None = 0,
    OnFoot = 1u << 0,
    InTaxi = 1u << 1,
    InMulticrew = 1u << 2,
    OnFootInStation = 1u << 3,
    OnFootOnPlanet = 1u << 4,
    GlideMode = 1u << 12,
    OnFootInHangar = 1u << 13,
    OnFootSocialSpace = 1u << 14,
    OnFootExterior = 1u << 15,
    BreathableAtmosphere = 1u << 16,
}

/// <summary>
/// Which full-screen interface, if any, has the Commander's attention — the <c>GuiFocus</c>
/// number Elite writes into Status.json. Only the values d47 acts on are named.
/// <para>
/// This is what makes driving the galaxy map verifiable step by step rather than only at the end:
/// "the map is open" and "the map has closed again" are both readable here, so the macro that
/// plots a course waits on the game rather than on a guess about how long the map takes to open.
/// </para>
/// </summary>
public enum GuiFocus
{
    None = 0,
    InternalPanel = 1,
    ExternalPanel = 2,
    CommsPanel = 3,
    RolePanel = 4,
    StationServices = 5,
    GalaxyMap = 6,
    SystemMap = 7,
    Orrery = 8,
    FssMode = 9,
    SaaMode = 10,
    Codex = 11,
}

/// <summary>
/// The live state Elite writes to Status.json — the only continuous signal the game gives, and
/// the one Phase 8's danger callouts need.
/// <para>
/// The journal reports things that <em>happened</em>; this reports how things <em>are</em>.
/// Shields dropping is a journal event, but "shields are still down thirty seconds later" is
/// only answerable from here, and so is fuel, cargo and heat between the events that mention
/// them.
/// </para>
/// </summary>
public sealed record GameStatus
{
    public static readonly GameStatus Unknown = new();

    public StatusFlags Flags { get; init; }

    /// <summary>Odyssey's second bitfield. See <see cref="StatusFlags2"/>.</summary>
    public uint Flags2 { get; init; }

    public bool Has2(StatusFlags2 flag) => ((StatusFlags2)Flags2 & flag) == flag;

    /// <summary>
    /// On foot, which is the one mode question the first bitfield cannot answer: it keeps
    /// reporting <see cref="StatusFlags.InMainShip"/> for a Commander who has got out.
    /// </summary>
    public bool OnFoot => Has2(StatusFlags2.OnFoot);

    /// <summary>Which full-screen interface is showing, if any. See <see cref="GuiFocus"/>.</summary>
    public GuiFocus GuiFocus { get; init; }

    public double? FuelMain { get; init; }

    public double? FuelReservoir { get; init; }

    /// <summary>Tonnes in the hold, as of the last write.</summary>
    public double? Cargo { get; init; }

    /// <summary>0 to 1. Elite reports this on foot and in the SRV; null in a ship.</summary>
    public double? Heat { get; init; }

    public string? BodyName { get; init; }

    public long? Balance { get; init; }

    /// <summary>
    /// Where the Commander is standing, in degrees (Phase 18, "Exobiology sampling").
    /// <para>
    /// <b>Written only when there is a surface to be above</b>, which is what
    /// <see cref="StatusFlags.HasLatLong"/> announces — so these are null in supercruise and in deep
    /// space rather than zero. Reading a missing coordinate as 0,0 would put the Commander on the
    /// equator at the prime meridian and quietly make every distance from there enormous.
    /// </para>
    /// </summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Metres above the surface, where Elite reports one.</summary>
    public double? Altitude { get; init; }

    /// <summary>
    /// The body's radius in metres, which Elite writes beside the position rather than making d47
    /// go and find it.
    /// <para>
    /// <b>This is a correction to what the checklist item assumed.</b> It expected the radius to come
    /// off the <c>Scan</c> event — which would have meant a sample distance was only computable on a
    /// body the Commander had scanned, and wrong or absent on one they had merely landed on.
    /// <c>Status.json</c> carries it directly, so the distance needs nothing but the file d47 is
    /// already reading ten times a second.
    /// </para>
    /// </summary>
    public double? PlanetRadius { get; init; }

    /// <summary>Whether a position is actually being reported, rather than merely flagged.</summary>
    public bool HasPosition => Latitude is not null && Longitude is not null;

    /// <summary>When this was read. Null means Status.json has not been seen.</summary>
    public DateTimeOffset? ReadAt { get; init; }

    public bool IsKnown => ReadAt is not null;

    public bool Has(StatusFlags flag) => (Flags & flag) == flag;

    public bool ShieldsUp => Has(StatusFlags.ShieldsUp);

    public bool InShip => Has(StatusFlags.InMainShip);

    /// <summary>
    /// Fuel as a fraction of the tank, which Status.json cannot answer on its own — it reports
    /// the level and never the capacity. The capacity comes from the Loadout event, so this
    /// takes it as an argument rather than pretending to know it.
    /// </summary>
    public double? FuelFraction(double? tankCapacity) =>
        FuelMain is { } fuel && tankCapacity is { } capacity && capacity > 0
            ? fuel / capacity
            : null;
}

/// <summary>
/// Pull-based reads of Status.json. Owns no thread and no clock, like every other reader here
/// (architecture.md §4).
/// <para>
/// Elite rewrites this file continuously — several times a second while flying — so it is
/// re-read only when the last-write time moves, and a read that lands mid-write is retried on
/// the next tick rather than reported as a failure. At 10 Hz that race is routine, not
/// exceptional, and treating it as an error would fill the log with the loop working correctly.
/// </para>
/// </summary>
public sealed class GameStatusReader(string directory, ILogger logger)
{
    public const string FileName = "Status.json";

    private DateTime _stamp;

    public GameStatus Current { get; private set; } = GameStatus.Unknown;

    /// <summary>Re-reads if the file changed. True when <see cref="Current"/> was replaced.</summary>
    public bool Poll()
    {
        var path = Path.Combine(directory, FileName);

        DateTime written;

        try
        {
            var info = new FileInfo(path);

            // Not an error: Elite has never run, or is not running now.
            if (!info.Exists)
            {
                return false;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat Status.json");
            return false;
        }

        if (written == _stamp)
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var document = JsonDocument.Parse(stream);
            var root = document.RootElement;

            Current = new GameStatus
            {
                Flags = (StatusFlags)(root.Long("Flags") ?? 0),
                Flags2 = (uint)(root.Long("Flags2") ?? 0),
                GuiFocus = (GuiFocus)(root.Int("GuiFocus") ?? 0),
                FuelMain = root.Object("Fuel")?.Double("FuelMain"),
                FuelReservoir = root.Object("Fuel")?.Double("FuelReservoir"),
                Cargo = root.Double("Cargo"),
                Heat = root.Double("Temperature"),
                BodyName = root.String("BodyName"),
                Balance = root.Long("Balance"),

                // Absent everywhere except near a surface, and absent is not zero — see the remarks
                // on these properties.
                Latitude = root.Double("Latitude"),
                Longitude = root.Double("Longitude"),
                Altitude = root.Double("Altitude"),
                PlanetRadius = root.Double("PlanetRadius"),

                ReadAt = new DateTimeOffset(written, TimeSpan.Zero),
            };

            // Only after a successful parse, so a file caught mid-write is retried next tick.
            _stamp = written;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogDebug(ex, "Could not read Status.json; will retry");
            return false;
        }
    }
}
