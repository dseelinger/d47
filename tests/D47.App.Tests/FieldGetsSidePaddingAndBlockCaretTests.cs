using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>The field's side padding, Grey2 placeholder and block caret (#350).</summary>
public class FieldGetsSidePaddingAndBlockCaretTests
{
    private static (Window Window, TextBox Box) Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        // Not in HeadlessApp — it stands in for App.axaml but leaves the control kit out — so the
        // TextBox theme this test exercises has to come from the real file.
        Application.Current!.Styles.Add(
            new Avalonia.Markup.Xaml.Styling.StyleInclude((Uri?)null)
            {
                Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml"),
            });

        var box = new TextBox { PlaceholderText = "Ask something" };
        var window = new Window { Content = box, Width = 400, Height = 200 };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return (window, box);
    }

    [AvaloniaFact]
    public void PaddingIsThirteenHorizontalAndPlaceholderIsGrey2()
    {
        var (_, box) = Open();

        Assert.Equal(new Thickness(13, 0), box.Padding);

        var resources = Application.Current!.Resources;
        Assert.Equal(resources[ThemeManager.Grey2Key], box.PlaceholderForeground);
    }

    [AvaloniaFact]
    public void TheTextPresenterCaretIsTransparentAndABlockCaretTracksIt()
    {
        var (_, box) = Open();

        var presenter = box.GetVisualDescendants().OfType<TextPresenter>().Single();
        Assert.Equal(Avalonia.Media.Brushes.Transparent, presenter.CaretBrush);

        var caret = box.GetVisualDescendants().OfType<BlockCaret>().Single();
        Assert.Same(presenter, caret.Target);

        var resources = Application.Current!.Resources;
        Assert.Equal(resources[ThemeManager.AKey], caret.Brush);
        Assert.Equal(TimeSpan.FromSeconds(1.1), caret.Interval);
    }

    [AvaloniaFact]
    public void TheBlockCaretIsActiveOnlyWhileTheFieldHasFocus()
    {
        var (_, box) = Open();
        var caret = box.GetVisualDescendants().OfType<BlockCaret>().Single();

        Assert.False(caret.IsActive);

        box.Focus();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(caret.IsActive);
    }
}
