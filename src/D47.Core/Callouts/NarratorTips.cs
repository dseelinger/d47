using System.Text.Json;
using D47.Core.Help;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Callouts;

/// <summary>A tip on using D47, taken from a capability page's ELI5 band.</summary>
public sealed record NarratorTip(string CapabilityId, string Title, string Text);

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
