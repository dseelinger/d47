using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace D47.Core.Journal;

/// <summary>
/// What a star is, as far as it matters to a Commander crossing it: can it refuel them, and
/// will it hurt them.
/// <para>
/// <b>This is the "scoopable-star ambiguity resolved rather than guessed" the checklist asks
/// for</b>, and the ambiguity is real. The KGBFOAM mnemonic is a rule about the first letter,
/// and applying it as a first-letter test gets two cases wrong in opposite directions: Herbig
/// <c>AeBe</c> stars start with A and are <em>not</em> scoopable, while <c>K_OrangeGiant</c>
/// and the other giant and supergiant variants carry a suffix and <em>are</em>. Getting either
/// wrong strands a Commander, which is the specific harm the guardrails already name.
/// </para>
/// </summary>
public static class StarClasses
{
    /// <summary>
    /// The main-sequence classes a fuel scoop works on. Exact matches only — see
    /// <see cref="IsScoopable"/> for the suffixed variants.
    /// </summary>
    private static readonly HashSet<string> Scoopable =
        new(["K", "G", "B", "F", "O", "A", "M"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Classes that start with a scoopable letter but are not scoopable. Only Herbig Ae/Be so
    /// far, and it is the whole reason this list exists rather than a first-letter test.
    /// </summary>
    private static readonly HashSet<string> NotScoopableDespiteTheLetter =
        new(["AeBe"], StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a fuel scoop works here. Null for a class d47 does not recognise — which is
    /// answered as "I do not know" rather than as "no", because a Commander told a star is
    /// unscoopable will route around a star that would have refuelled them.
    /// </summary>
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

        // The giant and supergiant variants: "K_OrangeGiant", "M_RedSuperGiant",
        // "A_BlueWhiteSuperGiant". Suffixed with an underscore, and scoopable like their base
        // class. The underscore is what makes this safe to split on — no unscoopable class uses
        // one.
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

        // Brown dwarfs, Wolf-Rayets, carbon stars and the rest. Recognised as real classes that
        // a scoop does not work on.
        if (trimmed is "L" or "T" or "Y" or "TTS" or "MS" or "S" or "X" ||
            trimmed.StartsWith('W') || trimmed.StartsWith('C'))
        {
            return false;
        }

        return null;
    }

    /// <summary>A neutron star. Jet-cone boostable, and dangerous to arrive at unprepared.</summary>
    public static bool IsNeutron(string? starClass) =>
        string.Equals(starClass?.Trim(), "N", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// A white dwarf. Every white dwarf class starts with D — DA, DB, DC, DQ, DAV and so on —
    /// and no other class does, so the prefix test is exact here in a way it is not for A.
    /// </summary>
    public static bool IsWhiteDwarf(string? starClass) =>
        starClass?.Trim() is { Length: > 0 } trimmed &&
        trimmed.StartsWith('D') &&
        !trimmed.Contains('_');

    public static bool IsBlackHole(string? starClass) =>
        starClass?.Trim() is "H" or "SupermassiveBlackHole";

    /// <summary>
    /// Whether arriving here warrants a warning of its own. Neutron stars and white dwarfs both
    /// have exclusion zones that will drop a Commander out of supercruise into a cone that
    /// damages them.
    /// </summary>
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
    /// <summary>
    /// Galactic coordinates, from the route file. Null when Elite did not write them — which
    /// makes the distance between two hops unknown rather than zero, and that distinction is
    /// what keeps a missing coordinate from reading as "the next jump is trivially short".
    /// </summary>
    public (double X, double Y, double Z)? Position { get; init; }

    public bool? Scoopable => StarClasses.IsScoopable(StarClass);

    public bool Hazardous => StarClasses.IsHazardous(StarClass);

    /// <summary>Light years to another hop, or null when either position is missing.</summary>
    public double? DistanceTo(RouteHop other)
    {
        if (Position is not { } from || other.Position is not { } to)
        {
            return null;
        }

        var dx = from.X - to.X;
        var dy = from.Y - to.Y;
        var dz = from.Z - to.Z;

        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}

/// <summary>
/// The route Elite plotted, read from the <c>NavRoute.json</c> file it writes locally
/// (list.md Phase 8, "Route Progress").
/// <para>
/// This is the whole reason no route-planning service is needed: the file already contains
/// every hop and every star class, which is exactly what the fuel and hazard warnings need. It
/// is written when a route is plotted and emptied when one is cleared.
/// </para>
/// </summary>
public sealed record NavRoute
{
    public static readonly NavRoute None = new();

    public IReadOnlyList<RouteHop> Hops { get; init; } = [];

    public DateTimeOffset? ReadAt { get; init; }

    public bool IsPlotted => Hops.Count > 0;

    /// <summary>
    /// The hops still ahead, given where the Commander is now. The route file keeps the origin
    /// system as hop zero and is not rewritten as the Commander advances, so "what is left" is
    /// a question about the current system's position in it — not about the file changing.
    /// </summary>
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

        // Off the plotted route — the Commander jumped somewhere else. Every hop is still
        // "ahead" in the sense that none has been reached, and saying so beats claiming the
        // route is complete.
        return Hops;
    }
}

/// <summary>
/// Pull-based reads of NavRoute.json, on the same terms as every other reader here: no thread,
/// no clock, re-read only when the file's write time moves.
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

    /// <summary>
    /// StarPos is a three-element array. Anything else is treated as absent rather than
    /// partially read: a coordinate with a missing axis would produce a confident wrong
    /// distance, and the fuel warning is built on those distances.
    /// </summary>
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
