namespace D47.Core.Input;

public enum InputStepKind
{
    KeyDown,
    KeyUp,
    MouseDown,
    MouseUp,

    /// <summary>A run of literal characters, for the one place d47 sends text rather than keys.</summary>
    Text,

    /// <summary>Wait.</summary>
    Delay,
}

/// <summary>One thing to send.</summary>
/// <param name="Kind">What sort of step.</param>
/// <param name="Code">A virtual-key code, or a one-based mouse button.</param>
/// <param name="Extended">Whether the scancode needs the extended-key prefix.</param>
/// <param name="Text">The characters, for <see cref="InputStepKind.Text"/>.</param>
/// <param name="Delay">How long to wait, for <see cref="InputStepKind.Delay"/>.</param>
/// <param name="Capture">Whether this is a step worth seeing (#365).</param>
/// <param name="Mark">What to call that still.</param>
public readonly record struct InputStep(
    InputStepKind Kind,
    uint Code = 0,
    bool Extended = false,
    string? Text = null,
    TimeSpan Delay = default,
    bool Capture = false,
    string? Mark = null)
{
    public static InputStep Wait(TimeSpan duration) => new(InputStepKind.Delay, Delay: duration);

    public static InputStep Type(string text) => new(InputStepKind.Text, Text: text);

    /// <summary>The same step, marked as one to take a still after (#365).</summary>
    public InputStep Watched(string mark) => this with { Capture = true, Mark = mark };

    public override string ToString() => Kind switch
    {
        InputStepKind.Text => $"Text({Text})",
        InputStepKind.Delay => $"Delay({Delay.TotalMilliseconds:0}ms)",
        InputStepKind.MouseDown or InputStepKind.MouseUp => $"{Kind}({Code})",
        _ => $"{Kind}(0x{Code:X2}{(Extended ? ",ext" : string.Empty)})",
    };
}

/// <summary>Turning a binding into the exact sequence of presses that performs it.</summary>
public static class InputSequence
{
    /// <summary>How long a key is held for a tap.</summary>
    public static readonly TimeSpan TapHold = TimeSpan.FromMilliseconds(60);

    /// <summary>Presses and releases a binding, modifiers included.</summary>
    public static IReadOnlyList<InputStep> Tap(EliteBinding binding) => Hold(binding, TapHold);

    /// <summary>Presses a binding, waits, and releases it.</summary>
    public static IReadOnlyList<InputStep> Hold(EliteBinding binding, TimeSpan duration)
    {
        var key = EliteKeys.Resolve(binding.Key);

        if (!key.IsPressable)
        {
            // Not an exception: an unpressable binding is a state the caller already asked about through
            // ActionReachability, and this is the belt to that braces.
            return [];
        }

        var modifiers = binding.Modifiers
            .Select(symbol => (Symbol: symbol, Key: EliteKeys.Resolve(symbol)))
            .Where(modifier => modifier.Key.Kind == EliteInputKind.Keyboard)
            .ToArray();

        var steps = new List<InputStep>(modifiers.Length * 2 + 3);

        foreach (var modifier in modifiers)
        {
            steps.Add(new InputStep(InputStepKind.KeyDown, modifier.Key.Code, EliteKeys.IsExtended(modifier.Symbol)));
        }

        var extended = EliteKeys.IsExtended(binding.Key);
        var down = key.Kind == EliteInputKind.Mouse ? InputStepKind.MouseDown : InputStepKind.KeyDown;
        var up = key.Kind == EliteInputKind.Mouse ? InputStepKind.MouseUp : InputStepKind.KeyUp;

        steps.Add(new InputStep(down, key.Code, extended));

        if (duration > TimeSpan.Zero)
        {
            steps.Add(InputStep.Wait(duration));
        }

        steps.Add(new InputStep(up, key.Code, extended));

        // Reverse order on the way up.
        foreach (var modifier in modifiers.Reverse())
        {
            steps.Add(new InputStep(InputStepKind.KeyUp, modifier.Key.Code, EliteKeys.IsExtended(modifier.Symbol)));
        }

        return steps;
    }

    /// <summary>
    /// The same run with its last step marked for a still (#365) — the way a caller asks to see what a
    /// hold landed on, since the moment worth looking at is after the key comes up and the run's own
    /// last step is where that is.
    /// </summary>
    public static IReadOnlyList<InputStep> WatchedAfter(IReadOnlyList<InputStep> steps, string mark)
    {
        ArgumentNullException.ThrowIfNull(steps);

        if (steps.Count == 0)
        {
            return steps;
        }

        var marked = new List<InputStep>(steps);
        marked[^1] = marked[^1].Watched(mark);

        return marked;
    }

    /// <summary>Everything a sequence pressed, so it can be let go of unconditionally.</summary>
    public static IReadOnlyList<InputStep> ReleaseFor(IEnumerable<InputStep> steps)
    {
        var held = new List<InputStep>();

        foreach (var step in steps)
        {
            switch (step.Kind)
            {
                case InputStepKind.KeyDown:
                    Hold(new InputStep(InputStepKind.KeyUp, step.Code, step.Extended));
                    break;
                case InputStepKind.MouseDown:
                    Hold(new InputStep(InputStepKind.MouseUp, step.Code));
                    break;
                case InputStepKind.KeyUp:
                    held.RemoveAll(existing => existing.Kind == InputStepKind.KeyUp && existing.Code == step.Code);
                    break;
                case InputStepKind.MouseUp:
                    held.RemoveAll(existing => existing.Kind == InputStepKind.MouseUp && existing.Code == step.Code);
                    break;
            }
        }

        held.Reverse();
        return held;

        void Hold(InputStep release)
        {
            if (!held.Any(existing => existing.Kind == release.Kind && existing.Code == release.Code))
            {
                held.Add(release);
            }
        }
    }
}
