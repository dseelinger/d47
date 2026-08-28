using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// One point of interest from a curated catalogue, cut down to what a story can stand on
/// (Phase 47, "The Galactic Mapping lookup").
/// </summary>
/// <param name="SystemAddress">
/// The catalogue's own id for the system. Carried so the dry run can hold it against the galaxy
/// service's — two sources, and a disagreement refuses the place, which is Phase 23's generator
/// assertion applied at runtime.
/// </param>
public sealed record NotablePlace(
    string Name,
    string Type,
    string System,
    long? SystemAddress,
    StarPosition Position)
{
    public string? Region { get; init; }

    /// <summary>The catalogue's one-line description. Third-party prose: labelled as such wherever it is shown to a model.</summary>
    public string? Summary { get; init; }

    public double? Rating { get; init; }

    public double DistanceFrom(StarPosition from) => from.DistanceTo(Position);
}

/// <summary>
/// The seam to a catalogue of notable places. Core declares it and never implements it, as with
/// <see cref="IGalaxyService"/>: the host, the fetch and the document's shape live outside.
/// <para>
/// <b>A lookup, never a copy.</b> Nothing returned from here is written to a shipped table or to
/// the lore store. The catalogue's licence is its own and is acknowledged in <c>NOTICE</c>.
/// </para>
/// </summary>
public interface INotablePlacesService
{
    /// <summary>
    /// The most notable places within a radius of a position, best first. Throws
    /// <see cref="GalaxyUnavailableException"/> with a sentence fit to say aloud when the catalogue
    /// cannot be reached.
    /// </summary>
    Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken);
}
