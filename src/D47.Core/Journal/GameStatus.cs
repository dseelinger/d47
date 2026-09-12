using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>The <c>Flags</c> bitfield Elite writes into Status.json.</summary>
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

    /// <summary>Analysis mode rather than combat mode.</summary>
    AnalysisMode = 1u << 27,

    NightVision = 1u << 28,
    FsdJump = 1u << 30,
    SrvHighBeam = 1u << 31,
}

/// <summary>Odyssey's second bitfield.</summary>
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
/// Which full-screen interface, if any, has the Commander's attention — the <c>GuiFocus</c> number
/// Elite writes into Status.json.
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

/// <summary>What the Commander has selected to travel to, as Status.json reports it.</summary>
/// <param name="System">The system's address.</param>
/// <param name="Body">The body's id, and zero where the destination is the system itself.</param>
/// <param name="Name">What Elite calls it.</param>
public readonly record struct StatusDestination(long System, long Body, string? Name);

/// <summary>
/// The live state Elite writes to Status.json — the only continuous signal the game gives, and the one
/// Phase 8's danger callouts need.
/// </summary>
public sealed record GameStatus
{
    public static readonly GameStatus Unknown = new();

    public StatusFlags Flags { get; init; }

    /// <summary>Odyssey's second bitfield.</summary>
    public uint Flags2 { get; init; }

    public bool Has2(StatusFlags2 flag) => ((StatusFlags2)Flags2 & flag) == flag;

    /// <summary>
    /// On foot, which is the one mode question the first bitfield cannot answer: it keeps reporting
    /// <see cref="StatusFlags.InMainShip"/> for a Commander who has got out.
    /// </summary>
    public bool OnFoot => Has2(StatusFlags2.OnFoot);

    /// <summary>Which full-screen interface is showing, if any.</summary>
    public GuiFocus GuiFocus { get; init; }

    public double? FuelMain { get; init; }

    public double? FuelReservoir { get; init; }

    /// <summary>Tonnes in the hold, as of the last write.</summary>
    public double? Cargo { get; init; }

    /// <summary>0 to 1.</summary>
    public double? Heat { get; init; }

    public string? BodyName { get; init; }

    /// <summary>What is selected in the nav panel, or null where nothing is.</summary>
    public StatusDestination? Destination { get; init; }

    public long? Balance { get; init; }

    /// <summary>Where the Commander is standing, in degrees (Phase 18, "Exobiology sampling").</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Metres above the surface, where Elite reports one.</summary>
    public double? Altitude { get; init; }

    /// <summary>
    /// The body's radius in metres, which Elite writes beside the position rather than making d47 go
    /// and find it.
    /// </summary>
    public double? PlanetRadius { get; init; }

    /// <summary>Whether a position is actually being reported, rather than merely flagged.</summary>
    public bool HasPosition => Latitude is not null && Longitude is not null;

    /// <summary>When this was read.</summary>
    public DateTimeOffset? ReadAt { get; init; }

    public bool IsKnown => ReadAt is not null;

    public bool Has(StatusFlags flag) => (Flags & flag) == flag;

    public bool ShieldsUp => Has(StatusFlags.ShieldsUp);

    public bool InShip => Has(StatusFlags.InMainShip);

    /// <summary>
    /// Whether the Commander is actually in the game world — aboard something, on foot, or a passenger
    /// — as opposed to Elite merely sitting at its main menu (#242).
    /// </summary>
    public bool CommanderIsInTheGame =>
        IsKnown
        && (Has(StatusFlags.InMainShip)
            || Has(StatusFlags.InFighter)
            || Has(StatusFlags.InSrv)
            || OnFoot
            || Has2(StatusFlags2.InTaxi)
            || Has2(StatusFlags2.InMulticrew));

    /// <summary>How old a read may be and still describe the game now.</summary>
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Whether this read describes the game as it is at <paramref name="now"/>: the Commander in the
    /// game world, and the read recent enough to be the game's own account of it (#242).
    /// </summary>
    public bool IsLiveAt(DateTimeOffset now) =>
        CommanderIsInTheGame && ReadAt is { } read && now - read < FreshFor;

    /// <summary>
    /// Fuel as a fraction of the tank, which Status.json cannot answer on its own — it reports the
    /// level and never the capacity.
    /// </summary>
    public double? FuelFraction(double? tankCapacity) =>
        FuelMain is { } fuel && tankCapacity is { } capacity && capacity > 0
            ? fuel / capacity
            : null;
}

/// <summary>Pull-based reads of Status.json.</summary>
public sealed class GameStatusReader(string directory, ILogger logger)
{
    public const string FileName = "Status.json";

    private DateTime _stamp;

    public GameStatus Current { get; private set; } = GameStatus.Unknown;

    /// <summary>Re-reads if the file changed.</summary>
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
                Destination = Destination(root),
                Balance = root.Long("Balance"),

                // Absent everywhere except near a surface, and absent is not zero — see the remarks on these
                // properties.
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

    private static StatusDestination? Destination(JsonElement root) =>
        root.Object("Destination") is { } destination
            ? new StatusDestination(
                destination.Long("System") ?? 0,
                destination.Long("Body") ?? 0,
                destination.String("Name"))
            : null;
}
