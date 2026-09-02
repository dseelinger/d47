using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using D47.App;
using D47.App.Controls;
using D47.Core.Help;
using Xunit;

namespace D47.App.Tests;

/// <summary>
/// The Transcript's pop-up windows carry a help mark, and it goes to the site
/// (<a href="https://github.com/dseelinger/d47/issues/252">#252</a>).
/// <para>
/// <b>A dialog cannot reach the in-app help.</b> <c>HelpLevel.Open</c> works by <c>nav.Take</c>, so
/// help is a level of the panel — and these windows are shown over it with <c>ShowDialog</c>, which
/// leaves a mark inside one with no panel to navigate. The Commander's ruling was to open the site,
/// following <c>CoverageWindow</c>, which was the only pop-up in the app that had a mark at all.
/// </para>
/// <para>
/// <b>What is asserted is the address</b>, because that is the half that can silently rot: a mark
/// pointing at a page nobody wrote is a browser tab showing a 404, and no test that only looked for
/// a button would see it.
/// </para>
/// </summary>
public sealed class PopUpWindowsCarryAHelpMarkTests
{
    private static Button Mark(Window window, string name) =>
        window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    /// <summary>
    /// <b>Help improve D47</b> is the window this issue was reported against — five paragraphs of
    /// prose above the control that does the thing, and no mark anywhere on it.
    /// </summary>
    [AvaloniaFact]
    public void HelpImproveCarriesAMarkForItsOwnPage()
    {
        var window = new HelpImproveWindow(
            new DateTimeOffset(2026, 9, 1, 21, 0, 0, TimeSpan.Zero),
            _ => "an excerpt");

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var mark = Mark(window, "HelpImproveHelp");

        Assert.Equal("?", mark.Content);
        Assert.Equal(
            "https://dseelinger.github.io/d47/help-improve.html",
            DocsSite.Page(HelpImproveWindow.HelpPage));

        // The address is the tooltip, which is how a control that launches a browser says where it
        // is about to go on a window with no status line.
        Assert.Equal(DocsSite.Page(HelpImproveWindow.HelpPage), ToolTip.GetTip(mark));

        window.Close();
    }

    /// <summary>
    /// And the page it names is written, with a band. Without this the assertion above is only
    /// that two strings match each other.
    /// </summary>
    [Fact]
    public void TheHelpImprovePageExistsAndHasABand()
    {
        var article = HelpLibrary.For(HelpImproveWindow.HelpPage);

        Assert.NotNull(article);
        Assert.Equal("Help improve D47", article.Title);
        Assert.NotEmpty(article.Sections);
    }

    /// <summary>
    /// The spend receipt goes to the running totals rather than the top of the Language model
    /// page, which is long and is mostly about providers and keys.
    /// </summary>
    [Fact]
    public void TheSpendMarkNamesTheRunningTotals()
    {
        var url = DocsSite.Capability(
            D47.Core.Capabilities.Builtin.ConversationCapability.Id, "running-totals");

        Assert.Equal(
            "https://dseelinger.github.io/d47/capabilities/conversation.html#running-totals",
            url);

        // And the anchor is a heading that page actually declares — an anchor that resolves to
        // nothing lands a Commander at the top with no sign anything went wrong.
        var page = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "docs", "capabilities", "conversation.md"));

        Assert.Contains("{#running-totals}", page, StringComparison.Ordinal);
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
