using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// What a star is, as far as it matters to a Commander crossing it: can it refuel them, and will it
/// hurt them.
/// </summary>
public static class StarClasses
{
    /// <summary>The main-sequence classes a fuel scoop works on.</summary>
    private static readonly HashSet<string> Scoopable =
        new(["K", "G", "B", "F", "O", "A", "M"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Classes that start with a scoopable letter but are not scoopable.</summary>
    private static readonly HashSet<string> NotScoopableDespiteTheLetter =
        new(["AeBe"], StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a fuel scoop works here.</summary>
    public static bool? IsScoopable(string? starClass)
    {
        if (string.IsNullOrWhiteSpace(starClass))
        {
            return null;
        }

        var trimmed = starClass.Trim();

        if (NotScoopableDespiteTheLetter.Contains(trimmed))
        {
            return false;
        }

        if (Scoopable.Contains(trimmed))
        {
            return true;
        }

        // The giant and supergiant variants: "K_OrangeGiant", "M_RedSuperGiant", "A_BlueWhiteSuperGiant".
        var underscore = trimmed.IndexOf('_');

        if (underscore > 0 && Scoopable.Contains(trimmed[..underscore]))
        {
            return true;
        }

        // Known and definitely not scoopable.
        if (IsNeutron(trimmed) || IsWhiteDwarf(trimmed) || IsBlackHole(trimmed))
        {
            return false;
        }

        // Brown dwarfs, Wolf-Rayets, carbon stars and the rest.
        if (trimmed is "L" or "T" or "Y" or "TTS" or "MS" or "S" or "X" ||
            trimmed.StartsWith('W') || trimmed.StartsWith('C'))
        {
            return false;
        }

        return null;
    }

    /// <summary>A neutron star.</summary>
    public static bool IsNeutron(string? starClass) =>
        string.Equals(starClass?.Trim(), "N", StringComparison.OrdinalIgnoreCase);

    /// <summary>A white dwarf.</summary>
    public static bool IsWhiteDwarf(string? starClass) =>
        starClass?.Trim() is { Length: > 0 } trimmed &&
        trimmed.StartsWith('D') &&
        !trimmed.Contains('_');

    public static bool IsBlackHole(string? starClass) =>
        starClass?.Trim() is "H" or "SupermassiveBlackHole";

    /// <summary>Whether arriving here warrants a warning of its own.</summary>
    public static bool IsHazardous(string? starClass) =>
        IsNeutron(starClass) || IsWhiteDwarf(starClass) || IsBlackHole(starClass);

    /// <summary>How to say the class out loud.</summary>
    public static string Speak(string? starClass) => starClass?.Trim() switch
    {
        null or "" => "unknown class",
        "N" => "a neutron star",
        "H" or "SupermassiveBlackHole" => "a black hole",
        { } dwarf when IsWhiteDwarf(dwarf) => "a white dwarf",
        { } other => $"class {other}",
    };
}

/// <summary>One system on the plotted route.</summary>
public sealed record RouteHop(string StarSystem, string? StarClass)
{
    /// <summary>Galactic coordinates, from the route file.</summary>
    public (double X, double Y, double Z)? Position { get; init; }

    public bool? Scoopable => StarClasses.IsScoopable(StarClass);

    public bool Hazardous => StarClasses.IsHazardous(StarClass);

    /// <summary>Light years to another hop, or null when either position is missing.</summary>
    public double? DistanceTo(RouteHop other) =>
        Position is { } from && other.Position is { } to
            ? StarPosition.Between(from, to)
            : null;
}

/// <summary>
/// The route Elite plotted, read from the <c>NavRoute.json</c> file it writes locally (Phase 8, "Route
/// Progress").
/// </summary>
public sealed record NavRoute
{
    public static readonly NavRoute None = new();

    public IReadOnlyList<RouteHop> Hops { get; init; } = [];

    public DateTimeOffset? ReadAt { get; init; }

    public bool IsPlotted => Hops.Count > 0;

    /// <summary>The hops still ahead, given where the Commander is now.</summary>
    public IReadOnlyList<RouteHop> Ahead(string? currentSystem)
    {
        if (currentSystem is null)
        {
            return Hops;
        }

        for (var index = 0; index < Hops.Count; index++)
        {
            if (string.Equals(Hops[index].StarSystem, currentSystem, StringComparison.OrdinalIgnoreCase))
            {
                return [.. Hops.Skip(index + 1)];
            }
        }

        // Off the plotted route — the Commander jumped somewhere else.
        return Hops;
    }
}

/// <summary>Where the Commander is in a plotted route, and what is left of it (Phase 37, "Progress").</summary>
/// <param name="Index">
/// Which hop the Commander is standing on, or -1 when they are not on the route at all.
/// </param>
/// <param name="JumpsRemaining">Hops still ahead — the whole route when off it.</param>
/// <param name="DistanceRemaining">
/// Light years left along the route, or null when any leg of it has an unknown length.
/// </param>
public sealed record RouteProgress(int Index, int JumpsRemaining, double? DistanceRemaining)
{
    /// <summary>Nothing plotted at all.</summary>
    public static readonly RouteProgress None = new(-1, 0, null);

    /// <summary>Whether the Commander is somewhere the route does not mention.</summary>
    public bool OffRoute => Index < 0;

    public static RouteProgress For(NavRoute route, string? currentSystem)
    {
        if (!route.IsPlotted)
        {
            return None;
        }

        var index = -1;

        for (var candidate = 0; candidate < route.Hops.Count; candidate++)
        {
            if (string.Equals(route.Hops[candidate].StarSystem, currentSystem, StringComparison.OrdinalIgnoreCase))
            {
                index = candidate;
                break;
            }
        }

        // Off the route, everything is still ahead — the same reading NavRoute.Ahead takes, and for the same
        // reason: none of it has been reached.
        var from = index < 0 ? -1 : index;

        return new RouteProgress(
            index,
            route.Hops.Count - from - 1,
            DistanceFrom(route, Math.Max(from, 0)));
    }

    private static double? DistanceFrom(NavRoute route, int from)
    {
        if (route.Hops.Count <= from + 1)
        {
            return null;
        }

        var total = 0d;

        for (var hop = from; hop + 1 < route.Hops.Count; hop++)
        {
            if (route.Hops[hop].DistanceTo(route.Hops[hop + 1]) is not { } leg)
            {
                return null;
            }

            total += leg;
        }

        return total;
    }
}

/// <summary>
/// Pull-based reads of NavRoute.json, on the same terms as every other reader here: no thread, no
/// clock, re-read only when the file's write time moves.
/// </summary>
public sealed class NavRouteReader(string directory, ILogger logger)
{
    public const string FileName = "NavRoute.json";

    private DateTime _stamp;

    public NavRoute Current { get; private set; } = NavRoute.None;

    public bool Poll()
    {
        var path = Path.Combine(directory, FileName);

        DateTime written;

        try
        {
            var info = new FileInfo(path);

            if (!info.Exists)
            {
                return false;
            }

            written = info.LastWriteTimeUtc;
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not stat NavRoute.json");
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

            var hops = new List<RouteHop>();

            foreach (var element in document.RootElement.Items("Route"))
            {
                if (element.String("StarSystem") is { } system)
                {
                    hops.Add(new RouteHop(system, element.String("StarClass"))
                    {
                        Position = ReadPosition(element),
                    });
                }
            }

            Current = new NavRoute
            {
                Hops = hops,
                ReadAt = new DateTimeOffset(written, TimeSpan.Zero),
            };

            _stamp = written;
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogDebug(ex, "Could not read NavRoute.json; will retry");
            return false;
        }
    }

    /// <summary>StarPos is a three-element array.</summary>
    private static (double X, double Y, double Z)? ReadPosition(JsonElement element)
    {
        if (!element.TryGetProperty("StarPos", out var position) ||
            position.ValueKind != JsonValueKind.Array ||
            position.GetArrayLength() != 3)
        {
            return null;
        }

        var axes = new double[3];

        for (var index = 0; index < 3; index++)
        {
            var axis = position[index];

            if (axis.ValueKind != JsonValueKind.Number || !axis.TryGetDouble(out axes[index]))
            {
                return null;
            }
        }

        return (axes[0], axes[1], axes[2]);
    }
}
