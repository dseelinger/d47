using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace D47.App.Controls;

/// <summary>
/// What this build is, for the one moment a Commander needs to know: filing a report, or
/// checking whether an update landed.
/// <para>
/// It exists because the exact build — version <em>and</em> commit — is the thing a bug report
/// cannot do without, and the panel's first line is the wrong place to keep it. That line is on
/// screen for the whole session and reads as chrome after a minute; a dialog is read once,
/// deliberately, at the moment the answer is wanted. The title bar carries the short version so
/// the common question needs no dialog at all.
/// </para>
/// <para>
/// Everything here is selectable, because the point of the commit hash is to be pasted
/// somewhere else.
/// </para>
/// </summary>
public sealed class AboutWindow : Window
{
    public AboutWindow(AppPaths paths)
    {
        Title = "About Directive 47";
        Width = 460;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        ShowInTaskbar = false;

        Themed(this, BackgroundProperty, ThemeManager.BackgroundKey);

        var name = new TextBlock
        {
            Text = "Directive 47",
            FontSize = TypeScale.Heading,
            FontWeight = FontWeight.Medium,
        };

        Themed(name, TextBlock.ForegroundProperty, ThemeManager.TextKey);

        var close = new Button { Content = "Close", MinWidth = 110 };
        close.Click += (_, _) => Close();

        // The permanent way in. The first-run prompt is a convenience; without this, declining
        // it once would make the decision irreversible, which is a poor property for an offer.
        var addToStartMenu = new Button
        {
            Name = "AddToStartMenu",
            Content = "Add to Start Menu",
            IsVisible = !StartMenuShortcut.Exists() && Environment.ProcessPath is not null,
        };

        addToStartMenu.Click += (_, _) =>
        {
            var added = Environment.ProcessPath is { } executable
                        && StartMenuShortcut.TryCreate(
                            StartMenuShortcut.DefaultPath, executable, NullLogger.Instance);

            addToStartMenu.Content = added ? "Added" : "Could not add it";
            addToStartMenu.IsEnabled = false;
        };

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                name,
                Field("Version", BuildInfo.Semantic),
                Field("Build", BuildInfo.Full),
                Field("Data folder", paths.Data),

                // Frontier's own long-form wording, verbatim, because their media usage rules
                // supply it and ask that it be somewhere a person can find. The README and the
                // documentation site carry it too; this is the copy that ships with the binary,
                // which is the only one a Commander who never visits either will ever see.
                Field("Attribution", Attribution),
                new DockPanel
                {
                    Children =
                    {
                        new StackPanel
                        {
                            [DockPanel.DockProperty] = Dock.Right,
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Children = { close },
                        },
                        addToStartMenu,
                    },
                },
            },
        };

        Opened += (_, _) => close.Focus();
    }

    /// <summary>
    /// Frontier's long-form attribution, as their media usage rules word it.
    /// <para>
    /// A constant rather than a literal at the call site so that the one place it is authored is
    /// findable, and so a test can assert the app ships it. The same words are in <c>NOTICE</c>,
    /// the README and the documentation site; that is duplication on purpose, because the rules
    /// ask for it to be easy to locate rather than stored once.
    /// </para>
    /// </summary>
    public const string Attribution =
        "Directive 47 was created using assets and imagery from Elite Dangerous, with the "
        + "permission of Frontier Developments plc, for non-commercial purposes. It is not "
        + "endorsed by nor reflects the views or opinions of Frontier Developments and no "
        + "employee of Frontier Developments was involved in the making of it.";

    /// <summary>A labelled, selectable fact.</summary>
    private static Control Field(string label, string value)
    {
        var caption = new TextBlock { Text = label, FontSize = TypeScale.Secondary };
        Themed(caption, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        var text = new SelectableTextBlock
        {
            Text = value,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };

        Themed(text, SelectableTextBlock.ForegroundProperty, ThemeManager.TextKey);

        return new StackPanel { Spacing = 2, Children = { caption, text } };
    }

    private static void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target[!property] = new DynamicResourceExtension(key);
}
