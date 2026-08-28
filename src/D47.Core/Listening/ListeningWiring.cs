using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.Core.Listening;

/// <summary>What to do with the speech model when the listening settings are applied.</summary>
public enum SpeechModelAction
{
    /// <summary>Nothing usable is selected. Whatever is loaded is released.</summary>
    Unload,

    /// <summary>The selected model is on disk. Load it.</summary>
    Load,

    /// <summary>The selected model is not on disk yet. Fetch it, and load it when it lands.</summary>
    Fetch,
}

/// <summary>
/// What the composition root should do about the speech model, decided rather than discovered.
/// </summary>
public sealed record SpeechModelPlan
{
    public required SpeechModelAction Action { get; init; }

    /// <summary>The file to load. Set only when <see cref="Action"/> is <see cref="SpeechModelAction.Load"/>.</summary>
    public string? Path { get; init; }

    /// <summary>The model to load or fetch. Null only when unloading.</summary>
    public WhisperModel? Model { get; init; }

    public bool UseGpu { get; init; }
}

/// <summary>
/// The two decisions the listening settings turn into: what happens to the speech model, and
/// whether the microphone is opened at all.
/// <para>
/// Lifted out of <c>AppHost.ApplyListeningSettings</c>, which is a hundred and twenty lines of
/// device handles, file paths and native loads with these two questions threaded through it —
/// unreachable by a test, because answering them needed a microphone, a model file and a
/// keyboard hook. Neither of them needs any of those; both are functions of settings and of what
/// is on disk (Phase 19, "Give the composition root a test harness").
/// </para>
/// <para>
/// Pure, and deliberately in Core. The root still opens the device, loads the native model and
/// starts the download — it just no longer decides <em>whether</em>.
/// </para>
/// </summary>
public static class ListeningWiring
{
    /// <summary>
    /// Which of the three things happens to the speech model.
    /// <para>
    /// The order is the one the root already followed and the one the failure modes want: a model
    /// that is present is loaded, a model that is selected but absent is fetched rather than
    /// offered — the offer was a step between a fresh install and a working microphone — and
    /// anything else releases what is held. The selection is never rewritten, so a row does not
    /// drop to none because a file has not arrived yet.
    /// </para>
    /// </summary>
    public static SpeechModelPlan PlanModel(ListeningSettings listening, IModelStore models)
    {
        ArgumentNullException.ThrowIfNull(listening);
        ArgumentNullException.ThrowIfNull(models);

        if (WhisperModels.Find(listening.Model) is { } model && models.PathOf(model) is { } path)
        {
            return new SpeechModelPlan
            {
                Action = SpeechModelAction.Load,
                Path = path,
                Model = model,
                UseGpu = listening.UseGpu,
            };
        }

        if (WhisperModels.AwaitingDownload(listening.Model, models) is { } wanted)
        {
            return new SpeechModelPlan
            {
                Action = SpeechModelAction.Fetch,
                Model = wanted,
                UseGpu = listening.UseGpu,
            };
        }

        return new SpeechModelPlan { Action = SpeechModelAction.Unload };
    }

    /// <summary>
    /// Whether to open the input device.
    /// <para>
    /// No key bound and nothing that opens the gate by itself means no microphone: d47 holding an
    /// input device it will never read from is exactly the surprise the unset default exists to
    /// avoid. Hands-free with no key is a legitimate configuration and does open it.
    /// </para>
    /// </summary>
    /// <param name="keyBound">Whether the push-to-talk key actually bound. Answered by the root, which owns the hook.</param>
    public static bool NeedsMicrophone(string? mode, bool keyBound) =>
        keyBound || ListeningCapability.IsHandsFree(mode);

    /// <summary>
    /// What d47 answers to (Phase 13).
    /// <para>
    /// Empty outside wake-word mode, which is what makes the policy inert rather than every
    /// caller having to know which mode is in force: with no phrases the gate admits everything,
    /// and admitting everything is precisely what continuous listening means.
    /// </para>
    /// <para>
    /// An unset row means "the name the Commander gave their ship's AI", which is why the ship
    /// name is a parameter rather than a constant — a wake word that goes on being the previous
    /// core's name after a switch is a wake word that stops working for a reason nothing on
    /// screen explains.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> WakePhrases(string? mode, string? spelled, string shipName)
    {
        if (!string.Equals(mode, ListeningCapability.WakeMode, StringComparison.Ordinal))
        {
            return [];
        }

        return spelled is { Length: > 0 } written
            ? [.. written.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]
            : [shipName];
    }
}
