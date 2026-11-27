using D47.Core.Configuration;
using D47.Core.Podcasts;

namespace D47.App.Panel;

/// <summary>What Media › Podcasts reads and does, shared by the window and the headset.</summary>
public sealed class PodcastsHost(
    SettingsService settings,
    PodcastPlayer player,
    Func<IReadOnlyList<CachedShow>> shows,
    Func<IReadOnlyList<Uri>> feeds,
    Func<Task<IReadOnlyList<PodcastRefresh>>> refresh,
    Func<string, Task<PodcastSearchOutcome>> search,
    Func<Uri, Task<PodcastRefresh>> add,
    Action<Uri> remove,
    Func<Uri, PodcastShowPlay> playFor,
    Action<Uri, bool?, PodcastOrder?> setPlay,
    Func<Uri, Task<string>> playShow)
{
    private readonly Lock _gate = new();
    private Task<IReadOnlyList<PodcastRefresh>>? _refreshing;

    public SettingsService Settings { get; } = settings;

    public PodcastPlayer Player { get; } = player;

    /// <summary>The feed the page has selected, as an absolute address; both surfaces share it.</summary>
    public string? Selected { get; set; }

    /// <summary>Raised, on any thread, after a show was refreshed, added or removed, or its continuous play changed.</summary>
    public event Action? Changed;

    /// <summary>The cached show of each feed added, in the order added.</summary>
    public IReadOnlyList<CachedShow> Shows => shows();

    /// <summary>How many feeds are added, read or not.</summary>
    public int FeedCount => feeds().Count;

    public bool Following(Uri feed) =>
        feeds().Any(added => string.Equals(added.AbsoluteUri, feed.AbsoluteUri, StringComparison.Ordinal));

    /// <summary>Fetches every feed; a call while a fetch runs shares that fetch.</summary>
    public Task<IReadOnlyList<PodcastRefresh>> Refresh()
    {
        lock (_gate)
        {
            return _refreshing ??= Fetch();
        }
    }

    public Task<PodcastSearchOutcome> Search(string words) => search(words);

    /// <summary>Reads the feed and, once it parses, adds it to the feeds setting.</summary>
    public async Task<PodcastRefresh> Add(Uri feed)
    {
        var added = await add(feed).ConfigureAwait(false);

        if (added.Show is not null)
        {
            Selected = feed.AbsoluteUri;
            RaiseChanged();
        }

        return added;
    }

    /// <summary>Removes the feed, stops its episode if one plays and forgets its listening record.</summary>
    public void Remove(Uri feed)
    {
        remove(feed);

        if (string.Equals(Selected, feed.AbsoluteUri, StringComparison.Ordinal))
        {
            Selected = null;
        }

        RaiseChanged();
    }

    /// <summary>How the feed carries on: off and oldest first unless it has a setting.</summary>
    public PodcastShowPlay PlayFor(Uri feed) => playFor(feed);

    /// <summary>Sets the feed's continuous play and, where given, its order.</summary>
    public void SetPlay(Uri feed, bool? continuous, PodcastOrder? order) => setPlay(feed, continuous, order);

    /// <summary>Carries on the show's episode in the slot, or plays its newest unfinished one; says what it did.</summary>
    public Task<string> PlayShow(Uri feed) => playShow(feed);

    public void RaiseChanged() => Changed?.Invoke();

    private async Task<IReadOnlyList<PodcastRefresh>> Fetch()
    {
        try
        {
            return await refresh().ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _refreshing = null;
            }
        }
    }
}
