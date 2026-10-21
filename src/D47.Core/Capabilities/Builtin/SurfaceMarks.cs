using D47.Core.Journal;

namespace D47.Core.Capabilities.Builtin;

/// <summary>One saved point on a body, valid for the visit it was made in.</summary>
public sealed record SurfaceMark(string Name, (long SystemAddress, int BodyId) Body, SurfaceFix At, DateTimeOffset MadeAt);

/// <summary>
/// The Commander's marks on the body they are at, held in memory. A mark is valid while
/// <see cref="SurfacePoints.Body"/> is its body and it was made after <see cref="SurfacePoints.Since"/>;
/// the rest are dropped on the next read or write. Safe to call from any thread.
/// </summary>
public sealed class SurfaceMarks
{
    private readonly object _gate = new();

    private readonly List<SurfaceMark> _marks = [];

    /// <summary>The marks valid for <paramref name="surface"/>, oldest first.</summary>
    public IReadOnlyList<SurfaceMark> Valid(SurfacePoints surface)
    {
        lock (_gate)
        {
            Prune(surface);
            return [.. _marks];
        }
    }

    /// <summary>
    /// Saves <paramref name="at"/> under <paramref name="name"/>, or under the lowest unused "mark N" where
    /// none is given. Returns null where there is no body or the name is taken.
    /// </summary>
    public SurfaceMark? Add(SurfacePoints surface, string? name, SurfaceFix at, DateTimeOffset madeAt)
    {
        lock (_gate)
        {
            Prune(surface);

            if (surface.Body is not { } body)
            {
                return null;
            }

            var chosen = string.IsNullOrWhiteSpace(name) ? NextName() : name.Trim();

            if (_marks.Any(mark => string.Equals(mark.Name, chosen, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var added = new SurfaceMark(chosen, body, at, madeAt);
            _marks.Add(added);
            return added;
        }
    }

    private string NextName()
    {
        for (var number = 1; ; number++)
        {
            var candidate = $"mark {number}";

            if (!_marks.Any(mark => string.Equals(mark.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    private void Prune(SurfacePoints surface) =>
        _marks.RemoveAll(mark => surface.Body != mark.Body || surface.Since is not { } since || mark.MadeAt <= since);
}
