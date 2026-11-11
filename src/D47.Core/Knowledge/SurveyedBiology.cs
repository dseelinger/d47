namespace D47.Core.Knowledge;

/// <summary>The surveyed biology asked for in each system this session, an empty answer included.</summary>
public sealed class SurveyedBiology
{
    private readonly Lock _gate = new();
    private readonly Dictionary<long, SystemBiology> _bySystem = [];

    public bool TryGet(long systemAddress, out SystemBiology biology)
    {
        lock (_gate)
        {
            return _bySystem.TryGetValue(systemAddress, out biology!);
        }
    }

    public void Set(SystemBiology biology)
    {
        lock (_gate)
        {
            _bySystem[biology.SystemAddress] = biology;
        }
    }
}
