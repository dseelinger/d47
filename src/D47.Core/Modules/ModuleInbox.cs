namespace D47.Core.Modules;

/// <summary>A notification a module sent, held for the tick.</summary>
public abstract record ModuleNotice(string ModuleId);

/// <summary>A <c>notifications/d47/say</c>: a line the module asks to have spoken.</summary>
public sealed record ModuleLine : ModuleNotice
{
    public const int MaxText = 300;

    public const int MaxKey = 64;

    public ModuleLine(string moduleId, string key, string text, bool askedUrgent = false)
        : base(moduleId)
    {
        Key = key.Length > MaxKey ? key[..MaxKey] : key;
        Text = text.Length > MaxText ? text[..MaxText] : text;
        AskedUrgent = askedUrgent;
    }

    public string Key { get; }

    public string Text { get; }

    /// <summary>Whether the module asked for the line to be urgent; it is spoken as routine either way.</summary>
    public bool AskedUrgent { get; }
}

/// <summary>A <c>notifications/resources/updated</c>.</summary>
public sealed record ModuleResourceUpdated(string ModuleId, string Uri) : ModuleNotice(ModuleId);

/// <summary>Notifications from every module, posted from any thread and drained on the tick.</summary>
public sealed class ModuleInbox
{
    /// <summary>The most notices held; posting past it drops the oldest.</summary>
    public const int Capacity = 256;

    private readonly Lock _gate = new();
    private readonly LinkedList<ModuleNotice> _notices = new();

    public void Post(ModuleNotice notice)
    {
        lock (_gate)
        {
            _notices.AddLast(notice);

            while (_notices.Count > Capacity)
            {
                _notices.RemoveFirst();
            }
        }
    }

    /// <summary>Takes every notice, in the order posted.</summary>
    public IReadOnlyList<ModuleNotice> Drain() => Drain<ModuleNotice>();

    /// <summary>Takes the notices of one kind, in the order posted, and leaves the rest queued.</summary>
    public IReadOnlyList<T> Drain<T>()
        where T : ModuleNotice
    {
        var drained = new List<T>();

        lock (_gate)
        {
            var node = _notices.First;

            while (node is not null)
            {
                var next = node.Next;

                if (node.Value is T notice)
                {
                    drained.Add(notice);
                    _notices.Remove(node);
                }

                node = next;
            }
        }

        return drained;
    }
}
