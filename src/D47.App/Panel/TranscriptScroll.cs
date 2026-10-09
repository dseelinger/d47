using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>The transcript's scroll, follow and fold state.</summary>
internal sealed class TranscriptScroll
{
    private const int FoldAnchorLength = 256;

    private readonly ScrollViewer _transcriptScroller;
    private readonly ScrollViewer _journalScroller;
    private readonly Avalonia.Controls.Panel _transcriptContent;
    private readonly ListBox _journalList;
    private readonly SelectableTextBlock _transcript;
    private readonly Button _followButton;
    private readonly Func<TranscriptPage> _page;
    private readonly Func<string> _shown;
    private readonly Func<bool> _outputOnly;

    private bool _following = true;

    // Set while this class scrolls, so the scroll handler does not read it as the Commander moving.
    private bool _scrollingItself;

    // Where the fold is, and on which reading: the content height when Ctrl+L was last pressed.
    private (TranscriptPage Page, double Mark, string? Anchor, double Settled)? _fold;

    // Set while the fold is applied, so its own layout pass does not re-enter it.
    private bool _folding;

    public TranscriptScroll(
        ScrollViewer transcriptScroller,
        ScrollViewer journalScroller,
        Avalonia.Controls.Panel transcriptContent,
        ListBox journalList,
        SelectableTextBlock transcript,
        Button followButton,
        Func<TranscriptPage> page,
        Func<string> shown,
        Func<bool> outputOnly)
    {
        _transcriptScroller = transcriptScroller;
        _journalScroller = journalScroller;
        _transcriptContent = transcriptContent;
        _journalList = journalList;
        _transcript = transcript;
        _followButton = followButton;
        _page = page;
        _shown = shown;
        _outputOnly = outputOnly;

        _transcriptScroller.ScrollChanged += OnScrolled;
        _journalScroller.ScrollChanged += OnScrolled;
        _followButton.Click += OnFollowClick;
    }

    public bool Following => _following;

    /// <summary>Whether none of the reading is inside the view.</summary>
    public bool ReadingIsAboveTheFold
    {
        get
        {
            var scroller = Scroller;
            var pad = FoldPad;
            var top = pad.Margin.Top;
            var content = Math.Max(0, scroller.Extent.Height - PadOn(pad));

            var from = scroller.Offset.Y;
            var to = from + scroller.Viewport.Height;

            return Math.Min(to, top + content) - Math.Max(from, top) <= 0.5;
        }
    }

    private TranscriptPage Page => _page();

    private bool NewestAtTop => Page is TranscriptPage.Journal or TranscriptPage.RawJournal;

    private ScrollViewer Scroller =>
        Page == TranscriptPage.Journal ? _journalScroller : _transcriptScroller;

    private Control FoldPad =>
        Page == TranscriptPage.Journal ? _journalList : _transcriptContent;

    /// <summary>Follows the transcript, from whichever thread grew it.</summary>
    public void ScrollToEnd()
    {
        if (!_following)
        {
            return;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Follow();
            return;
        }

        Dispatcher.UIThread.Post(Follow);
    }

    /// <summary>Goes to the newest line while following, without the scroll handler judging it.</summary>
    public void KeepUp()
    {
        if (_following)
        {
            Follow();
        }
    }

    /// <summary>The page changed: following again, without scrolling.</summary>
    public void Rejoin() => _following = true;

    /// <summary>A step moved the transcript: following is whether it landed at the newest line.</summary>
    public void Moved()
    {
        _following = AtTheNewest();
        ShowFollowButton();
    }

    /// <summary>Follows again and goes to the newest line, for a surface with no button.</summary>
    public PanelScrollOutcome ScrollToNewest()
    {
        var atNewest = AtTheNewest();
        var scroller = Scroller;

        _following = true;

        if (scroller.Viewport.Height <= 0 || scroller.Extent.Height <= scroller.Viewport.Height)
        {
            ShowFollowButton();
            return PanelScrollOutcome.NothingToScroll;
        }

        if (atNewest)
        {
            ShowFollowButton();
            return PanelScrollOutcome.AlreadyThere;
        }

        Follow();
        return PanelScrollOutcome.Moved;
    }

    /// <summary>Puts what is on the page above the top of the view, and deletes nothing.</summary>
    public bool ScrollPastReading()
    {
        var scroller = Scroller;

        // Laid out first: the mark is the content height as it is now, and a run appended a moment ago is
        // not in the extent until this returns.
        scroller.UpdateLayout();

        if (scroller.Viewport.Height <= 0)
        {
            return false;
        }

        var content = Math.Max(0, scroller.Extent.Height - PadOn(FoldPad));
        var anchor = FoldAnchor();

        // What the anchor already reads as, taken off every later reading of it.
        var arrived = anchor is null ? null : FoldArrived(anchor, content);

        _fold = (Page, content, arrived is null ? null : anchor, arrived ?? 0);
        ApplyFold(reassert: true);

        return true;
    }

    /// <summary>Holds the fold, and gives the space back as the reading grows into it.</summary>
    public void ApplyFold(bool reassert)
    {
        if (_folding)
        {
            return;
        }

        var pad = FoldPad;

        if (_fold is not { } fold || fold.Page != Page)
        {
            DropFold(pad);
            return;
        }

        var scroller = Scroller;
        var viewport = scroller.Viewport.Height;
        var content = Math.Max(0, scroller.Extent.Height - PadOn(pad));
        var was = fold.Mark;
        var mark = was;
        var grown = Math.Max(0, content - was);

        // On a reading that trims its front the anchor moves up under a fixed mark, so growth is measured
        // from the anchor.
        if (fold.Anchor is { } anchor)
        {
            if (FoldArrived(anchor, content) is not { } arrived)
            {
                DropFold(pad);
                return;
            }

            grown = Math.Max(0, arrived - fold.Settled);
            mark = Math.Max(0, content - grown);
            _fold = (fold.Page, mark, anchor, fold.Settled);
        }

        // Asked before the mark is allowed to move the view.
        var onTheFold = NewestAtTop
            ? scroller.Offset.Y <= _transcript.FontSize
            : Math.Abs(scroller.Offset.Y - was) <= _transcript.FontSize;

        var wanted = Math.Max(0, viewport - grown);

        _folding = true;

        try
        {
            var margin = NewestAtTop
                ? new Thickness(0, wanted, 0, 0)
                : new Thickness(0, 0, 0, wanted);

            if (pad.Margin != margin)
            {
                pad.Margin = margin;

                // Laid out now: the caller usually scrolls next, and would otherwise scroll to the end of an
                // extent still holding the space just given back.
                scroller.UpdateLayout();
            }
        }
        finally
        {
            _folding = false;
        }

        if (!reassert && (mark == was || !onTheFold))
        {
            return;
        }

        _scrollingItself = true;

        try
        {
            // The newest end goes to the top of the view.
            scroller.Offset = scroller.Offset.WithY(
                NewestAtTop
                    ? 0
                    : Math.Clamp(mark, 0, Math.Max(0, scroller.Extent.Height - viewport)));
        }
        finally
        {
            _scrollingItself = false;
        }

        ShowFollowButton();
    }

    /// <summary>Shows the jump-to-latest control, and says how far behind the reader is.</summary>
    public void ShowFollowButton()
    {
        var behind = !_following && !AtTheNewest();

        _followButton.IsVisible = behind && !_outputOnly();

        if (behind)
        {
            _followButton.Content = NewestAtTop ? "↑ Newest" : "↓ Newest";
        }
    }

    private static double PadOn(Control pad) => pad.Margin.Top + pad.Margin.Bottom;

    // The text the fold is set against, on the two file readings, which are the ones that trim.
    private string? FoldAnchor()
    {
        if (!_transcript.IsVisible || Page == TranscriptPage.Journal)
        {
            return null;
        }

        // The runs rather than Text: the flat block is written as inlines, so Text is empty.
        var text = _shown();

        if (text.Length == 0)
        {
            return null;
        }

        var take = Math.Min(FoldAnchorLength, text.Length);

        return NewestAtTop ? text[..take] : text[^take..];
    }

    // How much reading sits past the anchor: below it where the reading grows downwards, above it on the
    // two newest-first ones.
    private double? FoldArrived(string anchor, double content)
    {
        if (_transcript.TextLayout is not { } layout)
        {
            return null;
        }

        var text = _shown();

        var at = NewestAtTop
            ? text.IndexOf(anchor, StringComparison.Ordinal)
            : text.LastIndexOf(anchor, StringComparison.Ordinal);

        if (at < 0)
        {
            return null;
        }

        // Through the text layout, which knows where a character offset landed once the text wrapped.
        var where = layout.HitTestTextPosition(NewestAtTop ? at : at + anchor.Length - 1);

        return NewestAtTop ? Math.Max(0, where.Top) : Math.Max(0, content - where.Bottom);
    }

    // Gives the fold's space back and forgets it.
    private void DropFold(Control pad)
    {
        _fold = null;

        if (PadOn(pad) > 0)
        {
            pad.Margin = default;
        }
    }

    private void Follow()
    {
        _scrollingItself = true;

        try
        {
            var scroller = Scroller;

            scroller.UpdateLayout();

            // Before the scroll reads the extent, or the end of it is the fold's empty space.
            ApplyFold(reassert: false);

            if (NewestAtTop)
            {
                scroller.Offset = scroller.Offset.WithY(0);
            }
            else
            {
                scroller.ScrollToEnd();
            }
        }
        finally
        {
            _scrollingItself = false;
        }

        ShowFollowButton();
    }

    private bool AtTheNewest()
    {
        var scroller = Scroller;
        var tolerance = _transcript.FontSize;

        if (NewestAtTop)
        {
            return scroller.Offset.Y <= tolerance;
        }

        var slack = Math.Max(1, scroller.Extent.Height - scroller.Viewport.Height);

        return scroller.Offset.Y >= slack - tolerance;
    }

    private void OnScrolled(object? sender, ScrollChangedEventArgs e)
    {
        if (_scrollingItself)
        {
            return;
        }

        // The reading grew, or the view resized.
        if (e.ExtentDelta.Y != 0 || e.ViewportDelta.Y != 0)
        {
            ApplyFold(reassert: false);
        }

        if (e.ViewportDelta.Y != 0)
        {
            // A resize keeps the offset, so a following reader would lose the newest line off the bottom;
            // posted, because this runs inside a layout pass.
            if (_following)
            {
                Dispatcher.UIThread.Post(Follow);
            }
        }

        // Only a move on its own is the Commander's: an offset change arriving with a new extent or
        // viewport is layout settling.
        if (e.OffsetDelta.Y != 0 && e.ExtentDelta.Y == 0 && e.ViewportDelta.Y == 0)
        {
            _following = AtTheNewest();
        }
        else if (e.OffsetDelta.Y != 0 && _following)
        {
            Dispatcher.UIThread.Post(Follow);
        }

        ShowFollowButton();
    }

    private void OnFollowClick(object? sender, RoutedEventArgs e)
    {
        _following = true;
        Follow();
    }
}
