using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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

    private readonly TextBlock _state = new() { Name = StateName };
    private readonly TextBlock _track = new() { Name = TrackName };
    private readonly TextBlock _say = new() { Name = SayName };

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

        Child = new StackPanel { Spacing = 2, Children = { head, _track, _say } };
    }

    public void Update(AudioPlayerReadout read)
    {
        _state.Text = read.StateWord.ToUpperInvariant();
        ThemeInk.Bind(_state, TextBlock.ForegroundProperty, read.Playing ? ThemeManager.CyanKey : ThemeManager.GreyKey);

        _track.Text = read.Track;
        ThemeInk.Bind(_track, TextBlock.ForegroundProperty, read.Playing ? ThemeManager.WhiteKey : ThemeManager.GreyKey);

        var say = $"say “{(read.Playing ? "pause" : "play")}”, “next track” or “close the audio player”";
        _say.Text = read.Folder == AudioPlayerReadout.Nothing ? say : $"{read.Folder} · {say}";
    }
}
