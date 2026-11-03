using D47.Core.Capabilities;
using D47.Core.Configuration;

namespace D47.Core.Modules;

/// <summary>One option of a module's Speaks as row.</summary>
public sealed record ModuleSpeaker(string Value, string Label);

/// <summary>A module's status as the Modules place states it: a lead word in a tone, then plain text.</summary>
public sealed record ModuleStatusLine(string Lead, string Rest, ChoiceTone Tone);

/// <summary>What the Modules settings place says and writes for each installed module.</summary>
public static class ModulePlace
{
    public const string PlaceId = "modules";

    /// <summary>Stored in <see cref="ModuleSettings.SpeaksAs"/> for the core aboard.</summary>
    public const string Aboard = "aboard";

    /// <summary>Stored in <see cref="ModuleSettings.SpeaksAs"/> for the module's own character.</summary>
    public const string OwnCharacter = "own";

    public const string CoreValue = "core:";
    public const string YourCoreValue = "yours:";
    public const string CharacterValue = "character:";

    public const string NothingWhileOff = "Nothing while it is off.";

    public const string SpeaksAsHelp = "A core or a character, in its voice. Add a character on Asset mgmt › Characters.";

    /// <summary>The options in order: the core aboard, the module's own character, the cores, the Commander's cores, the Commander's characters.</summary>
    public static IReadOnlyList<ModuleSpeaker> Speakers(
        ModuleManifest manifest,
        IEnumerable<(string Id, string Name)> cores,
        IEnumerable<(string Id, string Name)> yourCores,
        IEnumerable<(string Id, string Name)> characters)
    {
        var speakers = new List<ModuleSpeaker> { new(Aboard, "The core aboard") };

        if (manifest.Character is { } own)
        {
            speakers.Add(new ModuleSpeaker(OwnCharacter, $"{own.Name} · its own character"));
        }

        speakers.AddRange(cores.Select(c => new ModuleSpeaker(CoreValue + c.Id, $"{c.Name} · core")));
        speakers.AddRange(yourCores.Select(c => new ModuleSpeaker(YourCoreValue + c.Id, $"{c.Name} · your core")));
        speakers.AddRange(characters.Select(c => new ModuleSpeaker(CharacterValue + c.Id, $"{c.Name} · character")));

        return speakers;
    }

    /// <summary>The stored choice if it is still offered; otherwise the module's own character when it has one, else the core aboard.</summary>
    public static ModuleSpeaker Chosen(ModuleManifest manifest, ModuleSettings chosen, IReadOnlyList<ModuleSpeaker> speakers)
    {
        if (chosen.SpeaksAs is { } stored && speakers.FirstOrDefault(s => s.Value == stored) is { } match)
        {
            return match;
        }

        var fallback = manifest.Character is null ? Aboard : OwnCharacter;

        return speakers.First(s => s.Value == fallback);
    }

    /// <summary>The choices of How often it may speak, in order.</summary>
    public static IReadOnlyList<ModuleInterval> Intervals { get; } =
        [ModuleInterval.Own, ModuleInterval.TenMinutes, ModuleInterval.ThirtyMinutes, ModuleInterval.Hourly];

    public static int Seconds(ModuleInterval interval, ModuleManifest manifest) => interval switch
    {
        ModuleInterval.TenMinutes => 600,
        ModuleInterval.ThirtyMinutes => 1800,
        ModuleInterval.Hourly => 3600,
        _ => manifest.MinimumIntervalSeconds,
    };

    /// <summary>A choice shorter than the module's own minimum is not offered.</summary>
    public static bool IntervalAllowed(ModuleInterval interval, ModuleManifest manifest) =>
        interval == ModuleInterval.Own || Seconds(interval, manifest) >= manifest.MinimumIntervalSeconds;

    public static string IntervalLabel(ModuleInterval interval) => interval switch
    {
        ModuleInterval.TenMinutes => "10 min apart",
        ModuleInterval.ThirtyMinutes => "30 min apart",
        ModuleInterval.Hourly => "Once an hour",
        _ => "Its own limit",
    };

    /// <summary>The module's minimum, as the status line under Its own limit.</summary>
    public static string Apart(int seconds) =>
        seconds % 60 == 0 ? $"{seconds / 60} min apart" : $"{seconds} sec apart";

    public static ModuleStatusLine StatusLine(ModuleStatus status, bool hasPages) => status switch
    {
        ModuleStatus.Running => new("Running.", hasPages ? " Its pages are on the Modules tab." : " It has no page.", ChoiceTone.Cyan),
        ModuleStatus.Starting => new(string.Empty, "Starting.", ChoiceTone.White),
        ModuleStatus.NotResponding => new("Not responding.", string.Empty, ChoiceTone.Yellow),
        ModuleStatus.StoppedAfterRepeatedCrashes => new("Stopped after repeated crashes.", string.Empty, ChoiceTone.Red),
        ModuleStatus.Refused => new("Refused.", " Not started.", ChoiceTone.Red),
        _ => new("Off.", string.Empty, ChoiceTone.Grey),
    };

    public static string StoppedNotice =>
        "It crashed each time D47 restarted it, so D47 stopped restarting it. Restart it to try again, or turn it off.";

    public static string RefusalNotice(ModuleRefusalReason reason) => reason switch
    {
        ModuleRefusalReason.UnknownContract =>
            "It is built for a module contract this version of D47 does not know, so D47 will not start it. A newer D47, or another build of the module, may run it.",
        ModuleRefusalReason.Unparseable => "Its module.json could not be read, so D47 will not start it.",
        ModuleRefusalReason.MalformedId => "Its id is not a short lowercase name, so D47 will not start it.",
        ModuleRefusalReason.DuplicateId => "Another module already uses its id, so D47 will not start it.",
        ModuleRefusalReason.CommandOutsideFolder => "Its command points outside its own folder, so D47 will not start it.",
        ModuleRefusalReason.CommandMissing => "Its command is not in its folder, so D47 will not start it.",
        _ => "It asks to speak more often than D47 allows, so D47 will not start it.",
    };

    /// <summary>What leaves this machine for one module: the disclosure while it is on, else a plain sentence.</summary>
    public static string Leaves(D47Settings settings, ReadyModule module) =>
        EgressDisclosure.ModuleEntries(settings, [module]).FirstOrDefault()?.Summary ?? NothingWhileOff;

    /// <summary>Writes one module's settings through <see cref="SettingsService.Replace"/>, which every listener of <see cref="SettingsService.Changed"/> hears.</summary>
    public static void Set(SettingsService settings, string id, string reason, Func<ModuleSettings, ModuleSettings> change) =>
        settings.Replace(reason, current => current with
        {
            Modules = new Dictionary<string, ModuleSettings>(current.Modules, StringComparer.Ordinal)
            {
                [id] = change(current.ModuleFor(id)),
            },
        });
}
