using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using D47.App.Theming;
using D47.Core.Hotas;

namespace D47.App.Controls;

/// <summary>
/// <em>Press the button you want</em> (list.md Phase 53).
/// <para>
/// The same gesture Phase 4 set for a key — press the thing and d47 works out what it was —
/// pointed at a stick. It reuses the reader the tick loop already polls and the clock the switch
/// walk already carries, so the window is a surface over <see cref="ButtonCapture"/> and holds no
/// state of its own beyond what is on screen.
/// </para>
/// </summary>
public sealed class ButtonBindWindow : Window
{
    private readonly IHotasReader _reader;
    private readonly Func<DateTimeOffset> _now;
    private readonly ButtonCapture _capture = new();
    private readonly DispatcherTimer _timer;
    private readonly TextBlock _says;
    private readonly Button _save;

    private DateTimeOffset _opened;
    private HotasButton? _caught;

    /// <summary>What was bound, or null if the window closed without one.</summary>
    public HotasButton? Result { get; private set; }

    public ButtonBindWindow(IHotasReader reader, Func<DateTimeOffset> now)
    {
        _reader = reader;
        _now = now;

        Title = "Bind push-to-talk";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;

        _says = new TextBlock
        {
            Text = "Press and release the button you want to talk with.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = TypeScale.Body,
        };

        Themed(_says, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        _save = new Button { Content = "Save", IsEnabled = false, Padding = new Thickness(14, 4) };
        var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 4) };

        _save.Click += (_, _) =>
        {
            Result = _caught;
            Close();
        };

        cancel.Click += (_, _) => Close();

        var note = new TextBlock
        {
            Text =
                "It has to be a button that springs back. A switch that stays where you put it is "
                + "assigned on the switch panel instead — held down, it would hold the microphone open.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = TypeScale.Small,
        };

        Themed(note, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var body = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(18),
            Children =
            {
                _says,
                note,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { _save, cancel },
                },
            },
        };

        Content = body;
        Themed(this, BackgroundProperty, ThemeManager.SurfaceKey);

        // The same 10 Hz the tick loop samples push-to-talk at. A capture that sampled faster
        // would be answering a question the runtime cannot ask.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += (_, _) => Sample();

        Opened += (_, _) =>
        {
            _opened = _now();
            _timer.Start();
        };

        Closed += (_, _) => _timer.Stop();
    }

    private void Sample()
    {
        if (_reader.Unavailable is { Length: > 0 } why)
        {
            _says.Text = why;
            _timer.Stop();
            return;
        }

        // Nothing is read until the device list stops changing: a single enumeration at startup
        // reported three of six devices on the bench (Phase 21, finding 1).
        if (!_reader.IsSettled)
        {
            _says.Text = "Looking for your controllers…";
            return;
        }

        var result = _capture.Poll(_reader.Poll(), _now() - _opened);

        _says.Text = result.Says;

        if (result.Stage == ButtonCaptureStage.Captured)
        {
            _caught = result.Binding;
            _save.IsEnabled = true;
            _timer.Stop();
        }
        else if (result.Stage == ButtonCaptureStage.Declined)
        {
            _timer.Stop();
        }
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, Application.Current!.Resources.GetResourceObservable(key));
}
