using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Capabilities;
using D47.Core.Configuration;

namespace D47.App.Settings;

/// <summary>
/// One key. Stored: a masked block, REPLACE, VERIFY and FORGET KEY, with no field. Not stored, or being
/// replaced: the field and SAVE, with CANCEL while replacing.
/// </summary>
public sealed class SecretEditor : UserControl
{
    private readonly SettingRow _row;
    private readonly SettingsService _settings;
    private readonly Border _masked;
    private readonly TextBox _box;
    private readonly CheckBox _reveal;
    private readonly Button _store;
    private readonly Button _cancel;
    private readonly Button _replace;
    private readonly Button _check;
    private readonly Button _forget;
    private readonly TextBlock _state;
    private readonly Border _badge;
    private readonly TextBlock _verdict;
    private readonly TextBlock _message;

    private SecretCheck _result = SecretCheck.Untested;
    private bool _replacing;

    /// <summary>Drawn in place of a stored key: a fixed count, whatever the key's length.</summary>
    public const string Mask = "••••••••";

    /// <summary>Raised after a key is stored or forgotten, so a host can advance or re-read state.</summary>
    public event Action? Changed;

    public SecretEditor(SettingRow row, SettingsService settings)
    {
        _row = row;
        _settings = settings;

        // Drawn without reading the key.
        var bullets = new TextBlock
        {
            Text = Mask,
            FontFamily = new FontFamily(Fonts.MonoFamily),
            FontSize = TypeScale.Secondary,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Themed(bullets, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        _masked = new Border
        {
            Padding = new Thickness(12, 8),
            MinHeight = TypeScale.MinimumTarget - 12,
            VerticalAlignment = VerticalAlignment.Center,
            Child = bullets,
        };
        Themed(_masked, Border.BackgroundProperty, ThemeManager.SlabKey);

        _box = new TextBox
        {
            PasswordChar = '•',
            PlaceholderText = "Paste a key to store it",
            Width = 280,
        };

        // Masked by default with a reveal, because the commonest reason a key does not work is that it was
        // pasted wrong and a Commander cannot see that through bullets.
        (_reveal, _) = LabeledCheckBox.Build("Show key");

        _reveal.IsCheckedChanged += (_, _) =>
        {
            var shown = _reveal.IsChecked == true;
            _box.PasswordChar = shown ? '\0' : '•';

            AutomationProperties.SetName(_reveal, shown ? "Hide the key" : "Show the key");
        };

        _state = D47.App.Panel.RoutingKit.Tag(string.Empty, ThemeManager.GreyKey);

        _badge = new Border
        {
            Padding = new Thickness(8, 2),
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

        Themed(_message, TextBlock.ForegroundProperty, ThemeManager.RedKey);

        _store = new Button { Content = "SAVE" };
        _cancel = new Button { Content = "CANCEL" };
        _replace = new Button { Content = "REPLACE" };
        _check = new Button { Content = "VERIFY", IsVisible = row.Verify is not null };
        _forget = new Button { Content = "FORGET KEY", Classes = { SettingsView.DestructiveClass } };
        ToolTip.SetShowOnDisabled(_check, true);

        // Asking means a dialog, which the headset's copy of this surface must not open.
        Panel.OffscreenSurface.OpensAWindow(_forget);

        _store.Click += (_, _) => Store();
        _cancel.Click += (_, _) => Cancel();
        _replace.Click += (_, _) => Replace();
        _check.Click += async (_, _) => await CheckAsync();
        _forget.Click += async (_, _) => await ForgetAsync();

        _box.TextChanged += (_, _) => RefreshCheck();

        // Both states share one line; Refresh shows the controls that belong to the current one.
        var controls = new WrapPanel { ItemSpacing = 8, LineSpacing = 8 };
        controls.Children.Add(_masked);
        controls.Children.Add(_box);
        controls.Children.Add(_reveal);
        controls.Children.Add(_badge);
        controls.Children.Add(_store);
        controls.Children.Add(_cancel);
        controls.Children.Add(_replace);
        controls.Children.Add(_check);
        controls.Children.Add(_forget);

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

    /// <summary>Whether the field is showing: no key is stored, or one is being replaced.</summary>
    private bool Entering => !IsStored || _replacing;

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
        _replacing = false;

        // A new key makes any previous verdict a statement about a value that is gone.
        _result = SecretCheck.Untested;

        Refresh();
        Changed?.Invoke();

        return true;
    }

    /// <summary>Opens the field over a stored key.</summary>
    private void Replace()
    {
        _replacing = true;
        _message.IsVisible = false;

        Refresh();
        _box.Focus();
    }

    /// <summary>Back to the stored line, with the field emptied and the store untouched.</summary>
    private void Cancel()
    {
        _box.Text = string.Empty;
        _reveal.IsChecked = false;
        _replacing = false;
        _message.IsVisible = false;

        Refresh();
    }

    /// <summary>Asks first, then deletes the stored key.</summary>
    private async Task ForgetAsync()
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

        if (wanted)
        {
            Forget();
        }
    }

    private void Forget()
    {
        _box.Text = string.Empty;
        _reveal.IsChecked = false;
        _replacing = false;
        _result = SecretCheck.Untested;

        var result = _settings.Apply(_row.Key, null, SettingsCaller.Panel);
        _message.IsVisible = !result.Ok;
        _message.Text = result.Message;

        Refresh();
        Changed?.Invoke();
    }

    /// <summary>Proves the stored key, or the key in the field, which means storing it first.</summary>
    private async Task CheckAsync()
    {
        if (_row.Verify is not { } verify)
        {
            return;
        }

        if (Entering && !Store())
        {
            return;
        }

        _check.IsEnabled = false;
        _verdict.IsVisible = true;
        _verdict.Text = "Checking…";
        Themed(_verdict, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

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
        var entering = Entering;

        _masked.IsVisible = !entering;
        _replace.IsVisible = !entering;
        _forget.IsVisible = !entering;

        _box.IsVisible = entering;
        _reveal.IsVisible = entering;
        _store.IsVisible = entering;
        _cancel.IsVisible = entering && stored;

        _state.Text = (stored ? "Key stored" : "No key").ToUpperInvariant();

        // Yellow for stored and Grey for not, so the two states differ in colour as well as in wording.
        Themed(
            _state,
            TextBlock.ForegroundProperty,
            stored ? ThemeManager.YellowKey : ThemeManager.GreyKey);

        RefreshCheck();

        _box.PlaceholderText = stored ? "Paste a new key to replace it" : "Paste a key to store it";

        _verdict.IsVisible = _result.Verdict != SecretVerdict.Untested;
        _verdict.Text = _result.Detail;

        Themed(
            _verdict,
            TextBlock.ForegroundProperty,
            _result.Verdict switch
            {
                SecretVerdict.Works => ThemeManager.BlueKey,
                SecretVerdict.Rejected => ThemeManager.RedKey,
                _ => ThemeManager.GreyKey,
            });
    }

    /// <summary>VERIFY checks a stored key as it is, and a typed one once something is typed.</summary>
    private void RefreshCheck()
    {
        _check.IsEnabled = _row.Verify is not null && (!Entering || !string.IsNullOrWhiteSpace(_box.Text));

        ToolTip.SetTip(_check, _check.IsEnabled ? null : "Paste a key first — there is nothing here to check yet");
    }

    private void Themed(AvaloniaObject target, AvaloniaProperty property, string key) =>
        target.Bind(property, this.GetResourceObservable(key));
}
