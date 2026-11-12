using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;

namespace D47.App.Panel;

/// <summary>Renames a screenshot: the name in a field with a fixed <c>.jpg</c>.</summary>
public sealed class RenameScreenshotDialog : ModalDialog
{
    public const string NameFieldName = "RenameScreenshotName";

    public const string ConfirmName = "ConfirmRenameScreenshot";

    public const string CancelName = "CancelRenameScreenshot";

    public const string FailureName = "RenameScreenshotFailure";

    public const string Hint = "The date taken stays with the picture, so its place in the grid does not change.";

    private readonly TextBox _name;
    private readonly Button _confirm;
    private readonly Button _cancel;
    private readonly Notice _failure;
    private readonly Func<string, Task<ScreenshotRename>> _rename;

    /// <param name="rename">Renames the picture off the UI thread.</param>
    public RenameScreenshotDialog(string fileName, Func<string, Task<ScreenshotRename>> rename)
    {
        _rename = rename;
        Title = fileName;
        Width = Modal.Width;

        _name = new TextBox
        {
            Name = NameFieldName,
            Text = Path.GetFileNameWithoutExtension(fileName),
            Height = TypeScale.MinimumTarget,
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        Avalonia.Automation.AutomationProperties.SetName(_name, "New name");
        _name.TextChanged += (_, _) => FieldMessage.Clear(_name);

        var extension = new TextBlock { Text = ScreenshotEdits.Extension, VerticalAlignment = VerticalAlignment.Center };
        extension.FontFamily = new FontFamily(Fonts.MonoFamily);
        ThemeInk.Bind(extension, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        Grid.SetColumn(extension, 1);
        row.Children.Add(_name);
        row.Children.Add(extension);

        var hint = new TextBlock { Text = Hint, FontSize = TypeScale.FieldMessage, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

        _failure = new Notice(NoticeLevel.Error, inline: true) { Name = FailureName, IsVisible = false, Label = "Not renamed" };

        _confirm = new Button { Name = ConfirmName, Content = "SAVE", MinWidth = 76, Height = TypeScale.MinimumTarget };
        _cancel = new Button { Name = CancelName, Content = "CANCEL", MinWidth = 76, Height = TypeScale.MinimumTarget };
        _confirm.Click += async (_, _) => await Save();
        _cancel.Click += (_, _) => Close();

        var body = new StackPanel
        {
            Spacing = 6,
            Children = { SaveSearchDialog.Caption("New name"), row, hint, _failure },
        };

        Modal.Apply(this, "Rename screenshot", Title, body, [_confirm, _cancel]);

        Opened += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
    }

    private async Task Save()
    {
        _confirm.IsEnabled = false;
        _cancel.IsEnabled = false;
        _failure.IsVisible = false;

        ScreenshotRename result;

        try
        {
            result = await _rename(_name.Text ?? string.Empty);
        }
        finally
        {
            _confirm.IsEnabled = true;
            _cancel.IsEnabled = true;
        }

        if (result.Done)
        {
            Close();
        }
        else if (result.IsName)
        {
            FieldMessage.ShowError(_name, result.Error ?? string.Empty);
        }
        else
        {
            _failure.Text = result.Error;
            _failure.IsVisible = true;
        }
    }
}

/// <summary>Asks before a screenshot goes to the Recycle Bin, with Elite's BMP and the Codex pictures cut from it named.</summary>
public sealed class DeleteScreenshotDialog : ModalDialog
{
    public const string BmpBoxName = "DeleteScreenshotBmp";

    public const string ConfirmName = "ConfirmDeleteScreenshot";

    public const string CancelName = "CancelDeleteScreenshot";

    public const string FailureName = "DeleteScreenshotFailure";

    public const string CodexLineName = "DeleteScreenshotCodex";

    public const string Question = "D47 moves the JPEG to the Recycle Bin. You can restore it from there.";

    public const string BmpLabel = "Also delete Elite's original BMP";

    private const int NamedAtMost = 3;

    private readonly CheckBox? _bmp;
    private readonly Button _confirm;
    private readonly Button _cancel;
    private readonly Notice _failure;
    private readonly Func<bool, Task<string?>> _delete;

    /// <param name="bmp">Whether Elite's BMP is still there, so the box is shown.</param>
    /// <param name="codexEntries">The Codex entries whose picture is cut from the screenshot.</param>
    /// <param name="delete">Deletes the picture off the UI thread, with the box's value; returns why it stopped, or null.</param>
    public DeleteScreenshotDialog(string fileName, bool bmp, IReadOnlyList<string> codexEntries, Func<bool, Task<string?>> delete)
    {
        _delete = delete;
        Title = fileName;
        Width = Modal.Width;

        var question = new TextBlock
        {
            Text = Question,
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(question, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

        var body = new StackPanel { Spacing = 12, Children = { question } };

        if (codexEntries.Count > 0)
        {
            var codex = new TextBlock
            {
                Name = CodexLineName,
                Text = CodexLine(codexEntries),
                FontSize = TypeScale.FieldMessage,
                TextWrapping = TextWrapping.Wrap,
            };
            ThemeInk.Bind(codex, TextBlock.ForegroundProperty, ThemeManager.YellowKey);
            body.Children.Add(codex);
        }

        if (bmp)
        {
            var (box, _) = LabeledCheckBox.Build(BmpLabel);
            box.Name = BmpBoxName;
            _bmp = box;
            body.Children.Add(box);
        }

        _failure = new Notice(NoticeLevel.Error, inline: true) { Name = FailureName, IsVisible = false, Label = "Not deleted" };
        body.Children.Add(_failure);

        _confirm = new Button
        {
            Name = ConfirmName,
            Content = "DELETE",
            MinWidth = 76,
            Height = TypeScale.MinimumTarget,
            Classes = { Settings.SettingsView.DestructiveClass },
        };
        _cancel = new Button { Name = CancelName, Content = "CANCEL", MinWidth = 76, Height = TypeScale.MinimumTarget };
        _confirm.Click += async (_, _) => await Confirm();
        _cancel.Click += (_, _) => Close();

        Modal.Apply(this, "Delete screenshot", Title, body, [_confirm, _cancel]);

        Opened += (_, _) => _cancel.Focus();
    }

    /// <summary>The line naming the Codex pictures that go with the screenshot: up to three entries, then a count.</summary>
    public static string CodexLine(IReadOnlyList<string> entries)
    {
        var named = string.Join(", ", entries.Take(NamedAtMost));
        var rest = entries.Count - NamedAtMost;
        var list = rest > 0 ? $"{named} and {rest} more" : named;

        return $"The Codex {(entries.Count == 1 ? "picture" : "pictures")} cut from this screenshot "
            + $"{(entries.Count == 1 ? "is" : "are")} deleted with it: {list}. "
            + $"{(entries.Count == 1 ? "That entry shows" : "Each entry shows")} its stock picture or placeholder again.";
    }

    private async Task Confirm()
    {
        _confirm.IsEnabled = false;
        _cancel.IsEnabled = false;
        _failure.IsVisible = false;

        string? reason;

        try
        {
            reason = await _delete(_bmp?.IsChecked == true);
        }
        finally
        {
            _confirm.IsEnabled = true;
            _cancel.IsEnabled = true;
        }

        if (reason is null)
        {
            Close();
        }
        else
        {
            _failure.Text = reason;
            _failure.IsVisible = true;
        }
    }
}
