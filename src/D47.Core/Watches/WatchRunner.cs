using System.Collections.Concurrent;
using System.Globalization;
using D47.Core.Knowledge;
using Microsoft.Extensions.Logging;

namespace D47.Core.Watches;

/// <summary>A line a watch made ready, keyed by the watch.</summary>
public sealed record WatchNotice(Guid WatchId, string Text);

/// <summary>
/// Checks stock watches against the market index, one market at a time and each watch at most once every
/// <see cref="Interval"/>, only while Galaxy search and BGS tick time are on, a BGS tick is known and the line
/// would be said.
/// The quote runs on the pool. Tick thread only.
/// </summary>
public sealed class WatchRunner
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly WatchStore _store;
    private readonly ITradePlanService _trade;
    private readonly Func<DateTimeOffset?> _lastTick;
    private readonly Func<bool> _galaxySearch;
    private readonly Func<bool> _bgsTick;
    private readonly ILogger<WatchRunner> _logger;

    /// <summary>One entry per finished quote; the quote is null when the index has no report or the check failed.</summary>
    private readonly ConcurrentQueue<Answer> _answers = new();

    private readonly Dictionary<Guid, DateTimeOffset> _checkedAt = [];

    private bool _fetching;

    public WatchRunner(
        WatchStore store,
        ITradePlanService trade,
        Func<DateTimeOffset?> lastTick,
        Func<bool> galaxySearch,
        Func<bool> bgsTick,
        ILogger<WatchRunner> logger)
    {
        _store = store;
        _trade = trade;
        _lastTick = lastTick;
        _galaxySearch = galaxySearch;
        _bgsTick = bgsTick;
        _logger = logger;
    }

    /// <summary>Starts the quote off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>Whether a ready line would be said; while false nothing is checked and no watch is marked fired.</summary>
    public Func<bool> Audible { get; set; } = () => true;

    /// <summary>The lines made ready in the last <see cref="Poll"/>.</summary>
    public IReadOnlyList<WatchNotice> Ready { get; private set; } = [];

    /// <summary>Takes any finished quote, then starts one for the next watch that is due.</summary>
    public void Poll(DateTimeOffset now)
    {
        _store.Poll();

        var ready = new List<WatchNotice>();
        var allowed = _galaxySearch() && _bgsTick() && Audible();
        var tick = _lastTick();

        while (_answers.TryDequeue(out var answer))
        {
            _fetching = false;

            if (allowed && tick is { } current && Fires(answer, current, now) is { } notice)
            {
                ready.Add(notice);
            }
        }

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

    /// <summary>The spoken line: the commodity, the station, the system and the stock; a missing station or system is left out.</summary>
    public static string Line(Watch watch, StationQuote quote)
    {
        ArgumentNullException.ThrowIfNull(watch);
        ArgumentNullException.ThrowIfNull(quote);

        var place = string.Join(", ", new[] { watch.Station, watch.System }.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()));
        var stock = $"{quote.Stock.ToString("N0", CultureInfo.InvariantCulture)} in stock.";

        return place.Length == 0 ? $"{watch.Commodity}: {stock}" : $"{watch.Commodity} at {place}: {stock}";
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
}
