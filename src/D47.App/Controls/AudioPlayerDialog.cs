using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.App.Controls;

/// <summary>The ambient music's track, folder, transport and level; closing it closes the player on every surface.</summary>
public sealed class AudioPlayerDialog : ModalDialog
{
    public const string TrackName = "AudioPlayerTrack";

    public const string FolderName = "AudioPlayerFolder";

    public const string StateName = "AudioPlayerState";

    public const string PreviousName = "AudioPlayerPrevious";

    public const string PlayName = "AudioPlayerPlay";

    public const string NextName = "AudioPlayerNext";

    public const string ThumbsDownName = "AudioPlayerThumbsDown";

    public const string CloseName = "AudioPlayerClose";

    public const string LevelName = "AudioPlayerLevel";

    public const string HintName = "AudioPlayerHint";

    public const string SeekName = "AudioPlayerSeek";

    public const string ElapsedName = "AudioPlayerElapsed";

    public const string LengthName = "AudioPlayerLength";

    /// <summary>How often the bar and times are read while a draggable dialog is open.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(250);

    /// <summary>How often they are read on the headset's big panel, where each update redraws the overlay.</summary>
    public static readonly TimeSpan PollEveryInHeadset = TimeSpan.FromSeconds(1);

    private readonly PanelViewModel _model;
    private readonly SettingsService _settings;
    private readonly Func<MusicAction, string> _control;
    private readonly Action _thumbsDown;
    private readonly Func<TrackProgress?>? _progress;
    private readonly Func<TimeSpan, bool>? _seek;
    private readonly DispatcherTimer _poll = new() { Interval = PollEvery };
    private readonly SeekBar _bar = new() { Name = SeekName };
    private readonly TextBlock _elapsed = new() { Name = ElapsedName };
    private readonly TextBlock _length = new() { Name = LengthName };
    private TimeSpan? _known;

    private readonly TextBlock _state = new() { Name = StateName };
    private readonly TextBlock _track = new() { Name = TrackName };
    private readonly TextBlock _folder = new() { Name = FolderName };
    private readonly Button _previous = Tile(PreviousName, "◄ PREVIOUS");
    private readonly Button _play = Tile(PlayName, "PAUSE");
    private readonly Button _next = Tile(NextName, "NEXT ►");
    private readonly Button _down = Tile(ThumbsDownName, "THUMBS DOWN");
    private readonly Level _level = new() { Name = LevelName, Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center };
    private bool _refreshing;

    public AudioPlayerDialog(
        PanelViewModel model,
        SettingsService settings,
        Func<MusicAction, string> control,
        Action thumbsDown,
        Func<TrackProgress?>? progress = null,
        Func<TimeSpan, bool>? seek = null)
    {
        _model = model;
        _settings = settings;
        _control = control;
        _thumbsDown = thumbsDown;
        _progress = progress;
        _seek = seek;
        _bar.Draggable = seek is not null;
        _poll.Interval = seek is null ? PollEveryInHeadset : PollEvery;
        _bar.Dragged += OnDragged;
        _bar.Released += OnReleased;
        _poll.Tick += (_, _) => Poll();

        foreach (var time in new[] { _elapsed, _length })
        {
            time.FontFamily = new FontFamily(Fonts.MonoFamily);
            time.FontSize = TypeScale.Small;
            time.VerticalAlignment = VerticalAlignment.Center;
            time.Width = 48;
        }

        _length.TextAlignment = TextAlignment.Right;

        Title = "Audio player";
        Width = Modal.Width;

        _down.Classes.Add("destructive");

        _track.FontFamily = new FontFamily(Fonts.ChromeFamily);
        _track.FontSize = TypeScale.Description;
        _track.TextTrimming = TextTrimming.CharacterEllipsis;
        _track.TextWrapping = TextWrapping.NoWrap;

        _folder.FontFamily = new FontFamily(Fonts.ChromeFamily);
        _folder.FontSize = TypeScale.Description;
        _folder.TextTrimming = TextTrimming.CharacterEllipsis;

        _state.FontFamily = new FontFamily(Fonts.ChromeFamily);
        _state.FontSize = TypeScale.Figure;
        _state.FontWeight = FontWeight.Medium;

        _previous.Click += (_, _) => _control(MusicAction.Previous);
        _play.Click += (_, _) => _control(_model.AudioPlayer.Playing ? MusicAction.Pause : MusicAction.Resume);
        _next.Click += (_, _) => _control(MusicAction.Next);
        _down.Click += (_, _) => _thumbsDown();

        _level.ValueChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                _settings.Apply(
                    AudioCapability.LevelKey(AudioChannel.Music),
                    _level.Value.ToString("0.00", CultureInfo.InvariantCulture),
                    SettingsCaller.Panel);
            }
        };

        var close = new Button { Name = CloseName, Content = "CLOSE", MinWidth = 110, MinHeight = TypeScale.MinimumTarget, VerticalContentAlignment = VerticalAlignment.Center };
        close.Click += (_, _) => Close();

        var figure = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Right };
        figure.Children.Add(StatTile.Caption("NOW", ThemeManager.GreyKey));
        figure.Children.Add(_state);

        var layout = Modal.Build(
            "Ambient music",
            "Audio player",
            Body(),
            [close],
            figure: figure,
            scrolls: false,
            trailing: Hint());
        ThemeInk.Bind(this, TemplatedControl.BackgroundProperty, ThemeManager.BarKey);
        Content = layout;

        AttachedToVisualTree += (_, _) =>
        {
            _model.PropertyChanged += OnModelChanged;
            _settings.Changed += OnSettingsChanged;
            Refresh();
            Poll();
            _poll.Start();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _model.PropertyChanged -= OnModelChanged;
            _settings.Changed -= OnSettingsChanged;
            _poll.Stop();
        };

        Refresh();
    }

    private Control Body()
    {
        var data = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*"), ColumnSpacing = Gaps.Tile };
        data.Children.Add(StatTile.Tile("NOW PLAYING", _track));
        var folder = StatTile.Tile("FOLDER", _folder);
        Grid.SetColumn(folder, 1);
        data.Children.Add(folder);

        var tiles = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*"), ColumnSpacing = Gaps.Tile };
        Control[] buttons = [_previous, _play, _next, _down];

        for (var i = 0; i < buttons.Length; i++)
        {
            Grid.SetColumn(buttons[i], i);
            tiles.Children.Add(buttons[i]);
        }

        var label = Typeset.Text("Music level", TypeScale.Secondary, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);
        var level = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), ColumnSpacing = 12, MinHeight = TypeScale.MinimumTarget };
        level.Children.Add(label);
        Grid.SetColumn(_level, 1);
        level.Children.Add(_level);

        var seek = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        seek.Children.Add(_elapsed);
        Grid.SetColumn(_bar, 1);
        seek.Children.Add(_bar);
        Grid.SetColumn(_length, 2);
        seek.Children.Add(_length);

        return new StackPanel { Spacing = 16, Children = { data, seek, tiles, level } };
    }

    private Control Hint()
    {
        var line = new StackPanel
        {
            Name = HintName,
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var key = _settings.Current.Hotkeys.AudioPlayer;

        line.Children.Add(Typeset.Text(
            key is { Length: > 0 } ? "Say “audio player”, or press" : "Say “audio player”",
            TypeScale.Description,
            ThemeManager.GreyKey,
            vertical: VerticalAlignment.Center));

        if (key is { Length: > 0 })
        {
            var chord = new TextBlock
            {
                Text = key.ToUpperInvariant(),
                FontFamily = new FontFamily(Fonts.MonoFamily),
                FontSize = TypeScale.Description,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ThemeInk.Bind(chord, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
            line.Children.Add(chord);
        }

        return line;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PanelViewModel.AudioPlayer))
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    private void OnSettingsChanged(SettingsChanged change)
    {
        if (change.Key == AudioCapability.LevelKey(AudioChannel.Music) || change.Key == AudioCapability.MuteKey(AudioChannel.Music))
        {
            Dispatcher.UIThread.Post(Refresh);
        }
    }

    internal bool Polling => _poll.IsEnabled;

    internal TimeSpan PollInterval => _poll.Interval;

    /// <summary>Reads the track's position and length and redraws the bar and times; a held drag keeps its own.</summary>
    internal void Poll()
    {
        var read = _model.AudioPlayer;
        var on = read.Enabled;
        TrackProgress? now = on ? _progress?.Invoke() : null;
        var timeInk = on ? ThemeManager.GreyKey : ThemeManager.Grey2Key;

        _bar.Active = on && now is not null;
        _known = now?.Length;
        ThemeInk.Bind(_elapsed, TextBlock.ForegroundProperty, timeInk);
        ThemeInk.Bind(_length, TextBlock.ForegroundProperty, timeInk);
        _length.Text = AudioPlayerReadout.Clock(_known);

        if (_bar.IsDragging)
        {
            return;
        }

        _elapsed.Text = AudioPlayerReadout.Clock(now?.Elapsed);
        _bar.Position = now is { Length: { } length } && length > TimeSpan.Zero ? now.Value.Elapsed / length : 0;
    }

    private TimeSpan? Target(double fraction) => _known is { } length ? length * fraction : null;

    private void OnDragged(double fraction)
    {
        if (Target(fraction) is { } to)
        {
            _elapsed.Text = AudioPlayerReadout.Clock(to);
        }
    }

    private void OnReleased(double fraction)
    {
        if (Target(fraction) is { } to)
        {
            _seek?.Invoke(to);
        }

        Poll();
    }

    /// <summary>Redraws from the model's readout and the music channel's setting.</summary>
    internal void Refresh()
    {
        var read = _model.AudioPlayer;
        var mix = _settings.Current.Audio.Music;
        var live = read.Enabled;

        _state.Text = read.StateWord;
        ThemeInk.Bind(_state, TextBlock.ForegroundProperty, read.Playing ? ThemeManager.CyanKey : ThemeManager.GreyKey);

        _track.Text = read.Track;
        ThemeInk.Bind(_track, TextBlock.ForegroundProperty, live ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

        _folder.Text = read.Folder;
        ThemeInk.Bind(_folder, TextBlock.ForegroundProperty, live || read.CanStart ? ThemeManager.AKey : ThemeManager.GreyKey);

        _play.Content = read.PlayLabel.ToUpperInvariant();

        _previous.IsEnabled = live;
        _down.IsEnabled = live;
        _play.IsEnabled = live || read.CanStart;
        _next.IsEnabled = live || read.CanStart;

        Poll();

        _refreshing = true;
        _level.Value = mix.Level;
        _level.Muted = mix.Muted;
        _refreshing = false;
    }

    private static Button Tile(string name, string text) => new()
    {
        Name = name,
        Content = text,
        MinHeight = TypeScale.MinimumTarget,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        HorizontalContentAlignment = HorizontalAlignment.Center,
        VerticalContentAlignment = VerticalAlignment.Center,
    };
}
