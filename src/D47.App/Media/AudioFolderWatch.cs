using D47.Core.Audio;
using Microsoft.Extensions.Logging;

namespace D47.App.Media;

/// <summary>
/// Watches <c>data/audio/</c> and runs <c>rebuild</c> on the thread pool once the folder has been quiet
/// for <see cref="QuietFor"/>. One rebuild at a time; a change during a rebuild gives one more after it.
/// </summary>
public sealed class AudioFolderWatch : IDisposable
{
    public static readonly TimeSpan QuietFor = TimeSpan.FromSeconds(3);

    private readonly FileSystemWatcher _watcher;
    private readonly Timer _timer;
    private readonly Action _rebuild;
    private readonly ILogger _logger;
    private readonly QuietPeriod _quiet = new(QuietFor);
    private readonly Lock _gate = new();
    private bool _disposed;

    public AudioFolderWatch(string folder, Action rebuild, ILogger logger)
    {
        _rebuild = rebuild;
        _logger = logger;
        _timer = new Timer(_ => Expired(), null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName
                | NotifyFilters.DirectoryName
                | NotifyFilters.LastWrite
                | NotifyFilters.Size,
        };

        _watcher.Created += (_, _) => Changed();
        _watcher.Changed += (_, _) => Changed();
        _watcher.Deleted += (_, _) => Changed();
        _watcher.Renamed += (_, _) => Changed();
        _watcher.Error += (_, e) =>
        {
            _logger.LogWarning(e.GetException(), "The audio folder watch lost events; rescanning it");
            Changed();
        };

        _watcher.EnableRaisingEvents = true;
    }

    private void Changed()
    {
        lock (_gate)
        {
            _quiet.Changed(Now());
            Arm();
        }
    }

    private void Expired()
    {
        lock (_gate)
        {
            if (_disposed || !_quiet.Start(Now()))
            {
                Arm();
                return;
            }
        }

        try
        {
            _rebuild();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not rebuild the audio library; keeping the one already loaded");
        }
        finally
        {
            lock (_gate)
            {
                _quiet.Finished();
                Arm();
            }
        }
    }

    /// <summary>Sets the timer for the next due rebuild, if one is waiting. Called under the gate.</summary>
    private void Arm()
    {
        if (!_disposed && _quiet.Wait(Now()) is { } wait)
        {
            _timer.Change(wait, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>A monotonic clock, so a change to the system time cannot stretch the quiet period.</summary>
    private static DateTimeOffset Now() =>
        DateTimeOffset.UnixEpoch + TimeSpan.FromMilliseconds(Environment.TickCount64);

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        _watcher.Dispose();
        _timer.Dispose();
    }
}
