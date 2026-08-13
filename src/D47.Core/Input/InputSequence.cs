namespace D47.Core.Input;

public enum InputStepKind
{
    KeyDown,
    KeyUp,
    MouseDown,
    MouseUp,

    /// <summary>A run of literal characters, for the one place d47 sends text rather than keys.</summary>
    Text,

    /// <summary>Wait. Elite reads held inputs, so a tap that is too short is a tap it misses.</summary>
    Delay,
}

/// <summary>
/// One thing to send. Deliberately a description rather than a call: composing the sequence is
/// arithmetic and belongs where it can be asserted, and only the last step out of the process
/// needs to be in the app (architecture.md §8, "Injector in dry-run mode — key sequences
/// asserted, nothing sent").
/// </summary>
/// <param name="Kind">What sort of step.</param>
/// <param name="Code">A virtual-key code, or a one-based mouse button. Unused otherwise.</param>
/// <param name="Extended">
/// Whether the scancode needs the extended-key prefix. Carried on the step because the symbol
/// it came from is gone by the time anything sends it, and the difference between the arrow
/// keys and the numpad is this bit alone.
/// </param>
/// <param name="Text">The characters, for <see cref="InputStepKind.Text"/>.</param>
/// <param name="Delay">How long to wait, for <see cref="InputStepKind.Delay"/>.</param>
public readonly record struct InputStep(
    InputStepKind Kind,
    uint Code = 0,
    bool Extended = false,
    string? Text = null,
    TimeSpan Delay = default)
{
    public static InputStep Wait(TimeSpan duration) => new(InputStepKind.Delay, Delay: duration);

    public static InputStep Type(string text) => new(InputStepKind.Text, Text: text);

    public override string ToString() => Kind switch
    {
        InputStepKind.Text => $"Text({Text})",
        InputStepKind.Delay => $"Delay({Delay.TotalMilliseconds:0}ms)",
        InputStepKind.MouseDown or InputStepKind.MouseUp => $"{Kind}({Code})",
        _ => $"{Kind}(0x{Code:X2}{(Extended ? ",ext" : string.Empty)})",
    };
}

/// <summary>
/// Turning a binding into the exact sequence of presses that performs it.
/// <para>
/// Pure, and separate from anything that sends: the ordering rules here are the part that is
/// easy to get wrong and impossible to see going wrong. A modifier released before the key it
/// modifies produces a keystroke the Commander did not ask for, and the only symptom is the
/// game doing something else.
/// </para>
/// </summary>
public static class InputSequence
{
    /// <summary>
    /// How long a key is held for a tap. Elite samples input per frame, so a press and release
    /// inside one frame is a press it never sees — the failure looks like a key that works
    /// four times in five.
    /// </summary>
    public static readonly TimeSpan TapHold = TimeSpan.FromMilliseconds(60);

    /// <summary>Presses and releases a binding, modifiers included.</summary>
    public static IReadOnlyList<InputStep> Tap(EliteBinding binding) => Hold(binding, TapHold);

    /// <summary>
    /// Presses a binding, waits, and releases it. Modifiers go down before the key and come up
    /// after it, in reverse order — the same discipline a person's hand follows.
    /// </summary>
    public static IReadOnlyList<InputStep> Hold(EliteBinding binding, TimeSpan duration)
    {
        var key = EliteKeys.Resolve(binding.Key);

        if (!key.IsPressable)
        {
            // Not an exception: an unpressable binding is a state the caller already asked
            // about through ActionReachability, and this is the belt to that braces.
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

        // Reverse order on the way up. Releasing Control before X while X is still down sends a
        // bare X to whatever reads it next.
        foreach (var modifier in modifiers.Reverse())
        {
            steps.Add(new InputStep(InputStepKind.KeyUp, modifier.Key.Code, EliteKeys.IsExtended(modifier.Symbol)));
        }

        return steps;
    }

    /// <summary>
    /// Everything a sequence pressed, so it can be let go of unconditionally. Order is the
    /// reverse of the order things went down, and a key that went down twice appears once.
    /// </summary>
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
