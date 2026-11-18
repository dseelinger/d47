using D47.Core.Journal;

namespace D47.Core.Knowledge;

/// <summary>
/// One point of interest from a curated catalogue, cut down to what a story can stand on (Phase 47,
/// "The Galactic Mapping lookup").
/// </summary>
/// <param name="SystemAddress">The catalogue's own id for the system.</param>
public sealed record NotablePlace(
    string Name,
    string Type,
    string System,
    long? SystemAddress,
    StarPosition Position)
{
    /// <summary>The catalogue's one-line description.</summary>
    public string? Summary { get; init; }

    public double? Rating { get; init; }

    public int? Id { get; init; }

    public string? Type2 { get; init; }

    public int? CategoryId { get; init; }

    public int? CategoryId2 { get; init; }

    public string? Region { get; init; }

    public string? Callsign { get; init; }

    public string? Source { get; init; }

    public string? DescriptionMarkdown { get; init; }

    public string? DescriptionHtml { get; init; }

    public string? GalMapUrl { get; init; }

    public string? PoiUrl { get; init; }

    public string? MainImage { get; init; }

    public double? SolDistance { get; init; }

    public double? Curation { get; init; }

    public double? AvgStars { get; init; }

    public int? Votes { get; init; }

    public bool? Rare { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double DistanceFrom(StarPosition from) => from.DistanceTo(Position);
}

/// <summary>The seam to a catalogue of notable places.</summary>
public interface INotablePlacesService
{
    /// <summary>The most notable places within a radius of a position, best first.</summary>
    Task<IReadOnlyList<NotablePlace>> NearAsync(StarPosition from, double radiusLightYears, int limit, CancellationToken cancellationToken);
}
