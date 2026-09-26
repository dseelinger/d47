using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Settings;
using Xunit;

namespace D47.App.Tests;

/// <summary>A confirm dialog marks its confirm tile destructive only when the caller says so (#509).</summary>
public sealed class AConfirmIsRedOnlyWhenAskedTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheConfirmTileIsDestructiveOnlyWhenAsked(bool destructive)
    {
        using var look = AppLook.Put();
        var ask = new ConfirmWindow("Title", "Question?", "Yes", "No", destructive);
        ask.Show();
        Dispatcher.UIThread.RunJobs();

        var buttons = ask.GetVisualDescendants().OfType<Button>().ToList();
        var confirm = buttons.Single(button => button.Content as string == "Yes");
        var decline = buttons.Single(button => button.Content as string == "No");

        Assert.Equal(destructive, confirm.Classes.Contains(SettingsView.DestructiveClass));
        Assert.DoesNotContain(SettingsView.DestructiveClass, decline.Classes);

        ask.Close();
    }

    /// <summary>A plain and a destructive tile at rest and hovered, for a human to look at.</summary>
    [AvaloniaFact]
    public void PlainAndDestructiveTilesAreCaptured()
    {
        var rows = new StackPanel { Spacing = 8, Margin = new Thickness(24) };

        foreach (var hovered in new[] { false, true })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            foreach (var weight in new[] { "", SettingsView.DestructiveClass })
            {
                var tile = new Button { Content = weight.Length == 0 ? "Keep it" : "Delete key" };
                if (weight.Length > 0)
                {
                    tile.Classes.Add(weight);
                }

                if (hovered)
                {
                    ((IPseudoClasses)tile.Classes).Set(":pointerover", true);
                }

                row.Children.Add(tile);
            }

            rows.Children.Add(row);
        }

        Assert.True(File.Exists(AppLook.Capture(rows, "destructive-tiles.png", width: 360, height: 150)));
    }
}
