using Avalonia.Controls;

namespace D47.App.Controls;

/// <summary>
/// Says that something is working, on the affordance that was touched
/// (list.md Phase 12, "Anything that might take a moment says it is working").
/// <para>
/// One line at a call site:
/// <code>await Busy.While(control, glyph, async () =&gt; …);</code>
/// Written as something to opt into rather than as a pattern to reimplement, because the next
/// long operation is the one that will ship without it — and because the two numbers below have
/// to be the same two numbers everywhere or the surface reads as several applications.
/// </para>
/// <para>
/// It holds off two failure modes at once. <b>No silence on the slow case</b>: a network call
/// that takes two seconds with nothing moving reads as a control that did not take the click,
/// and the Commander clicks it again. <b>No flash on the fast one</b>: an operation that
/// finishes in 40 ms must not strobe, so nothing is shown until the work has been running for
/// <see cref="Delay"/>, and once something is shown it stays for at least <see cref="Minimum"/>.
/// </para>
/// <para>
/// The control shuts while it works, as the picker button's hand-rolled version already did, so
/// a second click cannot queue a second copy of the same fetch. It is reopened in a
/// <c>finally</c>: an operation that throws on its way must not leave the row unusable.
/// </para>
/// </summary>
public static class Busy
{
    /// <summary>
    /// How long the work has to have been running before anything is said about it. Below this,
    /// a glyph appears and vanishes inside one blink, which reads as a glitch rather than as
    /// progress.
    /// </summary>
    public static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(120);

    /// <summary>
    /// How long it stays once it is up. Without this, work that runs 130 ms shows a spinner for
    /// 10 ms — which is the flash the delay above was avoiding, moved to the other end.
    /// </summary>
    public static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Runs <paramref name="work"/>, shutting <paramref name="control"/> for the duration and
    /// spinning <paramref name="glyph"/> if it takes long enough to be worth saying so.
    /// </summary>
    /// <param name="control">
    /// The affordance that was touched — the tab, the button, the row. Never a spinner somewhere
    /// else on the surface: a Commander who clicked here does not look there. Null where there
    /// is nothing to shut.
    /// </param>
    /// <param name="glyph">Where the motion goes. Null where the caller shows it another way.</param>
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

            // Raced rather than delayed-then-checked, so fast work is not held up by the delay
            // it is meant to be exempt from.
            if (await Task.WhenAny(running, Task.Delay(Delay)) != running)
            {
                if (glyph is not null)
                {
                    glyph.IsVisible = true;
                }

                shownAt = DateTime.UtcNow;
            }

            // Awaited whichever way the race went, so a failure inside the work is the failure
            // this call reports rather than something swallowed by the race.
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
