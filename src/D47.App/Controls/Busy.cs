using Avalonia.Controls;

namespace D47.App.Controls;

/// <summary>
/// Says that something is working, on the affordance that was touched (Phase 12, "Anything that might
/// take a moment says it is working").
/// </summary>
public static class Busy
{
    /// <summary>How long the work has to have been running before anything is said about it.</summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(120);

    /// <summary>How long it stays once it is up.</summary>
    public static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Runs <paramref name="work"/>, shutting <paramref name="control"/> for the duration and spinning
    /// <paramref name="glyph"/> if it takes long enough to be worth saying so.
    /// </summary>
    /// <paramref name="work"/>
    /// , shutting <paramref name="control"/> for the duration and spinning <paramref name="glyph"/> if
    /// it takes long enough to be worth saying so.
    /// </paramref>
    /// <param name="glyph">Where the motion goes.</param>
    public static async Task While(Control? control, BusyGlyph? glyph, Func<Task> work)
    {
        if (control is not null)
        {
            control.IsEnabled = false;
        }

        var shownAt = default(DateTime?);

        try
        {
            var running = work();

            // Raced rather than delayed-then-checked, so fast work is not held up by the delay it is meant to
            // be exempt from.
            if (await Task.WhenAny(running, Task.Delay(Delay)) != running)
            {
                if (glyph is not null)
                {
                    glyph.IsVisible = true;
                }

                shownAt = DateTime.UtcNow;
            }

            // Awaited whichever way the race went, so a failure inside the work is the failure this call
            // reports rather than something swallowed by the race.
            await running;

            if (shownAt is { } at && Minimum - (DateTime.UtcNow - at) is { Ticks: > 0 } left)
            {
                await Task.Delay(left);
            }
        }
        finally
        {
            if (glyph is not null)
            {
                glyph.IsVisible = false;
            }

            if (control is not null)
            {
                control.IsEnabled = true;
            }
        }
    }
}
