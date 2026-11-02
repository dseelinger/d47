using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace D47.App.Tests;

internal static class PageText
{
    /// <summary>The non-empty text of <paramref name="page"/>, shown in a window of <paramref name="height"/> first if it has none.</summary>
    public static string[] Of(Control page, double height = 900)
    {
        if (page.GetVisualParent() is null)
        {
            new Window { Content = page, Width = 900, Height = height }.Show();
        }

        Dispatcher.UIThread.RunJobs();

        return [.. page.GetVisualDescendants().OfType<TextBlock>().Select(CatalogueControls.TextOf).Where(text => text.Length > 0)];
    }
}
