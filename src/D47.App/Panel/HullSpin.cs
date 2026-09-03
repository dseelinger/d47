using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace D47.App.Panel;

/// <summary>
/// Turns a fleet card's hull when the Commander points at it.
/// <para>
/// <b>The card the pointer is on, not all of them.</b> The drawing was always meant to move -
/// asked for 2026-09-03 as <i>"a slowly spinning GIF for the image"</i> - but a grid of fifty
/// turning ships is a slot machine, and it is also unaffordable: a hull's frames are about 20 MB
/// decoded, so a fleet spinning at once would be most of a gigabyte of bitmaps. Pointing at one
/// card is both the calmer page and the one that costs a single sheet.
/// </para>
/// <para>
/// <b>One timer for the whole page.</b> A timer per card would be fifty objects to keep in step
/// and fifty chances to leave one running over a card that has been rebuilt out from under it.
/// Only one hull turns at a time, so there is one timer, and it stops whenever nothing is being
/// pointed at.
/// </para>
/// <para>
/// 120 frames at six a second is the twenty-second rotation that was asked for, and three degrees
/// a step - slow enough that the low frame rate is not what the eye notices.
/// </para>
/// </summary>
internal static class HullSpin
{
    private const int FramesPerSecond = 6;

    private static readonly DispatcherTimer Ticker = Build();

    private static Image? _turning;
    private static IReadOnlyList<CroppedBitmap>? _frames;
    private static Bitmap? _resting;
    private static int _frame;

    private static DispatcherTimer Build()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / FramesPerSecond),
        };

        timer.Tick += (_, _) => Advance();

        return timer;
    }

    private static void Advance()
    {
        if (_turning is null || _frames is not { Count: > 0 })
        {
            Stop();

            return;
        }

        // A card can be rebuilt while the pointer sits on it - the switch at the head of the page
        // does exactly that. An image no longer in the tree is not worth drawing to.
        if (_turning.GetVisualParent() is null)
        {
            Stop();

            return;
        }

        _frame = (_frame + 1) % _frames.Count;
        _turning.Source = _frames[_frame];
    }

    private static void Stop()
    {
        Ticker.Stop();

        if (_turning is not null && _resting is not null && _turning.GetVisualParent() is not null)
        {
            _turning.Source = _resting;
        }

        _turning = null;
        _frames = null;
        _resting = null;
        _frame = 0;
    }

    /// <summary>
    /// Makes a card turn its hull while the pointer is over it.
    /// </summary>
    /// <param name="card">The card itself, so anywhere on it counts as pointing at the ship.</param>
    /// <param name="drawing">The image to swap frames into.</param>
    /// <param name="hull">The hull symbol, for finding the frames.</param>
    /// <param name="resting">What to put back when the pointer leaves.</param>
    internal static void Attach(Control card, Image drawing, string? hull, Bitmap resting)
    {
        card.PointerEntered += (_, _) =>
        {
            // Asked for on the first hover rather than when the card is built, so a fleet of fifty
            // does not decode fifty sheets to draw a page nobody has pointed at yet.
            var frames = ShipArt.Frames(hull);

            if (frames is not { Count: > 0 })
            {
                return;
            }

            if (!ReferenceEquals(_turning, drawing))
            {
                Stop();
            }

            _turning = drawing;
            _frames = frames;
            _resting = resting;

            Ticker.Start();
        };

        card.PointerExited += (_, _) =>
        {
            if (ReferenceEquals(_turning, drawing))
            {
                Stop();
            }
        };
    }
}
