using D47.Core.Actions;
using Microsoft.Extensions.Logging;

namespace D47.App.Input;

/// <summary>
/// One docking request's view of the journal (#150): how many <c>DockingRequested</c> events the tick
/// loop had counted when the walk began, against how many it has counted since.
/// </summary>
public sealed class DockingRequestWatch(Func<int> requests, ILogger logger) : IDockingWatch
{
    /// <summary>How long to wait for the event.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(6);

    private readonly int _before = requests();

    public async Task<bool?> ConfirmAsync(CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.Now;
        var deadline = started + Patience;

        while (DateTimeOffset.Now < deadline)
        {
            if (requests() != _before)
            {
                logger.LogInformation(
                    "A docking request reached the journal after {Elapsed:0.0}s",
                    (DateTimeOffset.Now - started).TotalSeconds);

                return true;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        logger.LogInformation(
            "No docking request reached the journal within {Seconds:0}s",
            Patience.TotalSeconds);

        return false;
    }
}
