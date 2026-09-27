namespace D47.App.Controls;

/// <summary>
/// A dialog drawn as a page of the panel, opened with <see cref="Panel.PanelView.Open"/> and left
/// through its breadcrumb (#529).
/// </summary>
public abstract class DialogPage : HostedDialog
{
    /// <summary>The breadcrumb's word for this page.</summary>
    public abstract string Crumb { get; }
}
