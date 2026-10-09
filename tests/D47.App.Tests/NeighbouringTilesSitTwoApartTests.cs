using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Two tiles side by side sit <see cref="Gaps.Tile"/> apart (#502).</summary>
public class NeighbouringTilesSitTwoApartTests
{
    private static Window Open(Control content)
    {
        var window = new Window { Width = 900, Height = 700, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static double Between(Visual left, Visual right, Visual root)
    {
        var leftEdge = left.TranslatePoint(new Point(left.Bounds.Width, 0), root)!.Value.X;
        var rightEdge = right.TranslatePoint(default, root)!.Value.X;

        return rightEdge - leftEdge;
    }

    [AvaloniaFact]
    public void AcceptAndDeclineSitTwoApart()
    {
        var row = ProposalActions.Build(() => { }, () => { }, "Waiting");
        var window = Open(row);

        var buttons = row.GetVisualDescendants().OfType<Button>().ToList();
        var accept = buttons.Single(b => Equals(b.Content, "Accept"));
        var decline = buttons.Single(b => Equals(b.Content, "Decline"));

        Assert.Equal(Gaps.Tile, Between(accept, decline, window));

        window.Close();
    }

    [AvaloniaFact]
    public void SendSitsTwoFromTheMessageBox()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var window = Open(panel);

        var send = panel.FindControl<Button>("AskButton")!;
        var row = panel.FindControl<DockPanel>("AskRow")!;
        var box = row.Children.OfType<TextBox>().Single();

        Assert.Equal(Gaps.Tile, Between(box, send, window));

        window.Close();
    }

    [AvaloniaFact]
    public void TileRowsRenderTwoApart()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });

        var content = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(16),
            Children =
            {
                ProposalActions.Build(() => { }, () => { }, "Waiting for your answer"),
                RoutingKit.Actions(new Button { Content = "Plan" }, new Button { Content = "Copy" }, new Button { Content = "Clear" }),
            },
        };
        var window = Open(content);
        window.Width = 600;
        window.Height = 220;
        Dispatcher.UIThread.RunJobs();

        window.CaptureRenderedFrame()!.SaveCapture("tile-rows-two-apart.png");

        window.Close();
    }
}
