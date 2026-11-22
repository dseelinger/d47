using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Media;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Capabilities.Builtin;
using D47.Core.Interface;
using D47.Core.Viewing;
using D47.Core.Vr;

namespace D47.App.Panel;

/// <summary>Media › Movies › one title: the picture, its captions, where it is, and how it plays.</summary>
public sealed class VideoPlayerPage : UserControl, IPageSummary, IPageChrome
{
    public const string KeyPrefix = "media-movies.watch.";

    public const string PictureName = "VideoPicture";

    public const string CaptionName = "VideoCaption";

    public const string SeekName = "VideoSeek";

    public const string BackName = "VideoBack";

    public const string PlayName = "VideoPlay";

    public const string ForwardName = "VideoForward";

    public const string FromStartName = "VideoFromStart";

    public const string RetryName = "VideoRetry";

    public const string PushToTalkName = "VideoPushToTalk";

    public const string VolumeName = "VideoVolume";

    public const string PlaysOnName = "VideoPlaysOn";

    public const string CaptionsChoiceName = "VideoCaptions";

    public const string StoppedLabel = "Video stopped";

    public const string NoDecoderLabel = "Can't play this file";

    public const string NoSubtitlesLabel = "No subtitle file";

    public const string NotKept = "This title is not in your list or carrying on.";

    private const double CaptionSize = 20;

    private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(40);

    private static readonly TimeSpan PollEveryInHeadset = TimeSpan.FromSeconds(1);

    private readonly WatchHost _host;
    private readonly VideoTitle _title;
    private readonly bool _big;
    private readonly DispatcherTimer _poll = new();
    private readonly Image _picture = new() { Stretch = Stretch.Uniform, Name = PictureName };
    private readonly TextBlock _caption = new()
    {
        Name = CaptionName,
        FontFamily = new FontFamily("Segoe UI"),
        FontWeight = FontWeight.SemiBold,
        FontSize = CaptionSize,
        Foreground = Brushes.White,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
    };

    private readonly Border _captionBand = new()
    {
        Background = ThemeManager.Scrim(),
        Padding = new Thickness(20, 8),
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Bottom,
        Margin = new Thickness(20),
        IsVisible = false,
    };

    private readonly StatusLine _wait = new() { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(20) };
    private readonly Notice _stopped = new(NoticeLevel.Error) { IsVisible = false };
    private readonly Notice _noSubtitles = new(NoticeLevel.Warning) { IsVisible = false };
    private readonly TextBlock _elapsed = Typeset.Mono("00:00:00", 13, ThemeManager.CyanKey, vertical: VerticalAlignment.Center);
    private readonly TextBlock _length = Typeset.Mono("00:00:00", 13, ThemeManager.GreyKey, vertical: VerticalAlignment.Center, align: TextAlignment.Right);
    private readonly SeekBar _bar = new() { Name = SeekName };
    private readonly Button _back = Tile(BackName, "◄ 30 S", "Back 30 seconds");
    private readonly Button _play = Tile(PlayName, "PLAY", "Play or pause");
    private readonly Button _forward = Tile(ForwardName, "30 S ►", "Forward 30 seconds");
    private readonly Button _fromStart = Tile(FromStartName, "FROM THE START", "From the start");
    private readonly TextBlock _place = Typeset.Mono(string.Empty, 15, ThemeManager.CyanKey, wrap: true);
    private readonly TextBlock _videoLength = Typeset.Mono(string.Empty, 15, ThemeManager.WhiteKey, wrap: true);
    private readonly TextBlock _playsFrom = Typeset.Mono(string.Empty, 13, ThemeManager.WhiteKey, wrap: true);
    private readonly TextBlock _videoKind = Typeset.Mono(string.Empty, 13, ThemeManager.WhiteKey, wrap: true);
    private readonly Segment _playsOn = new() { Name = PlaysOnName };
    private readonly Segment _captions = new() { Name = CaptionsChoiceName };
    private readonly CheckBox _pushToTalk = LabeledCheckBox.Caps("PAUSE WHILE YOU HOLD PUSH-TO-TALK");
    private readonly Level _volume = new() { Name = VolumeName, Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center };

    private WriteableBitmap? _bitmap;
    private VideoFrame? _shown;
    private bool _refreshing;
    private bool _attached;
    private (bool None, bool On)? _captionState;
    private (VideoFailure? Failure, string Text)? _failureState;
    private string? _waitingFor;

    public VideoPlayerPage(WatchHost host, VideoTitle title, Control? settingsStrip = null, bool big = false)
    {
        _host = host;
        BarTool = PageChrome.ToolOf(settingsStrip);
        _title = title;
        _big = big;

        _bar.Draggable = !big;
        _poll.Interval = big ? PollEveryInHeadset : PollEvery;
        _poll.Tick += (_, _) => Refresh();

        _bar.Dragged += fraction =>
        {
            if (Length() is { } length)
            {
                _elapsed.Text = WatchPage.Clock(length * fraction);
            }
        };
        _bar.Released += fraction =>
        {
            if (Length() is { } length)
            {
                Seek(length * fraction);
            }

            Refresh();
        };
        _back.Click += (_, _) => Skip(-VideoPlayback.SkipBy);
        _forward.Click += (_, _) => Skip(VideoPlayback.SkipBy);
        _play.Click += (_, _) => PlayOrPause();
        _fromStart.Click += (_, _) => _host.FromStart();

        _pushToTalk.Name = PushToTalkName;
        _pushToTalk.IsCheckedChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                _host.SetPauseForPushToTalk(_pushToTalk.IsChecked == true);
            }
        };

        _volume.ValueChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                _host.SetVolume(_volume.Value);
            }
        };

        _playsOn.ItemsSource = ["THIS WINDOW", "BIG PANEL", "MINI PANEL"];
        _playsOn.Statuses =
        [
            new ChoiceStatus("Desktop", ChoiceTone.Grey),
            new ChoiceStatus("Headset", ChoiceTone.Grey),
            new ChoiceStatus("Desktop and headset", ChoiceTone.Grey),
        ];
        _playsOn.SelectedIndex = 0;
        _playsOn.SetOptionEnabled(1, false);
        _playsOn.SetOptionEnabled(2, false);

        _captions.ItemsSource = ["SUBTITLE FILE", "WRITE FROM THE SOUND", "OFF"];
        _captions.SelectionChanged += (_, _) =>
        {
            if (!_refreshing && _captions.SelectedIndex is 0 or 2)
            {
                _host.CaptionsOn = _captions.SelectedIndex == 0;
            }
        };

        _noSubtitles.Label = NoSubtitlesLabel;
        _noSubtitles.Text = "This video came without captions. D47 can write them from the soundtrack on this computer.";
        var write = new Button { Content = "WRITE CAPTIONS", MinHeight = TypeScale.MinimumTarget, IsEnabled = false };
        _noSubtitles.Actions.Add(write);

        _captionBand.Child = _caption;

        var frame = new Grid { Background = Brushes.Black, MinHeight = 120 };
        frame.Children.Add(_picture);
        frame.Children.Add(_wait);
        frame.Children.Add(_captionBand);
        var picture = new Border
        {
            Child = frame,
            BorderThickness = new Thickness(1),
            Name = "VideoFrame",
        };
        ThemeInk.Bind(picture, Border.BorderBrushProperty, ThemeManager.Line2Key);
        picture.SizeChanged += (_, e) => picture.Height = Math.Max(120, e.NewSize.Width * 9 / 16);

        var timeline = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        _elapsed.Width = 76;
        _length.Width = 76;
        DockPanel.SetDock(_elapsed, Dock.Left);
        DockPanel.SetDock(_length, Dock.Right);
        timeline.Children.Add(_elapsed);
        timeline.Children.Add(_length);
        timeline.Children.Add(_bar);

        var tiles = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,2*,*,*"),
            ColumnSpacing = Gaps.Tile,
            Children = { _back, _play, _forward, _fromStart },
        };
        Grid.SetColumn(_play, 1);
        Grid.SetColumn(_forward, 2);
        Grid.SetColumn(_fromStart, 3);

        var left = new StackPanel { Spacing = 12, Children = { picture, timeline, tiles } };

        var slabs = new StackPanel
        {
            Spacing = Gaps.Tile,
            Children =
            {
                Datum("Your place", _place),
                Datum("Length", _videoLength),
                Datum("Credit", Typeset.Prose(title.Credit ?? WatchHost.SourceOf(title).Split(" · ")[0], 13, ThemeManager.WhiteKey, wrap: true)),
                Datum("Licence", Typeset.Prose(string.IsNullOrEmpty(title.Licence) ? "Your own file" : title.Licence, 13, ThemeManager.WhiteKey, wrap: true)),
                Datum("Plays from", _playsFrom),
                Datum("Video", _videoKind),
            },
        };

        var top = _big
            ? (Control)new StackPanel { Spacing = 16, Children = { left, slabs } }
            : new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,300"),
                ColumnSpacing = 28,
                Children = { left, slabs },
            };

        if (!_big)
        {
            Grid.SetColumn(slabs, 1);
        }

        var body = new StackPanel
        {
            Spacing = 16,
            MaxWidth = 1100,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children =
            {
                _stopped,
                _noSubtitles,
                top,
                Head("Plays on"),
                _playsOn,
                Head("Captions"),
                _captions,
                Head("Sound"),
                _pushToTalk,
                VolumeRow(),
            },
        };

        var page = new DockPanel();

        if (settingsStrip is not null)
        {
            page.DockStripAtTop(settingsStrip);
        }

        page.Children.Add(new ScrollViewer
        {
            Content = new Border { Padding = new Thickness(14), Child = body },
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = page;

        Refresh();
    }

    public Control? BarTool { get; }

    public string? HelpTopic => InterfaceCapability.Id;

    public string Summary =>
        _title.Subtitles is null ? $"{_title.Title}." : $"{_title.Title}, with captions from its subtitle file.";

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    /// <summary>The crumb of one title's page, found again by <see cref="TitleOf"/>.</summary>
    public static NavCrumb CrumbOf(VideoTitle title) =>
        new(KeyPrefix + title.Source + "." + title.Id, title.Title) { Help = InterfaceCapability.Id, Whole = true };

    /// <summary>The title a crumb key names, if the host knows it.</summary>
    public static VideoTitle? TitleOf(WatchHost host, string key)
    {
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = key[KeyPrefix.Length..];
        var dot = rest.IndexOf('.', StringComparison.Ordinal);

        return dot <= 0 ? null : host.Find(rest[..dot], rest[(dot + 1)..]);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.Changed += OnChanged;
        _attached = true;
        _poll.Start();
        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Changed -= OnChanged;
        _attached = false;
        _poll.Stop();
    }

    private void OnChanged()
    {
        if (_attached)
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    private bool IsCurrent => _host.Current is { } current && current.Source == _title.Source && current.Id == _title.Id;

    private bool HasVideo => _host.Video.Source == WatchHost.PlaySource(_title) && _host.Video.State is not null;

    private TimeSpan? Length() => HasVideo && _host.Video.Length is { } length ? length : _title.Length;

    private void Seek(TimeSpan to)
    {
        if (HasVideo)
        {
            _host.Video.Seek(to);
        }
        else
        {
            _host.Play(_title, to);
        }
    }

    private void Skip(TimeSpan by)
    {
        if (HasVideo)
        {
            _host.Video.Skip(by);
        }
    }

    private void PlayOrPause()
    {
        if (HasVideo && _host.Video.State is VideoState.Playing or VideoState.Stalled or VideoState.Opening)
        {
            _host.Pause();
        }
        else
        {
            if (!IsCurrent)
            {
                _host.Open(_title);
            }

            _host.Resume();
        }

        Refresh();
    }

    internal void Refresh()
    {
        _refreshing = true;

        try
        {
            var video = _host.Video;
            var loaded = HasVideo;
            var state = loaded ? video.State : null;
            var kept = _host.Store.Find(_title.Source, _title.Id);
            var position = loaded ? video.Position : kept?.Place ?? TimeSpan.Zero;
            var length = Length();

            _play.Content = state is VideoState.Playing or VideoState.Stalled or VideoState.Opening ? "PAUSE" : "PLAY";
            _play.IsEnabled = _title.Playable;
            _back.IsEnabled = loaded;
            _forward.IsEnabled = loaded;
            _fromStart.IsEnabled = _title.Playable;

            if (!_bar.IsDragging)
            {
                _elapsed.Text = WatchPage.Clock(position);
                _bar.Position = length is { } total && total > TimeSpan.Zero ? Math.Clamp(position / total, 0, 1) : 0;
            }

            _length.Text = length is { } l ? WatchPage.Clock(l) : "--:--:--";
            _place.Text = WatchPage.Clock(position);
            _videoLength.Text = length is { } whole ? WatchPage.Clock(whole) : "Not given";
            _playsFrom.Text = _title.Media is { IsFile: false, Host: { Length: > 0 } host } ? host : "This computer";

            ShowFrame(loaded ? video.Frame : null);
            DrawKind(loaded ? video.Frame : null);
            DrawWait(state);
            DrawFailure(loaded ? video.Failure : null, position);
            DrawCaptions(position);

            _pushToTalk.IsChecked = _host.PauseForPushToTalk;
            _volume.Value = _host.Volume;
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void DrawKind(VideoFrame? frame)
    {
        var extension = _title.Media is { } media ? Path.GetExtension(media.IsFile ? media.LocalPath : media.AbsolutePath).TrimStart('.').ToUpperInvariant() : string.Empty;
        var size = frame is null ? string.Empty : string.Create(CultureInfo.InvariantCulture, $"{frame.Width}×{frame.Height}");

        _videoKind.Text = string.Join(" · ", new[] { extension, size }.Where(part => part.Length > 0));
    }

    private void DrawWait(VideoState? state)
    {
        var waiting = state is VideoState.Opening or VideoState.Stalled ? $"Waiting for {SourceName()}…" : null;

        if (waiting == _waitingFor)
        {
            return;
        }

        _waitingFor = waiting;

        if (waiting is null)
        {
            _wait.Clear();
        }
        else
        {
            _wait.Wait(waiting);
        }
    }

    private string SourceName() =>
        _title.Source == WatchHost.FileSource || !VideoSources.All.Contains(_title.Source, StringComparer.Ordinal)
            ? "this file"
            : VideoSources.NameOf(_title.Source);

    private void DrawFailure(VideoFailure? failure, TimeSpan position)
    {
        var text = failure == VideoFailure.Unreachable
            ? $"{SourceName()} stopped answering at {WatchPage.Clock(position)}. Your place is kept; RETRY carries on from there."
            : string.Empty;

        if (_failureState == (failure, text))
        {
            return;
        }

        _failureState = (failure, text);
        _stopped.Actions.Clear();

        if (failure is null)
        {
            _stopped.IsVisible = false;
            return;
        }

        _stopped.IsVisible = true;

        if (failure == VideoFailure.Unreachable)
        {
            _stopped.Label = StoppedLabel;
            _stopped.Text = text;
            _stopped.Detail = _title.Media?.Host.ToUpperInvariant();

            var retry = new Button { Name = RetryName, Content = "RETRY", MinHeight = TypeScale.MinimumTarget };
            retry.Click += (_, _) => _host.Resume();
            _stopped.Actions.Add(retry);

            return;
        }

        _stopped.Label = NoDecoderLabel;
        _stopped.Text = failure == VideoFailure.Picture
            ? "Windows has no decoder for this video. MP4 files with H.264 video play; try the MP4 copy from the same source."
            : "Windows has no decoder for this video's soundtrack. MP4 files with AAC sound play; try the MP4 copy from the same source.";
        _stopped.Detail = $"MEDIA FOUNDATION · NO DECODER · {Path.GetFileName(_title.Media?.IsFile == true ? _title.Media.LocalPath : _title.Media?.AbsolutePath ?? string.Empty)}";
    }

    private void DrawCaptions(TimeSpan position)
    {
        var cues = IsCurrent ? _host.Cues : [];
        var none = _title.Subtitles is null || (IsCurrent && !_host.CuesLoading && cues.Count == 0);
        var cue = _host.CaptionsOn && !none ? SubtitleCues.At(cues, position) : null;

        _caption.Text = cue?.Text ?? string.Empty;
        _captionBand.IsVisible = cue is not null;
        _noSubtitles.IsVisible = none;

        if (_captionState == (none, _host.CaptionsOn))
        {
            return;
        }

        _captionState = (none, _host.CaptionsOn);

        _captions.Statuses =
        [
            new ChoiceStatus(none ? "None for this title" : _title.Source == WatchHost.FileSource ? "Beside the video" : "From the source", none ? ChoiceTone.Grey : ChoiceTone.Yellow),
            new ChoiceStatus("As it plays · Whisper", ChoiceTone.Grey),
            new ChoiceStatus("No captions", ChoiceTone.Grey),
        ];
        _captions.SetOptionEnabled(0, !none);
        _captions.SetOptionEnabled(1, false);
        _captions.SelectedIndex = _host.CaptionsOn && !none ? 0 : 2;
    }

    private void ShowFrame(VideoFrame? frame)
    {
        if (frame is null || ReferenceEquals(frame, _shown))
        {
            return;
        }

        _shown = frame;

        if (_bitmap is null || _bitmap.PixelSize.Width != frame.Width || _bitmap.PixelSize.Height != frame.Height)
        {
            _bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            _picture.Source = _bitmap;
        }

        using (var buffer = _bitmap.Lock())
        {
            var row = frame.Width * 4;

            for (var y = 0; y < frame.Height; y++)
            {
                Marshal.Copy(frame.Pixels, y * row, buffer.Address + (y * buffer.RowBytes), row);
            }
        }

        _picture.InvalidateVisual();
    }

    private Control VolumeRow()
    {
        var label = Typeset.Chrome("VIDEO VOLUME", 13, ThemeManager.GreyKey, FontWeight.SemiBold, vertical: VerticalAlignment.Center);
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("240,*"), ColumnSpacing = 12, Children = { label, _volume } };
        Grid.SetColumn(_volume, 1);

        return row;
    }

    private static Control Head(string text) =>
        Typeset.Chrome(text.ToUpperInvariant(), 13, ThemeManager.AKey, FontWeight.SemiBold, margin: new Thickness(0, 8, 0, 0));

    private static Border Datum(string label, Control value)
    {
        var name = Typeset.Chrome(label.ToUpperInvariant(), 12, ThemeManager.GreyKey, FontWeight.Medium);
        var slab = new Border
        {
            Padding = new Thickness(14, 10),
            Child = new StackPanel { Spacing = 2, Children = { name, value } },
        };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.TileKey);

        return slab;
    }

    private static Button Tile(string name, string text, string accessibleName)
    {
        var tile = new Button
        {
            Name = name,
            Content = text,
            MinHeight = TypeScale.MinimumTarget,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(tile, accessibleName);

        return tile;
    }
}
