using Microsoft.Extensions.Logging;

namespace D47.Core.Audio;

/// <summary>A shipped Chatterbox reference voice.</summary>
public sealed record ChatterboxVoice(VoiceInfo Voice, VoiceRole? Role, string Source, string ClipPath);

/// <summary>Reads <c>voices.tsv</c> and the reference clips beside it.</summary>
public static class ChatterboxVoices
{
    public const string TableName = "voices.tsv";

    public const int SampleRate = 24_000;

    public static readonly TimeSpan MinLength = TimeSpan.FromSeconds(5);

    public static readonly TimeSpan MaxLength = TimeSpan.FromSeconds(7);

    private static readonly string[] Columns = ["id", "name", "gender", "locale", "role", "source"];

    /// <summary>The voices in <paramref name="folder"/> whose rows and clips are valid; each rejected row is logged with its reason.</summary>
    public static IReadOnlyList<ChatterboxVoice> Load(string folder, ILogger logger)
    {
        var table = Path.Combine(folder, TableName);

        if (!File.Exists(table))
        {
            logger.LogWarning("Chatterbox voices: {Table} is missing.", table);
            return [];
        }

        var lines = File.ReadAllLines(table).Where(line => line.Length > 0).ToList();

        if (lines.Count == 0 || !lines[0].Split('\t').SequenceEqual(Columns))
        {
            logger.LogWarning("Chatterbox voices: {Table} does not start with the columns {Columns}.", table, string.Join(", ", Columns));
            return [];
        }

        var voices = new List<ChatterboxVoice>();

        foreach (var line in lines.Skip(1))
        {
            var fields = line.Split('\t');

            if (fields.Length != Columns.Length)
            {
                logger.LogWarning("Chatterbox voices: row \"{Row}\" has {Count} columns, not {Expected}; left out.", line, fields.Length, Columns.Length);
                continue;
            }

            var id = fields[0];
            var reason = Reject(folder, fields, out var role, out var clipPath);

            if (reason is not null)
            {
                logger.LogWarning("Chatterbox voices: {Id} left out: {Reason}", id, reason);
                continue;
            }

            var gender = fields[2].Length > 0 ? fields[2] : null;
            voices.Add(new ChatterboxVoice(new VoiceInfo(id, fields[1], fields[3], gender), role, fields[5], clipPath));
        }

        return voices;
    }

    private static string? Reject(string folder, string[] fields, out VoiceRole? role, out string clipPath)
    {
        role = null;
        clipPath = Path.Combine(folder, fields[0] + ".wav");

        if (fields[0].Length == 0)
        {
            return "the id is empty.";
        }

        if (string.IsNullOrWhiteSpace(fields[5]))
        {
            return "the source is empty.";
        }

        if (fields[4].Length > 0)
        {
            if (!Enum.TryParse<VoiceRole>(fields[4], out var parsed) || !Enum.IsDefined(parsed))
            {
                return $"the role \"{fields[4]}\" is not a voice role.";
            }

            role = parsed;
        }

        if (!File.Exists(clipPath))
        {
            return $"the clip {clipPath} is missing.";
        }

        AudioClip clip;

        try
        {
            clip = WavReader.Read(clipPath);
        }
        catch (WavFormatException ex)
        {
            return ex.Message;
        }

        if (clip.Format.SampleRate != SampleRate || clip.Format.Channels != 1)
        {
            return $"the clip is {clip.Format.SampleRate} Hz with {clip.Format.Channels} channels, not {SampleRate} Hz mono.";
        }

        if (clip.Duration < MinLength || clip.Duration > MaxLength)
        {
            return $"the clip is {clip.Duration.TotalSeconds:0.0} s long, outside {MinLength.TotalSeconds:0.0} to {MaxLength.TotalSeconds:0.0} s.";
        }

        return null;
    }
}
