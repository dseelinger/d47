using System.Security.Cryptography;
using D47.Core.Interface;

namespace D47.Core.Characters;

/// <summary>A character the Commander made. Storage is append-only: add properties, never rename one.</summary>
/// <param name="Id">Eight hex characters, generated once and never reused.</param>
/// <param name="Voice">This character's own voice, or null to use the ship's.</param>
/// <param name="Notes">The Commander's personality notes, or null.</param>
/// <param name="System">The system the character is in while not <paramref name="Aboard"/>; kept while aboard.</param>
public sealed record CustomCharacter(
    string Id,
    string Name,
    CharacterVoice? Voice,
    string? Notes,
    bool Aboard,
    string? System)
{
    public const int MaxName = 32;

    public const int MaxNotes = 600;

    public string Picture => SpeakerPictures.Character(Id);

    public static string NewId() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));

    public static bool IsId(string? id) =>
        id is { Length: 8 } && id.All(char.IsAsciiHexDigitLower);
}

/// <summary>A voice id and the provider that issued it; the id means nothing to any other provider.</summary>
public sealed record CharacterVoice(string Provider, string VoiceId);

/// <summary>The carrier's two characters, whose names and voices are settings.</summary>
public enum CarrierCharacter
{
    Captain,
    Tower,
}

/// <summary>The field a refused save names.</summary>
public enum CharacterField
{
    Name,
    Notes,
    System,
}

/// <summary>Why a save was refused, in words the page shows under <see cref="Field"/>.</summary>
public sealed record CharacterRefusal(CharacterField Field, string Reason);

/// <summary>One entry the file was asked to hold, and why it was refused.</summary>
public sealed record CharacterProblem(string Where, string Reason);
