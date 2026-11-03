using System.Collections.Concurrent;

namespace D47.Core.Modules;

/// <summary>A notification a module sent, held for the tick.</summary>
public abstract record ModuleNotice(string ModuleId);

/// <summary>A <c>notifications/d47/say</c>: a line the module asks to have spoken.</summary>
public sealed record ModuleLine : ModuleNotice
{
    public const int MaxText = 300;

    public const int MaxKey = 64;

    public ModuleLine(string moduleId, string key, string text)
        : base(moduleId)
    {
        Key = key.Length > MaxKey ? key[..MaxKey] : key;
        Text = text.Length > MaxText ? text[..MaxText] : text;
    }

    public string Key { get; }

    public string Text { get; }
}

/// <summary>A <c>notifications/resources/updated</c>.</summary>
public sealed record ModuleResourceUpdated(string ModuleId, string Uri) : ModuleNotice(ModuleId);

/// <summary>Notifications from every module, posted from any thread and drained on the tick.</summary>
public sealed class ModuleInbox
{
    /// <summary>The most notices held; posting past it drops the oldest.</summary>
    public const int Capacity = 256;

    private readonly ConcurrentQueue<ModuleNotice> _notices = new();

    public void Post(ModuleNotice notice)
    {
        _notices.Enqueue(notice);

        while (_notices.Count > Capacity)
        {
            _notices.TryDequeue(out _);
        }
    }

    public IReadOnlyList<ModuleNotice> Drain()
    {
        var drained = new List<ModuleNotice>();

        while (_notices.TryDequeue(out var notice))
        {
            drained.Add(notice);
        }

        return drained;
    }
}
