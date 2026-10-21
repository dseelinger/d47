namespace D47.Core.Knowledge;

/// <summary>The earlier finds asked for in each system this session, an empty answer included.</summary>
public sealed class EarlierFinds
{
    private readonly Lock _gate = new();
    private readonly Dictionary<long, IReadOnlyList<EarlierFind>> _bySystem = [];

    public bool TryGet(long systemAddress, out IReadOnlyList<EarlierFind> finds)
    {
        lock (_gate)
        {
            return _bySystem.TryGetValue(systemAddress, out finds!);
        }
    }

    public void Set(long systemAddress, IReadOnlyList<EarlierFind> finds)
    {
        lock (_gate)
        {
            _bySystem[systemAddress] = finds;
        }
    }
}
