namespace D47.Core.Input;

/// <summary>One step, as it left the injector (#365).</summary>
/// <param name="Index">Where the step sat in the sequence, from zero.</param>
/// <param name="Step">The step itself, including its capture mark.</param>
/// <param name="At">When it was sent.</param>
/// <param name="Foreground">Whether Elite held the foreground at that instant.</param>
public readonly record struct InputStepReport(
    int Index,
    InputStep Step,
    DateTimeOffset At,
    bool Foreground);

/// <summary>One named piece of work being watched, from its first key to its verdict (#365).</summary>
public interface IInputTrace : IDisposable
{
    /// <summary>One step, reported by the injector after it was sent.</summary>
    void Stepped(InputStepReport report);

    /// <summary>
    /// Something the caller offers as its own evidence of what the sequence did — the plot's route
    /// stamp and last hop, a launch's docked flag.
    /// </summary>
    void Declare(string name, string value);

    /// <summary>How it ended, and why.</summary>
    void Verdict(string verdict, string reason);
}

/// <summary>Makes a trace when one has been asked for.</summary>
public interface IInputStepObserver
{
    /// <summary>
    /// What a sequence with no caller of its own is traced as — the launch walk, a Commander's macro,
    /// an autonomous honk.
    /// </summary>
    public const string Anonymous = "unnamed";

    /// <summary>Opens a trace for one named caller.</summary>
    IInputTrace Open(string caller);
}
