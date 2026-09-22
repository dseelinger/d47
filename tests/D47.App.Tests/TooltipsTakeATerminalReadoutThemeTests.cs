using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App.Theming;
using D47.Core.Interface;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// Every tooltip draws square, opaque, shadowless and un-Fluent (#381): fill-3 ground, a 1px rule
/// border, no corner radius, no bloom, opening 600ms after the pointer settles, below and
/// left-aligned with the control it explains.
///
/// HeadlessApp does not merge ControlKitTheme.axaml the way App.axaml does (that gap predates this
/// issue and is outside it), so each test merges it onto Application.Current for its own duration
/// only — the same resource the tooltip's own production code resolves from, not a copy built for
/// the test.
/// </summary>
public class TooltipsTakeATerminalReadoutThemeTests
{
    private static (Window Window, Button Button) Open()
    {
        new ThemeManager(Application.Current!, NullLogger<ThemeManager>.Instance).Apply(ThemeCatalog.Elite);

        Application.Current!.Styles.Add(
            new StyleInclude((Uri?)null) { Source = new Uri("avares://d47/Theming/ControlKitTheme.axaml") });

        var button = new Button { Content = "Something" };
        ToolTip.SetTip(button, "Explains the thing");

        var window = new Window { Content = button, Width = 400, Height = 400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, button);
    }

    [AvaloniaFact]
    public void EveryControlTakesTheSixHundredMillisecondDelayAndBottomLeftPlacement()
    {
        var (_, button) = Open();

        Assert.Equal(600, ToolTip.GetShowDelay(button));
        Assert.Equal(PlacementMode.BottomEdgeAlignedLeft, ToolTip.GetPlacement(button));
        Assert.Equal(8d, ToolTip.GetVerticalOffset(button));
    }

    [AvaloniaFact]
    public void TheOpenTooltipDrawsAnOpaqueGroundARuleBorderAndSquareCorners()
    {
        var (window, button) = Open();

        ToolTip.SetIsOpen(button, true);
        Dispatcher.UIThread.RunJobs();

        var tip = window.GetVisualDescendants().OfType<ToolTip>().Single();
        var resources = Application.Current!.Resources;

        Assert.Equal(resources[ThemeManager.FillHigherKey], tip.Background);
        Assert.Equal(resources[ThemeManager.RuleKey], tip.BorderBrush);
        Assert.Equal(new Thickness(1), tip.BorderThickness);
        Assert.Equal(new CornerRadius(0), tip.CornerRadius);
        Assert.Equal(new Thickness(12, 8), tip.Padding);
        Assert.Equal(280d, tip.MaxWidth);
        Assert.Equal(TypeScale.Tip, tip.FontSize);
        Assert.Equal(resources[ThemeManager.TextKey], tip.Foreground);

        var border = tip.GetVisualDescendants().OfType<Border>().First();
        Assert.Null(border.Effect);
        Assert.Equal(default, border.BoxShadow);
    }

    /// <summary>The delay is set once for every control rather than at each call site (#381).</summary>
    [Fact]
    public void NoCallSiteSetsItsOwnShowDelay()
    {
        var root = RepositoryRoot();
        var source = Path.Combine(root, "src", "D47.App");
        var sightings = new List<string>();

        foreach (var path in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            var tree = CSharpSyntaxTree.ParseText(
                File.ReadAllText(path), path: path, cancellationToken: TestContext.Current.CancellationToken);

            var syntaxRoot = tree.GetRoot(TestContext.Current.CancellationToken);

            foreach (var invocation in syntaxRoot.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is MemberAccessExpressionSyntax
                    {
                        Expression: IdentifierNameSyntax { Identifier.ValueText: "ToolTip" },
                        Name.Identifier.ValueText: "SetShowDelay",
                    })
                {
                    var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    sightings.Add($"{Path.GetRelativePath(root, path).Replace('\\', '/')}:{line}");
                }
            }
        }

        Assert.True(
            sightings.Count == 0,
            "ToolTip.SetShowDelay is set at a call site rather than once for every control (#381):\n"
            + string.Join(Environment.NewLine, sightings));
    }

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
