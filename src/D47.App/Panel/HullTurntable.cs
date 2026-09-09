using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Media;

namespace D47.App.Panel;

/// <summary>Turns a fleet card's hull once, when the Commander opens that ship (#289).</summary>
internal static class HullTurntable
{
    private static readonly DispatcherTimer Ticker = Build();

    private static Image? _turning;
    private static VideoFrames? _video;
    private static WriteableBitmap? _frame;
    private static Bitmap? _resting;
    private static Image? _waiting;
    private static Bitmap? _waitingRest;
    private static string? _awaited;
    private static string? _awaitedHull;
    private static string? _wanted;
    private static bool _listening;

    /// <summary>Whether a hull can be turned at all: its turntable is on disk.</summary>
    internal static bool Ready(string? hull) => ShipArt.SpinFile(hull) is not null;

    /// <summary>
    /// Whether a card being built is for the hull that should be turning, so it can pick the rotation
    /// back up.
    /// </summary>
    internal static bool Resumes(string? hull) =>
        _wanted is not null && string.Equals(_wanted, ShipArt.Symbol(hull), StringComparison.Ordinal);

    /// <summary>
    /// Plays one rotation on a card's drawing, and rests on <paramref name="resting"/> at the end.
    /// </summary>
    internal static void Play(Image drawing, string? hull, Bitmap resting)
    {
        Stop();

        if (ShipArt.Symbol(hull) is not { } symbol)
        {
            return;
        }

        // Set after the stop, which clears it, and before anything that can fail: a card rebuilt out from
        // under this — which is what opening a ship does — reads it to know it should still be turning.
        _wanted = symbol;

        if (ShipArt.SpinFile(hull) is not { } path)
        {
            Await(drawing, hull, resting);

            return;
        }

        if (VideoFrames.Open(path) is not { } video)
        {
            _wanted = null;

            return;
        }

        _turning = drawing;
        _video = video;
        _frame = video.Frame();
        _resting = resting;

        Ticker.Interval = TimeSpan.FromSeconds(1 / Math.Max(1, video.FramesPerSecond));
        Ticker.Start();

        // Straight to the first frame rather than waiting a tick for it.
        Advance();
    }

    /// <summary>Stops whatever is playing and puts the still back.</summary>
    internal static void Stop()
    {
        Ticker.Stop();
        _awaited = null;
        _wanted = null;

        if (_turning is not null && _resting is not null && _turning.GetVisualParent() is not null)
        {
            _turning.Source = _resting;
        }

        _video?.Dispose();

        _turning = null;
        _video = null;
        _frame = null;
        _resting = null;
    }

    /// <summary>Waits for the fetch this selection started, and plays when it lands.</summary>
    private static void Await(Image drawing, string? hull, Bitmap resting)
    {
        if (ShipArt.Symbol(hull) is not { } wanted)
        {
            return;
        }

        _awaited = wanted;
        _awaitedHull = hull;
        _waiting = drawing;
        _waitingRest = resting;

        // Subscribed once for the life of the process rather than per selection.
        if (!_listening)
        {
            ShipArtStore.Arrived += Landed;
            _listening = true;
        }

        ShipArtStore.Want(hull);
    }

    private static void Landed(string symbol) => Dispatcher.UIThread.Post(() =>
    {
        if (!string.Equals(symbol, _awaited, StringComparison.Ordinal)
            || _waiting is not { } drawing
            || _waitingRest is not { } resting)
        {
            return;
        }

        _awaited = null;

        if (drawing.GetVisualParent() is not null)
        {
            Play(drawing, _awaitedHull, resting);
        }
    });

    private static DispatcherTimer Build()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background);

        timer.Tick += (_, _) => Advance();

        return timer;
    }

    private static void Advance()
    {
        if (_turning is null || _video is null || _frame is null)
        {
            Stop();

            return;
        }

        // A card can be rebuilt while it is turning — the switch at the head of the page does exactly that,
        // and so does the drill dropping a pane on a narrower window.
        if (_turning.GetVisualParent() is null)
        {
            Stop();

            return;
        }

        if (!_video.Next(_frame))
        {
            // The end of the rotation, which is where it stops: nothing loops.
            Stop();

            return;
        }

        // Set once and invalidated after: the source is the same object every frame, and an Image does not
        // redraw for a bitmap whose contents changed underneath it.
        if (!ReferenceEquals(_turning.Source, _frame))
        {
            _turning.Source = _frame;
        }

        _turning.InvalidateVisual();
    }
}
