using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Media;

namespace D47.App.Panel;

/// <summary>
/// Turns a fleet card's hull once, when the Commander opens that ship
/// (<a href="https://github.com/dseelinger/d47/issues/289">#289</a>).
/// <para>
/// <b>On the pick, not on the pointer, and that is a correction rather than a preference.</b> The
/// first version span whatever the mouse was over, which made the fleet page something that reacts
/// to the mouse crossing it on the way somewhere else. A rotation now says <i>this is the ship you
/// just opened</i>: one turn, then it rests on the still again. Opening it again plays it again.
/// </para>
/// <para>
/// <b>And it is visible because of how the drill lays out.</b> A pick opens Ship Details, which on
/// anything but the narrowest pane appears <em>beside</em> the fleet rather than instead of it
/// (<see cref="DrillView"/>) — so the card keeps turning next to the page it opened. On one pane
/// the card leaves the tree and the rotation stops with it, which is the right answer for a card
/// nobody can see.
/// </para>
/// <para>
/// <b>One timer and one video for the whole page.</b> A timer per card would be fifty objects to
/// keep in step and fifty chances to leave one running over a card that has been rebuilt out from
/// under it. Only one hull turns at a time, so there is one of each, and both stop together.
/// </para>
/// <para>
/// <b>The sheet is gone.</b> This read a 120-frame sprite sheet before the turntables existed —
/// one 3 MB PNG per hull, sliced into <c>CroppedBitmap</c> views, about 20 MB decoded. The MP4 is
/// the same rotation at 180 frames for a tenth of the bytes, and it is decoded one frame at a
/// time, so a rotation costs one bitmap rather than a sheet.
/// </para>
/// </summary>
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
    private static bool _listening;

    /// <summary>
    /// Whether a hull can be turned at all: its turntable is on disk. Asked before a card is told
    /// to play, so a fleet with no art fetched yet does nothing rather than opening files.
    /// </summary>
    internal static bool Ready(string? hull) => ShipArt.SpinFile(hull) is not null;

    /// <summary>
    /// Plays one rotation on a card's drawing, and rests on <paramref name="resting"/> at the end.
    /// <para>
    /// <b>Asks for the turntable if it is not here, and plays it when it lands.</b> The first
    /// selection of a hull is exactly the one that has nothing on disk yet, so a version that only
    /// played what was already there never played on a first click — which is the whole of what a
    /// Commander sees the first time they open each ship. The fetch is a few seconds on a home
    /// connection and the card is still on the page beside the ship it opened, so the rotation
    /// starts late rather than not at all. A hull that never arrives simply never turns.
    /// </para>
    /// </summary>
    internal static void Play(Image drawing, string? hull, Bitmap resting)
    {
        Stop();

        if (ShipArt.SpinFile(hull) is not { } path)
        {
            Await(drawing, hull, resting);

            return;
        }

        if (VideoFrames.Open(path) is not { } video)
        {
            return;
        }

        _turning = drawing;
        _video = video;
        _frame = video.Frame();
        _resting = resting;

        Ticker.Interval = TimeSpan.FromSeconds(1 / Math.Max(1, video.FramesPerSecond));
        Ticker.Start();

        // Straight to the first frame rather than waiting a tick for it. A card that sits on its
        // still for a twelfth of a second before moving reads as a stutter, not as a start.
        Advance();
    }

    /// <summary>Stops whatever is playing and puts the still back. Safe when nothing is.</summary>
    internal static void Stop()
    {
        Ticker.Stop();
        _awaited = null;

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

    /// <summary>
    /// Waits for the fetch this selection started, and plays when it lands.
    /// <para>
    /// One waiting card at a time, cleared by the next selection, so a Commander clicking through
    /// a fleet does not end up with six turntables arriving at once over whatever is on screen by
    /// then. The check that the card is still in the tree is the same one <see cref="Advance"/>
    /// makes and matters more here, because minutes can pass.
    /// </para>
    /// </summary>
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

        // Subscribed once for the life of the process rather than per selection. A handler per
        // wait would need unsubscribing on a landing that may never come, so a Commander clicking
        // through a fleet with the network down would leave one behind on every card.
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

        // A card can be rebuilt while it is turning — the switch at the head of the page does
        // exactly that, and so does the drill dropping a pane on a narrower window. An image no
        // longer in the tree is not worth decoding to.
        if (_turning.GetVisualParent() is null)
        {
            Stop();

            return;
        }

        if (!_video.Next(_frame))
        {
            // The end of the rotation, which is where it stops: nothing loops. Also every failure
            // mid-file, which lands in the same place because the same thing is right for both.
            Stop();

            return;
        }

        // Set once and invalidated after: the source is the same object every frame, and an Image
        // does not redraw for a bitmap whose contents changed underneath it. Assigning null and
        // back would also work and would blank the card for a layout pass on the way.
        if (!ReferenceEquals(_turning.Source, _frame))
        {
            _turning.Source = _frame;
        }

        _turning.InvalidateVisual();
    }
}
