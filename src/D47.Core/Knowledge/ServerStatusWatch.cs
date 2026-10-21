using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace D47.Core.Knowledge;

/// <summary>
/// Holds the state of Elite's servers, asking <see cref="IServerStatusSource"/> once when switched on, then every
/// <see cref="OnlineInterval"/> after an Online answer and every <see cref="OtherInterval"/> after any other,
/// whether or not Elite runs. The fetch runs on the pool. Tick thread only.
/// </summary>
public sealed class ServerStatusWatch
{
    public static readonly TimeSpan OnlineInterval = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan OtherInterval = TimeSpan.FromMinutes(1);

    private readonly IServerStatusSource _source;
    private readonly Func<bool> _enabled;
    private readonly ILogger<ServerStatusWatch> _logger;

    private readonly ConcurrentQueue<GameServerState> _answers = new();

    private bool _fetching;
    private bool _wasEnabled;
    private DateTimeOffset? _fetchedAt;
    private GameServerState? _answer;
    private DateTimeOffset? _answeredAt;
    private GameServerState? _lastKnown;

    public ServerStatusWatch(IServerStatusSource source, Func<bool> enabled, ILogger<ServerStatusWatch> logger)
    {
        _source = source;
        _enabled = enabled;
        _logger = logger;
    }

    /// <summary>Starts the fetch off the tick thread; must return without waiting for it.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>The latest answer; null while the setting is off or before the first answer.</summary>
    public GameServerState? Shown { get; private set; }

    /// <summary>When <see cref="Shown"/> was last answered; null whenever it is null.</summary>
    public DateTimeOffset? ShownAt { get; private set; }

    /// <summary>Raised on the tick thread when <see cref="Shown"/> changes.</summary>
    public event Action<GameServerState?>? ShownChanged;

    /// <summary>The state to announce after the last <see cref="Poll"/>, or null for nothing.</summary>
    public GameServerState? Announce { get; private set; }

    /// <summary>The state a change from <paramref name="previous"/> to <paramref name="next"/> is announced as, or null.</summary>
    public static GameServerState? Announces(GameServerState? previous, GameServerState next) => next switch
    {
        GameServerState.Maintenance or GameServerState.Offline when previous is null or GameServerState.Online => next,
        GameServerState.Online when previous is GameServerState.Maintenance or GameServerState.Offline => next,
        _ => null,
    };

    /// <summary>Takes any finished answer, then starts a fetch when one is due.</summary>
    public void Poll(DateTimeOffset now)
    {
        Announce = null;

        var enabled = _enabled();

        if (!enabled)
        {
            _answers.Clear();
            _answer = null;
            _answeredAt = null;
            _lastKnown = null;
            _fetching = false;
        }

        while (_answers.TryDequeue(out var answer))
        {
            _fetching = false;
            _answer = answer;
            _answeredAt = now;

            if (answer == GameServerState.Unknown)
            {
                continue;
            }

            Announce = Announces(_lastKnown, answer) ?? Announce;
            _lastKnown = answer;
        }

        var shown = enabled ? _answer : null;
        ShownAt = shown is null ? null : _answeredAt;

        if (shown != Shown)
        {
            Shown = shown;
            ShownChanged?.Invoke(shown);
        }

        if (enabled && !_wasEnabled)
        {
            _fetchedAt = null;
        }

        _wasEnabled = enabled;

        if (!enabled || _fetching)
        {
            return;
        }

        var interval = _answer == GameServerState.Online ? OnlineInterval : OtherInterval;

        if (_fetchedAt is { } at && now - at < interval)
        {
            return;
        }

        _fetching = true;
        _fetchedAt = now;
        Dispatch(Fetch);
    }

    private async Task Fetch()
    {
        var state = GameServerState.Unknown;

        try
        {
            state = await _source.ReadAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Server status fetch failed: {Reason}", ex.GetType().Name);
        }
        finally
        {
            _answers.Enqueue(state);
        }
    }
}
