using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using D47.App.Panel;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

public class APickedCommanderRebuildsTheOpenPageTests
{
    [AvaloniaFact]
    public void ForgettingEveryPageDrawsTheShownOneAgain()
    {
        var nav = new PanelNavigator();
        nav.Register(PanelTab.Transcript, new NavCrumb("root", "Root"));

        var builds = 0;
        var view = new DrillView(nav, PanelTab.Transcript, _ =>
        {
            builds++;
            return new TextBlock();
        });

        var window = new Window { Content = view, Width = 800, Height = 600 };
        window.Show();

        Assert.Equal(1, builds);

        view.ForgetAll();

        Assert.Equal(2, builds);
        window.Close();
    }
}
