using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using D47.Core.Capabilities;

namespace D47.App.Panel;

/// <summary>Getting a system name into the game (Phase 37, "Course").</summary>
public sealed class RouteCoursePage : UserControl
{
    private readonly CapabilityRegistry _registry;

    private readonly TextBox _system = new()
    {
        // "Shinrarta Dezhra" was an example drawn where a default goes, so the one field on this page — a
        // required one — read as already answered (#253).
        PlaceholderText = "a system",
        Width = 260,
        MinHeight = 30,

        // A fixed width in a stretching slot centres itself, which puts the box in the middle of the page
        // with its own label off on the left.
        HorizontalAlignment = HorizontalAlignment.Left,
    };

    private readonly TextBlock _status;

    public RouteCoursePage(CapabilityRegistry registry, Func<string?>? suggestion = null)
    {
        _registry = registry;

        // What a screen reader hears, since the mark on the label is a glyph and a glyph is not read aloud
        // (#253).
        D47.App.Controls.FormField.Announce(
            _system, "System", D47.App.Controls.FieldNeed.Required);

        _status = RoutingKit.Status();

        // The destination of the route being flown, where there is one.
        if (suggestion?.Invoke() is { Length: > 0 } already)
        {
            _system.Text = already;
        }

        var copy = new Button { Content = "Copy" };
        copy.Click += async (_, _) => await RunAsync("copy_to_clipboard", "text").ConfigureAwait(true);

        var plot = new Button { Content = "Copy and plot in the galaxy map" };

        plot.Click += async (_, _) => await RunAsync("plot_course", "system").ConfigureAwait(true);

        var body = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                RoutingKit.Title("Course").Row,
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
                RoutingKit.Actions(copy, plot),
                _status,
                RoutingKit.Prose(
                    "Asking for a course always puts the name on your clipboard first, before "
                    + "anything else is tried — paste it into the galaxy map's search box and it "
                    + "works every time. Letting d47 drive the map is best-effort, and it checks "
                    + "afterwards whether it took."),
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
            RoutingKit.Say(_status, "Name a system first.", error: true);
            return;
        }

        RoutingKit.Say(_status, "…");

        try
        {
            // Through the registry, like everything else on this tab: the same path the model's tool call
            // takes and the same one the keyword router uses.
            var result = await _registry
                .InvokeAsync(
                    tool,
                    new ToolArguments(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [argument] = system,
                    }),
                    CancellationToken.None)
                .ConfigureAwait(true);

            RoutingKit.Say(_status, result.Content, result.IsError);
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Stopped.";
        }
    }
}
