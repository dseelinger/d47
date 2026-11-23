using D47.Core.Configuration;

namespace D47.Core.Podcasts;

/// <summary>One show and the titles of its unfinished episodes, newest first.</summary>
public sealed record PodcastShowLine(string Title, IReadOnlyList<string> Unfinished);

/// <summary>
/// What the podcast voice commands and the media keys act on: the player, the cached shows, and the three things
/// that reach the network (a refresh, a directory search and adding a feed).
/// </summary>
public sealed class PodcastDesk(
    PodcastPlayer player,
    Func<IReadOnlyList<CachedShow>> shows,
    Func<Task<IReadOnlyList<PodcastRefresh>>> refresh,
    Func<string, Task<PodcastSearchOutcome>> search,
    Func<Uri, Task<PodcastRefresh>> add,
    Func<Uri, PodcastShowPlay>? playFor = null,
    Action<Uri, bool?, PodcastOrder?>? setPlay = null)
{
    public PodcastPlayer Player { get; } = player;

    /// <summary>An episode is playing, or the Commander has paused it.</summary>
    public bool Active => Player.State is not null;

    /// <summary>
    /// Plays the newest unfinished episode of the show whose title is, or contains, <paramref name="show"/>; with no
    /// show, carries on the episode in the slot or plays the newest unfinished episode of any show.
    /// </summary>
    public Task<string> Play(string? show)
    {
        var cached = shows();

        if (string.IsNullOrWhiteSpace(show))
        {
            if (Player.State is { } now)
            {
                return Task.FromResult(now.Playing ? "The podcast is already playing." : Player.Resume());
            }

            return PlayNewest(cached, "No podcast shows are added.", "Every episode of every show is finished.");
        }

        var wanted = show.Trim();
        var found = cached.FirstOrDefault(entry => string.Equals(entry.Show.Title, wanted, StringComparison.OrdinalIgnoreCase))
            ?? cached.FirstOrDefault(entry => entry.Show.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase));

        return found is null
            ? Task.FromResult($"No added show is called {wanted}.")
            : PlayNewest([found], string.Empty, $"Every episode of {found.Show.Title} is finished.");
    }

    /// <summary>
    /// Carries on the episode in the slot when it is from <paramref name="feed"/>; otherwise plays that show's newest
    /// unfinished episode. The show is found by feed, not by title.
    /// </summary>
    public Task<string> PlayFeed(Uri feed)
    {
        var found = shows().FirstOrDefault(entry => string.Equals(entry.Feed.AbsoluteUri, feed.AbsoluteUri, StringComparison.Ordinal));

        if (found is null)
        {
            return Task.FromResult("That show is not added.");
        }

        if (Player.State is { } now && string.Equals(now.Feed.AbsoluteUri, feed.AbsoluteUri, StringComparison.Ordinal))
        {
            return Task.FromResult(Player.Resume());
        }

        return PlayNewest([found], string.Empty, $"Every episode of {found.Show.Title} is finished.");
    }

    public string Pause() => Player.Pause();

    public string Resume() => Player.Resume();

    public string Stop() => Player.Stop();

    public Task<string> Next() => Player.Next();

    /// <summary>
    /// Turns continuous play on or off for the show whose title is, or contains, <paramref name="show"/>, or the show in
    /// the slot, and sets its order where given; says what is now set.
    /// </summary>
    public string SetContinuous(string? show, bool? on, PodcastOrder? order)
    {
        Uri feed;
        string title;

        if (string.IsNullOrWhiteSpace(show))
        {
            if (Player.State is not { } now)
            {
                return "No podcast is playing.";
            }

            feed = now.Feed;
            title = now.Show;
        }
        else
        {
            var wanted = show.Trim();
            var cached = shows();
            var found = cached.FirstOrDefault(entry => string.Equals(entry.Show.Title, wanted, StringComparison.OrdinalIgnoreCase))
                ?? cached.FirstOrDefault(entry => entry.Show.Title.Contains(wanted, StringComparison.OrdinalIgnoreCase));

            if (found is null)
            {
                return $"No added show is called {wanted}.";
            }

            feed = found.Feed;
            title = found.Show.Title;
        }

        if (setPlay is null || playFor is null)
        {
            return "Continuous play is not available.";
        }

        setPlay(feed, on, order);
        var set = playFor(feed);

        return set.Continuous
            ? $"Continuous play is on for {title}, {(set.Order == PodcastOrder.NewestFirst ? "newest" : "oldest")} first."
            : $"Continuous play is off for {title}.";
    }

    public string SkipBack() => Player.SkipBack();

    public string SkipForward() => Player.SkipForward();

    /// <summary>Fetches every feed added and says which could not be read.</summary>
    public async Task<string> Refresh()
    {
        var results = await refresh().ConfigureAwait(false);

        if (results.Count == 0)
        {
            return "No podcast feeds are added.";
        }

        var problems = results.Where(result => result.Problem is not null).ToList();
        var read = results.Count - problems.Count;
        var said = read == 1 ? "Refreshed 1 podcast feed." : $"Refreshed {read} podcast feeds.";

        return problems.Count == 0 ? said : said + " " + string.Join(" ", problems.Select(result => result.Problem));
    }

    /// <summary>Every cached show, with its unfinished episodes.</summary>
    public IReadOnlyList<PodcastShowLine> Shows() =>
        [.. shows().Select(entry => new PodcastShowLine(
            entry.Show.Title,
            [.. Unfinished(entry).Select(episode => episode.Title)]))];

    public Task<PodcastSearchOutcome> Find(string words) => search(words);

    /// <summary>Adds the result's feed, fetches it, and says what it holds.</summary>
    public async Task<string> Add(PodcastSearchResult result)
    {
        var added = await add(result.Feed).ConfigureAwait(false);

        if (added.Show is not { } show)
        {
            return $"Added {result.Title}, but its feed could not be read yet. {added.Problem}";
        }

        return show.Episodes.Count == 1
            ? $"Added {show.Title}, with 1 episode."
            : $"Added {show.Title}, with {show.Episodes.Count} episodes.";
    }

    private IEnumerable<PodcastEpisode> Unfinished(CachedShow entry)
    {
        var listening = Player.Listening;

        return entry.Show.Episodes
            .Where(episode => listening.Find(entry.Feed, episode.Id) is not { Finished: true })
            .OrderByDescending(episode => episode.Published);
    }

    private async Task<string> PlayNewest(IReadOnlyList<CachedShow> from, string none, string allFinished)
    {
        if (from.Count == 0)
        {
            return none;
        }

        var newest = from
            .SelectMany(entry => Unfinished(entry).Select(episode => (Entry: entry, Episode: episode)))
            .OrderByDescending(pair => pair.Episode.Published)
            .Cast<(CachedShow Entry, PodcastEpisode Episode)?>()
            .FirstOrDefault();

        return newest is not { } pick
            ? allFinished
            : await Player.Play(pick.Entry.Feed, pick.Entry.Show, pick.Episode).ConfigureAwait(false);
    }
}
