namespace D47.Core.Knowledge;

/// <summary>One GalNet article. <see cref="Body"/> is plain text with its paragraphs.</summary>
public sealed record GalNetArticle(
    string Id,
    string Title,
    IReadOnlyList<string> Body,
    DateTimeOffset Published,
    string GameDate);
