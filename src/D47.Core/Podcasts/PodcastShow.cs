namespace D47.Core.Podcasts;

/// <summary>A podcast feed's title and its episodes, newest first.</summary>
public sealed record PodcastShow(string Title, IReadOnlyList<PodcastEpisode> Episodes);

/// <summary>One episode. <see cref="Id"/> is the feed's guid, or the enclosure URL when the item has none.</summary>
public sealed record PodcastEpisode(
    string Id,
    string Title,
    DateTimeOffset Published,
    Uri Enclosure,
    string MediaType,
    long? Length,
    TimeSpan? Duration);

/// <summary>A feed that cannot be read; the message is written for the Commander.</summary>
public sealed class PodcastFeedException(string message) : Exception(message);
