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
    Func<Uri, Task<PodcastRefresh>> add)
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

    public string Pause() => Player.Pause();

    public string Resume() => Player.Resume();

    public string Stop() => Player.Stop();

    public Task<string> Next() => Player.Next();

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
