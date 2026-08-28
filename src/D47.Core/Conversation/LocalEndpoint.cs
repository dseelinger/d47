namespace D47.Core.Conversation;

/// <summary>
/// Whether an endpoint address is on this machine (Phase 29).
/// <para>
/// One judgement in one place, because two things read it and they must never disagree. The
/// <b>disclosure</b> reads it to say that nothing leaves this machine — the first time in d47's
/// life that the honest answer to <em>what is leaving</em> is <em>nothing</em> — and the
/// <b>price table</b> reads it to charge zero. A turn that is disclosed as private but billed as
/// remote, or the reverse, is one of them lying about the other.
/// </para>
/// <para>
/// Deliberately syntactic. It reads the host out of the URL and compares it against the names
/// and ranges that mean "here"; it does not resolve DNS, and it never opens a socket. Resolving
/// would make the answer depend on a name server — which is to say, on something off this
/// machine — and would let a hostname that resolves to 127.0.0.1 today report a turn as private
/// that tomorrow goes to a stranger. Being wrong in the safe direction is the point: an address
/// this cannot recognise is treated as remote, disclosed in full, and priced as unknown.
/// </para>
/// </summary>
public static class LocalEndpoint
{
    /// <summary>
    /// The names that mean this machine and are not IP literals. <c>localhost</c> is reserved to
    /// loopback by RFC 6761 and cannot be pointed elsewhere by a resolver that follows it; the
    /// <c>.localhost</c> suffix is reserved by the same rule, which is what a docker-style
    /// <c>ollama.localhost</c> relies on.
    /// </summary>
    private const string LocalhostName = "localhost";

    /// <summary>
    /// True when <paramref name="endpoint"/> names this machine. Null, empty and anything that
    /// does not parse as an absolute URL are all false: an endpoint d47 cannot read is not one it
    /// gets to make promises about.
    /// </summary>
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

        // The IP literals. IPAddress.IsLoopback covers 127.0.0.0/8 and ::1 — the whole of the
        // first, not just 127.0.0.1, because a server bound to 127.0.0.2 is no less here.
        //
        // Uri strips the brackets from an IPv6 literal but leaves a scope id on it, and
        // IPAddress.TryParse rejects the result — so the host is taken from the parser's own
        // idea of it rather than from the string, and the scope is trimmed.
        var literal = host.Split('%')[0];

        return System.Net.IPAddress.TryParse(literal, out var address)
               && System.Net.IPAddress.IsLoopback(address);
    }
}
