using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Windowing;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A minimised dialog is owned and has no taskbar button, and its owner stays disabled behind it (#507).
/// <see cref="Window.CanMinimize"/> clears the minimise box, which also closes the system-menu and
/// Win+Down routes.
/// </summary>
public class ADialogCannotBeMinimisedTests
{
    private static string[] CaptionButtonNames(Window window) =>
        [.. window.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Classes.Contains("caption-button"))
            .Select(b => AutomationProperties.GetName(b) ?? "")];

    [AvaloniaFact]
    public void ADialogShownOverItsOwnerHasNoMinimiseBoxAndNoMinimiseButton()
    {
        var owner = new Window { Content = new TextBlock() };
        owner.Show();

        var dialog = new Window { Content = new TextBlock(), Title = "A dialog", CanResize = true };
        _ = dialog.Over(owner);

        Assert.False(dialog.CanMinimize);
        Assert.DoesNotContain("Minimize", CaptionButtonNames(dialog));
        Assert.Contains("Close", CaptionButtonNames(dialog));

        dialog.Close();
        owner.Close();
    }

    [Trait("Category", "Integration")]
    [AvaloniaFact]
    public void TheMainWindowStillMinimises()
    {
        new D47.App.Theming.ThemeManager(Application.Current!, NullLogger<D47.App.Theming.ThemeManager>.Instance)
            .Apply(TestSurface.Settings().Current.Ui.Theme);

        var window = new MainWindow(host: null);
        window.Show();

        Assert.True(window.CanMinimize);
        Assert.Contains("Minimize", CaptionButtonNames(window));

        window.Close();
    }
}
