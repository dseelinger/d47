using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;

namespace D47.App.Panel;

/// <summary>
/// The <em>d47 is composing</em> indicator (asked for 2026-08-22).
/// <para>
/// <b>What it is for.</b> A beat fires the moment the Commander arrives, and the line they hear is
/// deliberately not immediate: <c>AdventureCallout.Settle</c> holds it twenty seconds so the prose
/// is not read out over the jump that reached it, and the model then spends up to three more
/// rewriting it in the core's voice. Reported as <em>"it can take a while after triggering a
/// trigger for me to hear anything"</em> — with the real question being not <em>where is the
/// line</em> but <em>did I do the thing at all</em>. <see cref="D47.Core.Adventures.AdventureAcks"/>
/// answers that out loud at once; this answers it on the panel, and keeps answering it for as long
/// as the wait lasts.
/// </para>
/// <para>
/// <b>Driven, not self-winding.</b> <see cref="Beat"/> is called from the tick — the same 10 Hz
/// both surfaces already run on, by the same route <c>TickClocks</c> takes — rather than from a
/// <c>DispatcherTimer</c> of its own. That is not tidiness: the headset's copy of this panel is
/// rasterised offscreen and redrawn only when something marks it dirty, so an animation that moved
/// on a private timer would move in a widget tree nobody was converting to pixels. Beat returns
/// whether the frame actually changed, which is what the VR surface sets its dirty flag from.
/// </para>
/// </summary>
public sealed class AdventureThinking : UserControl
{
    /// <summary>How many ticks one dot holds for. Three at 10 Hz is a step every 300 ms.</summary>
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

    /// <summary>
    /// One tick. Returns whether the drawing changed, so a surface that only redraws when something
    /// moved has something honest to ask.
    /// </summary>
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

    /// <summary>
    /// Which dot is lit. There is a fourth state with none lit, so the cycle reads as a pulse
    /// travelling and then starting again rather than as one dot jumping back to the left.
    /// </summary>
    private void Step(int step)
    {
        _step = step;

        for (var index = 0; index < Dots; index++)
        {
            _dots[index].Opacity = index == step ? Lit : Dim;
        }
    }
}
