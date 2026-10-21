namespace D47.Core.Knowledge;

/// <summary>The seam to whatever serves GalNet articles, newest first.</summary>
public interface IGalNetSource
{
    /// <summary>One page starting at <paramref name="offset"/>; null when the source did not answer or answered with nothing usable.</summary>
    Task<GalNetPage?> PageAsync(int offset, CancellationToken cancellationToken);
}

/// <summary>Articles newest first, and whether older ones follow.</summary>
public sealed record GalNetPage(IReadOnlyList<GalNetArticle> Articles, bool HasOlder);
