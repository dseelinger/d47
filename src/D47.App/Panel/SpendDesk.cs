namespace D47.App.Panel;

/// <summary>
/// What a surface needs to show the SPEND row: the dialog over its own window, given the panel's way to open a
/// page, and the figures.
/// </summary>
public sealed record SpendDesk(
    Func<Avalonia.Controls.Window, Func<Controls.DialogPage, Task>, Task> Show,
    Func<decimal> Session,
    Func<string> Detail);
