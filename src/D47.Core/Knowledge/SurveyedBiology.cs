namespace D47.Core.Knowledge;

/// <summary>The surveyed biology asked for in each system this session, an empty answer included.</summary>
public sealed class SurveyedBiology
{
    private readonly Lock _gate = new();
    private readonly Dictionary<long, SystemBiology> _bySystem = [];
    private readonly Dictionary<long, Task<SystemBiology?>> _asking = [];

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

    /// <summary>The held answer, else one ask whose answer is held; readers of one system share the ask. Null when the ask fails.</summary>
    public Task<SystemBiology?> GetAsync(
        long systemAddress,
        Func<long, CancellationToken, Task<SystemBiology>> ask,
        CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_bySystem.TryGetValue(systemAddress, out var held))
            {
                return Task.FromResult<SystemBiology?>(held);
            }

            if (!_asking.TryGetValue(systemAddress, out var asking))
            {
                asking = _asking[systemAddress] = Ask(systemAddress, ask);
            }

            return asking.WaitAsync(cancellationToken);
        }
    }

    private async Task<SystemBiology?> Ask(long systemAddress, Func<long, CancellationToken, Task<SystemBiology>> ask)
    {
        SystemBiology? answer = null;

        try
        {
            answer = await Task.Run(() => ask(systemAddress, CancellationToken.None)).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }

        lock (_gate)
        {
            if (answer is not null)
            {
                _bySystem[answer.SystemAddress] = answer;
            }

            _asking.Remove(systemAddress);
        }

        return answer;
    }
}
