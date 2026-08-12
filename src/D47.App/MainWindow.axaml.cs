using System.Reflection;
using Avalonia.Controls;

namespace D47.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Shown so a published build can be identified without unpacking it.
        var version = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        BuildLine.Text = $"build {version ?? "unknown"}";
    }
}
