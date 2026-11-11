using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.App.Voice;
using D47.Core.Audio;
using D47.Core.Messages;

namespace D47.App.Controls;

/// <summary>The story run being played: its line's picture, text and progress, with Play or Pause, Next and Stop. Closing it stops the playback.</summary>
public sealed class StoryPlaybackDialog : ModalDialog
{
    public const string LabelName = "StoryPlaybackLabel";
    public const string ChapterName = "StoryPlaybackChapter";
    public const string PictureName = "StoryPlaybackPicture";
    public const string PausedChipName = "StoryPlaybackPaused";
    public const string ProgressName = "StoryPlaybackProgress";
    public const string SpeakerName = "StoryPlaybackSpeaker";
    public const string TimeName = "StoryPlaybackTime";
    public const string TextName = "StoryPlaybackText";
    public const string StateLineName = "StoryPlaybackState";
    public const string PlayName = "StoryPlaybackPlay";
    public const string NextName = "StoryPlaybackNext";
    public const string StopName = "StoryPlaybackStop";
    public const string HintName = "StoryPlaybackHint";

    /// <summary>The share of the host's width the box takes.</summary>
    public const double WidthOfHost = 0.5;

    /// <summary>The tallest the picture area is drawn; it shrinks where the host is shorter.</summary>
    public const double PictureHeight = 600;

    public const string HintWithNext = "Say: “play”, “pause”, “next” or “stop”";
    public const string HintWithoutNext = "Say: “play”, “pause” or “stop”";

    /// <summary>How often the progress line and times are read.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(250);

    /// <summary>How often they are read on the headset's big panel, where each update redraws the overlay.</summary>
    public static readonly TimeSpan PollEveryInHeadset = TimeSpan.FromSeconds(1);

    private const double MinPictureHeight = 120;

    private const double TextHeight = TypeScale.Body * 1.5 * 3;

    private readonly StoryRunPlayer _player;
    private readonly Func<TrackProgress?> _progress;
    private readonly Func<StoryRunPlayback, string?> _chapter;
    private readonly SpeakerPortraits? _portraits;
    private readonly DispatcherTimer _poll;

    private readonly TextBlock _label = new() { Name = LabelName };
    private readonly TextBlock _chapterText = new() { Name = ChapterName };
    private readonly TextBlock _stamp = new();
    private readonly Grid _stage = new() { Background = Brushes.Black };
    private readonly Image _picture = new() { Name = PictureName, Stretch = Stretch.Uniform };
    private readonly Border _pausedChip = new() { Name = PausedChipName };
    private readonly Grid _rail = new() { Name = ProgressName, Height = 3 };
    private readonly Border _fill = new();
    private readonly TextBlock _speaker = new() { Name = SpeakerName };
    private readonly TextBlock _time = new() { Name = TimeName };
    private readonly TextBlock _text = new() { Name = TextName };
    private readonly ScrollViewer _textScroll;
    private readonly TextBlock _stateLine = new() { Name = StateLineName };
    private readonly StackPanel _below = new() { Spacing = 8 };
    private readonly Grid _progressRow;
    private readonly Button _play = Tile(PlayName, "Pause");
    private readonly Button _next = Tile(NextName, "Next");
    private readonly Button _stop = Tile(StopName, "Stop");
    private readonly TextBlock _hint = new() { Name = HintName };
    private Grid? _generating;
    private bool _ending;

    public StoryPlaybackDialog(
        StoryRunPlayer player,
        Func<TrackProgress?> progress,
        Func<StoryRunPlayback, string?> chapter,
        SpeakerPortraits? portraits,
        TimeSpan? pollEvery = null)
    {
        _player = player;
        _progress = progress;
        _chapter = chapter;
        _portraits = portraits;
        _poll = new DispatcherTimer { Interval = pollEvery ?? PollEvery };

        Title = "Story playback";
        HorizontalAlignment = HorizontalAlignment.Stretch;

        _textScroll = new ScrollViewer
        {
            Height = TextHeight,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(0, 0, 8, 0),
            Content = _text,
        };

        Typed(_label, TypeScale.Meta, Fonts.ChromeFamily, FontWeight.SemiBold, TypeScale.Meta * Fonts.ChromeTracking);
        Typed(_chapterText, TypeScale.Meta, Fonts.ChromeFamily, FontWeight.Medium, TypeScale.Meta * Fonts.ChromeTracking);
        Typed(_stamp, TypeScale.Meta, Fonts.MonoFamily);
        Typed(_speaker, TypeScale.Description, Fonts.ChromeFamily, FontWeight.Medium);
        Typed(_time, TypeScale.Meta, Fonts.MonoFamily);
        Typed(_text, TypeScale.Body, Fonts.ProseFamily);
        Typed(_stateLine, TypeScale.Meta, Fonts.ProseFamily);
        Typed(_hint, TypeScale.Description, Fonts.ProseFamily);
        _text.TextWrapping = TextWrapping.Wrap;
        _text.LineHeight = TypeScale.Body * 1.5;
        _stateLine.TextWrapping = TextWrapping.Wrap;
        _hint.VerticalAlignment = VerticalAlignment.Center;
        _hint.Margin = new Thickness(14, 0, 0, 0);

        ThemeInk.Bind(_chapterText, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        ThemeInk.Bind(_stamp, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        ThemeInk.Bind(_speaker, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(_text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
        ThemeInk.Bind(_stateLine, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        ThemeInk.Bind(_hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _play.Click += (_, _) =>
        {
            if (_player.Now is { IsPaused: true })
            {
                _player.Resume();
            }
            else
            {
                _player.Pause(StoryRunPause.Commander);
            }
        };
        _next.Click += (_, _) => _player.Skip();
        _stop.Click += (_, _) => Close();

        _rail.ColumnDefinitions.Add(new ColumnDefinition(0, GridUnitType.Star));
        _rail.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        var ground = new Border();
        ThemeInk.Bind(ground, Border.BackgroundProperty, ThemeManager.Line2Key);
        Grid.SetColumnSpan(ground, 2);
        _rail.Children.Add(ground);
        _rail.Children.Add(_fill);
        _progressRow = new Grid { Height = 3, Children = { _rail } };

        Content = Layout();
        ThemeInk.Bind(this, TemplatedControl.BackgroundProperty, ThemeManager.BarKey);

        _poll.Tick += (_, _) => Poll();
        _player.Changed += OnPlayerChanged;
        AttachedToVisualTree += (_, _) =>
        {
            Refresh();
            Poll();
            _poll.Start();
        };
        DetachedFromVisualTree += (_, _) => _poll.Stop();

        Refresh();
    }

    public override double? WidthShare => WidthOfHost;

    /// <summary>Takes the box away and leaves the playback running.</summary>
    internal void Dismiss()
    {
        _ending = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _player.Changed -= OnPlayerChanged;
        _poll.Stop();

        if (!_ending)
        {
            _player.Stop();
        }

        base.OnClosed(e);
    }

    private static Button Tile(string name, string label) => new()
    {
        Name = name,
        Content = label,
        Width = 112,
        MinHeight = TypeScale.MinimumTarget,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

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

    private Control Layout()
    {
        var head = new Grid
        {
            Height = 40,
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 16,
            Margin = new Thickness(16, 0),
        };
        _label.VerticalAlignment = VerticalAlignment.Center;
        _chapterText.VerticalAlignment = VerticalAlignment.Center;
        _stamp.VerticalAlignment = VerticalAlignment.Center;
        head.Children.Add(_label);
        Grid.SetColumn(_chapterText, 1);
        head.Children.Add(_chapterText);
        Grid.SetColumn(_stamp, 2);
        head.Children.Add(_stamp);

        var headRow = Ruled(head, bottom: true);

        _pausedChip.HorizontalAlignment = HorizontalAlignment.Left;
        _pausedChip.VerticalAlignment = VerticalAlignment.Bottom;
        _pausedChip.Margin = new Thickness(16, 0, 0, 14);
        _pausedChip.Padding = new Thickness(10, 4);
        ThemeInk.Bind(_pausedChip, Border.BackgroundProperty, ThemeManager.TileKey);
        _pausedChip.Child = Typeset.Chrome("PAUSED", TypeScale.Meta, ThemeManager.AKey, FontWeight.SemiBold, TypeScale.Meta * Fonts.ChromeTracking);

        _picture.HorizontalAlignment = HorizontalAlignment.Center;
        _stage.Children.Add(_picture);
        _stage.Children.Add(_pausedChip);

        var speakerRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        _speaker.VerticalAlignment = VerticalAlignment.Center;
        speakerRow.Children.Add(_speaker);
        Grid.SetColumn(_time, 1);
        speakerRow.Children.Add(_time);

        var caption = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(20, 12, 20, 10),
            Children = { speakerRow, _textScroll, _stateLine, _below },
        };

        var foot = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(12, 8),
            Children = { _play, _next, _stop, _hint },
        };
        var footRow = Ruled(foot, bottom: false);

        var column = new Column(_stage);
        column.Children.Add(headRow);
        column.Children.Add(_stage);
        column.Children.Add(_progressRow);
        column.Children.Add(caption);
        column.Children.Add(footRow);
        return column;
    }

    /// <summary>
    /// Stacks its children top to bottom. The stage takes the height left over, between <see cref="MinPictureHeight"/> and
    /// <see cref="PictureHeight"/>.
    /// </summary>
    private sealed class Column(Control stage) : Avalonia.Controls.Panel
    {
        private double _picture;

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = availableSize.Width;
            var used = 0d;

            foreach (var child in Children.Where(child => !ReferenceEquals(child, stage)))
            {
                child.Measure(new Size(width, double.PositiveInfinity));
                used += child.DesiredSize.Height;
            }

            var picture = double.IsInfinity(availableSize.Height)
                ? PictureHeight
                : Math.Clamp(availableSize.Height - used, MinPictureHeight, PictureHeight);
            _picture = picture;
            stage.Measure(new Size(width, picture));

            return new Size(double.IsInfinity(width) ? Children.Max(child => child.DesiredSize.Width) : width, used + picture);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var top = 0d;

            foreach (var child in Children)
            {
                var height = ReferenceEquals(child, stage) ? _picture : child.DesiredSize.Height;
                child.Arrange(new Rect(0, top, finalSize.Width, height));
                top += height;
            }

            return finalSize;
        }
    }

    private static Border Ruled(Control child, bool bottom)
    {
        var border = new Border { BorderThickness = bottom ? new Thickness(0, 0, 0, 1) : new Thickness(0, 1, 0, 0), Child = child };
        ThemeInk.Bind(border, Border.BorderBrushProperty, ThemeManager.Line2Key);
        return border;
    }

    private void OnPlayerChanged() => Dispatcher.UIThread.Post(Refresh);

    /// <summary>Draws the playback's line, label and controls; closes the box when nothing is playing.</summary>
    internal void Refresh()
    {
        if (_player.Now is not { } playback)
        {
            _ending = true;
            Dispatcher.UIThread.Post(Close);
            return;
        }

        var state = playback.IsPaused ? (StoryRunLineState?)null : playback.State;
        var line = playback.Line;
        var speaker = MessagesView.Sender(line.From);
        var one = playback.Lines.Count == 1;
        var place = one
            ? speaker
            : $"{(playback.Index + 1).ToString(CultureInfo.InvariantCulture)} of {playback.Lines.Count.ToString(CultureInfo.InvariantCulture)} · {speaker}";

        _label.Text = (state switch
        {
            null => $"Paused · {place}",
            StoryRunLineState.SpeakingAgain => $"{StoryRunPage.SpeakingAgainLabel} · {place}",
            StoryRunLineState.Held => $"{StoryRunPage.ShownForReadingLabel} · {place}",
            _ => one ? $"Playing · {place}" : $"Playing {place}",
        }).ToUpperInvariant();
        ThemeInk.Bind(
            _label,
            TextBlock.ForegroundProperty,
            playback.IsPaused ? ThemeManager.AKey : state == StoryRunLineState.Held ? ThemeManager.WhiteKey : ThemeManager.CyanKey);

        _chapterText.Text = _chapter(playback)?.ToUpperInvariant() ?? string.Empty;
        _stamp.Text = line.Sent.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        _picture.Source = _portraits?.For(StoryRunPlayer.PictureOf(line), (int)PictureHeight);
        _pausedChip.IsVisible = playback.IsPaused;
        _speaker.Text = speaker;

        if (!string.Equals(_text.Text, line.Body, StringComparison.Ordinal))
        {
            _text.Text = line.Body;
            _textScroll.Offset = default;
        }

        _stateLine.Text = playback.Pause switch
        {
            StoryRunPause.PushToTalk => StoryRunPage.PushToTalkText,
            StoryRunPause.None => state switch
            {
                StoryRunLineState.SpeakingAgain => StoryRunPage.SpeakingAgainText(speaker),
                StoryRunLineState.Held => StoryRunPage.HeldText,
                _ => StoryRunPage.SilencedText,
            },
            _ => StoryRunPage.PausedText,
        };

        _play.Content = playback.IsPaused ? "Play" : "Pause";
        _next.IsVisible = !one;
        _hint.Text = one ? HintWithoutNext : HintWithNext;

        _below.Children.Clear();
        _generating = null;

        if (state == StoryRunLineState.Held)
        {
            _below.Children.Add(new Notice(NoticeLevel.Warning, inline: true)
            {
                Label = StoryRunPage.NoVoiceLabel,
                Text = StoryRunVoicing.Why(playback.Hold),
                Detail = playback.HoldDetail,
            });
        }

        if (state == StoryRunLineState.SpeakingAgain)
        {
            var track = Gauge.Track(playback.Progress, ThemeManager.AKey);
            track.Width = 220;
            track.VerticalAlignment = VerticalAlignment.Center;
            var generating = Typeset.Mono(StoryRunPage.GeneratingText, TypeScale.Meta, ThemeManager.GreyKey);
            generating.VerticalAlignment = VerticalAlignment.Center;
            _generating = track;
            _below.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { track, generating } });
        }

        _below.IsVisible = _below.Children.Count > 0;
        _progressRow.IsVisible = state != StoryRunLineState.SpeakingAgain;
        ThemeInk.Bind(_fill, Border.BackgroundProperty, playback.IsPaused ? ThemeManager.AKey : ThemeManager.CyanKey);

        Poll();
    }

    /// <summary>Reads the line's position and length, or a held line's time left, into the progress line and the time slot.</summary>
    internal void Poll()
    {
        if (_player.Now is not { } playback)
        {
            return;
        }

        if (_generating is { } track)
        {
            Gauge.Fill(track, playback.Progress);
        }

        if (playback is { State: StoryRunLineState.Held, HeldUntil: { } until })
        {
            var left = Math.Max(0, (int)Math.Ceiling((until - _player.Time.GetUtcNow()).TotalSeconds));
            _time.Text = Clock(TimeSpan.FromSeconds(left));
            SetProgress(0);
            return;
        }

        var now = _progress();

        _time.Text = now is { } read ? $"{Clock(read.Elapsed)} / {Clock(read.Length)}" : string.Empty;
        SetProgress(now is { Length: { } length } known && length > TimeSpan.Zero ? known.Elapsed / length : 0);
    }

    private void SetProgress(double share)
    {
        var clamped = Math.Clamp(double.IsFinite(share) ? share : 0, 0, 1);
        _rail.ColumnDefinitions[0].Width = new GridLength(clamped, GridUnitType.Star);
        _rail.ColumnDefinitions[1].Width = new GridLength(1 - clamped, GridUnitType.Star);
    }

    private static string Clock(TimeSpan? time) => AudioPlayerReadout.Clock(time);
}
