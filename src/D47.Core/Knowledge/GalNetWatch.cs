using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>
/// Keeps <see cref="GalNetArchive"/> current: one fetch when switched on, then one every <see cref="Interval"/>
/// while the setting is on. The fetch, merge and save run on the pool. Tick thread only.
/// </summary>
public sealed class GalNetWatch
{
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>How far back the first fetch reads.</summary>
    public static readonly TimeSpan Reach = TimeSpan.FromDays(731);

    /// <summary>Pages read in one fetch, at most.</summary>
    public const int MostPages = 40;

    private readonly IGalNetSource _source;
    private readonly GalNetArchive _archive;
    private readonly Func<bool> _enabled;
    private readonly ILogger<GalNetWatch> _logger;

    /// <summary>One entry per finished fetch: the articles new to the archive, empty after a failure or a first backfill.</summary>
    private readonly ConcurrentQueue<IReadOnlyList<GalNetArticle>> _answers = new();

    private bool _fetching;
    private bool _wasEnabled;
    private DateTimeOffset? _fetchedAt;

    public GalNetWatch(IGalNetSource source, GalNetArchive archive, Func<bool> enabled, ILogger<GalNetWatch> logger)
    {
        _source = source;
        _archive = archive;
        _enabled = enabled;
        _logger = logger;
    }

    /// <summary>Starts the fetch off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    public GalNetArchive Archive => _archive;

    /// <summary>The articles that arrived in the last <see cref="Poll"/>, newest first.</summary>
    public IReadOnlyList<GalNetArticle> Arrived { get; private set; } = [];

    /// <summary>Takes any finished fetch, then starts one when due.</summary>
    public void Poll(DateTimeOffset now)
    {
        var arrived = new List<GalNetArticle>();

        while (_answers.TryDequeue(out var answer))
        {
            _fetching = false;
            arrived.AddRange(answer);
        }

        Arrived = [.. arrived.OrderByDescending(article => article.Published)];

        var enabled = _enabled();

        if (enabled && !_wasEnabled)
        {
            _fetchedAt = null;
        }

        _wasEnabled = enabled;

        if (!enabled || _fetching)
        {
            return;
        }

        if (_fetchedAt is { } at && now - at < Interval)
        {
            return;
        }

        _fetching = true;
        _fetchedAt = now;
        Dispatch(() => Fetch(now));
    }

    private async Task Fetch(DateTimeOffset now)
    {
        IReadOnlyList<GalNetArticle> added = [];

        try
        {
            var firstFill = _archive.IsEmpty;
            var taken = await Read(now - Reach).ConfigureAwait(false);

            if (taken is not null)
            {
                var merged = _archive.Merge(taken);
                added = firstFill ? [] : merged;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInformation("GalNet fetch failed: {Reason}", ex.GetType().Name);
        }
        finally
        {
            _answers.Enqueue(added);
        }
    }

    /// <summary>Pages from the newest until one holds an article already kept or older than <paramref name="since"/>; null when a page failed.</summary>
    private async Task<List<GalNetArticle>?> Read(DateTimeOffset since)
    {
        var taken = new List<GalNetArticle>();

        var offset = 0;

        for (var page = 0; page < MostPages; page++)
        {
            var read = await _source.PageAsync(offset, CancellationToken.None).ConfigureAwait(false);

            if (read is null)
            {
                _logger.LogInformation("GalNet fetch stopped: page {Page} was not read", page);
                return null;
            }

            offset += read.Articles.Count;
            var recent = read.Articles.Where(article => article.Published >= since).ToList();
            taken.AddRange(recent);

            var allNew = recent.Count == read.Articles.Count && recent.All(article => !_archive.Contains(article.Id));

            if (!read.HasOlder || read.Articles.Count == 0 || !allNew)
            {
                break;
            }
        }

        return taken;
    }
}
