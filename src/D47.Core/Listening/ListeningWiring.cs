using D47.Core.Capabilities.Builtin;
using D47.Core.Configuration;

namespace D47.Core.Listening;

/// <summary>What to do with the speech model when the listening settings are applied.</summary>
public enum SpeechModelAction
{
    /// <summary>Nothing usable is selected.</summary>
    Unload,

    /// <summary>The selected model is on disk.</summary>
    Load,

    /// <summary>The selected model is not on disk yet.</summary>
    Fetch,
}

/// <summary>What the composition root should do about the speech model, decided rather than discovered.</summary>
public sealed record SpeechModelPlan
{
    public required SpeechModelAction Action { get; init; }

    /// <summary>The file to load.</summary>
    public string? Path { get; init; }

    /// <summary>The model to load or fetch.</summary>
    public WhisperModel? Model { get; init; }

    /// <summary>Whether the loader should offload to the GPU.</summary>
    public bool UseGpu { get; init; }
}

/// <summary>
/// The two decisions the listening settings turn into: what happens to the speech model, and whether
/// the microphone is opened at all.
/// </summary>
public static class ListeningWiring
{
    /// <summary>Which of the three things happens to the speech model.</summary>
    public static SpeechModelPlan PlanModel(ListeningSettings listening, IModelStore models)
    {
        ArgumentNullException.ThrowIfNull(listening);
        ArgumentNullException.ThrowIfNull(models);

        // Adopted rather than looked up raw: a settings file naming a retired multilingual model resolves to
        // its English twin here (#187).
        var selected = WhisperModels.AdoptedId(listening.Model);

        if (WhisperModels.Find(selected) is { } model && models.PathOf(model) is { } path)
        {
            return new SpeechModelPlan
            {
                Action = SpeechModelAction.Load,
                Path = path,
                Model = model,
                UseGpu = listening.UseGpu,
            };
        }

        if (WhisperModels.AwaitingDownload(selected, models) is { } wanted)
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

    /// <summary>Whether to open the input device.</summary>
    /// <param name="keyBound">Whether the push-to-talk key actually bound.</param>
    public static bool NeedsMicrophone(string? mode, bool keyBound) =>
        keyBound || ListeningCapability.IsHandsFree(mode);

    /// <summary>What d47 answers to (Phase 13).</summary>
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
