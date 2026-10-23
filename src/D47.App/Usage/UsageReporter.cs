using System.Net.Http;
using System.Text;
using D47.Core.Capabilities;
using D47.Core.Configuration;
using D47.Core.Storage;
using D47.Core.Usage;
using Microsoft.Extensions.Logging;

namespace D47.App.Usage;

/// <summary>
/// Counts tool calls into a <see cref="UsageLedger"/> kept in <c>data\usage-counts.json</c> and sends each completed
/// UTC day once, at startup and hourly. Sends run on the pool; recording only takes a lock.
/// </summary>
public sealed class UsageReporter : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly UsageLedger _ledger;
    private readonly string _file;
    private readonly IFileSystem _files;
    private readonly Func<bool> _counts;
    private readonly Func<DateTimeOffset> _now;
    private readonly bool _canSend;
    private readonly ILogger _logger;
    private readonly HttpClient _http;
    private readonly string _address;
    private readonly SemaphoreSlim _sending = new(1, 1);
    private Timer? _timer;
    private int _dirty;

    /// <param name="counts">Read at each call, so turning the setting off stops the next count and send.</param>
    /// <param name="canSend">False on a test drive: counts are kept and nothing is sent.</param>
    public UsageReporter(
        string file,
        IFileSystem files,
        Func<bool> counts,
        Func<DateTimeOffset> now,
        bool canSend,
        ILogger logger,
        HttpMessageHandler? handler = null,
        string address = UsageCountSettings.Address)
    {
        _file = file;
        _files = files;
        _counts = counts;
        _now = now;
        _canSend = canSend;
        _logger = logger;
        _address = address.TrimEnd('/');
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47-usage");

        if (counts())
        {
            _ledger = Load();
        }
        else
        {
            _ledger = new UsageLedger();
            DeleteFile();
        }
    }

    /// <summary>The counts held, for the panel and tests.</summary>
    public UsageLedger Ledger => _ledger;

    /// <summary>Starts counting tool calls.</summary>
    public void Follow(CapabilityRegistry registry) =>
        registry.ToolInvoked += invocation => Record(invocation.Tool, invocation.Caller);

    /// <summary>Call when any setting changes; empties the ledger and deletes its file if counting is now off.</summary>
    public void SettingsChanged()
    {
        if (!_counts())
        {
            Disable();
        }
    }

    /// <summary>Counts one call; does nothing while the setting is off.</summary>
    public void Record(string tool, ToolCaller caller)
    {
        if (!_counts())
        {
            return;
        }

        _ledger.Record(tool, caller, _now());
        Volatile.Write(ref _dirty, 1);
    }

    /// <summary>Sends now and then hourly, unless this build cannot send.</summary>
    public void Start()
    {
        if (!_canSend || _timer is not null)
        {
            return;
        }

        _ = Task.Run(Tick);
        _timer = new Timer(_ => _ = Task.Run(Tick), null, Interval, Interval);
    }

    /// <summary>Empties the ledger and deletes its file.</summary>
    public void Disable()
    {
        _ledger.Clear();
        Volatile.Write(ref _dirty, 0);
        DeleteFile();
    }

    /// <summary>Writes the ledger when it has changed.</summary>
    public void Save()
    {
        if (!_counts() || Interlocked.Exchange(ref _dirty, 0) == 0)
        {
            return;
        }

        try
        {
            _files.WriteText(_file, _ledger.ToJson());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Volatile.Write(ref _dirty, 1);
            _logger.LogWarning(ex, "Could not write the usage counts");
        }
    }

    /// <summary>Sends every completed day not yet accepted, one request at a time.</summary>
    public async Task SendDueAsync()
    {
        await _sending.WaitAsync().ConfigureAwait(false);

        try
        {
            if (!_counts())
            {
                return;
            }

            var today = UsageLedger.DayOf(_now());
            _ledger.DropOld(today);
            Volatile.Write(ref _dirty, 1);

            foreach (var day in _ledger.CompletedDays(today))
            {
                if (_ledger.Report(day) is not { } body)
                {
                    _ledger.Remove(day);
                    continue;
                }

                if (!await SendDayAsync(day, body).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
        finally
        {
            _sending.Release();
            Save();
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _http.Dispose();
        _sending.Dispose();
    }

    private async Task Tick()
    {
        try
        {
            await SendDueAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Sending usage counts failed");
        }
    }

    /// <summary>False when the day should be tried again later and the rest should wait.</summary>
    private async Task<bool> SendDayAsync(DateOnly day, string body)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _address + "/usage")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("d47-format", "1");

            using var response = await _http.SendAsync(request, CancellationToken.None).ConfigureAwait(false);
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                _ledger.Remove(day);
                return true;
            }

            if (status is >= 400 and < 500)
            {
                _logger.LogInformation("Usage counts for {Day} were refused: the usage Worker returned {Status}.", day, response.StatusCode);
                _ledger.Remove(day);
                return true;
            }

            _logger.LogInformation("Usage counts for {Day} were not accepted: the usage Worker returned {Status}.", day, response.StatusCode);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            _logger.LogInformation("Usage counts for {Day} could not be sent ({Error}).", day, ex.GetType().Name);
            return false;
        }
    }

    private UsageLedger Load()
    {
        try
        {
            return _files.ReadText(_file) is { } json ? UsageLedger.Parse(json) : new UsageLedger();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Could not read the usage counts; starting new ones");
            return new UsageLedger();
        }
    }

    private void DeleteFile()
    {
        try
        {
            _files.Delete(_file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not delete the usage counts");
        }
    }
}
