using D47.Core.Conversation;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>A system, body or station named in the Bookmarks location lookup.</summary>
public sealed record PlaceMatch(BookmarkKind Kind, string Name, long SystemAddress);

/// <summary>The line the Bookmarks list shows under ADD, written by the add and edit pages.</summary>
public sealed class BookmarkReply
{
    public string? Text { get; set; }
}

/// <summary>Looking up systems, bodies and stations together, and naming what a bookmark points at.</summary>
public static class BookmarkPlaces
{
    /// <summary>Systems first, then bodies, then stations; a lookup that fails lists nothing.</summary>
    public static Func<string, CancellationToken, Task<IReadOnlyList<PlaceMatch>>> Lookup(IStarSystemService service) =>
        async (typed, cancellation) =>
        {
            var systems = Task.Run(() => Quietly(service.PrefixNamesAsync(typed, cancellation)), cancellation);
            var bodies = Task.Run(() => Quietly(service.BodyNamesAsync(typed, cancellation)), cancellation);
            var stations = Task.Run(() => Quietly(service.StationNamesAsync(typed, cancellation)), cancellation);

            await Task.WhenAll(systems, bodies, stations);

            return
            [
                .. systems.Result.Select(match => new PlaceMatch(BookmarkKind.System, match.Name, match.SystemAddress)),
                .. bodies.Result.Select(match => new PlaceMatch(BookmarkKind.Body, match.Name, match.SystemAddress)),
                .. stations.Result.Select(match => new PlaceMatch(BookmarkKind.Station, match.Name, match.SystemAddress)),
            ];
        };

    /// <summary>The kind as the lookup list and the list rows tag it.</summary>
    public static string Word(BookmarkKind kind) => kind switch
    {
        BookmarkKind.Body => "Body",
        BookmarkKind.Station => "Station",
        _ => "System",
    };

    /// <summary>What the location field shows for a place: the body or station, else the system.</summary>
    public static string Label(BookmarkPlace place) => place.Place ?? place.System ?? string.Empty;

    private static async Task<IReadOnlyList<T>> Quietly<T>(Task<IReadOnlyList<T>> lookup)
    {
        try
        {
            return await lookup.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return [];
        }
    }
}
