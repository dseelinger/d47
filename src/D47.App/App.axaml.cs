using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace D47.App;

public partial class App(AppHost? host) : Application
{
    /// <summary>The designer constructs the application with no host.</summary>
    public App() : this(host: null)
    {
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(host);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
