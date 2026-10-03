namespace D47.App.Panel;

/// <summary>A page that gives the panel's title line its one-line summary while it is the one showing.</summary>
public interface IPageSummary
{
    /// <summary>The summary, or empty for none.</summary>
    string Summary { get; }

    /// <summary>Raised when <see cref="Summary"/> changes.</summary>
    event EventHandler? SummaryChanged;
}
