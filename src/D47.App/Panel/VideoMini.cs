using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using D47.App.Controls;
using D47.App.Media;
using D47.Core.Vr;

namespace D47.App.Panel;

/// <summary>
/// Media › Movies on the mini panel: the picture beside the avatar while the video plays there, an inline notice when it
/// stopped, and a line saying where it plays otherwise. No buttons. In the headset the picture is the video quad and
/// the caption band carries the captions; on the desktop strip the captions are drawn over the picture.
/// </summary>
public sealed class VideoMini : UserControl
{
    /// <summary>What the Commander says to try a stopped video again.</summary>
    public const string TryAgain = "continue watching";

    public const string NothingPlaying = "No video is playing.";

    /// <summary>The picture's size on the mini panel at its default 792×280.</summary>
    public const double PictureWidth = 498;

    public const double PictureHeight = 280;

    private static readonly TimeSpan PollEvery = TimeSpan.FromMilliseconds(15);

    private readonly WatchHost _host;
    private readonly bool _headset;
    private readonly DispatcherTimer _poll = new() { Interval = PollEvery };
    private readonly Image _picture = new() { Stretch = Stretch.Uniform, Name = VideoPlayerPage.PictureName };
    private readonly Border _caption;
    private readonly TextBlock _captionText;
    private readonly Border _frame;
    private readonly Viewbox _fit;
    private readonly Notice _stopped = new(NoticeLevel.Error, inline: true) { IsVisible = false };
    private readonly TextBlock _note = AdventuresPage.Muted(NothingPlaying);
    private readonly StackPanel _words;

    private WriteableBitmap? _bitmap;
    private VideoFrame? _shown;
    private string? _ticked;

    /// <param name="headset">Whether this is the headset's mini panel.</param>
    public VideoMini(WatchHost host, bool headset)
    {
        _host = host;
        _headset = headset;

        (_caption, _captionText) = FrameBitmap.Caption(16, new Thickness(14, 6), new Thickness(12));

        var picture = new Grid { Children = { _picture, _caption } };
        _frame = new Border
        {
            Background = Brushes.Black,
            Width = PictureWidth,
            Height = PictureHeight,
            Child = picture,
        };
        AutomationProperties.SetName(_frame, "The video picture");

        // The largest 16:9 picture the text area holds, centred.
        _fit = new Viewbox { Stretch = Stretch.Uniform, Child = _frame };

        _words = new StackPanel { Spacing = 14, Margin = new Thickness(24), Children = { _stopped, _note } };

        Content = new Grid { Children = { _fit, _words } };

        _poll.Tick += (_, _) => Refresh();

        Refresh();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _host.Changed += OnChanged;

        if (!_headset)
        {
            _poll.Start();
        }

        Refresh();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _host.Changed -= OnChanged;
        _poll.Stop();
    }

    /// <summary>Refreshes from the headset's tick; true when anything drawn changed since the last tick.</summary>
    internal bool Tick()
    {
        Refresh();

        var drawn = string.Join('|', _fit.IsVisible, _bitmap?.PixelSize, _stopped.IsVisible, _stopped.Text, _note.Text);

        if (drawn == _ticked)
        {
            return false;
        }

        _ticked = drawn;
        return true;
    }

    private void OnChanged() => Dispatcher.UIThread.Post(Refresh);

    private void Refresh()
    {
        var video = _host.Video;
        var loaded = _host.Loaded;
        var failure = loaded ? video.Failure : null;
        var showing = loaded && failure is null && _host.PlaysOn == VideoScreen.MiniPanel;

        _fit.IsVisible = showing;
        _words.IsVisible = !showing;

        if (showing)
        {
            ShowFrame(video.Frame);
            DrawCaption(video.Position);
            return;
        }

        _stopped.IsVisible = failure is not null;
        _note.IsVisible = failure is null;

        if (failure is { } failed && _host.Current is { } title)
        {
            _stopped.Label = failed == VideoFailure.Unreachable ? VideoPlayerPage.StoppedLabel : VideoPlayerPage.NoDecoderLabel;
            _stopped.Text = failed == VideoFailure.Unreachable
                ? $"{VideoPlayerPage.SourceName(title)} stopped answering. Say “{TryAgain}” to try again."
                : "Windows has no decoder for this video. MP4 files with H.264 video and AAC sound play.";
            return;
        }

        _note.Text = loaded ? VideoPlayerPage.Elsewhere(_host.PlaysOn) : NothingPlaying;
    }

    private void DrawCaption(TimeSpan position)
    {
        var cue = !_headset && _host.CaptionsOn ? SubtitleCues.At(_host.Cues, position) : null;

        _captionText.Text = cue?.Text ?? string.Empty;
        _caption.IsVisible = cue is not null;
    }

    /// <summary>Draws the newest picture; in the headset only the first of each size, since the video quad covers it.</summary>
    private void ShowFrame(VideoFrame? frame)
    {
        if (frame is null
            || ReferenceEquals(frame, _shown)
            || (_headset && _bitmap is { } drawn && drawn.PixelSize == new PixelSize(frame.Width, frame.Height)))
        {
            return;
        }

        _shown = frame;
        var bitmap = FrameBitmap.Draw(frame, _bitmap);

        if (!ReferenceEquals(bitmap, _bitmap))
        {
            _bitmap = bitmap;
            _picture.Source = bitmap;
        }

        _picture.InvalidateVisual();
    }
}
