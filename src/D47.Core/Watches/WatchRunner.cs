using System.Collections.Concurrent;
using System.Globalization;
using D47.Core.Callouts;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Watches;

/// <summary>A line a watch made ready, keyed by the watch.</summary>
public sealed record WatchNotice(Guid WatchId, string Text);

/// <summary>
/// Checks stock watches against the market index, one market at a time and each watch at most once every
/// <see cref="Interval"/>, only while a BGS tick is known and the line would be said.
/// A New Community Goal watch speaks for GalNet launches.
/// A next-system watch speaks once on arrival in a system with a matching station, then turns itself off; one station
/// query is sent per arrival, for every next-system watch.
/// Quotes and station queries run on the pool. Tick thread only.
/// </summary>
public sealed class WatchRunner
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly WatchStore _store;
    private readonly ITradePlanService _trade;
    private readonly Func<DateTimeOffset?> _lastTick;
    private readonly Func<IReadOnlyList<GalNetArticle>> _arrived;
    private readonly ILogger<WatchRunner> _logger;

    /// <summary>One entry per finished quote; the quote is null when the index has no report or the check failed.</summary>
    private readonly ConcurrentQueue<Answer> _answers = new();

    private readonly Dictionary<Guid, DateTimeOffset> _checkedAt = [];

    private readonly List<GalNetArticle> _launches = [];

    private readonly ConcurrentQueue<Arrival> _arrivals = new();

    private bool _fetching;

    private string? _lastSystem;

    /// <summary>An answered arrival whose firing could not be stored, tried again each poll while still in that system.</summary>
    private Arrival? _held;

    private bool _toldNoGalaxy;

    public WatchRunner(
        WatchStore store,
        ITradePlanService trade,
        Func<DateTimeOffset?> lastTick,
        Func<IReadOnlyList<GalNetArticle>> arrived,
        ILogger<WatchRunner> logger)
    {
        _store = store;
        _trade = trade;
        _lastTick = lastTick;
        _arrived = arrived;
        _logger = logger;
    }

    /// <summary>Starts the quote off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>Whether a ready line would be said; while false nothing is checked and no watch is marked fired.</summary>
    public Func<bool> Audible { get; set; } = () => true;

    /// <summary>The system the Commander is in, or null when not known or Elite is not running.</summary>
    public Func<string?> CurrentSystem { get; set; } = () => null;

    /// <summary>The station search a next-system watch asks, or null when there is none.</summary>
    public Func<IGalaxyService?> Galaxy { get; set; } = () => null;

    /// <summary>The lines made ready in the last <see cref="Poll"/>.</summary>
    public IReadOnlyList<WatchNotice> Ready { get; private set; } = [];

    /// <summary>Takes any finished quote, then starts one for the next watch that is due.</summary>
    public void Poll(DateTimeOffset now)
    {
        _store.Poll();

        var ready = new List<WatchNotice>();
        var allowed = Audible();
        var tick = _lastTick();

        while (_answers.TryDequeue(out var answer))
        {
            _fetching = false;

            if (allowed && tick is { } current && Fires(answer, current, now) is { } notice)
            {
                ready.Add(notice);
            }
        }

        if (GalNetLaunchNotice(allowed, tick) is { } launch)
        {
            ready.Add(launch);
        }

        ready.AddRange(ArrivalNotices(allowed, now));

        Ready = ready;

        if (!allowed || _fetching || tick is null)
        {
            return;
        }

        var due = _store.Watches
            .Where(watch => watch is { Enabled: true, Kind: WatchKind.Stock, MarketId: not null, Commodity: not null })
            .Where(watch => WatchRule.MayFire(tick, watch.FiredForTick))
            .Where(watch => !_checkedAt.TryGetValue(watch.Id, out var at) || now - at >= Interval)
            .OrderBy(watch => _checkedAt.TryGetValue(watch.Id, out var at) ? at : DateTimeOffset.MinValue)
            .FirstOrDefault();

        if (due is null)
        {
            return;
        }

        _fetching = true;
        _checkedAt[due.Id] = now;

        var request = new Request(due.Id, due.MarketId!.Value, due.Commodity!);
        Dispatch(() => Fetch(request));
    }

    /// <summary>The spoken line for held launches, titles joined as the GalNet headlines are.</summary>
    public static string LaunchLine(IReadOnlyList<GalNetArticle> launches)
    {
        ArgumentNullException.ThrowIfNull(launches);

        var titles = launches.Select(article => article.Title.Trim().TrimEnd('.')).ToList();

        return titles.Count == 1
            ? $"GalNet may have a new Community Goal: {titles[0]}."
            : $"GalNet may have new Community Goals: {GalNetCallout.Join(titles)}.";
    }

    /// <summary>The spoken line: the commodity, the station, the system and the stock; a missing station or system is left out.</summary>
    public static string Line(Watch watch, StationQuote quote)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(quote);

        var place = string.Join(", ", new[] { watch.Station, watch.System }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        var stock = $"{quote.Stock.ToString("N0", CultureInfo.InvariantCulture)} in stock.";

        return place.Length == 0 ? $"{watch.Commodity}: {stock}" : $"{watch.Commodity} at {place}: {stock}";
    }

    private WatchNotice? GalNetLaunchNotice(bool allowed, DateTimeOffset? tick)
    {
        var watch = _store.Watches.FirstOrDefault(w => w is { Enabled: true, Kind: WatchKind.NewCommunityGoal });

        if (watch is null)
        {
            _launches.Clear();
            return null;
        }

        foreach (var article in _arrived())
        {
            if (CommunityGoalLaunch.Recognises(article) && !_launches.Contains(article))
            {
                _launches.Add(article);
            }
        }

        if (_launches.Count == 0 || !allowed || tick is not { } current || !WatchRule.MayFire(current, watch.FiredForTick))
        {
            return null;
        }

        var line = LaunchLine([.. _launches.OrderByDescending(article => article.Published)]);

        // Held until the firing is stored, so a file that cannot be written does not drop the launches.
        if (!_store.MarkFired(watch.Id, current))
        {
            return null;
        }

        _launches.Clear();
        return new WatchNotice(watch.Id, line);
    }

    /// <summary>Says what answered arrivals match, then asks about the system just arrived in.</summary>
    private List<WatchNotice> ArrivalNotices(bool allowed, DateTimeOffset now)
    {
        var notices = new List<WatchNotice>();
        var here = CurrentSystem()?.Trim();

        var answered = new List<Arrival>();

        if (_held is { } held)
        {
            answered.Add(held);
            _held = null;
        }

        while (_arrivals.TryDequeue(out var arrival))
        {
            answered.Add(arrival);
        }

        foreach (var arrival in answered)
        {
            if (!allowed || arrival.Stations is not { } stations || !string.Equals(arrival.System, here, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var watch in _store.Watches.Where(watch => watch is { Enabled: true, Kind: WatchKind.NextSystem }))
            {
                if (WatchRule.Arrival(watch, arrival.System, stations) is not { } station)
                {
                    continue;
                }

                // Said only once the watch is stored as off, so a file that cannot be written does not drop the line.
                if (_store.MarkArrived(watch.Id, arrival.System, now))
                {
                    notices.Add(new WatchNotice(watch.Id, NextSystemWatch.Line(arrival.System, watch, station)));
                }
                else
                {
                    _held = arrival;
                }
            }
        }

        if (string.IsNullOrEmpty(here) || string.Equals(here, _lastSystem, StringComparison.OrdinalIgnoreCase))
        {
            return notices;
        }

        _lastSystem = here;

        if (!allowed || !_store.Watches.Any(watch => watch is { Enabled: true, Kind: WatchKind.NextSystem }))
        {
            return notices;
        }

        if (Galaxy() is not { } galaxy)
        {
            if (!_toldNoGalaxy)
            {
                _toldNoGalaxy = true;
                _logger.LogInformation("Next-system watches are not checked: there is no station search");
            }

            return notices;
        }

        var system = here;
        Dispatch(() => LookUp(galaxy, system));

        return notices;
    }

    private async Task LookUp(IGalaxyService galaxy, string system)
    {
        IReadOnlyList<StationSummary>? stations = null;

        try
        {
            var result = await galaxy.FindStationsAsync(StationQuery.InSystem(system), CancellationToken.None).ConfigureAwait(false);
            stations = result.Stations;
        }
        catch (GalaxyUnavailableException ex)
        {
            _logger.LogInformation("Next-system watch check for {System} failed: {Reason}", system, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Next-system watch check for {System} failed: {Reason}", system, ex.GetType().Name);
        }
        finally
        {
            _arrivals.Enqueue(new Arrival(system, stations));
        }
    }

    private WatchNotice? Fires(Answer answer, DateTimeOffset tick, DateTimeOffset now)
    {
        var request = answer.Request;

        if (_store.Watches.FirstOrDefault(watch => watch.Id == request.WatchId) is not { } watch
            || !watch.Enabled
            || watch.Kind != WatchKind.Stock
            || watch.MarketId != request.MarketId
            || !string.Equals(watch.Commodity, request.Commodity, StringComparison.Ordinal))
        {
            return null;
        }

        if (answer.Quote is not { } quote
            || !WatchRule.MayFire(tick, watch.FiredForTick)
            || !WatchRule.StockHolds(watch, quote, now))
        {
            return null;
        }

        // Said only once the firing is stored, so a file that cannot be written does not repeat the line every check.
        return _store.MarkFired(watch.Id, tick) ? new WatchNotice(watch.Id, Line(watch, quote)) : null;
    }

    private async Task Fetch(Request request)
    {
        StationQuote? quote = null;

        try
        {
            quote = await _trade.QuoteAsync(request.MarketId, request.Commodity, CancellationToken.None).ConfigureAwait(false);
        }
        catch (GalaxyUnavailableException ex)
        {
            _logger.LogInformation("Watch check for {Commodity} at market {MarketId} failed: {Reason}", request.Commodity, request.MarketId, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Watch check for {Commodity} at market {MarketId} failed: {Reason}", request.Commodity, request.MarketId, ex.GetType().Name);
        }
        finally
        {
            _answers.Enqueue(new Answer(request, quote));
        }
    }

    private sealed record Request(Guid WatchId, long MarketId, string Commodity);

    private sealed record Answer(Request Request, StationQuote? Quote);

    /// <summary>A system's stations; null when the query failed.</summary>
    private sealed record Arrival(string System, IReadOnlyList<StationSummary>? Stations);
}
