using System.Text.Json;
using D47.Core.Help;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>A tip on using D47 or, when <paramref name="AboutElite"/> is set, on playing Elite Dangerous.</summary>
public sealed record NarratorTip(string CapabilityId, string Title, string Text, bool AboutElite = false);

/// <summary>The capabilities whose tip each Commander has been told — <c>data/narrator-tips.json</c>.</summary>
public sealed class NarratorTipStore(string path, ILogger<NarratorTipStore> logger)
{
    private readonly Lock _gate = new();

    private Dictionary<string, HashSet<string>>? _said;

    public bool Said(string? frontierId, string capabilityId)
    {
        lock (_gate)
        {
            return Load().TryGetValue(frontierId ?? string.Empty, out var ids) && ids.Contains(capabilityId);
        }
    }

    public void Record(string? frontierId, string capabilityId)
    {
        lock (_gate)
        {
            var said = Load();

            if (!said.TryGetValue(frontierId ?? string.Empty, out var ids))
            {
                said[frontierId ?? string.Empty] = ids = new HashSet<string>(StringComparer.Ordinal);
            }

            if (!ids.Add(capabilityId))
            {
                return;
            }

            try
            {
                AtomicFile.WriteAllText(
                    path,
                    JsonSerializer.Serialize(
                        said.ToDictionary(pair => pair.Key, pair => pair.Value.Order(StringComparer.Ordinal).ToArray()),
                        new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not save {Path}", path);
            }
        }
    }

    private Dictionary<string, HashSet<string>> Load()
    {
        if (_said is not null)
        {
            return _said;
        }

        _said = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        try
        {
            if (File.Exists(path)
                && JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path)) is { } read)
            {
                foreach (var (commander, ids) in read)
                {
                    _said[commander] = new HashSet<string>(ids, StringComparer.Ordinal);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            logger.LogWarning(ex, "Could not read {Path}", path);
        }

        return _said;
    }
}

/// <summary>Chooses the next tip a Commander has not been told.</summary>
public static class NarratorTips
{
    /// <summary>The first capability page, in <see cref="HelpLibrary.Pages"/> order, whose tip <paramref name="said"/> rejects.</summary>
    public static NarratorTip? Next(Func<string, bool> said)
    {
        ArgumentNullException.ThrowIfNull(said);

        foreach (var id in HelpLibrary.Pages)
        {
            if (!said(id) && HelpLibrary.For(id) is { Intro.Length: > 0 } article)
            {
                return new NarratorTip(id, article.Title, article.Intro);
            }
        }

        return null;
    }
}

/// <summary>The hand-written tips on playing Elite Dangerous, each triggered by a journal event.</summary>
public static class EliteTips
{
    /// <summary>A tip is offered only while <c>Exploration.Time_Played</c> is under this many seconds.</summary>
    public const double NewPlayerSeconds = 50 * 3600;

    public const string IdPrefix = "elite:";

    private const string ResourceName = "D47.Core.EliteTips";

    private static readonly Lazy<IReadOnlyList<(string Key, string Event, string Text)>> Loaded =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The first tip, in file order, whose event is in <paramref name="seen"/> and which <paramref name="said"/> rejects.</summary>
    public static NarratorTip? Next(IReadOnlySet<string> seen, Func<string, bool> said)
    {
        ArgumentNullException.ThrowIfNull(seen);
        ArgumentNullException.ThrowIfNull(said);

        foreach (var (key, trigger, text) in Loaded.Value)
        {
            if (seen.Contains(trigger) && !said(IdPrefix + key))
            {
                return new NarratorTip(IdPrefix + key, key, text, AboutElite: true);
            }
        }

        return null;
    }

    private static IReadOnlyList<(string Key, string Event, string Text)> Load()
    {
        using var stream = typeof(EliteTips).Assembly.GetManifestResourceStream(ResourceName);

        if (stream is null)
        {
            return [];
        }

        using var reader = new StreamReader(stream);
        var rows = new List<(string, string, string)>();

        while (reader.ReadLine() is { } line)
        {
            var cells = line.Split('	');

            if (line.Length == 0 || line[0] == '#' || cells[0] == "key" || cells.Length < 3)
            {
                continue;
            }

            rows.Add((cells[0], cells[1], cells[2]));
        }

        return rows;
    }
}
