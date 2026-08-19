using System.Diagnostics;
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
    /// <param name="setUpKeys">
    /// Reopens the guided key setup (list.md Phase 16). Optional: null hides the button, which is
    /// what a caller with no host to drive it gets. Here rather than only on first run because
    /// <b>keys get rotated and revoked</b>, so the state that triggers the guide is a state a
    /// working install can return to — and About is where a Commander already goes when
    /// something is not working.
    /// </param>
    
    public AboutWindow(AppPaths paths, Func<Task>? setUpKeys = null)
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

        // What changed, rather than what this is. The dialog already answers "which build am
        // I running"; the question that follows it is "and what came with it", and the answer
        // is a file in the repository rather than anything shipped beside the executable.
        // Opened in a browser because that is where it is readable — CHANGELOG.md is markdown,
        // and a self-contained app has no renderer for it worth carrying.
        var changelog = new Button
        {
            Name = "Changelog",
            Content = "Changelog",
        };

        changelog.Click += (_, _) => Process.Start(
            new ProcessStartInfo(ChangelogUrl) { UseShellExecute = true });

        var keys = new Button
        {
            Name = "SetUpKeys",
            Content = "Set up keys",
            IsVisible = setUpKeys is not null,
        };

        keys.Click += async (_, _) =>
        {
            if (setUpKeys is { } open)
            {
                await open();
            }
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
                            Spacing = 8,
                            Children = { changelog, keys, close },
                        },
                        addToStartMenu,
                    },
                },
            },
        };

        Opened += (_, _) => close.Focus();
    }

    /// <summary>
    /// The changelog, on GitHub, at the branch rather than at a tag.
    /// <para>
    /// A literal rather than something composed from <see cref="BuildInfo"/>: the question this
    /// answers is "what changed", which is asked most often by a Commander who is one release
    /// behind, and a URL pinned to the running build would show them everything except the
    /// entry they came for. Held to the same repository prefix the update path pins itself to,
    /// for the reason recorded there — <c>UseShellExecute</c> resolves anything, not just http.
    /// </para>
    /// </summary>
    public const string ChangelogUrl =
        "https://github.com/dseelinger/d47/blob/main/CHANGELOG.md";

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
