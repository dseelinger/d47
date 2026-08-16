using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>
/// One key: paste it, store it, prove it works, clear it (list.md Phase 16).
/// <para>
/// <b>One control, two surfaces.</b> The settings row and the first-run guide both show a key,
/// and the guide is explicitly not allowed to re-author these fields — "a parallel copy of these
/// three fields is a second thing to keep in step and the one that drifts". Extracting the whole
/// control rather than only the row definition means the store, the trim, the reveal and the
/// check cannot drift either.
/// </para>
/// <para>
/// <b>The value never leaves this control except into the store.</b> It is not logged at any
/// level, it is not put on the clipboard, it is not shown back once stored, and the box is
/// emptied the moment it is accepted. <see cref="SecretCheck"/> carries a verdict and a sentence
/// and never the key.
/// </para>
/// </summary>
public sealed class SecretEditor : UserControl
{
    private readonly SettingRow _row;
    private readonly SettingsService _settings;
    private readonly TextBox _box;
    private readonly ToggleButton _reveal;
    private readonly Button _check;
    private readonly TextBlock _state;
    private readonly Border _badge;
    private readonly TextBlock _verdict;
    private readonly TextBlock _message;

    private SecretCheck _result = SecretCheck.Untested;

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

        // Masked by default with a reveal, because the commonest reason a key does not work is
        // that it was pasted wrong and a Commander cannot see that through bullets. The reveal
        // shows only what is in the box on the way in — a stored key is never shown back, which
        // is what makes the store write-only.
        _reveal = new ToggleButton { Content = "Show", MinWidth = 56 };
        _reveal.IsCheckedChanged += (_, _) =>
        {
            var shown = _reveal.IsChecked == true;
            _box.PasswordChar = shown ? '\0' : '•';
            _reveal.Content = shown ? "Hide" : "Show";
        };

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

        var store = new Button { Content = "Store" };
        var clear = new Button { Content = "Clear" };
        _check = new Button { Content = "Check", IsVisible = row.Verify is not null };

        store.Click += (_, _) => Store();
        clear.Click += (_, _) => Clear();
        _check.Click += async (_, _) => await CheckAsync();

        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        controls.Children.Add(_box);
        controls.Children.Add(_reveal);
        controls.Children.Add(store);
        controls.Children.Add(_check);
        controls.Children.Add(clear);
        controls.Children.Add(_badge);

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(controls);
        stack.Children.Add(_verdict);
        stack.Children.Add(_message);

        Content = stack;
        Refresh();
    }

    /// <summary>Whether the store currently holds a value for this row.</summary>
    public bool IsStored => _settings.HasSecret(_row.SecretName);

    /// <summary>The last real check, or <see cref="SecretCheck.Untested"/>.</summary>
    public SecretCheck Result => _result;

    private void Store()
    {
        // Trimmed on the way in. A key copied from a browser arrives with whitespace or a
        // newline more often than not, and a trailing newline fails at the provider in a way
        // that reads as a wrong key rather than as a bad paste.
        var value = _box.Text?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            Fail("Paste a key first.");
            return;
        }

        var result = _settings.Apply(_row.Key, value, SettingsCaller.Panel);

        _message.IsVisible = !result.Ok;
        _message.Text = result.Message;

        if (!result.Ok)
        {
            return;
        }

        // Never held in a control after it is stored.
        _box.Text = string.Empty;
        _reveal.IsChecked = false;

        // A new key makes any previous verdict a statement about a value that is gone.
        _result = SecretCheck.Untested;

        Refresh();
        Changed?.Invoke();
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

    private async Task CheckAsync()
    {
        if (_row.Verify is not { } verify)
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
            // A check that throws is a check that could not be made, which says nothing about
            // the key. Reporting it as a rejection would send a Commander to reissue a key that
            // works.
            _result = SecretCheck.Unreachable(ex.Message);
        }
        finally
        {
            _check.IsEnabled = true;
            Refresh();
        }
    }

    private void Fail(string reason)
    {
        _message.IsVisible = true;
        _message.Text = reason;
    }

    /// <summary>Re-reads what the store says and repaints. Safe to call from a host's refresh.</summary>
    public void Refresh()
    {
        var stored = IsStored;

        _state.Text = stored ? "Key stored" : "No key";

        // Accent for stored and muted for not, so the two states differ in colour as well as in
        // wording — the row is glanced at far more often than it is read.
        Themed(
            _state,
            TextBlock.ForegroundProperty,
            stored ? ThemeManager.AccentKey : ThemeManager.TextMutedKey);

        Themed(
            _badge,
            Border.BorderBrushProperty,
            stored ? ThemeManager.AccentKey : ThemeManager.BorderKey);

        // Nothing to check until there is something stored to check.
        _check.IsEnabled = stored && _row.Verify is not null;

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

    private void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));
}
