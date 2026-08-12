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
    ShieldsUp = 1u << 3,
    Supercruise = 1u << 4,
    HardpointsDeployed = 1u << 6,
    CargoScoopDeployed = 1u << 9,
    ScoopingFuel = 1u << 11,
    FsdMassLocked = 1u << 16,
    FsdCharging = 1u << 17,
    FsdCooldown = 1u << 18,

    /// <summary>Elite sets this below 25% of the main tank.</summary>
    LowFuel = 1u << 19,

    /// <summary>Elite sets this above 100% heat.</summary>
    Overheating = 1u << 20,

    InDanger = 1u << 22,
    BeingInterdicted = 1u << 23,
    InMainShip = 1u << 24,
    InFighter = 1u << 25,
    InSrv = 1u << 26,
    FsdJump = 1u << 30,
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

    /// <summary>Odyssey's second bitfield, kept whole — nothing reads it yet.</summary>
    public uint Flags2 { get; init; }

    public double? FuelMain { get; init; }

    public double? FuelReservoir { get; init; }

    /// <summary>Tonnes in the hold, as of the last write.</summary>
    public double? Cargo { get; init; }

    /// <summary>0 to 1. Elite reports this on foot and in the SRV; null in a ship.</summary>
    public double? Heat { get; init; }

    public string? BodyName { get; init; }

    public long? Balance { get; init; }

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
                FuelMain = root.Object("Fuel")?.Double("FuelMain"),
                FuelReservoir = root.Object("Fuel")?.Double("FuelReservoir"),
                Cargo = root.Double("Cargo"),
                Heat = root.Double("Temperature"),
                BodyName = root.String("BodyName"),
                Balance = root.Long("Balance"),
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
