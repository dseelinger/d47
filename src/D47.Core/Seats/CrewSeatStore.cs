using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Seats;

/// <summary>
/// Every ship's crew seats, in one file beside the executable. Reads and writes block on file I/O: call
/// from the pool, not from a tick.
/// </summary>
public sealed class CrewSeatStore(string path, ILogger<CrewSeatStore> logger)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>The most ships one file may hold.</summary>
    public const int MaxShips = 128;

    private readonly Lock _gate = new();

    private IReadOnlyList<ShipSeats> _ships = [];
    private IReadOnlyList<CrewSeatProblem> _problems = [];

    /// <summary>The file's contents as last read.</summary>
    private string? _seen;

    /// <summary>Raised when the set changed, whoever wrote it.</summary>
    public event Action? Changed;

    public string Path => path;

    public IReadOnlyList<ShipSeats> Ships
    {
        get
        {
            lock (_gate)
            {
                return _ships;
            }
        }
    }

    /// <summary>Seats and ships that were refused, and why.</summary>
    public IReadOnlyList<CrewSeatProblem> Problems
    {
        get
        {
            lock (_gate)
            {
                return _problems;
            }
        }
    }

    /// <summary>The seats this Commander has on this ship, or null when it has none stored.</summary>
    public ShipSeats? For(string? fid, int shipId) =>
        Ships.FirstOrDefault(ship => Same(ship, fid ?? string.Empty, shipId));

    /// <summary>Re-reads if the file changed.</summary>
    public bool Poll()
    {
        string text;

        try
        {
            if (!File.Exists(path))
            {
                if (_seen is null)
                {
                    return false;
                }

                lock (_gate)
                {
                    _ships = [];
                    _problems = [];
                    _seen = null;
                }

                Changed?.Invoke();
                return true;
            }

            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            using var reader = new StreamReader(stream);
            text = reader.ReadToEnd();
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read the crew seat file");
            return false;
        }

        return !string.Equals(text, _seen, StringComparison.Ordinal) && Reload(text);
    }

    /// <summary>Replaces one ship's seats.</summary>
    public void Set(ShipSeats seats)
    {
        Save([
            .. Ships
                .Where(existing => !Same(existing, seats.CommanderFid, seats.ShipId))
                .Take(MaxShips - 1)
                .Append(seats)
                .OrderBy(existing => existing.CommanderFid, StringComparer.Ordinal)
                .ThenBy(existing => existing.ShipId),
        ]);
    }

    /// <summary>Removes one ship's seats.</summary>
    public bool Forget(string? fid, int shipId)
    {
        if (For(fid, shipId) is null)
        {
            return false;
        }

        Save([.. Ships.Where(existing => !Same(existing, fid ?? string.Empty, shipId))]);

        return true;
    }

    /// <summary>Writes a new set, then re-reads it so the store holds only what the file says.</summary>
    public void Save(IReadOnlyList<ShipSeats> ships)
    {
        var file = new SeatFile
        {
            Ships = [.. ships.Take(MaxShips).Select(ship => new ShipLine
            {
                CommanderFid = ship.CommanderFid.Length > 0 ? ship.CommanderFid : null,
                ShipId = ship.ShipId,
                Hull = ship.Hull,
                Seats = [.. ship.Seats.Select(seat => new SeatLine
                {
                    Id = seat.Id,
                    Role = seat.Role.ToString(),
                    Title = seat.Title,
                    Name = seat.Name,
                })],
            })],
        };

        var text = JsonSerializer.Serialize(file, Json);

        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            File.WriteAllText(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not write the crew seat file");
            return;
        }

        Reload(text);
    }

    private bool Reload(string text)
    {
        SeatFile? file;

        try
        {
            file = JsonSerializer.Deserialize<SeatFile>(text, Json);
        }
        catch (JsonException ex)
        {
            lock (_gate)
            {
                _problems = [new CrewSeatProblem(System.IO.Path.GetFileName(path), ex.Message)];
                _seen = text;
            }

            logger.LogWarning(ex, "The crew seat file could not be read");
            Changed?.Invoke();
            return true;
        }

        var ships = new List<ShipSeats>();
        var problems = new List<CrewSeatProblem>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in file?.Ships ?? [])
        {
            var where = $"ship {line.ShipId?.ToString() ?? "(none)"}";

            if (line.ShipId is not { } shipId)
            {
                problems.Add(new CrewSeatProblem(where, "it names no ship id, and that is the key."));
                continue;
            }

            var fid = (line.CommanderFid ?? string.Empty).Trim();

            if (ships.Any(existing => Same(existing, fid, shipId)))
            {
                problems.Add(new CrewSeatProblem(where, $"ship {shipId} appears twice, and a ship has one set of seats."));
                continue;
            }

            if (ships.Count >= MaxShips)
            {
                problems.Add(new CrewSeatProblem(where, $"the file already holds {MaxShips} ships."));
                continue;
            }

            var hull = Blank(line.Hull);

            ships.Add(new ShipSeats(fid, shipId, hull, ReadSeats(line, hull, where, ids, problems)));
        }

        lock (_gate)
        {
            _ships = ships;
            _problems = problems;
            _seen = text;
        }

        Changed?.Invoke();
        return true;
    }

    private static List<CrewSeat> ReadSeats(
        ShipLine line, string? hull, string ship, HashSet<string> ids, List<CrewSeatProblem> problems)
    {
        var offered = CrewSeats.CountFor(hull) ?? 0;
        var kept = new List<CrewSeat>();

        foreach (var seat in line.Seats)
        {
            var name = Blank(seat.Name);
            var where = name is null ? ship : $"{ship}, {name}";

            if (!Enum.TryParse<CrewRole>(seat.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role))
            {
                problems.Add(new CrewSeatProblem(where, $"there is no role called \"{seat.Role}\"."));
                continue;
            }

            if (name is null || name.Length > CrewSeat.MaxName)
            {
                problems.Add(new CrewSeatProblem(
                    where, $"a name must be 1 to {CrewSeat.MaxName} characters."));
                continue;
            }

            var title = Blank(seat.Title);

            if (role == CrewRole.Custom && (title is null || title.Length > CrewSeat.MaxTitle))
            {
                problems.Add(new CrewSeatProblem(
                    where, $"a custom role needs a title of 1 to {CrewSeat.MaxTitle} characters."));
                continue;
            }

            if (!CrewSeat.IsId(seat.Id))
            {
                problems.Add(new CrewSeatProblem(where, "its id is not eight hex characters."));
                continue;
            }

            if (kept.Count >= offered)
            {
                problems.Add(new CrewSeatProblem(where, $"the hull offers {offered} seats and they are taken."));
                continue;
            }

            if (role != CrewRole.Custom && kept.Any(existing => existing.Role == role))
            {
                problems.Add(new CrewSeatProblem(where, $"the ship already has a {role} seat."));
                continue;
            }

            if (kept.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                problems.Add(new CrewSeatProblem(where, "another seat on this ship has the same name."));
                continue;
            }

            if (!ids.Add(seat.Id!))
            {
                problems.Add(new CrewSeatProblem(where, $"the id {seat.Id} is already in use."));
                continue;
            }

            kept.Add(new CrewSeat(seat.Id!, role, role == CrewRole.Custom ? title : null, name));
        }

        return kept;
    }

    private static bool Same(ShipSeats ship, string fid, int shipId) =>
        ship.ShipId == shipId && string.Equals(ship.CommanderFid, fid, StringComparison.Ordinal);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record SeatFile
    {
        public IReadOnlyList<ShipLine> Ships { get; init; } = [];
    }

    private sealed record ShipLine
    {
        public string? CommanderFid { get; init; }

        public int? ShipId { get; init; }

        public string? Hull { get; init; }

        public IReadOnlyList<SeatLine> Seats { get; init; } = [];
    }

    private sealed record SeatLine
    {
        public string? Id { get; init; }

        public string? Role { get; init; }

        public string? Title { get; init; }

        public string? Name { get; init; }
    }
}
