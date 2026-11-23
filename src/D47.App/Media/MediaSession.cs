using System.Runtime.InteropServices;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Books;
using D47.Core.Capabilities;
using D47.Core.Podcasts;
using D47.Core.Viewing;
using static D47.App.Interop.WinRtInterop;
using Microsoft.Extensions.Logging;
using Windows.Media;

namespace D47.App.Media;

/// <summary>
/// d47's entry in Windows' media sessions: the keyboard's play/pause and next-track keys and the volume
/// flyout's buttons reach the ambient music through it. Windows sends the keys to whichever session was
/// used last, so nothing is taken from other players. The keys and the flyout belong to the item a spoken bare
/// "pause" would act on (<see cref="MediaOwners.Of"/>): an open story playback, a book, a podcast episode or a
/// video, else the music.
/// </summary>
internal sealed class MediaSession : IDisposable
{
    private readonly SystemMediaTransportControls _controls;
    private readonly AmbientMusic _music;
    private readonly PodcastDesk? _podcasts;
    private readonly AppHost _host;
    private readonly ILogger _logger;
    private readonly HostTargets _targets;
    private bool _disposed;

    private MediaSession(SystemMediaTransportControls controls, AmbientMusic music, PodcastDesk? podcasts, AppHost host, ILogger logger)
    {
        _controls = controls;
        _music = music;
        _podcasts = podcasts;
        _host = host;
        _logger = logger;
        _targets = new HostTargets(host, music, podcasts);

        _controls.IsEnabled = true;
        _controls.IsPlayEnabled = true;
        _controls.IsPauseEnabled = true;
        _controls.IsNextEnabled = true;
        _controls.IsPreviousEnabled = true;
        _controls.ButtonPressed += OnButtonPressed;
        _music.Changed += OnChanged;

        if (_podcasts is not null)
        {
            _podcasts.Player.Changed += OnPodcastChanged;
        }

        _host.BookPlayer.Changed += OnOwnerChanged;
        _host.Video.Changed += OnOwnerChanged;
        _host.RunPlayer.Changed += OnOwnerChanged;

        Show();
    }

    /// <summary>Registers the session for <paramref name="window"/>, or returns null where Windows refuses.</summary>
    public static MediaSession? Attach(nint window, AmbientMusic music, PodcastDesk? podcasts, AppHost host, ILogger logger)
    {
        try
        {
            return new MediaSession(ForWindow(window), music, podcasts, host, logger);
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or TypeLoadException or PlatformNotSupportedException)
        {
            logger.LogWarning(ex, "Media keys are unavailable; the music still answers to voice");
            return null;
        }
    }

    private void OnButtonPressed(
        SystemMediaTransportControls sender,
        SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        MediaKey? key = args.Button switch
        {
            SystemMediaTransportControlsButton.Play => MediaKey.Play,
            SystemMediaTransportControlsButton.Pause => MediaKey.Pause,
            SystemMediaTransportControlsButton.Next => MediaKey.Next,
            SystemMediaTransportControlsButton.Previous => MediaKey.Previous,
            _ => null,
        };

        if (key is not { } pressed)
        {
            return;
        }

        var owner = Owner();

        if (owner == MediaOwner.Podcast && _podcasts is null)
        {
            owner = MediaOwner.Music;
        }

        // Playing the next podcast episode waits for it to open, so the key does not.
        _ = MediaKeys.Press(owner, pressed, _targets)?.ContinueWith(
            done => _logger.LogInformation(
                "Media key: {Button} ({Result})", args.Button, done.IsCompletedSuccessfully ? done.Result : "failed"),
            TaskScheduler.Default);
    }

    private MediaOwner Owner() => MediaOwners.Of(
        () => _host.AdventureDesk?.PlaybackOpen() == true,
        () => _host.BookDesk,
        () => _host.PodcastDesk,
        () => _host.WatchDesk);

    /// <summary>Raised on the tick; the flyout is updated on the UI thread instead.</summary>
    private void OnChanged(MusicState state) => Avalonia.Threading.Dispatcher.UIThread.Post(Show);

    private void OnPodcastChanged(PodcastPlayerState? state) => Avalonia.Threading.Dispatcher.UIThread.Post(Show);

    private void OnOwnerChanged() => Avalonia.Threading.Dispatcher.UIThread.Post(Show);

    private void Show()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var flyout = MediaKeys.Flyout(Owner(), _targets);

            _controls.PlaybackStatus = flyout.Status switch
            {
                MediaFlyoutStatus.Playing => MediaPlaybackStatus.Playing,
                MediaFlyoutStatus.Paused => MediaPlaybackStatus.Paused,
                _ => MediaPlaybackStatus.Stopped,
            };

            var display = _controls.DisplayUpdater;

            if (flyout.Title is { } title)
            {
                display.Type = MediaPlaybackType.Music;
                display.MusicProperties.Title = title;
                display.MusicProperties.Artist = flyout.Artist;
            }
            else
            {
                display.ClearAll();
            }

            display.Update();
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "The media flyout could not be updated");
        }
    }

    private sealed class HostTargets(AppHost host, AmbientMusic music, PodcastDesk? podcasts) : IMediaKeyTargets, IMediaFlyoutSource
    {
        public void StoryResume() => host.RunPlayer.Resume();

        public void StoryPause() => host.RunPlayer.Pause(StoryRunPause.Commander);

        public void StorySkip() => host.RunPlayer.Skip();

        public string BookResume() => host.BookDesk.Resume();

        public string BookPause() => host.BookDesk.Pause();

        public string BookNextChapter() => host.BookDesk.NextChapter();

        public string BookBackAParagraph() => host.BookDesk.BackAParagraph();

        public string VideoResume() => host.WatchDesk.Resume();

        public string VideoPause() => host.WatchDesk.Pause();

        public string VideoSkip(int seconds) => host.WatchDesk.Skip(seconds);

        public string PodcastResume() => podcasts!.Resume();

        public string PodcastPause() => podcasts!.Pause();

        public string PodcastSkipBack() => podcasts!.SkipBack();

        public Task<string> PodcastNext() => podcasts!.Next();

        public string MusicControl(MusicAction action) => music.Control(action);

        public string? StoryTitle => host.RunPlayer.Now?.Run.Title;

        public bool StoryPaused => host.RunPlayer.Now?.IsPaused == true;

        public BookNow? Book => host.BookPlayer.Now?.ToBookNow();

        public PodcastPlayerState? Podcast => podcasts?.Player.State;

        public VideoTitle? Video => host.WatchHost.Current;

        public bool VideoPlaying => host.WatchDesk.Playing;

        public MusicState Music => music.State;
    }

    public void Dispose()
    {
        _disposed = true;
        _music.Changed -= OnChanged;

        if (_podcasts is not null)
        {
            _podcasts.Player.Changed -= OnPodcastChanged;
        }

        _host.BookPlayer.Changed -= OnOwnerChanged;
        _host.Video.Changed -= OnOwnerChanged;
        _host.RunPlayer.Changed -= OnOwnerChanged;
        _controls.ButtonPressed -= OnButtonPressed;
        _controls.IsEnabled = false;
    }

    private static SystemMediaTransportControls ForWindow(nint window)
    {
        const string className = "Windows.Media.SystemMediaTransportControls";

        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var name));

        try
        {
            var interopIid = typeof(ISystemMediaTransportControlsInterop).GUID;

            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, ref interopIid, out var factory));

            try
            {
                var interop = (ISystemMediaTransportControlsInterop)Marshal.GetObjectForIUnknown(factory);
                var iid = ControlsIid;
                var abi = interop.GetForWindow(window, ref iid);

                try
                {
                    return WinRT.MarshalInspectable<SystemMediaTransportControls>.FromAbi(abi);
                }
                finally
                {
                    Marshal.Release(abi);
                }
            }
            finally
            {
                Marshal.Release(factory);
            }
        }
        finally
        {
            WindowsDeleteString(name);
        }
    }

    /// <summary><c>ISystemMediaTransportControls</c>, which the factory is asked to produce.</summary>
    private static readonly Guid ControlsIid = new("99fa3ff4-1742-42a6-902e-087d41f965ec");

    /// <summary>Derives from IInspectable, whose three slots are declared here because .NET cannot marshal it.</summary>
    [ComImport]
    [Guid("ddb0472d-c911-4a1f-86d9-dc3d71a95f5a")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISystemMediaTransportControlsInterop
    {
        void GetIids();

        void GetRuntimeClassName();

        void GetTrustLevel();

        nint GetForWindow(nint window, ref Guid iid);
    }
}
