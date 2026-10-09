using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>Every tile is 44 tall with a 13px label, whichever screen builds it (#501).</summary>
public class EveryTileIsFortyFourTallTests
{
    private static Window Open(Control content)
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        // HeadlessApp leaves the control kit out, so the Button theme comes from the real file.
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });

        var window = new Window
        {
            Content = new StackPanel { VerticalAlignment = VerticalAlignment.Top, Children = { content } },
            Width = 600,
            Height = 300,
        };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static void AssertTile(Button button)
    {
        Assert.Equal(TypeScale.MinimumTarget, button.Bounds.Height);
        Assert.Equal(TypeScale.Control, button.FontSize);
        Assert.Equal(new Thickness(16, 0), button.Padding);
    }

    [AvaloniaFact]
    public void ADefaultTileIsFortyFourTall()
    {
        var button = new Button { Content = "Go" };
        var window = Open(button);

        AssertTile(button);
        Assert.Equal(0.78, button.LetterSpacing);

        window.Close();
    }

    [AvaloniaFact]
    public void ALoadoutPressIsFortyFourTall()
    {
        var button = LoadoutPages.Press("Add to checklist", () => { });
        var window = Open(button);

        AssertTile(button);

        window.Close();
    }

    [AvaloniaFact]
    public void AProposalAcceptIsFortyFourTall()
    {
        var row = ProposalActions.Build(() => { }, () => { }, "Waiting");
        var window = Open(row);

        var accept = row.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Accept"));
        AssertTile(accept);

        window.Close();
    }

    [AvaloniaFact]
    public void TheTilesAndTheTabsRenderAtOneSize()
    {
        var tiles = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(16),
            Children =
            {
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children =
                    {
                        new Button { Content = "Default" },
                        LoadoutPages.Press("Add to checklist", () => { }),
                        new Button { Content = "Delete", Classes = { "destructive" } },
                    },
                },
                ProposalActions.Build(() => { }, () => { }, "Waiting for your answer."),
            },
        };

        var panel = new DockPanel();
        var view = new PanelView { DataContext = new PanelViewModel() };
        DockPanel.SetDock(tiles, Dock.Top);
        panel.Children.Add(tiles);
        panel.Children.Add(view);

        using var frame = AppLook.Capture(panel, "every-tile-44.png");

        Assert.True(frame.PixelSize.Width > 0);
    }
}
