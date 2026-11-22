using System.Runtime.InteropServices;
using D47.Core.Audio;
using D47.Core.Podcasts;
using static D47.App.Interop.WinRtInterop;
using Microsoft.Extensions.Logging;
using Windows.Media;

namespace D47.App.Media;

/// <summary>
/// d47's entry in Windows' media sessions: the keyboard's play/pause and next-track keys and the volume
/// flyout's buttons reach the ambient music through it. Windows sends the keys to whichever session was
/// used last, so nothing is taken from other players. While a podcast episode is playing or paused by the
/// Commander the keys and the flyout are the podcast's: play, pause, next episode and skip back 30 seconds.
/// </summary>
internal sealed class MediaSession : IDisposable
{
    private readonly SystemMediaTransportControls _controls;
    private readonly AmbientMusic _music;
    private readonly PodcastDesk? _podcasts;
    private readonly ILogger _logger;
    private bool _disposed;

    private MediaSession(SystemMediaTransportControls controls, AmbientMusic music, PodcastDesk? podcasts, ILogger logger)
    {
        _controls = controls;
        _music = music;
        _podcasts = podcasts;
        _logger = logger;

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

        Show();
    }

    /// <summary>Registers the session for <paramref name="window"/>, or returns null where Windows refuses.</summary>
    public static MediaSession? Attach(nint window, AmbientMusic music, PodcastDesk? podcasts, ILogger logger)
    {
        try
        {
            return new MediaSession(ForWindow(window), music, podcasts, logger);
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
        if (_podcasts is { Active: true } podcasts)
        {
            PressOnPodcast(podcasts, args.Button);
            return;
        }

        MusicAction? action = args.Button switch
        {
            SystemMediaTransportControlsButton.Play => MusicAction.Resume,
            SystemMediaTransportControlsButton.Pause => MusicAction.Pause,
            SystemMediaTransportControlsButton.Next => MusicAction.Next,
            SystemMediaTransportControlsButton.Previous => MusicAction.Previous,
            _ => null,
        };

        if (action is { } pressed)
        {
            _logger.LogInformation("Media key: {Button} ({Result})", args.Button, _music.Control(pressed));
        }
    }

    private void PressOnPodcast(PodcastDesk podcasts, SystemMediaTransportControlsButton button)
    {
        switch (button)
        {
            case SystemMediaTransportControlsButton.Play:
                _logger.LogInformation("Media key: {Button} ({Result})", button, podcasts.Resume());
                break;

            case SystemMediaTransportControlsButton.Pause:
                _logger.LogInformation("Media key: {Button} ({Result})", button, podcasts.Pause());
                break;

            case SystemMediaTransportControlsButton.Previous:
                _logger.LogInformation("Media key: {Button} ({Result})", button, podcasts.SkipBack());
                break;

            case SystemMediaTransportControlsButton.Next:
                // Playing the next episode waits for it to open, so the key does not.
                _ = podcasts.Next().ContinueWith(
                    done => _logger.LogInformation(
                        "Media key: {Button} ({Result})", button, done.IsCompletedSuccessfully ? done.Result : "failed"),
                    TaskScheduler.Default);
                break;

            default:
                break;
        }
    }

    /// <summary>Raised on the tick; the flyout is updated on the UI thread instead.</summary>
    private void OnChanged(MusicState state) => Avalonia.Threading.Dispatcher.UIThread.Post(Show);

    private void OnPodcastChanged(PodcastPlayerState? state) => Avalonia.Threading.Dispatcher.UIThread.Post(Show);

    private void Show()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_podcasts?.Player.State is { } episode)
            {
                DisplayPodcast(episode);
            }
            else
            {
                Display(_music.State);
            }
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "The media flyout could not be updated");
        }
    }

    private void DisplayPodcast(PodcastPlayerState state)
    {
        _controls.PlaybackStatus = state.Playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;

        var display = _controls.DisplayUpdater;
        display.Type = MediaPlaybackType.Music;
        display.MusicProperties.Title = state.Episode.Title;
        display.MusicProperties.Artist = state.Show;
        display.Update();
    }

    private void Display(MusicState state)
    {
        _controls.PlaybackStatus = state switch
        {
            { Playing: true } => MediaPlaybackStatus.Playing,
            { Paused: true } or { Idle: true } => MediaPlaybackStatus.Paused,
            _ => MediaPlaybackStatus.Stopped,
        };

        var display = _controls.DisplayUpdater;

        if (state.Track is { } track)
        {
            display.Type = MediaPlaybackType.Music;
            display.MusicProperties.Title = Path.GetFileNameWithoutExtension(track);
            display.MusicProperties.Artist = "Directive 47";
        }
        else
        {
            display.ClearAll();
        }

        display.Update();
    }

    public void Dispose()
    {
        _disposed = true;
        _music.Changed -= OnChanged;

        if (_podcasts is not null)
        {
            _podcasts.Player.Changed -= OnPodcastChanged;
        }

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
