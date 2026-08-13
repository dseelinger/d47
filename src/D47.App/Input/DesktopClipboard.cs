using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// The Commander's clipboard, through Avalonia's own abstraction (list.md Phase 10, "Put
/// something on the clipboard").
/// <para>
/// Avalonia reaches the clipboard through a top-level window, so this needs one — and on a run
/// where the desktop window is not up, there is nothing to reach it with. That is a capability
/// being off rather than a failure: the tool says the clipboard could not be written to, and
/// the answer still contains the text.
/// </para>
/// <para>
/// Marshalled to the UI thread, because the clipboard is one and a tool handler runs on the
/// thread pool.
/// </para>
/// <para>
/// Core's clipboard contract is named in full rather than imported. Avalonia's own interface is
/// also called <c>IClipboard</c>, and <c>SetTextAsync</c> is an extension method on it — so its
/// namespace has to be imported for the call to resolve, and importing both would make the bare
/// name ambiguous.
/// </para>
/// </summary>
public sealed class DesktopClipboard(ILogger<DesktopClipboard> logger)
    : D47.Core.Capabilities.Builtin.IClipboard
{
    public async Task<bool> SetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (Avalonia.Application.Current?.ApplicationLifetime
                    is not IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
                {
                    logger.LogInformation("No window is up, so there is no clipboard to write to");
                    return false;
                }

                await clipboard.SetTextAsync(text).ConfigureAwait(true);
                return true;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Another process can hold the clipboard open, and that is somebody else's bug
            // rather than a reason for the turn to fail.
            logger.LogWarning(ex, "The clipboard could not be written to");
            return false;
        }
    }
}
