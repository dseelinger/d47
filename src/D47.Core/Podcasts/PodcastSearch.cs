using System.Text.Json;

namespace D47.Core.Podcasts;

/// <summary>One show from the podcast directory. <see cref="EpisodeCount"/> and <see cref="Artwork"/> are null when the directory gives none.</summary>
public sealed record PodcastSearchResult(string Title, string Author, Uri Feed, int? EpisodeCount, Uri? Artwork);

/// <summary>A directory answer that cannot be read; the message is written for the Commander.</summary>
public sealed class PodcastSearchException(string message) : Exception(message);

/// <summary>Reads Apple's podcast search answer. The answer's fields are written by strangers.</summary>
public static class PodcastSearch
{
    /// <summary>The shows in the answer, in its order; a show with no http or https feed URL is left out.</summary>
    public static IReadOnlyList<PodcastSearchResult> Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using var document = JsonDocument.Parse(json);

            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("results", out var results)
                || results.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. results.EnumerateArray().Select(Read).OfType<PodcastSearchResult>()];
        }
        catch (JsonException)
        {
            throw new PodcastSearchException("The podcast directory's answer could not be read.");
        }
    }

    private static PodcastSearchResult? Read(JsonElement result)
    {
        if (result.ValueKind != JsonValueKind.Object || WebAddress(result, "feedUrl") is not { } feed)
        {
            return null;
        }

        var title = Text(result, "collectionName");
        int? episodes = result.TryGetProperty("trackCount", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var n) ? n : null;

        return new PodcastSearchResult(
            title.Length > 0 ? title : feed.Host,
            Text(result, "artistName"),
            feed,
            episodes,
            WebAddress(result, "artworkUrl600"));
    }

    private static string Text(JsonElement result, string name) =>
        result.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()!.Trim() : string.Empty;

    private static Uri? WebAddress(JsonElement result, string name) =>
        Uri.TryCreate(Text(result, name), UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;
}
