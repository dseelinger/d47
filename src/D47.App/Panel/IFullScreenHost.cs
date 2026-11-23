using Avalonia.Controls;

namespace D47.App.Panel;

/// <summary>A surface that can draw one control over the whole of itself, in place of its tabs, header and page.</summary>
public interface IFullScreenHost
{
    /// <summary>Draws <paramref name="content"/> over the surface; <paramref name="leave"/> is what Escape and Back call.</summary>
    void ShowFullScreen(Control content, Action leave);

    /// <summary>Takes <paramref name="content"/> off the surface, if it is the one shown.</summary>
    void EndFullScreen(Control content);
}
