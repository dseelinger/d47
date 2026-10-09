#if DEBUG
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Theming;
using D47.Core.Interface;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Control Kit has a card for each component folder under <c>design/system/components/</c>, and no
/// card states a size or a colour as text: what a card shows comes from the controls it draws.
/// </summary>
[Trait("Category", "Integration")]
public sealed partial class TheControlKitDrawsEveryDesignComponentTests
{
    [AvaloniaFact]
    public void EveryDesignComponentHasACard()
    {
        using var look = AppLook.Put();

        var kit = new ControlKitWindow { Width = 1280, Height = 1000 };
        kit.Show();
        Dispatcher.UIThread.RunJobs();

        var cards = kit.GetVisualDescendants()
            .OfType<Border>()
            .Select(border => border.Name)
            .Where(name => name?.StartsWith(ControlKitWindow.CardPrefix, StringComparison.Ordinal) == true)
            .Select(name => name![ControlKitWindow.CardPrefix.Length..])
            .ToHashSet(StringComparer.Ordinal);

        var components = Directory.GetDirectories(Path.Combine(RepositoryRoot(), "design", "system", "components"))
            .SelectMany(Directory.GetDirectories)
            .Select(Path.GetFileName)
            .ToList();

        Assert.NotEmpty(components);
        var missing = components.Where(component => !cards.Contains(component!)).ToList();
        Assert.True(missing.Count == 0, $"No card for: {string.Join(", ", missing)}");

        kit.Close();
    }

    [AvaloniaFact]
    public void NoCardStatesASizeOrAColour()
    {
        using var look = AppLook.Put();

        var kit = new ControlKitWindow { Width = 1280, Height = 1000 };
        kit.Show();
        Dispatcher.UIThread.RunJobs();

        var stated = kit.GetVisualDescendants()
            .OfType<TextBlock>()
            .Select(block => block.Text ?? string.Empty)
            .Where(text => StatedValue().IsMatch(text))
            .ToList();

        Assert.Empty(stated);

        kit.Close();
    }

    /// <summary>Captures the whole kit a window's height at a time.</summary>
    [AvaloniaTheory]
    [InlineData(ThemeCatalog.Elite)]
    [InlineData(ThemeCatalog.Light)]
    public void TheKitIsCaptured(string themeId)
    {
        using var look = AppLook.Put(themeId);

        const double Height = 1000;

        var kit = new ControlKitWindow { Width = 1280, Height = Height };
        kit.Show();
        Dispatcher.UIThread.RunJobs();

        var scroller = kit.GetVisualDescendants().OfType<ScrollViewer>().First();
        var pages = (int)Math.Ceiling(scroller.Extent.Height / Height);

        Assert.True(pages > 1);

        for (var page = 0; page < pages; page++)
        {
            scroller.Offset = new Vector(0, page * Height);
            Dispatcher.UIThread.RunJobs();

            using var frame = kit.CaptureRenderedFrame()!;
            frame.SaveCapture($"control-kit-{themeId}-{page + 1:00}.png");
        }

        kit.Close();
    }

    [GeneratedRegex(@"\d+\s*px\b|#[0-9A-Fa-f]{6}\b|\d+\s*[×x]\s*\d+", RegexOptions.IgnoreCase)]
    private static partial Regex StatedValue();

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "d47.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException(
                   $"Could not find the repository root: no d47.slnx above {AppContext.BaseDirectory}.");
    }
}
#endif
