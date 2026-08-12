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

        var errors = new List<string>();
        if (_host.StartupError is { } startupError)
        {
            errors.Add(startupError);
        }

        // The Phase 1 claim is that a request produces a real tool call that runs and returns
        // a result. These are that call, twice — dispatched by name through the registry,
        // validated against each tool's declared schema, and rendered verbatim.
        var status = await _host.Capabilities.InvokeAsync("get_app_status", ToolArguments.Empty);
        var location = await _host.Capabilities.InvokeAsync("get_location", ToolArguments.Empty);

        StatusText.Text = status.Content + Environment.NewLine + Environment.NewLine + location.Content;

        errors.AddRange(new[] { status, location }.Where(r => r.IsError).Select(r => r.Content));

        if (errors.Count > 0)
        {
            ErrorText.Text = string.Join(Environment.NewLine, errors);
            ErrorBanner.IsVisible = true;
        }
    }
}
