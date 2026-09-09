using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;

// The shape, not the file-system helper.
using Path = Avalonia.Controls.Shapes.Path;

namespace D47.App.Settings;

/// <summary>One key: paste it, store it, prove it works, clear it (Phase 16).</summary>
public sealed class SecretEditor : UserControl
{
    private readonly SettingRow _row;
    private readonly SettingsService _settings;
    private readonly TextBox _box;
    private readonly ToggleButton _reveal;
    private readonly Path _revealSlash;
    private readonly Button _clear;
    private readonly Button _store;
    private readonly Button _check;
    private readonly TextBlock _state;
    private readonly Border _badge;
    private readonly TextBlock _verdict;
    private readonly TextBlock _message;

    private SecretCheck _result = SecretCheck.Untested;

    /// <summary>Marks the two glyph controls that live inside the field rather than beside it.</summary>
    private const string InBoxClass = "in-box";

    /// <summary>Raised after a key is stored or cleared, so a host can advance or re-read state.</summary>
    public event Action? Changed;

    public SecretEditor(SettingRow row, SettingsService settings)
    {
        _row = row;
        _settings = settings;

        _box = new TextBox
        {
            PasswordChar = '•',
            PlaceholderText = "Paste a key to store it",
            Width = 280,
        };

        // Masked by default with a reveal, because the commonest reason a key does not work is that it was
        // pasted wrong and a Commander cannot see that through bullets.
        _revealSlash = Stroked("M 3.8,3.8 L 20.2,20.2");
        _revealSlash.IsVisible = false;

        _reveal = new ToggleButton
        {
            Content = Glyph(
                16,
                Stroked("M 1.5,12 C 4.5,6 8,3.5 12,3.5 C 16,3.5 19.5,6 22.5,12"
                        + " C 19.5,18 16,20.5 12,20.5 C 8,20.5 4.5,18 1.5,12 Z"),
                Filled(new EllipseGeometry(new Rect(8.1, 8.1, 7.8, 7.8))),
                _revealSlash),
        };

        InTheBox(_reveal, "Show the key while you paste it");

        _reveal.IsCheckedChanged += (_, _) =>
        {
            var shown = _reveal.IsChecked == true;
            _box.PasswordChar = shown ? '\0' : '•';

            // The eye is struck through while the key is legible, so the glyph says what is true now rather
            // than what pressing it would do.
            _revealSlash.IsVisible = shown;
            ToolTip.SetTip(_reveal, shown ? "Hide the key" : "Show the key while you paste it");
            AutomationProperties.SetName(_reveal, shown ? "Hide the key" : "Show the key");
        };

        // An undo arrow, and deliberately the one control here that asks before it acts: it blanks the box,
        // and if a key is stored it deletes that too.
        _clear = new Button
        {
            Content = Glyph(
                16,
                Filled(Geometry.Parse("M 3.4,9 L 9.4,4.6 L 9.4,13.4 Z")),
                Stroked("M 8.2,9 L 14,9 A 5.5,5.5 0 0 1 14,20 L 10.4,20")),
        };

        InTheBox(_clear, "Clear the key");

        // Asking means a dialog, which the headset's copy of this surface must not open.
        Panel.OffscreenSurface.OpensAWindow(_clear);

        _state = new TextBlock { FontSize = TypeScale.Secondary, VerticalAlignment = VerticalAlignment.Center };

        _badge = new Border
        {
            Padding = new Thickness(8, 2),
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            VerticalAlignment = VerticalAlignment.Center,
            Child = _state,
        };

        _verdict = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };

        _message = new TextBlock
        {
            FontSize = TypeScale.Secondary,
            TextWrapping = TextWrapping.Wrap,
            IsVisible = false,
        };

        Themed(_message, TextBlock.ForegroundProperty, ThemeManager.DangerKey);

        // Both glyphs ride inside the field.
        _box.InnerRightContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _reveal, _clear },
        };

        // "Save" with nothing behind it, "Overwrite" once there is: the second warns that a stored key is
        // about to be replaced, which "Store" said either way.
        _store = new Button();
        _check = new Button { Content = "Verify Key", IsVisible = row.Verify is not null };

        _store.Click += (_, _) => Store();
        _clear.Click += async (_, _) => await ClearAsync();
        _check.Click += async (_, _) => await CheckAsync();

        _box.TextChanged += (_, _) => RefreshBox();

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        controls.Children.Add(_box);
        controls.Children.Add(_store);
        controls.Children.Add(_check);
        controls.Children.Add(_badge);

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(controls);
        stack.Children.Add(_verdict);
        stack.Children.Add(_message);

        // A checked ToggleButton paints itself in the accent, which inside a text box is a solid chip sitting
        // on the field — loud enough to read as the state rather than as the button, when the state is
        // already said by the stroke through the eye.
        Styles.Add(new Style(x => x.OfType<ToggleButton>().Class(InBoxClass).Class(":checked")
            .Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent) },
        });

        Content = stack;
        Refresh();
    }

    /// <summary>Whether the store currently holds a value for this row.</summary>
    public bool IsStored => _settings.HasSecret(_row.SecretName);

    /// <summary>The last real check, or <see cref="SecretCheck.Untested"/>.</summary>
    public SecretCheck Result => _result;

    /// <summary>Takes what is in the box into the store.</summary>
    private bool Store()
    {
        // Trimmed on the way in.
        var value = _box.Text?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            Fail("Paste a key first.");
            return false;
        }

        var result = _settings.Apply(_row.Key, value, SettingsCaller.Panel);

        _message.IsVisible = !result.Ok;
        _message.Text = result.Message;

        if (!result.Ok)
        {
            return false;
        }

        // Never held in a control after it is stored.
        _box.Text = string.Empty;
        _reveal.IsChecked = false;

        // A new key makes any previous verdict a statement about a value that is gone.
        _result = SecretCheck.Untested;

        Refresh();
        Changed?.Invoke();

        return true;
    }

    /// <summary>Asks first, then clears.</summary>
    private async Task ClearAsync()
    {
        if (IsStored)
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            var wanted = await new ConfirmWindow(
                "Delete stored key",
                $"Delete the stored {_row.Label}? Directive 47 cannot show a stored key back, so "
                + "this cannot be undone — you would have to paste it again, or reissue it at the "
                + "provider if you no longer have a copy.",
                confirmLabel: "Delete key",
                declineLabel: "Keep it").AskAsync(owner);

            if (!wanted)
            {
                return;
            }
        }

        Clear();
    }

    private void Clear()
    {
        _box.Text = string.Empty;
        _reveal.IsChecked = false;
        _result = SecretCheck.Untested;

        var result = _settings.Apply(_row.Key, null, SettingsCaller.Panel);
        _message.IsVisible = !result.Ok;
        _message.Text = result.Message;

        Refresh();
        Changed?.Invoke();
    }

    /// <summary>Proves the key in the box, which means storing it first.</summary>
    private async Task CheckAsync()
    {
        if (_row.Verify is not { } verify)
        {
            return;
        }

        if (!Store())
        {
            return;
        }

        _check.IsEnabled = false;
        _verdict.IsVisible = true;
        _verdict.Text = "Checking…";
        Themed(_verdict, TextBlock.ForegroundProperty, ThemeManager.TextMutedKey);

        try
        {
            _result = await verify(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A check that throws is a check that could not be made, which says nothing about the key.
            _result = SecretCheck.Unreachable(ex.Message);
        }
        finally
        {
            // Refresh decides whether the button comes back, because by now the box has emptied into the
            // store and a shut button is the correct answer to that.
            Refresh();
        }
    }

    private void Fail(string reason)
    {
        _message.IsVisible = true;
        _message.Text = reason;
    }

    /// <summary>Re-reads what the store says and repaints.</summary>
    public void Refresh()
    {
        var stored = IsStored;

        _store.Content = stored ? "Overwrite" : "Save";

        _state.Text = stored ? "Key stored" : "No key";

        // Accent for stored and muted for not, so the two states differ in colour as well as in wording — the
        // row is glanced at far more often than it is read.
        Themed(
            _state,
            TextBlock.ForegroundProperty,
            stored ? ThemeManager.AccentKey : ThemeManager.TextMutedKey);

        Themed(
            _badge,
            Border.BorderBrushProperty,
            stored ? ThemeManager.AccentKey : ThemeManager.BorderKey);

        RefreshBox();

        _box.PlaceholderText = stored ? "Paste a new key to replace it" : "Paste a key to store it";

        _verdict.IsVisible = _result.Verdict != SecretVerdict.Untested;
        _verdict.Text = _result.Detail;

        Themed(
            _verdict,
            TextBlock.ForegroundProperty,
            _result.Verdict switch
            {
                SecretVerdict.Works => ThemeManager.AccentKey,
                SecretVerdict.Rejected => ThemeManager.DangerKey,
                _ => ThemeManager.TextMutedKey,
            });
    }

    /// <summary>The two controls that answer to what is in the box rather than to what is in the store.</summary>
    private void RefreshBox()
    {
        var typed = !string.IsNullOrWhiteSpace(_box.Text);

        _clear.IsVisible = IsStored || !string.IsNullOrEmpty(_box.Text);

        _check.IsEnabled = typed && _row.Verify is not null;

        ToolTip.SetTip(
            _check,
            typed
                ? "Store this key and check it against the provider"
                : "Paste a key first — there is nothing here to check yet");
    }

    private void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));

    /// <summary>
    /// A control that lives inside the text box rather than beside it: no chrome of its own, so the
    /// field's border stays the only box drawn, and a tooltip and an automation name because dropping
    /// the label is what buys the space and a glyph alone tells a screen reader nothing.
    /// </summary>
    private static void InTheBox(TemplatedControl control, string name)
    {
        control.Classes.Add(InBoxClass);
        control.Background = Brushes.Transparent;
        control.BorderThickness = new Thickness(0);
        control.Padding = new Thickness(5, 0);
        control.MinWidth = 0;
        control.VerticalAlignment = VerticalAlignment.Center;
        control.Cursor = new Cursor(StandardCursorType.Hand);

        ToolTip.SetTip(control, name);
        AutomationProperties.SetName(control, name);
    }

    /// <summary>
    /// Several paths drawn as one figure, in repo rather than taken from a font — the same rule the
    /// send glyph and the help mark follow.
    /// </summary>
    private static Viewbox Glyph(double size, params Path[] parts)
    {
        var canvas = new Canvas { Width = 24, Height = 24 };

        foreach (var part in parts)
        {
            canvas.Children.Add(part);
        }

        return new Viewbox { Width = size, Height = size, Child = canvas };
    }

    /// <summary>
    /// Themed through a dynamic resource rather than a literal brush, so a glyph repaints with the rest
    /// of the app on a theme change (Phase 4, "Themes").
    /// </summary>
    private static Path Stroked(string data)
    {
        var path = new Path
        {
            Data = Geometry.Parse(data),
            StrokeThickness = 1.7,
            StrokeJoin = PenLineJoin.Round,
            StrokeLineCap = PenLineCap.Round,
        };

        path[!Shape.StrokeProperty] = new DynamicResourceExtension(ThemeManager.TextMutedKey);
        return path;
    }

    private static Path Filled(Geometry data)
    {
        var path = new Path { Data = data };

        path[!Shape.FillProperty] = new DynamicResourceExtension(ThemeManager.TextMutedKey);
        return path;
    }
}
