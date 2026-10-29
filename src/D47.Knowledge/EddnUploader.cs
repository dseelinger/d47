using System.Net;
using System.Text;
using System.Threading.Channels;
using D47.Core.Contribution;
using Microsoft.Extensions.Logging;

namespace D47.Knowledge;

/// <summary>Sends EDDN messages from a bounded queue, off the tick.</summary>
public sealed class EddnUploader : IEddnSink, IDisposable
{
    public const string Endpoint = "https://eddn.edcd.io:4430/upload/";

    public const int Capacity = 100;

    public const int MaxAttempts = 3;

    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(60);

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly Channel<EddnEnvelope> _queue = Channel.CreateBounded<EddnEnvelope>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private readonly List<Waiting> _waiting = [];

    private readonly HttpClient _http;

    private readonly Func<bool> _enabled;

    private readonly ILogger<EddnUploader> _logger;

    private readonly bool _ownsClient;

    private readonly CancellationTokenSource _stop = new();

    private Task? _drain;

    public EddnUploader(Func<bool> enabled, ILogger<EddnUploader> logger, HttpClient? http = null)
    {
        _enabled = enabled;
        _logger = logger;
        _ownsClient = http is null;
        _http = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        if (!_http.DefaultRequestHeaders.UserAgent.Any())
        {
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("d47/0.1 (+https://github.com/dseelinger/d47)");
        }
    }

    public void Post(EddnEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        _queue.Writer.TryWrite(envelope);
    }

    /// <summary>Starts the drain on the thread pool. Does nothing if it is running.</summary>
    public void Start() => _drain ??= Task.Run(() => RunAsync(_stop.Token));

    public void Dispose()
    {
        _stop.Cancel();

        try
        {
            _drain?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The drain was cancelled.
        }

        _stop.Dispose();

        if (_ownsClient)
        {
            _http.Dispose();
        }
    }

    /// <summary>One pass: takes what is queued, then sends each message whose turn has come.</summary>
    public async Task ProcessAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        while (_queue.Reader.TryRead(out var envelope))
        {
            _waiting.Add(new Waiting(envelope, 0, now));
        }

        if (!_enabled())
        {
            _waiting.Clear();

            return;
        }

        var due = _waiting.Where(waiting => waiting.NotBefore <= now).ToList();

        foreach (var waiting in due)
        {
            if (!_enabled())
            {
                _waiting.Clear();

                return;
            }

            _waiting.Remove(waiting);

            if (await SendAsync(waiting.Envelope, cancellationToken).ConfigureAwait(false) is { } retry)
            {
                var attempts = waiting.Attempts + 1;

                if (attempts < MaxAttempts)
                {
                    _waiting.Add(new Waiting(waiting.Envelope, attempts, now + RetryDelay));
                }
                else
                {
                    _logger.LogWarning("EDDN message dropped after {Attempts} attempts: {Reason}", attempts, retry);
                }
            }
        }
    }

    /// <summary>Null when the message is finished, with sent or dropped; otherwise why it should be tried again.</summary>
    private async Task<string?> SendAsync(EddnEnvelope envelope, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionExact,
            Content = new StringContent(envelope.ToJson(), Encoding.UTF8, "application/json"),
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);

        try
        {
            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                return null;
            }

            if (status is 408 or 413 or >= 500)
            {
                return $"status {status}";
            }

            _logger.LogWarning(
                "EDDN refused a message with status {Status}: {Reason}",
                status,
                await FirstLineAsync(response).ConfigureAwait(false));

            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return "timed out";
        }
        catch (HttpRequestException ex)
        {
            return ex.GetType().Name;
        }
    }

    private static async Task<string> FirstLineAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return body.Split('\n', 2)[0].Trim();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return "";
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessAsync(DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);

                using var wake = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                if (_waiting.Count > 0)
                {
                    wake.CancelAfter(TimeSpan.FromSeconds(1));
                }

                try
                {
                    await _queue.Reader.WaitToReadAsync(wake.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }
            }
        }
        catch (OperationCanceledException)
        {
            // d47 is shutting down.
        }
    }

    private sealed record Waiting(EddnEnvelope Envelope, int Attempts, DateTimeOffset NotBefore);
}
