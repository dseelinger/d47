using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Audio;

namespace D47.App.Controls;

/// <summary>The mini panels' read-out while a story run plays: label and time, the line, and what to say. Nothing in it is pressable.</summary>
public sealed class StoryPlaybackReadout : Border
{
    public const string LabelName = "StoryReadoutLabel";
    public const string TimeName = "StoryReadoutTime";
    public const string TextName = "StoryReadoutText";
    public const string ScrollBarName = "StoryReadoutScroll";
    public const string HintName = "StoryReadoutHint";

    /// <summary>How often the time, scroll and progress are read.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(500);

    private readonly TextBlock _label = new() { Name = LabelName };
    private readonly TextBlock _time = new() { Name = TimeName };
    private readonly TextBlock _text = new() { Name = TextName };
    private readonly TextBlock _hint = new() { Name = HintName };
    private readonly ScrollViewer _scroll;
    private readonly Grid _bar = new() { Name = ScrollBarName, Width = 3 };
    private readonly Border _thumb = new();
    private readonly DispatcherTimer _poll = new() { Interval = PollEvery };
    private StoryRunPlayer? _player;
    private Func<TrackProgress?>? _progress;
    private string? _shown;

    public StoryPlaybackReadout()
    {
        IsHitTestVisible = false;

        Typed(_label, TypeScale.Meta, Fonts.ChromeFamily, FontWeight.SemiBold, TypeScale.Meta * Fonts.ChromeTracking);
        Typed(_time, TypeScale.Meta, Fonts.MonoFamily);
        Typed(_text, TypeScale.Body, Fonts.ProseFamily);
        Typed(_hint, TypeScale.Description, Fonts.ProseFamily);
        _text.TextWrapping = TextWrapping.Wrap;
        _text.LineHeight = TypeScale.Body * 1.5;
        ThemeInk.Bind(_time, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        ThemeInk.Bind(_text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(_hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _text,
        };

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 10 };
        head.Children.Add(_label);
        Grid.SetColumn(_time, 1);
        head.Children.Add(_time);

        ThemeInk.Bind(_bar, Avalonia.Controls.Panel.BackgroundProperty, ThemeManager.Line2Key);
        ThemeInk.Bind(_thumb, BackgroundProperty, ThemeManager.AKey);
        _bar.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Star));
        _bar.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
        _bar.RowDefinitions.Add(new RowDefinition(0, GridUnitType.Star));
        Grid.SetRow(_thumb, 1);
        _bar.Children.Add(_thumb);

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        body.Children.Add(_scroll);
        Grid.SetColumn(_bar, 1);
        body.Children.Add(_bar);

        var rows = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 8 };
        rows.Children.Add(head);
        Grid.SetRow(body, 1);
        rows.Children.Add(body);
        Grid.SetRow(_hint, 2);
        rows.Children.Add(_hint);
        Child = rows;

        _poll.Tick += (_, _) => Poll();
        DetachedFromVisualTree += (_, _) => _poll.Stop();
    }

    /// <summary>The share of the line played, 0 to 1.</summary>
    public double Share { get; private set; }

    /// <summary>Whether the playback is paused.</summary>
    public bool Paused { get; private set; }

    /// <summary>Raised when <see cref="Share"/> or <see cref="Paused"/> may have changed.</summary>
    public event Action? Moved;

    /// <summary>Reads <paramref name="player"/>'s playback and <paramref name="progress"/>; null stops reading.</summary>
    public void Watch(StoryRunPlayer? player, Func<TrackProgress?>? progress)
    {
        _player = player;
        _progress = progress;

        if (player is null)
        {
            _poll.Stop();
            return;
        }

        Refresh();
        _poll.Start();
    }

    /// <summary>Draws the playback's label, line and hint.</summary>
    internal void Refresh()
    {
        if (_player?.Now is not { } playback)
        {
            return;
        }

        _label.Text = StoryPlaybackDialog.LabelOf(playback);
        ThemeInk.Bind(_label, TextBlock.ForegroundProperty, StoryPlaybackDialog.LabelInk(playback));
        _hint.Text = playback.Lines.Count == 1 ? StoryPlaybackDialog.HintWithoutNext : StoryPlaybackDialog.HintWithNext;
        Paused = playback.IsPaused;

        if (!string.Equals(_shown, playback.Line.Body, StringComparison.Ordinal))
        {
            _shown = playback.Line.Body;
            _text.Text = _shown;
            _scroll.Offset = default;
        }

        Poll();
    }

    internal void Poll()
    {
        if (_player?.Now is not { } playback)
        {
            return;
        }

        double share;

        if (playback is { State: StoryRunLineState.Held, HeldUntil: { } until })
        {
            var left = Math.Max(0, (int)Math.Ceiling((until - _player.Time.GetUtcNow()).TotalSeconds));
            _time.Text = AudioPlayerReadout.Clock(TimeSpan.FromSeconds(left));
            share = 0;
        }
        else
        {
            var now = _progress?.Invoke();
            _time.Text = now is { } read ? $"{AudioPlayerReadout.Clock(read.Elapsed)} / {AudioPlayerReadout.Clock(read.Length)}" : string.Empty;
            share = now is { Length: { } length } known && length > TimeSpan.Zero ? known.Elapsed / length : 0;
        }

        Share = Math.Clamp(double.IsFinite(share) ? share : 0, 0, 1);
        Scroll();
        Moved?.Invoke();
    }

    /// <summary>Scrolls a line longer than the area so its end shows when the clip ends, and sizes the bar to match.</summary>
    private void Scroll()
    {
        var extent = _scroll.Extent.Height;
        var view = _scroll.Viewport.Height;

        if (extent <= 0 || view <= 0 || extent <= view)
        {
            SetBar(0, 1);
            return;
        }

        var offset = Share * (extent - view);
        _scroll.Offset = new Vector(0, offset);
        SetBar(offset / extent, view / extent);
    }

    private void SetBar(double top, double thumb)
    {
        _bar.RowDefinitions[0].Height = new GridLength(top, GridUnitType.Star);
        _bar.RowDefinitions[1].Height = new GridLength(thumb, GridUnitType.Star);
        _bar.RowDefinitions[2].Height = new GridLength(Math.Max(0, 1 - top - thumb), GridUnitType.Star);
    }

    private static void Typed(TextBlock block, double size, string family, FontWeight? weight = null, double? tracking = null)
    {
        block.FontSize = size;
        block.FontFamily = new FontFamily(family);

        if (weight is { } w)
        {
            block.FontWeight = w;
        }

        if (tracking is { } t)
        {
            block.LetterSpacing = t;
        }
    }
}
