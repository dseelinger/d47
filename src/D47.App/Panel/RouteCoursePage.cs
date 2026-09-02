using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Capabilities;

namespace D47.App.Panel;

/// <summary>
/// Getting a system name into the game (Phase 37, "Course").
/// <para>
/// <b>The clipboard is the part that always works</b>, and the order on this page is the order in
/// <c>navigation.md</c> because the order <em>is</em> the feature: the name goes on the clipboard
/// first, whatever happens next. Elite's galaxy map has a search box and pasting into it works
/// every time, in every language, whatever the Commander's controls look like.
/// </para>
/// <para>
/// Driving the map is the convenience on top that can fail. It depends on where the map's focus
/// happens to be, on the map's layout, and on the game's language — none of which d47 can see —
/// so it checks afterwards against the route file and says what it found.
/// </para>
/// </summary>
public sealed class RouteCoursePage : UserControl
{
    private readonly CapabilityRegistry _registry;

    private readonly TextBox _system = new()
    {
        // "Shinrarta Dezhra" was an example drawn where a default goes, so the one field on this
        // page — a required one — read as already answered (#253). The mark on the label carries
        // that now, and the placeholder says what to type rather than showing one.
        PlaceholderText = "a system",
        Width = 260,
        MinHeight = 30,

        // A fixed width in a stretching slot centres itself, which puts the box in the middle of
        // the page with its own label off on the left.
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private readonly TextBlock _status;

    public RouteCoursePage(CapabilityRegistry registry, Func<string?>? suggestion = null)
    {
        _registry = registry;

        // What a screen reader hears, since the mark on the label is a glyph and a glyph is not
        // read aloud (#253).
        D47.App.Controls.FormField.Announce(
            _system, "System", D47.App.Controls.FieldNeed.Required);

        _status = Text(string.Empty, TypeScale.Secondary, ThemeManager.TextMutedKey, wrap: true);
        _status.IsVisible = false;

        // The destination of the route being flown, where there is one. A Commander opening this
        // page usually wants the system they are already going to.
        if (suggestion?.Invoke() is { Length: > 0 } already)
        {
            _system.Text = already;
        }

        var copy = new Button { Content = "Copy", Padding = new Thickness(14, 4), MinHeight = 30 };
        copy.Click += async (_, _) => await RunAsync("copy_to_clipboard", "text").ConfigureAwait(true);

        var plot = new Button
        {
            Content = "Copy and plot in the galaxy map",
            Padding = new Thickness(14, 4),
            MinHeight = 30,
        };

        plot.Click += async (_, _) => await RunAsync("plot_course", "system").ConfigureAwait(true);

        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new StackPanel
                {
                    Spacing = 3,
                    Children =
                    {
                        D47.App.Controls.FormField.Label(
                            "System", D47.App.Controls.FieldNeed.Required),
                        _system,
                    },
                },
                D47.App.Controls.FormField.Legend(required: true),
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    Children = { copy, plot },
                },
                _status,
                Text(
                    "Asking for a course always puts the name on your clipboard first, before "
                    + "anything else is tried — paste it into the galaxy map's search box and it "
                    + "works every time. Letting d47 drive the map is best-effort, and it checks "
                    + "afterwards whether it took.",
                    TypeScale.Small,
                    ThemeManager.TextMutedKey,
                    wrap: true),
            },
        };

        Content = new ScrollViewer
        {
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = body,
        };
    }

    private async Task RunAsync(string tool, string argument)
    {
        if (_system.Text is not { Length: > 0 } system)
        {
            _status.IsVisible = true;
            _status.Text = "Name a system first.";
            return;
        }

        _status.IsVisible = true;
        _status.Text = "…";

        try
        {
            // Through the registry, like everything else on this tab: the same path the model's
            // tool call takes and the same one the keyword router uses.
            var result = await _registry
                .InvokeAsync(
                    tool,
                    new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [argument] = system,
                    }),
                    CancellationToken.None)
                .ConfigureAwait(true);

            _status.Text = result.Content;
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Stopped.";
        }
    }

    private static TextBlock Text(string text, double size, string colourKey, bool wrap = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = size,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            MaxWidth = wrap ? 520 : double.PositiveInfinity,

            // Same trap as the box above: a capped width centres unless it is told not to.
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        block.Bind(
            TextBlock.ForegroundProperty,
            Application.Current!.Resources.GetResourceObservable(colourKey));

        return block;
    }
}
