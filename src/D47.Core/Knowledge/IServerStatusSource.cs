namespace D47.Core.Knowledge;

/// <summary>What Elite's game servers are doing.</summary>
public enum GameServerState
{
    Online,
    Maintenance,
    Offline,
    Unknown,
}

/// <summary>The seam to whatever reports the state of Elite's game servers.</summary>
public interface IServerStatusSource
{
    /// <summary>The servers' state; <see cref="GameServerState.Unknown"/> when the request failed or the answer was unreadable.</summary>
    Task<GameServerState> ReadAsync(CancellationToken cancellationToken);
}
