using System.Text.Json;
using D47.Core.Configuration;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Characters;

/// <summary>Turns the crew seats in <c>crew-seats.json</c> into aboard custom characters, then renames the file.</summary>
public static class SeatMigration
{
    /// <summary>What the seat file is renamed to once its seats are characters.</summary>
    public const string MigratedName = "crew-seats.migrated.json";

    /// <summary>The settings key the role voices were stored under in <see cref="SpeechSettings"/>.</summary>
    public const string RoleVoicesKey = "seatVoices";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// The characters each Commander's seats become, keyed by Frontier id: one per name across ships, aboard,
    /// with each seat's role as a sentence in the notes. A seat's own voice wins, then its role's.
    /// </summary>
    /// <param name="provider">The provider that speaks for the ship, which issued <paramref name="roleVoices"/>.</param>
    /// <param name="roleVoices">The voice for each standard role, keyed by the role's name in camel case.</param>
    /// <exception cref="JsonException">The text is not a seat file.</exception>
    public static IReadOnlyDictionary<string, IReadOnlyList<CustomCharacter>> Read(
        string text, string? provider, IReadOnlyDictionary<string, string> roleVoices)
    {
        ArgumentNullException.ThrowIfNull(roleVoices);

        var file = JsonSerializer.Deserialize<SeatFile>(text, Json);
        var commanders = new Dictionary<string, List<Merged>>(StringComparer.Ordinal);

        foreach (var ship in file?.Ships ?? [])
        {
            var fid = Blank(ship.CommanderFid) ?? CharacterStore.NoCommander;

            if (!commanders.TryGetValue(fid, out var merged))
            {
                commanders[fid] = merged = [];
            }

            foreach (var seat in ship.Seats ?? [])
            {
                if (Blank(seat.Name) is not { Length: <= CustomCharacter.MaxName } name
                    || Said(seat.Role, Blank(seat.Title)) is not { } role)
                {
                    continue;
                }

                var voice = OwnVoice(seat) ?? RoleVoice(seat.Role, provider, roleVoices);

                if (merged.FirstOrDefault(other => string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)) is { } same)
                {
                    if (!same.Roles.Contains(role, StringComparer.OrdinalIgnoreCase))
                    {
                        same.Roles.Add(role);
                    }

                    same.Voice ??= voice;
                    continue;
                }

                merged.Add(new Merged(CustomCharacter.IsId(seat.Id) ? seat.Id! : CustomCharacter.NewId(), name, [role]) { Voice = voice });
            }
        }

        return commanders
            .Where(pair => pair.Value.Count > 0)
            .ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<CustomCharacter>)[.. pair.Value.Select(one => one.ToCharacter())],
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Adds every Commander's seats to <paramref name="store"/>, skipping a name the Commander already has, then
    /// renames the seat file to <see cref="MigratedName"/>. Does nothing when there is no seat file, and leaves it
    /// in place when a character could not be written. Blocks on file I/O: call from the pool, not from a tick.
    /// </summary>
    /// <returns>How many characters were added.</returns>
    public static int Run(
        IFileSystem files,
        string seatsPath,
        CharacterStore store,
        string? provider,
        IReadOnlyDictionary<string, string> roleVoices,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(logger);

        string? text;

        try
        {
            text = files.Stat(seatsPath) is null ? null : files.ReadText(seatsPath);
        }
        catch (IOException ex)
        {
            logger.LogDebug(ex, "Could not read the crew seat file");
            return 0;
        }

        if (text is null)
        {
            return 0;
        }

        IReadOnlyDictionary<string, IReadOnlyList<CustomCharacter>> found;

        try
        {
            found = Read(text, provider, roleVoices);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "The crew seat file could not be read, and no seat became a character");
            found = new Dictionary<string, IReadOnlyList<CustomCharacter>>();
        }

        var added = 0;
        var written = true;

        foreach (var (fid, characters) in found)
        {
            foreach (var seat in characters)
            {
                if (store.CharactersOf(fid).Any(held => string.Equals(held.Name, seat.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var character = store.Find(fid, seat.Id) is null ? seat : seat with { Id = CustomCharacter.NewId() };
                CharacterRefusal? refusal;

                try
                {
                    refusal = store.Set(fid, character);
                }
                catch (ArgumentException)
                {
                    character = seat with { Id = CustomCharacter.NewId() };
                    refusal = store.Set(fid, character);
                }

                if (refusal is not null)
                {
                    logger.LogWarning("The crew seat {Name} did not become a character: {Reason}", seat.Name, refusal.Reason);
                    continue;
                }

                if (store.Find(fid, character.Id) is null)
                {
                    written = false;
                    continue;
                }

                added++;
            }
        }

        if (!written)
        {
            logger.LogWarning("A crew seat could not be written as a character; the seat file stays for the next start");
            return added;
        }

        try
        {
            files.Copy(seatsPath, Path.Combine(Path.GetDirectoryName(seatsPath) ?? string.Empty, MigratedName));
            files.Delete(seatsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not rename the crew seat file");
        }

        return added;
    }

    /// <summary>The role voices <paramref name="speech"/> still holds under <see cref="RoleVoicesKey"/>; empty when none.</summary>
    public static IReadOnlyDictionary<string, string> RoleVoices(SpeechSettings speech)
    {
        ArgumentNullException.ThrowIfNull(speech);

        var voices = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (speech.Extra?.TryGetValue(RoleVoicesKey, out var held) == true && held.ValueKind == JsonValueKind.Object)
        {
            foreach (var role in held.EnumerateObject())
            {
                if (role.Value.ValueKind == JsonValueKind.String && Blank(role.Value.GetString()) is { } voice)
                {
                    voices[role.Name] = voice;
                }
            }
        }

        return voices;
    }

    /// <summary>The role as the notes say it, or null for a role that is not one.</summary>
    private static string? Said(string? role, string? title) => role?.Trim().ToLowerInvariant() switch
    {
        "firstofficer" => "First officer",
        "helm" => "Helm",
        "comms" => "Comms",
        "scienceofficer" => "Science officer",
        "securityofficer" => "Security officer",
        "navigation" => "Navigation",
        "custom" => title,
        _ => null,
    };

    private static CharacterVoice? OwnVoice(SeatLine seat) =>
        Blank(seat.Voice?.Provider) is { } provider && Blank(seat.Voice?.VoiceId) is { } id
            ? new CharacterVoice(provider, id)
            : null;

    private static CharacterVoice? RoleVoice(string? role, string? provider, IReadOnlyDictionary<string, string> roleVoices) =>
        provider is { Length: > 0 }
        && role is { Length: > 0 }
        && !string.Equals(role.Trim(), "custom", StringComparison.OrdinalIgnoreCase)
        && roleVoices.TryGetValue(KeyOf(role.Trim()), out var voice)
        && Blank(voice) is { } id
            ? new CharacterVoice(provider, id)
            : null;

    /// <summary>A role's name in camel case, as the settings file wrote it.</summary>
    private static string KeyOf(string role) => $"{char.ToLowerInvariant(role[0])}{role[1..]}";

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Merged(string Id, string Name, List<string> Roles)
    {
        public CharacterVoice? Voice { get; set; }

        public CustomCharacter ToCharacter() =>
            new(Id, Name, Voice, string.Join(" ", Roles.Select(role => $"{role}.")), Aboard: true, System: null);
    }

    private sealed record SeatFile
    {
        public IReadOnlyList<ShipLine>? Ships { get; init; }
    }

    private sealed record ShipLine
    {
        public string? CommanderFid { get; init; }

        public IReadOnlyList<SeatLine>? Seats { get; init; }
    }

    private sealed record SeatLine
    {
        public string? Id { get; init; }

        public string? Role { get; init; }

        public string? Title { get; init; }

        public string? Name { get; init; }

        public VoiceLine? Voice { get; init; }
    }

    private sealed record VoiceLine
    {
        public string? Provider { get; init; }

        public string? VoiceId { get; init; }
    }
}
