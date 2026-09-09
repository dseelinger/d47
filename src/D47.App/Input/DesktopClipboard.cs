using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// The Commander's clipboard, through Avalonia's own abstraction (Phase 10, "Put something on the
/// clipboard").
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
            // Another process can hold the clipboard open, and that is somebody else's bug rather than a
            // reason for the turn to fail.
            logger.LogWarning(ex, "The clipboard could not be written to");
            return false;
        }
    }
}
