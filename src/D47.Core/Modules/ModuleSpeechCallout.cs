using D47.Core.Audio;
using D47.Core.Callouts;
using D47.Core.Characters;
using D47.Core.Configuration;
using D47.Core.Interface;
using D47.Core.Stories;

namespace D47.Core.Modules;

/// <summary>Who says a module's line, and in what voice.</summary>
/// <param name="Name">The name the transcript line gives the speaker.</param>
/// <param name="Speaker">The <see cref="Announcement.Speaker"/>, or null for the core aboard.</param>
/// <param name="Pinned">The voice the line is spoken in, or null for the voice its role and speaker are cast.</param>
/// <param name="InRoleVoice">Spoken in the core aboard's voice under <paramref name="Name"/>.</param>
public sealed record ModuleVoicing(
    string Name,
    VoiceRole Role,
    string? Speaker,
    PinnedVoice? Pinned,
    string? Picture,
    bool InRoleVoice);

/// <summary>What <see cref="ModuleSpeechCallout"/> reads.</summary>
public sealed record ModuleSpeechSources
{
    /// <summary>Takes the lines the modules have sent since the last call.</summary>
    public required Func<IReadOnlyList<ModuleLine>> Lines { get; init; }

    public required Func<IReadOnlyList<InstalledModule>> Installed { get; init; }

    public required Func<D47Settings> Settings { get; init; }

    /// <summary>The name of the core aboard.</summary>
    public required Func<string> AboardName { get; init; }

    /// <summary>The stock cores.</summary>
    public required Func<IReadOnlyList<(string Id, string Name)>> Cores { get; init; }

    /// <summary>The Commander's own cores.</summary>
    public required Func<IReadOnlyList<(string Id, string Name)>> YourCores { get; init; }

    /// <summary>The shown Commander's characters.</summary>
    public required Func<IReadOnlyList<CustomCharacter>> Characters { get; init; }
}

/// <summary>Speaks the lines modules send with <c>notifications/d47/say</c>: routine, spaced per module, and never reworded.</summary>
public sealed class ModuleSpeechCallout(ModuleSpeechSources sources) : ICallout
{
    public const string CalloutId = "module-speech";

    public const string KeyPrefix = "module.";

    private readonly Dictionary<string, DateTimeOffset> _spokeAt = new(StringComparer.Ordinal);

    public string Id => CalloutId;

    /// <summary>Whether <paramref name="key"/> is a module line's.</summary>
    public static bool Owns(string? key) => key?.StartsWith(KeyPrefix, StringComparison.Ordinal) == true;

    /// <summary>The announcement key of a line: the module's own key under its id, so no module can use another's.</summary>
    public static string KeyOf(ModuleLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        return $"{KeyPrefix}{line.ModuleId}.{line.Key}";
    }

    /// <summary>The least time between two lines from one module: the Commander's choice, never under the module's own minimum.</summary>
    public static TimeSpan Spacing(ModuleManifest manifest, ModuleSettings chosen)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(chosen);

        return TimeSpan.FromSeconds(Math.Max(ModulePlace.Seconds(chosen.Interval, manifest), manifest.MinimumIntervalSeconds));
    }

    public IEnumerable<Announcement> Examine(CalloutContext context)
    {
        var lines = sources.Lines();

        // Dropped without counting against the module: the engine would refuse them, or the voice would.
        if (lines.Count == 0 || context.IsPriming || context.InOperation || !context.GameRunning)
        {
            return [];
        }

        var settings = sources.Settings();
        var ready = sources.Installed().OfType<ReadyModule>().ToList();
        var said = new List<Announcement>();

        foreach (var line in lines)
        {
            if (ready.FirstOrDefault(module => module.Manifest.Id == line.ModuleId)?.Manifest is not { } manifest
                || settings.ModuleFor(manifest.Id) is not { Enabled: true, Muted: false } chosen
                || string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            if (_spokeAt.TryGetValue(manifest.Id, out var last) && context.Now - last < Spacing(manifest, chosen))
            {
                continue;
            }

            _spokeAt[manifest.Id] = context.Now;
            said.Add(Line(line, manifest, Resolve(manifest, chosen, settings)));
        }

        return said;
    }

    /// <summary>The speaker the module's Speaks as row names, with that speaker's voice.</summary>
    public ModuleVoicing Resolve(ModuleManifest manifest, ModuleSettings chosen, D47Settings settings) =>
        Resolve(manifest, chosen, settings, sources.AboardName(), sources.Cores(), sources.YourCores(), sources.Characters());

    /// <inheritdoc cref="Resolve(ModuleManifest, ModuleSettings, D47Settings)"/>
    public static ModuleVoicing Resolve(
        ModuleManifest manifest,
        ModuleSettings chosen,
        D47Settings settings,
        string aboardName,
        IReadOnlyList<(string Id, string Name)> cores,
        IReadOnlyList<(string Id, string Name)> yourCores,
        IReadOnlyList<CustomCharacter> characters)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(characters);

        var speakers = ModulePlace.Speakers(manifest, cores, yourCores, characters.Select(c => (c.Id, c.Name)));
        var value = ModulePlace.Chosen(manifest, chosen, speakers).Value;
        var aboard = new ModuleVoicing(aboardName, VoiceRole.ShipAi, null, null, null, InRoleVoice: false);

        // Everyone a module speaks as is aboard, in the ship's provider.
        var provider = VoiceGroups.ProviderFor(settings.Speech, VoiceGroups.Of(VoiceRole.Crew));

        if (value == ModulePlace.OwnCharacter && manifest.Character is { } own)
        {
            return own.Voices.FirstOrDefault(v => string.Equals(v.Provider, provider, StringComparison.OrdinalIgnoreCase))
                is { Voice.Length: > 0 } voice
                ? new ModuleVoicing(own.Name, VoiceRole.Crew, own.Name, new PinnedVoice(provider, voice.Voice), null, InRoleVoice: false)
                : new ModuleVoicing(own.Name, VoiceRole.Crew, own.Name, null, null, InRoleVoice: true);
        }

        if (CoreId(value) is { } coreId)
        {
            if (coreId == settings.Persona.Id)
            {
                return aboard;
            }

            var name = cores.Concat(yourCores).First(core => core.Id == coreId).Name;
            var picture = SpeakerPictures.Core(coreId);

            return settings.Persona.Voices.GetValueOrDefault(coreId) is { Length: > 0 } stored
                ? new ModuleVoicing(name, VoiceRole.Crew, name, new PinnedVoice(provider, stored), picture, InRoleVoice: false)
                : new ModuleVoicing(name, VoiceRole.Crew, name, null, picture, InRoleVoice: true);
        }

        if (value.StartsWith(ModulePlace.CharacterValue, StringComparison.Ordinal)
            && characters.FirstOrDefault(c => c.Id == value[ModulePlace.CharacterValue.Length..]) is { } character)
        {
            // Their own voice where the ship's provider issued it; otherwise the one they are cast when addressed.
            var pinned = character.Voice is { } voice && string.Equals(voice.Provider, provider, StringComparison.OrdinalIgnoreCase)
                ? new PinnedVoice(provider, voice.VoiceId)
                : null;

            return new ModuleVoicing(character.Name, VoiceRole.Crew, character.Name, pinned, character.Picture, InRoleVoice: false);
        }

        return aboard;
    }

    private static string? CoreId(string value) =>
        value.StartsWith(ModulePlace.CoreValue, StringComparison.Ordinal) ? value[ModulePlace.CoreValue.Length..]
        : value.StartsWith(ModulePlace.YourCoreValue, StringComparison.Ordinal) ? value[ModulePlace.YourCoreValue.Length..]
        : null;

    private static Announcement Line(ModuleLine line, ModuleManifest manifest, ModuleVoicing voicing) =>
        new(KeyOf(line), line.Text, CalloutUrgency.Routine)
        {
            Voice = voicing.Role,
            Speaker = voicing.Speaker,
            Pinned = voicing.Pinned,
            Picture = voicing.Picture,
            InRoleVoice = voicing.InRoleVoice,

            // Set for every speaker, which also keeps the text out of the model's conversation feed.
            Transcript = $"{voicing.Name} [{manifest.Name}]: {line.Text}\n",
        };
}
