using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>What the game servers are doing.</summary>
public enum ServerState
{
    Online,
    Maintenance,
    Offline,
    Unknown,
}

/// <summary>
/// A read-only "■ SERVERS &lt;STATE&gt;" readout for the main window's title bar. Hidden until
/// <see cref="Show"/> supplies a state; call it on the UI thread.
/// </summary>
public sealed class ServerStatusReadout : StackPanel
{
    public const double Gap = 16;

    private readonly Rectangle _mark = new() { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _word = Label();

    public ServerStatusReadout()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 6;
        Margin = new Thickness(0, 0, Gap, 0);
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;
        IsVisible = false;

        var servers = Label();
        servers.Text = "SERVERS";
        servers.Bind(TextBlock.ForegroundProperty, servers.GetResourceObservable(ThemeManager.GreyKey));

        Children.Add(_mark);
        Children.Add(servers);
        Children.Add(_word);
    }

    public void Show(ServerState? state)
    {
        IsVisible = state is not null;

        if (state is not { } shown)
        {
            return;
        }

        var (word, key) = shown switch
        {
            ServerState.Online => ("ONLINE", ThemeManager.BlueKey),
            ServerState.Maintenance => ("MAINTENANCE", ThemeManager.WarnKey),
            ServerState.Offline => ("OFFLINE", ThemeManager.RedKey),
            _ => ("UNKNOWN", ThemeManager.GreyKey),
        };

        _word.Text = word;
        _word.Bind(TextBlock.ForegroundProperty, _word.GetResourceObservable(key));
        _mark.Bind(Shape.FillProperty, _mark.GetResourceObservable(key));
    }

    private static TextBlock Label() => new()
    {
        FontFamily = new FontFamily(Fonts.ChromeFamily),
        FontSize = TypeScale.Small,
        FontWeight = FontWeight.SemiBold,
        LetterSpacing = TypeScale.Small * Fonts.ChromeTracking,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
