namespace D47.Core.Podcasts;

/// <summary>A feed and its show as last read.</summary>
public sealed record CachedShow(Uri Feed, PodcastShow Show);

/// <summary>What one refresh did. <see cref="Problem"/> is a sentence for the Commander, null when the feed was read.</summary>
public sealed record PodcastRefresh(Uri Feed, PodcastShow? Show, string? Problem);

/// <summary>What one search found. <see cref="Problem"/> is a sentence for the Commander, null when the directory answered.</summary>
public sealed record PodcastSearchOutcome(IReadOnlyList<PodcastSearchResult> Results, string? Problem);
