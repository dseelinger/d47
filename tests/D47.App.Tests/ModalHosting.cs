using Avalonia.Controls;
using Avalonia.VisualTree;
using D47.App.Controls;
using D47.App.Windowing;

namespace D47.App.Tests;

/// <summary>Puts a modal on screen by itself, for a test that is about the modal and not the panel.</summary>
public static class ModalHosting
{
    /// <summary>Shows the modal over an empty window of its own, closed when the modal closes.</summary>
    public static Window Show(this ModalDialog dialog, double width = 820, double height = 900)
    {
        var host = new Window { Content = new Border(), Width = width, Height = height };

        dialog.Closed += (_, _) => host.Close();
        host.Show();
        _ = dialog.Over(host);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        return host;
    }

    /// <summary>The modals open over <paramref name="window"/>, newest last.</summary>
    public static IEnumerable<ModalDialog> Modals(this Window window) =>
        window.GetVisualDescendants().OfType<ModalDialog>();
}
