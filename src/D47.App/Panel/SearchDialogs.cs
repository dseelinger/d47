using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Knowledge;

namespace D47.App.Panel;

/// <summary>Names the search a page last answered and saves it, or says why it could not.</summary>
public sealed class SaveSearchDialog : ModalDialog
{
    public const double DialogWidth = 560;

    public const string NameFieldName = "SaveSearchName";

    public const string ConfirmName = "ConfirmSaveSearch";

    public const string CancelName = "CancelSaveSearch";

    public const string RefusalName = "SaveSearchRefusal";

    public const string FromHere = "Your system, wherever you are";

    private readonly TextBox _name;
    private readonly TextBlock _hint;
    private readonly TextBlock _refusal;

    private string? _refusedFor;

    public SaveSearchDialog(
        string noun,
        string tool,
        IReadOnlyDictionary<string, string> arguments,
        Func<string, SavedSearchOutcome> save)
    {
        Title = "Save this search";
        Width = DialogWidth;

        _name = new TextBox { Name = NameFieldName, Height = TypeScale.MinimumTarget, VerticalContentAlignment = VerticalAlignment.Center };
        Avalonia.Automation.AutomationProperties.SetName(_name, "Name");

        _hint = Small(string.Empty, ThemeManager.GreyKey);
        _refusal = Small(string.Empty, ThemeManager.RedKey);
        _refusal.Name = RefusalName;
        _refusal.IsVisible = false;

        _name.TextChanged += (_, _) =>
        {
            var typed = _name.Text?.Trim() ?? string.Empty;
            _hint.Text = typed.Length == 0 ? string.Empty : $"Say “{SavedSearchPhrases.Of(typed).First()}” to run it.";
            _refusal.IsVisible &= string.Equals(_name.Text, _refusedFor, StringComparison.Ordinal);
        };

        var field = new StackPanel { Spacing = 6, Children = { Caption("Name"), _name, _refusal, _hint } };

        var runs = new StackPanel { Spacing = 8, Children = { Caption("What it runs"), Line("Tool", tool, mono: true) } };

        foreach (var (key, value) in Lines(arguments))
        {
            runs.Children.Add(Line(key, value, mono: false, grey: value == FromHere));
        }

        var slab = new Border { Padding = new Thickness(14, 12), Child = runs };
        ThemeInk.Bind(slab, Border.BackgroundProperty, ThemeManager.SlabKey);

        var confirm = new Button { Name = ConfirmName, Content = "SAVE", MinWidth = 76, Height = TypeScale.MinimumTarget };
        var cancel = new Button { Name = CancelName, Content = "CANCEL", MinWidth = 76, Height = TypeScale.MinimumTarget };

        confirm.Click += (_, _) =>
        {
            var outcome = save(_name.Text ?? string.Empty);

            if (outcome.Saved)
            {
                Close();
                return;
            }

            _refusedFor = _name.Text;
            _refusal.Text = outcome.Message;
            _refusal.IsVisible = true;
        };
        cancel.Click += (_, _) => Close();

        Modal.Apply(this, noun, Title, new StackPanel { Spacing = Modal.BlockGap, Children = { field, slab } }, [confirm, cancel]);

        Opened += (_, _) => _name.Focus();
    }

    /// <summary>What the search runs, one label and value per argument, with a <c>near</c> left out said as “from here”.</summary>
    public static IReadOnlyList<(string Label, string Value)> Lines(IReadOnlyDictionary<string, string> arguments)
    {
        var lines = arguments
            .Where(pair => pair.Key != "near")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (pair.Key.Replace('_', ' ').ToUpperInvariant(), pair.Value))
            .ToList();

        lines.Add(("NEAR", arguments.TryGetValue("near", out var near) && near.Trim().Length > 0 ? near : FromHere));

        return lines;
    }

    private static Grid Line(string label, string value, bool mono, bool grey = false)
    {
        var text = new TextBlock
        {
            Text = value,
            FontFamily = mono ? new FontFamily(Fonts.MonoFamily) : Fonts.ChromeFamily,
            FontSize = mono ? TypeScale.Small : TypeScale.Tip,
            FontWeight = mono ? FontWeight.Normal : FontWeight.Medium,
            TextWrapping = TextWrapping.Wrap,
        };
        ThemeInk.Bind(text, TextBlock.ForegroundProperty, grey ? ThemeManager.GreyKey : ThemeManager.WhiteKey);
        Grid.SetColumn(text, 1);

        return new Grid { ColumnDefinitions = new ColumnDefinitions("150,*"), ColumnSpacing = 12, Children = { Caption(label), text } };
    }

    internal static TextBlock Caption(string text)
    {
        var block = new TextBlock
        {
            Text = text.ToUpperInvariant(),
            FontFamily = Fonts.ChromeFamily,
            FontSize = TypeScale.Caption,
            FontWeight = FontWeight.Medium,
            LetterSpacing = TypeScale.Caption * 0.06,
        };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, ThemeManager.GreyKey);
        return block;
    }

    private static TextBlock Small(string text, string key)
    {
        var block = new TextBlock { Text = text, FontSize = TypeScale.FieldMessage, TextWrapping = TextWrapping.Wrap };
        ThemeInk.Bind(block, TextBlock.ForegroundProperty, key);
        return block;
    }

}

/// <summary>Asks before one of the Commander's own saved searches is forgotten.</summary>
public sealed class ForgetSearchDialog : ModalDialog
{
    public const double DialogWidth = 560;

    public const string ConfirmName = "ConfirmForgetSearch";

    public const string CancelName = "CancelForgetSearch";

    public ForgetSearchDialog(string noun, string name, Action forget)
    {
        Title = $"Forget {name}";
        Width = DialogWidth;

        var question = new TextBlock
        {
            Text = Question(name),
            FontFamily = Fonts.ProseFamily,
            FontSize = TypeScale.Body,
            TextWrapping = TextWrapping.Wrap,
        };
        question[!TextBlock.ForegroundProperty] = new DynamicResourceExtension(ThemeManager.WhiteKey);

        var confirm = new Button
        {
            Name = ConfirmName,
            Content = "FORGET",
            MinWidth = 76,
            Height = TypeScale.MinimumTarget,
            Classes = { Settings.SettingsView.DestructiveClass },
        };
        var cancel = new Button { Name = CancelName, Content = "CANCEL", MinWidth = 76, Height = TypeScale.MinimumTarget };

        confirm.Click += (_, _) =>
        {
            forget();
            Close();
        };
        cancel.Click += (_, _) => Close();

        Modal.Apply(this, $"{noun} · Yours", Title, question, [confirm, cancel]);

        Opened += (_, _) => cancel.Focus();
    }

    public static string Question(string name) =>
        $"D47 will no longer run this search when you say “{SavedSearchPhrases.Of(name).First()}”. Quick searches can't be forgotten.";
}
