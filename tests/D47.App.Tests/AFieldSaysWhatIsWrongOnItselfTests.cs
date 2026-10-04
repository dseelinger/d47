using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Panel;
using D47.App.Theming;
using D47.Core.Capabilities;
using Xunit;

namespace D47.App.Tests;

/// <summary>A field outlines itself in Red or Warn and writes its message under itself.</summary>
public sealed class AFieldSaysWhatIsWrongOnItselfTests
{
    [AvaloniaFact]
    public void AnErrorOutlinesTheFieldInRedAndWritesUnderIt()
    {
        using var look = AppLook.Put();

        var box = new TextBox { Width = 260 };
        var window = Show(box);

        FieldMessage.ShowError(box, "Name a system first.");
        box.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Ink(ThemeManager.RedKey), Colour(box.BorderBrush));
        Assert.Equal(Ink(ThemeManager.RedKey), Colour(Text(box).Foreground));
        Assert.Equal(Ink(ThemeManager.RedKey), Colour(Bar(box).Fill));
        Assert.Equal("Name a system first.", Text(box).Text);
        Assert.Equal(TypeScale.FieldMessage, Text(box).FontSize);
        Assert.Equal(3, Bar(box).Width);
        Assert.True(Message(box).IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void AWarningOutlinesTheFieldInWarn()
    {
        using var look = AppLook.Put();

        var box = new TextBox { Width = 260 };
        var window = Show(box);

        FieldMessage.ShowWarning(box, "That key could not be checked.");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Ink(ThemeManager.WarnKey), Colour(box.BorderBrush));
        Assert.Equal(Ink(ThemeManager.WarnKey), Colour(Text(box).Foreground));
        Assert.Equal(Ink(ThemeManager.WarnKey), Colour(Bar(box).Fill));

        window.Close();
    }

    [AvaloniaFact]
    public void TypingClearsTheMessage()
    {
        using var look = AppLook.Put();

        var box = new TextBox { Width = 260 };
        var window = Show(box);

        FieldMessage.ShowError(box, "Name a system first.");
        Dispatcher.UIThread.RunJobs();

        box.Text = "Colonia";
        Dispatcher.UIThread.RunJobs();

        Assert.False(Message(box).IsVisible);
        Assert.Equal(Ink(ThemeManager.AKey), Colour(box.BorderBrush));

        window.Close();
    }

    [AvaloniaFact]
    public void AFormFieldIsFortyFourTall()
    {
        using var look = AppLook.Put();

        var field = new FormField("Destination", "a system", FieldNeed.Required);
        var window = Show(field.Control);

        Assert.Equal(TypeScale.MinimumTarget, field.Box.Bounds.Height);

        window.Close();
    }

    [AvaloniaFact]
    public void ASearchFieldIsLine2UntilFocusedThenA()
    {
        using var look = AppLook.Put();

        var box = new TextBox { Width = 340, VerticalAlignment = VerticalAlignment.Top, Classes = { FieldMessage.SearchClass } };
        var window = Show(box);

        Assert.Equal(Ink(ThemeManager.Line2Key), Colour(box.BorderBrush));
        Assert.Equal(TypeScale.Search, box.FontSize);
        Assert.Equal(TypeScale.MinimumTarget, box.Bounds.Height);

        box.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(Ink(ThemeManager.AKey), Colour(box.BorderBrush));

        window.Close();
    }

    [AvaloniaFact]
    public void ThePageSearchIsASearchField()
    {
        var panel = new PanelView { DataContext = new PanelViewModel() };
        var search = panel.FindControl<TextBox>("SearchInput")!;

        Assert.Contains(FieldMessage.SearchClass, search.Classes);
    }

    [AvaloniaFact]
    public void TheFieldStatesAreCaptured()
    {
        using var look = AppLook.Put();

        var error = new TextBox { Width = 260, PlaceholderText = "a system" };
        var warning = new TextBox { Width = 260, Text = "sk-12" };
        var search = new TextBox { Width = 340, PlaceholderText = "Search this page", Classes = { FieldMessage.SearchClass } };
        var focused = new TextBox { Width = 340, Text = "Giryak", Classes = { FieldMessage.SearchClass } };

        var column = new StackPanel
        {
            Spacing = 16,
            Margin = new Thickness(24),
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { new TextBox { Width = 260, PlaceholderText = "rest" }, error, warning, search, focused },
        };

        var window = new Window
        {
            Content = column,
            Width = 480,
            Height = 420,
            Background = (IBrush)Application.Current!.Resources[ThemeManager.BgKey]!,
        };
        window.Show();

        FieldMessage.ShowError(error, "Name a system first.");
        FieldMessage.ShowWarning(warning, "That key could not be checked. Try again when you are online.");
        focused.Focus();
        Dispatcher.UIThread.RunJobs();

        using var frame = window.CaptureRenderedFrame()!;
        var path = System.IO.Path.Combine(TestSurface.CaptureDirectory, "field-states.png");
        frame.Save(path, new PngBitmapEncoderOptions());
        Assert.True(File.Exists(path));

        window.Close();
    }

    private static Window Show(Control content)
    {
        var window = new Window { Content = content, Width = 480, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return window;
    }

    private static Control Message(TextBox box) =>
        box.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "PART_Message");

    private static TextBlock Text(TextBox box) =>
        box.GetVisualDescendants().OfType<TextBlock>().Single(block => block.Name == "PART_MessageText");

    private static Rectangle Bar(TextBox box) =>
        box.GetVisualDescendants().OfType<Rectangle>().Single(rectangle => rectangle.Name == "PART_MessageBar");

    private static Color? Colour(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    private static Color Ink(string key) =>
        ((ISolidColorBrush)Application.Current!.FindResource(key)!).Color;
}
