using Avalonia.Controls;

namespace D47.App.Panel;

/// <summary>A page that supplies part of the panel's chrome while it is the one showing.</summary>
public interface IPageChrome
{
    /// <summary>Drawn in the page bar beside the search field, or null for nothing.</summary>
    Control? BarTool { get; }

    /// <summary>The capability the panel's HELP opens while this page shows, or null for the tab's own.</summary>
    string? HelpTopic { get; }
}
