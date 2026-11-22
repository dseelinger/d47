namespace D47.Core.Viewing;

/// <summary>One source's own catalogue.</summary>
public interface IVideoSource
{
    /// <summary>The source's id in <see cref="VideoSources"/>.</summary>
    string Id { get; }

    /// <summary>
    /// Page <paramref name="page"/>, counted from 1, of <paramref name="pageSize"/> titles. A source that cannot be
    /// reached returns an empty page with a <see cref="VideoPage.Problem"/> rather than throwing.
    /// </summary>
    Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>The catalogue the Commander searches: every ticked source, a page of <see cref="VideoCatalogue.PageSize"/> at a time.</summary>
public interface IVideoCatalogue
{
    /// <summary>Page <paramref name="page"/>, counted from 1.</summary>
    Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, CancellationToken cancellationToken);
}

/// <summary>
/// Searches the sources <paramref name="searched"/> names. Each source gives a fixed share of every page, the 25
/// split as evenly as the number of sources allows, and the shares are interleaved; the total is their sum. A page
/// holds fewer than 25 once a source runs out, so <see cref="VideoPage.More"/>, not the total, says whether to page on.
/// </summary>
public sealed class VideoCatalogue(IReadOnlyList<IVideoSource> sources, Func<IReadOnlyList<string>> searched) : IVideoCatalogue
{
    public const int PageSize = 25;

    public async Task<VideoPage> SearchAsync(string query, VideoFilters filters, int page, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentNullException.ThrowIfNull(filters);

        var asked = searched()
            .Select(id => sources.FirstOrDefault(source => source.Id == id))
            .OfType<IVideoSource>()
            .Distinct()
            .ToList();

        if (asked.Count == 0)
        {
            return VideoPage.Empty with { Problem = "No video source is ticked, so nothing was searched." };
        }

        var shares = asked.Select((_, index) => (PageSize / asked.Count) + (index < PageSize % asked.Count ? 1 : 0)).ToList();

        var pages = await Task.WhenAll(asked.Select((source, index) =>
            AskAsync(source, query ?? string.Empty, filters, page, shares[index], cancellationToken)))
            .ConfigureAwait(false);

        var titles = new List<VideoTitle>(PageSize);

        for (var row = 0; titles.Count < PageSize && pages.Any(share => row < share.Titles.Count); row++)
        {
            foreach (var share in pages)
            {
                if (row < share.Titles.Count && titles.Count < PageSize)
                {
                    titles.Add(share.Titles[row]);
                }
            }
        }

        var problems = pages.Select(share => share.Problem).OfType<string>().ToList();

        return new VideoPage(
            titles,
            pages.Sum(share => share.Total),
            problems.Count == 0 ? null : string.Join(" ", problems),
            More: pages.Select((share, index) => (long)page * shares[index] < share.Total).Any(more => more));
    }

    private static async Task<VideoPage> AskAsync(
        IVideoSource source, string query, VideoFilters filters, int page, int size, CancellationToken cancellationToken)
    {
        try
        {
            return await source.SearchAsync(query, filters, page, size, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return VideoPage.Empty with { Problem = $"{VideoSources.NameOf(source.Id)} could not be searched." };
        }
    }
}
