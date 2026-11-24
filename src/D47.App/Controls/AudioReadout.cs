using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Audio;

namespace D47.App.Controls;

/// <summary>The mini panels' read-out while the audio player is open: state, track, folder and what to say.</summary>
public sealed class AudioReadout : Border
{
    public const string TitleName = "AudioReadoutTitle";

    public const string StateName = "AudioReadoutState";

    public const string TrackName = "AudioReadoutTrack";

    public const string SayName = "AudioReadoutSay";

    public const string ElapsedName = "AudioReadoutElapsed";

    public const string LengthName = "AudioReadoutLength";

    public const string BarName = "AudioReadoutBar";

    private readonly TextBlock _state = new() { Name = StateName };
    private readonly TextBlock _track = new() { Name = TrackName };
    private readonly TextBlock _say = new() { Name = SayName };
    private readonly TextBlock _elapsed = new() { Name = ElapsedName };
    private readonly TextBlock _length = new() { Name = LengthName };
    private readonly Border _fill = new() { Height = 4, VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _rail = new() { Height = 4, VerticalAlignment = VerticalAlignment.Center };
    private readonly Face _face;
    private readonly Grid _progressRow;
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(1) };
    private Func<TrackProgress?>? _progress;
    private bool _enabled;
    private double _position;
    private string? _ticked;

    public AudioReadout()
    {
        BorderThickness = new Thickness(0, 1, 0, 0);
        Padding = new Thickness(0, 6, 0, 0);
        Margin = new Thickness(0, 8, 0, 0);
        ThemeInk.Bind(this, BorderBrushProperty, ThemeManager.Line2Key);

        var title = new TextBlock
        {
            Name = TitleName,
            Text = "AUDIO PLAYER",
            FontFamily = new FontFamily(Fonts.ChromeFamily),
            FontSize = TypeScale.MetaSmall,
            LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking,
        };
        ThemeInk.Bind(title, TextBlock.ForegroundProperty, ThemeManager.AKey);

        _state.FontFamily = new FontFamily(Fonts.ChromeFamily);
        _state.FontSize = TypeScale.MetaSmall;
        _state.LetterSpacing = TypeScale.MetaSmall * Fonts.ChromeTracking;
        _state.HorizontalAlignment = HorizontalAlignment.Right;

        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(title);
        Grid.SetColumn(_state, 1);
        head.Children.Add(_state);

        _track.FontFamily = new FontFamily(Fonts.ChromeFamily);
        _track.FontSize = TypeScale.Description;
        _track.TextTrimming = TextTrimming.CharacterEllipsis;

        _say.FontSize = TypeScale.Meta;
        _say.TextWrapping = TextWrapping.Wrap;
        _say.MaxLines = 2;
        _say.TextTrimming = TextTrimming.CharacterEllipsis;
        ThemeInk.Bind(_say, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        foreach (var time in new[] { _elapsed, _length })
        {
            time.FontFamily = new FontFamily(Fonts.MonoFamily);
            time.FontSize = TypeScale.MetaSmall;
            time.VerticalAlignment = VerticalAlignment.Center;
            ThemeInk.Bind(time, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        }

        ThemeInk.Bind(_rail, BackgroundProperty, ThemeManager.Line2Key);
        ThemeInk.Bind(_fill, BackgroundProperty, ThemeManager.AKey);
        _face = new Face(this) { Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center, Height = 4 };
        _face.Children.Add(_rail);
        _face.Children.Add(_fill);

        _progressRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(0, 2) };
        _progressRow.Children.Add(_elapsed);
        Grid.SetColumn(_face, 1);
        _progressRow.Children.Add(_face);
        Grid.SetColumn(_length, 2);
        _progressRow.Children.Add(_length);

        _poll.Tick += (_, _) => Poll();
        DetachedFromVisualTree += (_, _) => _poll.Stop();

        Child = new StackPanel { Spacing = 2, Children = { head, _track, _progressRow, _say } };
    }

    internal bool Polling => _poll.IsEnabled;

    /// <summary>Reads the position once a second from <paramref name="progress"/>; null stops reading.</summary>
    public void Watch(Func<TrackProgress?>? progress)
    {
        _progress = progress;

        if (progress is null)
        {
            _poll.Stop();
            return;
        }

        Poll();
        _poll.Start();
    }

    internal void Poll()
    {
        TrackProgress? now = _enabled ? _progress?.Invoke() : null;
        _elapsed.Text = AudioPlayerReadout.Clock(now?.Elapsed);
        _length.Text = AudioPlayerReadout.Clock(now?.Length);
        _position = now is { Length: { } length } && length > TimeSpan.Zero ? Math.Clamp(now.Value.Elapsed / length, 0, 1) : 0;
        ThemeInk.Bind(_fill, BackgroundProperty, _enabled ? ThemeManager.AKey : ThemeManager.Grey2Key);
        _face.InvalidateArrange();
    }

    /// <summary>Reads the position from the headset's tick; true when the times or the bar drawn changed since the last tick.</summary>
    internal bool Tick()
    {
        if (_progress is null)
        {
            return false;
        }

        Poll();

        var drawn = string.Join('|', _elapsed.Text, _length.Text, _position, _enabled, _state.Text, _track.Text);

        if (drawn == _ticked)
        {
            return false;
        }

        _ticked = drawn;
        return true;
    }

    public void Update(AudioPlayerReadout read)
    {
        _enabled = read.Enabled;

        _state.Text = read.StateWord.ToUpperInvariant();
        ThemeInk.Bind(_state, TextBlock.ForegroundProperty, read.Playing ? ThemeManager.CyanKey : ThemeManager.GreyKey);

        _track.Text = read.Track;
        ThemeInk.Bind(_track, TextBlock.ForegroundProperty, read.Playing ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

        var say = $"say “{(read.Playing ? "pause" : "play")}”, “next track” or “close the audio player”";
        _say.Text = read.Folder == AudioPlayerReadout.Nothing ? say : $"{read.Folder} · {say}";

        if (_progress is not null)
        {
            Poll();
        }
    }

    /// <summary>Lays the rail across its width and the fill at the position of that width, in the same arrange.</summary>
    private sealed class Face(AudioReadout readout) : Avalonia.Controls.Panel
    {
        protected override Size ArrangeOverride(Size finalSize)
        {
            readout._rail.Arrange(new Rect(finalSize));
            readout._fill.Arrange(new Rect(0, 0, readout._position * finalSize.Width, finalSize.Height));

            return finalSize;
        }
    }
}
