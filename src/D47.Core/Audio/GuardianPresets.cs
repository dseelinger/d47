using D47.Core.Configuration;

namespace D47.Core.Audio;

/// <summary>A built-in Guardian voice preset: which effects it ticks, at the default order and levels.</summary>
public sealed record GuardianBuiltinPreset
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required IReadOnlyList<string> TickedIds { get; init; }
}

/// <summary>
/// The result of a preset operation: the settings to keep, or the sentence to show when it refuses.
/// Exactly one is set.
/// </summary>
public sealed record GuardianPresetChange(SpeechSettings? Settings, string? Refusal)
{
    public static GuardianPresetChange Ok(SpeechSettings settings) => new(settings, null);

    public static GuardianPresetChange Refused(string why) => new(null, why);
}

/// <summary>
/// Built-in and saved Guardian voice presets (#237): the four built-ins, matching the current effect
/// chain against them and the Commander's own saved ones, and the operations that create, update,
/// rename and delete a saved preset.
/// </summary>
public static class GuardianPresets
{
    public const string CustomId = "custom";

    private const string SavedPrefix = "saved:";

    private const int MaxNameLength = 32;

    /// <summary>The built-in presets, in the order offered.</summary>
    public static readonly IReadOnlyList<GuardianBuiltinPreset> Builtins =
    [
        new GuardianBuiltinPreset { Id = "off", Label = "Off", TickedIds = [] },
        new GuardianBuiltinPreset { Id = "vocoder", Label = "Vocoder", TickedIds = ["cylon", "chorus", "reverb"] },
        new GuardianBuiltinPreset
        {
            Id = "deep-core", Label = "Deep core", TickedIds = ["pitchDown", "octaveDown", "reverb"],
        },
        new GuardianBuiltinPreset
        {
            Id = "damaged-core", Label = "Damaged core", TickedIds = ["comb", "ringMod", "glitch"],
        },
    ];

    /// <summary>The built-in with this id, or null.</summary>
    public static GuardianBuiltinPreset? Find(string id) =>
        Builtins.FirstOrDefault(builtin => string.Equals(builtin.Id, id, StringComparison.Ordinal));

    /// <summary>The full effect chain a built-in preset sets: default order and levels, its own ticks.</summary>
    public static IReadOnlyList<GuardianVoiceEffect> EffectsFor(GuardianBuiltinPreset builtin) =>
        [.. GuardianVoice.Table.Select(effect => new GuardianVoiceEffect
        {
            Id = effect.Id,
            Ticked = builtin.TickedIds.Contains(effect.Id),
            Level = effect.DefaultLevel,
        })];

    /// <summary>
    /// The current preset: the first built-in or saved preset that matches exactly, built-ins first,
    /// then saved presets in list order; otherwise <see cref="CustomId"/>. A saved preset's id is
    /// <c>saved:&lt;name&gt;</c>.
    /// </summary>
    public static string Preset(SpeechSettings settings)
    {
        var effects = GuardianVoice.Effects(settings.GuardianVoice);

        foreach (var builtin in Builtins)
        {
            if (Matches(effects, EffectsFor(builtin)))
            {
                return builtin.Id;
            }
        }

        foreach (var saved in settings.GuardianVoice.SavedPresets ?? [])
        {
            if (Matches(effects, GuardianVoice.Effects(new GuardianVoiceSettings { Effects = saved.Effects })))
            {
                return SavedPrefix + saved.Name;
            }
        }

        return CustomId;
    }

    /// <summary>Every choice the preset row offers: the built-in ids, then a saved preset per entry, then Custom.</summary>
    public static IReadOnlyList<string> Choices(SpeechSettings settings) =>
    [
        .. Builtins.Select(builtin => builtin.Id),
        .. (settings.GuardianVoice.SavedPresets ?? []).Select(preset => SavedPrefix + preset.Name),
        CustomId,
    ];

    /// <summary>A choice's label: the built-in's, the saved preset's own name, or "Custom".</summary>
    public static string Label(string choice) =>
        Find(choice) is { } builtin
            ? builtin.Label
            : choice.StartsWith(SavedPrefix, StringComparison.Ordinal)
                ? choice[SavedPrefix.Length..]
                : "Custom";

    /// <summary>
    /// Writing the preset row: a built-in ticks exactly its effects, resets order and levels, and clears
    /// the basis; a saved preset restores its ticks, order and levels, and sets the basis to it; Custom
    /// changes nothing.
    /// </summary>
    public static SpeechSettings Select(SpeechSettings settings, string? choice)
    {
        if (Find(choice ?? "") is { } builtin)
        {
            return settings with
            {
                GuardianVoice = settings.GuardianVoice with { Effects = EffectsFor(builtin), Basis = null },
            };
        }

        if (choice is not null
            && choice.StartsWith(SavedPrefix, StringComparison.Ordinal)
            && Saved(settings, choice[SavedPrefix.Length..]) is { } preset)
        {
            return settings with
            {
                GuardianVoice = settings.GuardianVoice with
                {
                    Effects = GuardianVoice.Effects(new GuardianVoiceSettings { Effects = preset.Effects }),
                    Basis = preset.Name,
                },
            };
        }

        return settings;
    }

    /// <summary>Adds the current effects as a new saved preset, only when the current preset is Custom.</summary>
    public static GuardianPresetChange Save(SpeechSettings settings, string? name)
    {
        if (Preset(settings) != CustomId)
        {
            return GuardianPresetChange.Refused("That already matches a preset.");
        }

        if (ValidateName(settings, name) is { } refusal)
        {
            return GuardianPresetChange.Refused(refusal);
        }

        var trimmed = name!.Trim();
        var saved = (settings.GuardianVoice.SavedPresets ?? [])
            .Append(new GuardianVoicePreset { Name = trimmed, Effects = GuardianVoice.Effects(settings.GuardianVoice) })
            .ToList();

        return GuardianPresetChange.Ok(settings with
        {
            GuardianVoice = settings.GuardianVoice with { SavedPresets = saved, Basis = trimmed },
        });
    }

    /// <summary>Writes the current effects into the basis preset, only when the current preset is Custom.</summary>
    public static GuardianPresetChange Update(SpeechSettings settings)
    {
        if (Preset(settings) != CustomId)
        {
            return GuardianPresetChange.Refused("That already matches a preset.");
        }

        var saved = (settings.GuardianVoice.SavedPresets ?? []).ToList();
        var index = IndexOfBasis(settings, saved);

        if (index < 0)
        {
            return GuardianPresetChange.Refused("There is no preset to update.");
        }

        saved[index] = saved[index] with { Effects = GuardianVoice.Effects(settings.GuardianVoice) };

        return GuardianPresetChange.Ok(settings with
        {
            GuardianVoice = settings.GuardianVoice with { SavedPresets = saved },
        });
    }

    /// <summary>Renames the current preset, only when it is a saved one.</summary>
    public static GuardianPresetChange Rename(SpeechSettings settings, string? newName)
    {
        if (Preset(settings) is not { } current || !current.StartsWith(SavedPrefix, StringComparison.Ordinal))
        {
            return GuardianPresetChange.Refused("Only a saved preset can be renamed.");
        }

        var oldName = current[SavedPrefix.Length..];

        if (ValidateName(settings, newName, ignoring: oldName) is { } refusal)
        {
            return GuardianPresetChange.Refused(refusal);
        }

        var trimmed = newName!.Trim();

        if (string.Equals(trimmed, oldName, StringComparison.Ordinal))
        {
            return GuardianPresetChange.Refused("That is already its name.");
        }

        var saved = (settings.GuardianVoice.SavedPresets ?? []).ToList();
        var index = saved.FindIndex(preset => string.Equals(preset.Name, oldName, StringComparison.OrdinalIgnoreCase));
        saved[index] = saved[index] with { Name = trimmed };

        return GuardianPresetChange.Ok(settings with
        {
            GuardianVoice = settings.GuardianVoice with { SavedPresets = saved, Basis = trimmed },
        });
    }

    /// <summary>Removes the current preset, only when it is a saved one, leaving the effects as they are.</summary>
    public static GuardianPresetChange Delete(SpeechSettings settings)
    {
        if (Preset(settings) is not { } current || !current.StartsWith(SavedPrefix, StringComparison.Ordinal))
        {
            return GuardianPresetChange.Refused("Only a saved preset can be deleted.");
        }

        var name = current[SavedPrefix.Length..];
        var saved = (settings.GuardianVoice.SavedPresets ?? [])
            .Where(preset => !string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return GuardianPresetChange.Ok(settings with
        {
            GuardianVoice = settings.GuardianVoice with { SavedPresets = saved, Basis = null },
        });
    }

    /// <summary>Every effect off, default order and levels, basis cleared. Saved presets are untouched.</summary>
    public static GuardianPresetChange Reset(SpeechSettings settings) =>
        GuardianPresetChange.Ok(settings with
        {
            GuardianVoice = settings.GuardianVoice with { Effects = null, Basis = null },
        });

    private static GuardianVoicePreset? Saved(SpeechSettings settings, string name) =>
        (settings.GuardianVoice.SavedPresets ?? [])
            .FirstOrDefault(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

    private static int IndexOfBasis(SpeechSettings settings, List<GuardianVoicePreset> saved) =>
        settings.GuardianVoice.Basis is not { } basis
            ? -1
            : saved.FindIndex(preset => string.Equals(preset.Name, basis, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Trimmed, 1 to 32 characters, unique ignoring case against every built-in name, every saved name
    /// (<paramref name="ignoring"/> excepted) and "Custom". The refusal sentence, or null when the name
    /// is fine.
    /// </summary>
    private static string? ValidateName(SpeechSettings settings, string? name, string? ignoring = null)
    {
        var trimmed = (name ?? "").Trim();

        if (trimmed.Length is < 1 or > MaxNameLength)
        {
            return "Enter a name.";
        }

        var taken = Builtins.Select(builtin => builtin.Label)
            .Append("Custom")
            .Concat((settings.GuardianVoice.SavedPresets ?? []).Select(preset => preset.Name))
            .Where(existing => ignoring is null || !string.Equals(existing, ignoring, StringComparison.OrdinalIgnoreCase))
            .Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase));

        return taken ? "A preset with that name already exists." : null;
    }

    /// <summary>Whether two full effect chains are the same order, the same ticks and the same levels throughout.</summary>
    private static bool Matches(IReadOnlyList<GuardianVoiceEffect> first, IReadOnlyList<GuardianVoiceEffect> second) =>
        first.Count == second.Count
        && first.Zip(second).All(pair =>
            pair.First.Id == pair.Second.Id
            && pair.First.Ticked == pair.Second.Ticked
            && pair.First.Level == pair.Second.Level);
}
