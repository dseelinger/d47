using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Journal;
using Microsoft.Extensions.Logging;

namespace D47.Core.Web;

/// <summary>
/// Uploads the flying Commander's <see cref="WebSnapshot"/> while <see cref="WebAppSettings.Enabled"/> is on and a
/// token is stored for them. <see cref="Tick"/> builds a document at most once a <see cref="Floor"/> and puts it in a
/// one-item slot, replacing one still waiting; serialising, hashing, compressing and the request run through
/// <see cref="Dispatch"/>, one upload at a time. A document equal to the last one stored for its Commander with the
/// same token is not sent; <see cref="WebSnapshot.TakenAt"/> is left out of that comparison. A failure delays the next
/// build by <see cref="AfterFailure"/>; a 401 removes the token. A build that cannot send builds nothing.
/// </summary>
public sealed class WebSnapshotUpload
{
    /// <summary>The least time between two builds.</summary>
    public static readonly TimeSpan Floor = TimeSpan.FromMinutes(1);

    /// <summary>How long a failed upload delays the next build.</summary>
    public static readonly TimeSpan AfterFailure = TimeSpan.FromMinutes(5);

    private readonly Func<D47Settings> _settings;
    private readonly Func<CommanderGameState?> _active;
    private readonly Func<CommanderGameState, DateTimeOffset, WebSnapshot> _build;
    private readonly WebAppLink _link;
    private readonly IWebAppClient _client;
    private readonly bool _canSend;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly ManualResetEventSlim _idle = new(true);
    private readonly Dictionary<string, (byte[] Token, byte[] Body)> _stored = new(StringComparer.Ordinal);
    private WebSnapshot? _waiting;
    private int _running;
    private long _delayUntil = long.MinValue;
    private DateTimeOffset _nextBuild = DateTimeOffset.MinValue;

    /// <param name="build">Builds the document for a Commander at a time; called on the tick thread, so it reads no file.</param>
    /// <param name="canSend">False on a test drive: nothing is built or sent.</param>
    /// <param name="time">The clock a failure is timed by.</param>
    public WebSnapshotUpload(
        Func<D47Settings> settings,
        Func<CommanderGameState?> active,
        Func<CommanderGameState, DateTimeOffset, WebSnapshot> build,
        WebAppLink link,
        IWebAppClient client,
        bool canSend,
        TimeProvider time,
        ILogger<WebSnapshotUpload> logger)
    {
        _settings = settings;
        _active = active;
        _build = build;
        _link = link;
        _client = client;
        _canSend = canSend;
        _time = time;
        _logger = logger;
    }

    /// <summary>Runs an upload; must return without waiting.</summary>
    public Action<Func<Task>> Dispatch { get; set; } = work => _ = Task.Run(work);

    /// <summary>The tick subscriber: builds a document when one is due and hands it to the pool. Does not wait.</summary>
    public void Tick(DateTimeOffset now)
    {
        if (!_canSend)
        {
            return;
        }

        if (!_settings().WebApp.Enabled)
        {
            Interlocked.Exchange(ref _waiting, null);
            return;
        }

        if (now < _nextBuild || now.UtcTicks < Interlocked.Read(ref _delayUntil))
        {
            return;
        }

        if (_active() is not { } state || !_link.SignedIn(state.Identity.FrontierId))
        {
            return;
        }

        _nextBuild = now + Floor;
        Interlocked.Exchange(ref _waiting, _build(state, now));
        StartDrain();
    }

    /// <summary>At exit, after the last tick: sends a document still waiting, waiting no longer than <paramref name="most"/>.</summary>
    public void SendWaiting(TimeSpan most)
    {
        if (!_canSend)
        {
            return;
        }

        if (Volatile.Read(ref _waiting) is not null)
        {
            StartDrain();
        }

        _idle.Wait(most);
    }

    private bool Delayed() => _time.GetUtcNow().UtcTicks < Interlocked.Read(ref _delayUntil);

    private void StartDrain()
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            return;
        }

        _idle.Reset();
        Dispatch(DrainAsync);
    }

    private async Task DrainAsync()
    {
        try
        {
            while (Next() is { } document)
            {
                await SendAsync(document).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Uploading the web app snapshot failed");
            Delay();
        }
        finally
        {
            _idle.Set();
            Volatile.Write(ref _running, 0);
        }

        // A document offered after the last Next and before the flag cleared would otherwise wait for the next build.
        if (Volatile.Read(ref _waiting) is not null && !Delayed())
        {
            StartDrain();
        }
    }

    /// <summary>The waiting document, or null when there is none, the setting is off or a failure delays it.</summary>
    private WebSnapshot? Next()
    {
        if (!_settings().WebApp.Enabled)
        {
            Interlocked.Exchange(ref _waiting, null);
            return null;
        }

        return Delayed() ? null : Interlocked.Exchange(ref _waiting, null);
    }

    private async Task SendAsync(WebSnapshot document)
    {
        var fid = document.FrontierId;

        if (!_link.TryToken(fid, out var token))
        {
            return;
        }

        var key = (
            Token: SHA256.HashData(Encoding.UTF8.GetBytes(token)),
            Body: SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document with { TakenAt = DateTimeOffset.UnixEpoch }, WebSnapshot.Json)));

        if (_stored.TryGetValue(fid, out var last) && last.Token.AsSpan().SequenceEqual(key.Token) && last.Body.AsSpan().SequenceEqual(key.Body))
        {
            return;
        }

        var result = await _client.PutSnapshotAsync(token, Gzip(JsonSerializer.SerializeToUtf8Bytes(document, WebSnapshot.Json)), CancellationToken.None)
            .ConfigureAwait(false);

        if (result.Stored)
        {
            _stored[fid] = key;
            return;
        }

        _stored.Remove(fid);

        if (result.Unauthorized)
        {
            _link.Refused(fid, token);
            return;
        }

        if (result.Status is { } status)
        {
            _logger.LogInformation("The web app did not store the snapshot: it returned {Status}. D47 tries again in five minutes", status);
        }
        else
        {
            _logger.LogInformation("The web app snapshot could not be sent ({Error}). D47 tries again in five minutes", result.Error);
        }

        Delay();
    }

    private void Delay() => Interlocked.Exchange(ref _delayUntil, (_time.GetUtcNow() + AfterFailure).UtcTicks);

    private static byte[] Gzip(byte[] bytes)
    {
        using var compressed = new MemoryStream();

        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            gzip.Write(bytes);
        }

        return compressed.ToArray();
    }
}
