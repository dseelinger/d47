using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// A tab's label draws without a clip, selected or not. Clipped, the selected label lost its last
/// letter on a 150% desktop while headless rendering drew it whole, so this checks the clip rather than
/// the pixels.
/// </summary>
public class ATabLabelIsNeverClippedTests
{
    [AvaloniaTheory]
    [InlineData(true, 924, 640)]
    [InlineData(false, 924, 640)]
    [InlineData(true, 512, 280)]
    [InlineData(false, 512, 280)]
    public void TheLabelDoesNotClipToItsBounds(bool selected, double width, double height)
    {
        var include = new ResourceInclude((Uri?)null) { Source = new Uri("avares://d47/Panel/PanelTabs.axaml") };
        Application.Current!.Resources.MergedDictionaries.Add(include);

        try
        {
            var theme = (ControlTheme)Application.Current!.FindResource("D47.Tab")!;
            var tab = new RadioButton { Theme = theme, Content = "TRANSCRIPT", IsChecked = selected };

            var window = new Window { Content = new WrapPanel { Children = { tab } }, Width = width, Height = height };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var label = tab.GetVisualDescendants().OfType<TextBlock>().Single();
            Assert.False(label.ClipToBounds);

            window.Close();
        }
        finally
        {
            Application.Current!.Resources.MergedDictionaries.Remove(include);
        }
    }
}
