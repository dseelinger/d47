using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Panel;

/// <summary>The d47 is composing indicator (asked for 2026-08-22).</summary>
public sealed class AdventureThinking : UserControl
{
    /// <summary>How many ticks one dot holds for.</summary>
    private const int TicksPerStep = 3;

    private const int Dots = 3;

    private readonly Ellipse[] _dots = new Ellipse[Dots];

    private int _ticks;
    private int _step = -1;

    public AdventureThinking(string label = "Composing")
    {
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var word = new TextBlock
        {
            Text = label,
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
        };

        AdventuresPage.Themed(word, TextBlock.ForegroundProperty, ThemeManager.AccentKey);
        row.Children.Add(word);

        for (var index = 0; index < Dots; index++)
        {
            var dot = new Ellipse
            {
                Width = 5,
                Height = 5,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = Dim,
            };

            AdventuresPage.Themed(dot, Shape.FillProperty, ThemeManager.AccentKey);
            _dots[index] = dot;
            row.Children.Add(dot);
        }

        Content = row;
        Margin = new Thickness(0, 4, 0, 4);
        Step(0);
    }

    private const double Dim = 0.25;

    private const double Lit = 1.0;

    /// <summary>One tick.</summary>
    public bool Beat()
    {
        if (++_ticks < TicksPerStep)
        {
            return false;
        }

        _ticks = 0;
        Step((_step + 1) % (Dots + 1));
        return true;
    }

    /// <summary>Which dot is lit.</summary>
    private void Step(int step)
    {
        _step = step;

        for (var index = 0; index < Dots; index++)
        {
            _dots[index].Opacity = index == step ? Lit : Dim;
        }
    }
}
