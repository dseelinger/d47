using D47.Core.Journal;
using D47.Core.Knowledge;

namespace D47.Core.Conversation;

/// <summary>
/// What the Commander's Elite destination resolves to: a kind, a place, the system that holds it and its
/// address, or the text refusing it. <see cref="ElitesName"/> is the destination's own name, for naming.
/// </summary>
public sealed record BookmarkPlace(
    BookmarkKind? Kind,
    string? Place,
    string? System,
    long? SystemAddress,
    string? ElitesName,
    string? Error)
{
    public const string NothingTargeted = "Nothing is targeted. Select a system or a station first.";

    public const string AnotherSystem =
        "That is in another system, and Elite does not name the system. Target the system itself.";

    /// <summary>
    /// Resolves the Elite destination. The kind of a body or station comes from the system's profile and is
    /// null when the profile is missing, the lookup fails, or nothing in it matches.
    /// </summary>
    public static async Task<BookmarkPlace> FromDestinationAsync(
        GameStatus? status,
        JournalLocation? location,
        IStarSystemService? starSystems,
        CancellationToken cancellationToken)
    {
        if (status?.Destination is not { } destination)
        {
            return Refused(NothingTargeted);
        }

        if (destination.Body == 0)
        {
            return string.IsNullOrWhiteSpace(destination.Name)
                ? Refused(NothingTargeted)
                : new BookmarkPlace(
                    BookmarkKind.System,
                    null,
                    destination.Name,
                    destination.System == 0 ? null : destination.System,
                    destination.Name,
                    null);
        }

        if (location?.SystemAddress != destination.System || location?.StarSystem is not { } current)
        {
            return Refused(AnotherSystem);
        }

        var kind = await KindAsync(destination, starSystems, cancellationToken);

        return new BookmarkPlace(kind, destination.Name, current, destination.System, destination.Name, null);
    }

    private static BookmarkPlace Refused(string error) => new(null, null, null, null, null, error);

    private static async Task<BookmarkKind?> KindAsync(
        StatusDestination destination, IStarSystemService? starSystems, CancellationToken cancellationToken)
    {
        if (starSystems is null || string.IsNullOrWhiteSpace(destination.Name))
        {
            return null;
        }

        try
        {
            if (await starSystems.ProfileAsync(destination.System, cancellationToken) is not { } profile)
            {
                return null;
            }

            if (profile.Bodies.Any(body => body.BodyId == destination.Body))
            {
                return BookmarkKind.Body;
            }

            return profile.Stations.Any(station =>
                string.Equals(station.Name, destination.Name, StringComparison.OrdinalIgnoreCase))
                ? BookmarkKind.Station
                : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
