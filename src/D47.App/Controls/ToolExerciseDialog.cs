using Avalonia;
using Avalonia.Controls;
using D47.App.Coverage;
using D47.App.Theming;

namespace D47.App.Controls;

/// <summary>How to exercise a tool or settings row, opened by Go on the Features page.</summary>
public sealed class ToolExerciseDialog : ModalDialog
{
    public const string CloseName = "ExerciseClose";

    public const string GoThereName = "ExerciseGoThere";

    public const string HideName = "ExerciseHide";

    public const string Unwritten = "Steps for exercising this are not written yet.";

    /// <param name="goThere">When given, a GO THERE button closes the dialog and calls it.</param>
    /// <param name="hide">When given, a HIDE button closes the dialog and calls it.</param>
    public ToolExerciseDialog(ToolExercise tool, string mark, Action? goThere = null, Action? hide = null)
    {
        Title = tool.Name;

        var body = new StackPanel { Spacing = Modal.BlockGap };

        if (tool.Steps is { Length: > 0 } steps)
        {
            var text = new TextBlock
            {
                Name = "ExerciseSteps",
                Text = steps,
                FontFamily = Fonts.ProseFamily,
                FontSize = TypeScale.Body,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
            ThemeInk.Bind(text, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);
            body.Children.Add(text);
        }
        else
        {
            var what = new TextBlock
            {
                Text = tool.Description,
                FontSize = TypeScale.Body,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
            ThemeInk.Bind(what, TextBlock.ForegroundProperty, ThemeManager.WhiteKey);

            var hint = new TextBlock
            {
                Name = "ExerciseUnwritten",
                Text = Unwritten,
                FontSize = TypeScale.Small,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            };
            ThemeInk.Bind(hint, TextBlock.ForegroundProperty, ThemeManager.GreyKey);

            body.Children.Add(StatTile.Tile("What it does", what));
            body.Children.Add(hint);
        }

        var close = new Button { Name = CloseName, Content = "CLOSE", MinWidth = 76, Height = TypeScale.MinimumTarget };
        close.Click += (_, _) => Close();

        var buttons = new List<Control> { close };

        if (goThere is not null)
        {
            buttons.Add(Then(GoThereName, "GO THERE", goThere));
        }

        if (hide is not null)
        {
            buttons.Add(Then(HideName, "HIDE", hide));
        }

        Modal.Apply(this, "Features", tool.Name, body, buttons, StatTile.Build(mark, tool.CapabilityName, StatInk.Name));

        Opened += (_, _) => close.Focus();
    }

    /// <summary>A button that closes the dialog, then does its action.</summary>
    private Button Then(string name, string label, Action action)
    {
        var button = new Button { Name = name, Content = label, MinWidth = 76, Height = TypeScale.MinimumTarget };

        button.Click += (_, _) =>
        {
            Close();
            action();
        };

        return button;
    }
}
