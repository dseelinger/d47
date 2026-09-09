namespace D47.Core.Conversation;

/// <summary>Whether an endpoint address is on this machine (Phase 29).</summary>
public static class LocalEndpoint
{
    /// <summary>The names that mean this machine and are not IP literals.</summary>
    private const string LocalhostName = "localhost";

    /// <summary>True when <paramref name="endpoint"/> names this machine.</summary>
    public static bool IsLoopback(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;

        if (host.Length == 0)
        {
            return false;
        }

        if (string.Equals(host, LocalhostName, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith($".{LocalhostName}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The IP literals.
        var literal = host.Split('%')[0];

        return System.Net.IPAddress.TryParse(literal, out var address)
               && System.Net.IPAddress.IsLoopback(address);
    }
}
