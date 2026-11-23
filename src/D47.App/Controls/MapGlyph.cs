using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Media;
using D47.App.Panel;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>The button that opens Astrometrics › Galaxy map on a system: a <c>D47.GlyphButton</c> whose face is four corner legs round a centre point.</summary>
public static class MapGlyph
{
    public const string Name = RoutePlanResultPage.ShowOnGalaxyMapWords;

    /// <summary>The hover label in the window; the headset's panel has none.</summary>
    public const string Tip = RoutePlanResultPage.ShowOnGalaxyMapWords;

    /// <summary>The face's side, each corner leg's length and thickness, and the centre square's side.</summary>
    public const double Size = 22;
    public const double Leg = 6;
    public const double Stroke = 1.5;
    public const double Centre = 3;

    /// <summary>A map glyph that calls <paramref name="show"/> and does not reach the control it sits in.</summary>
    public static Button For(Action show)
    {
        var button = new Button
        {
            Theme = Application.Current?.FindResource("D47.GlyphButton") as ControlTheme,
            Width = TypeScale.MinimumTarget,
            Height = TypeScale.MinimumTarget,
            VerticalAlignment = VerticalAlignment.Center,
        };

        button.Content = Face(button);
        AutomationProperties.SetName(button, Name);
        GlyphLabel.Tip(button, Tip);

        button.Click += (_, e) =>
        {
            e.Handled = true;
            show();
        };

        return button;
    }

    private static Canvas Face(Button button)
    {
        const double edge = Stroke / 2;
        const double far = Size - edge;

        var legs = new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(FormattableString.Invariant(
                $"M{edge},{Leg} V{edge} H{Leg} M{Size - Leg},{edge} H{far} V{Leg} M{far},{Size - Leg} V{far} H{Size - Leg} M{Leg},{far} H{edge} V{Size - Leg}")),
            StrokeThickness = Stroke,
        };

        var centre = new Rectangle { Width = Centre, Height = Centre };

        legs.Bind(Shape.StrokeProperty, button.GetObservable(TemplatedControl.ForegroundProperty));
        centre.Bind(Shape.FillProperty, button.GetObservable(TemplatedControl.ForegroundProperty));
        Canvas.SetLeft(centre, (Size - Centre) / 2);
        Canvas.SetTop(centre, (Size - Centre) / 2);

        return new Canvas
        {
            Width = Size,
            Height = Size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { legs, centre },
        };
    }
}
