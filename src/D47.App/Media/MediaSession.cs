using System.Runtime.InteropServices;
using D47.Core.Audio;
using Microsoft.Extensions.Logging;
using Windows.Media;

namespace D47.App.Media;

/// <summary>
/// d47's entry in Windows' media sessions: the keyboard's play/pause and next-track keys and the volume
/// flyout's buttons reach the ambient music through it. Windows sends the keys to whichever session was
/// used last, so nothing is taken from other players.
/// </summary>
internal sealed class MediaSession : IDisposable
{
    private readonly SystemMediaTransportControls _controls;
    private readonly AmbientMusic _music;
    private readonly ILogger _logger;

    private MediaSession(SystemMediaTransportControls controls, AmbientMusic music, ILogger logger)
    {
        _controls = controls;
        _music = music;
        _logger = logger;

        _controls.IsEnabled = true;
        _controls.IsPlayEnabled = true;
        _controls.IsPauseEnabled = true;
        _controls.IsNextEnabled = true;
        _controls.IsPreviousEnabled = false;
        _controls.ButtonPressed += OnButtonPressed;
        _music.Changed += Show;

        Show(_music.State);
    }

    /// <summary>Registers the session for <paramref name="window"/>, or returns null where Windows refuses.</summary>
    public static MediaSession? Attach(nint window, AmbientMusic music, ILogger logger)
    {
        try
        {
            return new MediaSession(ForWindow(window), music, logger);
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
        MusicAction? action = args.Button switch
        {
            SystemMediaTransportControlsButton.Play => MusicAction.Resume,
            SystemMediaTransportControlsButton.Pause => MusicAction.Pause,
            SystemMediaTransportControlsButton.Next => MusicAction.Next,
            _ => null,
        };

        if (action is { } pressed)
        {
            _logger.LogInformation("Media key: {Button} ({Result})", args.Button, _music.Control(pressed));
        }
    }

    private void Show(MusicState state)
    {
        try
        {
            Display(state);
        }
        catch (COMException ex)
        {
            _logger.LogDebug(ex, "The media flyout could not be updated");
        }
    }

    private void Display(MusicState state)
    {
        _controls.PlaybackStatus = state switch
        {
            { Playing: true } => MediaPlaybackStatus.Playing,
            { Paused: true } => MediaPlaybackStatus.Paused,
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
        _music.Changed -= Show;
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

    [DllImport("combase.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WindowsCreateString(string source, int length, out nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int WindowsDeleteString(nint value);

    [DllImport("combase.dll", ExactSpelling = true)]
    private static extern int RoGetActivationFactory(nint className, ref Guid iid, out nint factory);
}
