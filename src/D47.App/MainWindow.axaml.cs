using Avalonia.Controls;
using Avalonia.Interactivity;
using D47.Core.Capabilities;

namespace D47.App;

public partial class MainWindow : Window
{
    private readonly AppHost? _host;

    public MainWindow() : this(host: null)
    {
    }

    public MainWindow(AppHost? host)
    {
        _host = host;
        InitializeComponent();
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (_host is null)
        {
            StatusText.Text = "No host: the window is running under the designer.";
            return;
        }

        VersionLine.Text = $"build {_host.Version}";

        if (_host.StartupError is { } error)
        {
            ErrorText.Text = error;
            ErrorBanner.IsVisible = true;
        }

        // The Phase 1 claim is that a request produces a real tool call that runs and returns
        // a result. This is that call — dispatched by name through the registry, validated
        // against the declared schema, and rendered verbatim.
        var result = await _host.Capabilities.InvokeAsync("get_app_status", ToolArguments.Empty);

        StatusText.Text = result.Content;

        if (result.IsError)
        {
            ErrorText.Text = result.Content;
            ErrorBanner.IsVisible = true;
        }
    }
}
