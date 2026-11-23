using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Reactive;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Audio;
using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Media › Music: the ambient music's track, folder, seek bar, transport and level.</summary>
public sealed class MusicPage : UserControl, IPageSummary, IPageChrome
{
    public const string RootKey = MediaFurnishing.MusicRoot;

    public const string PageSummary = "The ambient music: what plays now, and how it is set up.";

    public const string BodyName = "MusicBody";

    public const string TrackName = "MusicTrack";

    public const string FolderName = "MusicFolder";

    public const string StateName = "MusicState";

    public const string PreviousName = "MusicPrevious";

    public const string PlayName = "MusicPlay";

    public const string NextName = "MusicNext";

    public const string ThumbsDownName = "MusicThumbsDown";

    public const string LevelName = "MusicLevel";

    public const string HintName = "MusicHint";

    public const string SeekName = "MusicSeek";

    public const string ElapsedName = "MusicElapsed";

    public const string LengthName = "MusicLength";

    /// <summary>How often the bar and times are read on the d47 window.</summary>
    public static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(250);

    /// <summary>How often they are read on the headset's big panel, where each update redraws the overlay.</summary>
    public static readonly TimeSpan PollEveryInHeadset = TimeSpan.FromSeconds(1);

    private const double MostWidth = 900;

    private readonly MusicHost _host;
    private readonly PanelViewModel _model;
    private readonly PanelNavigator _nav;
    private readonly bool _big;
    private readonly DispatcherTimer _poll = new();
    private string? _ticked;
    private readonly SeekBar _bar = new() { Name = SeekName };
    private readonly TextBlock _elapsed = Typeset.Mono("0:00", 12, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center);
    private readonly TextBlock _length = Typeset.Mono("0:00", 12, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center, align: TextAlignment.Right);
    private readonly TextBlock _state = Typeset.Chrome(string.Empty, 12, ThemeManager.GreyKey, vertical: VerticalAlignment.Bottom);
    private readonly TextBlock _track = new() { Name = TrackName };
    private readonly TextBlock _folder = new() { Name = FolderName };
    private readonly Button _previous = Tile(PreviousName, "◄ PREVIOUS", "Previous track");
    private readonly Button _play = Tile(PlayName, "PAUSE", "Play or pause");
    private readonly Button _next = Tile(NextName, "NEXT ►", "Next track");
    private readonly Button _down = Tile(ThumbsDownName, "THUMBS DOWN", "Thumbs down this track");
    private readonly Level _level = new() { Name = LevelName, Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _hint = new() { Name = HintName, Orientation = Orientation.Horizontal };
    private readonly Control _body;
    private TimeSpan? _known;
    private bool _refreshing;
    private IDisposable? _mode;
    private bool _attached;
    private bool _mini;

    public MusicPage(MusicHost host, PanelViewModel model, PanelNavigator nav, Control? settingsStrip = null, bool big = false)
    {
        _host = host;
        _model = model;
        _nav = nav;
        _big = big;
        BarTool = PageChrome.ToolOf(settingsStrip);

        _bar.Draggable = !big;
        _poll.Interval = big ? PollEveryInHeadset : PollEvery;
        _bar.Dragged += OnDragged;
        _bar.Released += OnReleased;
        _poll.Tick += (_, _) => Poll();

        _elapsed.Name = ElapsedName;
        _length.Name = LengthName;
        _elapsed.Width = 48;
        _length.Width = 48;
        _state.Name = StateName;
        _down.Classes.Add("destructive");

        foreach (var value in new[] { _track, _folder })
        {
            value.FontFamily = new FontFamily(Fonts.ChromeFamily);
            value.FontSize = TypeScale.Description;
            value.TextTrimming = TextTrimming.CharacterEllipsis;
            value.TextWrapping = TextWrapping.NoWrap;
        }

        _previous.Click += (_, _) => _host.Control(MusicAction.Previous);
        _play.Click += (_, _) => _host.Control(_model.AudioPlayer.Playing ? MusicAction.Pause : MusicAction.Resume);
        _next.Click += (_, _) => _host.Control(MusicAction.Next);
        _down.Click += (_, _) => _host.ThumbsDown();

        _level.ValueChanged += (_, _) =>
        {
            if (!_refreshing)
            {
                _host.Settings.Apply(
                    AudioCapability.LevelKey(AudioChannel.Music),
                    _level.Value.ToString("0.00", CultureInfo.InvariantCulture),
                    SettingsCaller.Panel);
            }
        };

        var root = new DockPanel { Margin = new Thickness(14) };

        if (settingsStrip is not null)
        {
            root.DockStripAtTop(settingsStrip);
        }

        _body = big ? BigBody() : WindowBody();
        _body.Name = BodyName;
        root.Children.Add(new ScrollViewer
        {
            Content = _body,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });

        Content = root;
        DrawHint();
        Refresh();
    }

    public Control? BarTool { get; }

    public string? HelpTopic => AudioCapability.Id;

    string IPageSummary.Summary => PageSummary;

    public event EventHandler? SummaryChanged
    {
        add { }
        remove { }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _model.PropertyChanged += OnModelChanged;
        _host.Settings.Changed += OnSettingsChanged;
        _nav.Changed += OnNavChanged;
        _attached = true;

        // A mini surface reads the music from its read-out bar, so the page under it draws nothing.
        var panel = this.GetVisualAncestors().OfType<PanelView>().FirstOrDefault();
        _mode = panel?.GetObservable(PanelView.ModeProperty).Subscribe(new AnonymousObserver<PanelMode>(mode =>
        {
            _mini = mode == PanelMode.Mini;
            _body.IsVisible = !_mini;
            SyncPoll();
        }));

        DrawHint();
        Refresh();
        SyncPoll();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _model.PropertyChanged -= OnModelChanged;
        _host.Settings.Changed -= OnSettingsChanged;
        _nav.Changed -= OnNavChanged;
        _attached = false;
        _mode?.Dispose();
        _mode = null;
        SyncPoll();
    }

    private void OnNavChanged(object? sender, EventArgs e) => SyncPoll();

    /// <summary>Reads the bar and times only while this page is drawn: attached, on the page the surface shows, and not mini.</summary>
    private void SyncPoll()
    {
        if (_attached && !_mini && _nav.DestinationKey == RootKey)
        {
            if (!_poll.IsEnabled)
            {
                Poll();
                _poll.Start();
            }
        }
        else
        {
            _poll.Stop();
        }
    }

    // ----- layout

    private Grid WindowBody()
    {
        var head = RoutingKit.Section("Ambient music", _state);

        var tiles = Columns([_previous, _play, _next, _down]);

        var label = Typeset.Prose("Music level", 15, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);
        var level = new Grid { ColumnDefinitions = new ColumnDefinitions("120,*"), ColumnSpacing = 12, MinHeight = TypeScale.MinimumTarget };
        level.Children.Add(label);
        Grid.SetColumn(_level, 1);
        level.Children.Add(_level);

        var column = new StackPanel { Spacing = 14, Children = { head, Data(), Seek(), tiles, level, _hint } };

        // The column fills the page up to MostWidth and keeps to the left beyond it.
        var body = new Grid { ColumnDefinitions = [new ColumnDefinition(GridLength.Star) { MaxWidth = MostWidth }, new ColumnDefinition(GridLength.Auto)] };
        body.Children.Add(column);

        return body;
    }

    private StackPanel BigBody()
    {
        var label = Typeset.Prose("Music level", 15, ThemeManager.GreyKey, vertical: VerticalAlignment.Center);
        var level = new Grid { ColumnDefinitions = new ColumnDefinitions("96,*"), ColumnSpacing = 10 };
        level.Children.Add(label);
        Grid.SetColumn(_level, 1);
        level.Children.Add(_level);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*,24,380"), ColumnSpacing = Gaps.Tile };
        Control[] cells = [_previous, _play, _next, _down];

        for (var i = 0; i < cells.Length; i++)
        {
            Grid.SetColumn(cells[i], i);
            row.Children.Add(cells[i]);
        }

        Grid.SetColumn(level, 5);
        row.Children.Add(level);

        return new StackPanel { Spacing = 8, Children = { Data(), Seek(), row } };
    }

    /// <summary>Now playing over two columns and the folder in the third; on the big panel the state word sits in the folder tile.</summary>
    private Grid Data()
    {
        var data = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,*"), ColumnSpacing = Gaps.Tile };
        data.Children.Add(StatTile.Tile("NOW PLAYING", _track));

        Control folderValue = _folder;

        if (_big)
        {
            _state.VerticalAlignment = VerticalAlignment.Center;
            _state.FontSize = TypeScale.MetaSmall;
            folderValue = new DockPanel { Children = { Docked(_state, Dock.Right), _folder } };
        }

        var folder = StatTile.Tile("FOLDER", folderValue);
        Grid.SetColumn(folder, 1);
        data.Children.Add(folder);

        return data;
    }

    private Grid Seek()
    {
        var seek = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 12 };
        seek.Children.Add(_elapsed);
        Grid.SetColumn(_bar, 1);
        seek.Children.Add(_bar);
        Grid.SetColumn(_length, 2);
        seek.Children.Add(_length);

        return seek;
    }

    private static Grid Columns(IReadOnlyList<Control> cells)
    {
        var grid = new Grid { ColumnSpacing = Gaps.Tile };

        for (var i = 0; i < cells.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(cells[i], i);
            grid.Children.Add(cells[i]);
        }

        return grid;
    }

    /// <summary>The line naming how to come back here: the phrase, and the key when one is bound.</summary>
    private void DrawHint()
    {
        _hint.Children.Clear();
        _hint.IsVisible = !_big;

        var key = _host.Settings.Current.Hotkeys.AudioPlayer;
        var bound = key is { Length: > 0 };

        if (!bound)
        {
            _hint.Children.Add(Typeset.Prose("Say “audio player” to come back here from anywhere.", 14, ThemeManager.GreyKey, vertical: VerticalAlignment.Center));
            return;
        }

        _hint.Children.Add(Typeset.Prose("Say “audio player”, or press", 14, ThemeManager.GreyKey, vertical: VerticalAlignment.Center));
        _hint.Children.Add(Typeset.Mono(key!.ToUpperInvariant(), 14, ThemeManager.WhiteKey, vertical: VerticalAlignment.Center, margin: new Thickness(6, 0, 0, 0)));
        _hint.Children.Add(Typeset.Prose(", to come back here from anywhere.", 14, ThemeManager.GreyKey, vertical: VerticalAlignment.Center));
    }

    // ----- state

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
        else if (change.Key == AudioCapability.AudioPlayerHotkeyKey)
        {
            Dispatcher.UIThread.Post(DrawHint);
        }
    }

    /// <summary>Reads the track's position and length and redraws the bar and times; a held drag keeps its own.</summary>
    internal void Poll()
    {
        var on = _model.AudioPlayer.Enabled;
        TrackProgress? now = on ? _host.Progress() : null;
        var timeInk = on ? ThemeManager.WhiteKey : ThemeManager.Grey2Key;

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

    /// <summary>Reads the position from the headset's tick; true when the bar or the times drawn changed since the last tick.</summary>
    internal bool Tick()
    {
        if (_mini)
        {
            return false;
        }

        Poll();

        var drawn = string.Join('|', _elapsed.Text, _length.Text, _bar.Position, _bar.Active);

        if (drawn == _ticked)
        {
            return false;
        }

        _ticked = drawn;
        return true;
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
        if (!_big && Target(fraction) is { } to)
        {
            _host.Seek(to);
        }

        Poll();
    }

    /// <summary>Redraws from the model's readout and the music channel's setting.</summary>
    internal void Refresh()
    {
        var read = _model.AudioPlayer;
        var mix = _host.Settings.Current.Audio.Music;
        var live = read.Enabled;

        _state.Text = read.StateWord.ToUpperInvariant();
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

    // ----- small helpers

    private static T Docked<T>(T control, Dock dock)
        where T : Control
    {
        DockPanel.SetDock(control, dock);

        return control;
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
