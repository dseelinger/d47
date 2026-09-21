using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Normal, Destructive and Apply draw their theme's border; Primary and Quiet draw none (#363).</summary>
public class ButtonsDrawTheirBorderOutsidePrimaryTests
{
    private static Window Open(Button button)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        // Not in HeadlessApp — it stands in for App.axaml but leaves the control kit out — so the
        // Button theme this test exercises has to come from the real file.
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });

        var window = new Window { Content = button, Width = 400, Height = 200 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static ChamferedBorder Shape(Button button) =>
        button.GetVisualDescendants().OfType<ChamferedBorder>().Single(c => c.Name == "Shape");

    [AvaloniaFact]
    public void ANormalButtonDrawsItsRuleBorder()
    {
        var button = new Button { Content = "NORMAL" };
        Open(button);

        Assert.True(Shape(button).BorderThickness.Left > 0);
    }

    [AvaloniaFact]
    public void ADestructiveButtonDrawsItsDangerBorder()
    {
        var button = new Button { Content = "DESTRUCTIVE" };
        button.Classes.Add("destructive");
        Open(button);

        Assert.True(Shape(button).BorderThickness.Left > 0);
    }

    [AvaloniaFact]
    public void APrimaryButtonDrawsNoBorder()
    {
        var button = new Button { Content = "PRIMARY" };
        button.Classes.Add("primary");
        Open(button);

        Assert.Equal(0, Shape(button).BorderThickness.Left);
    }

    [AvaloniaFact]
    public void AQuietButtonDrawsNoBorder()
    {
        var button = new Button { Content = "QUIET" };
        button.Classes.Add("quiet");
        Open(button);

        Assert.Equal(0, Shape(button).BorderThickness.Left);
    }
}
