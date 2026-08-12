using System.Diagnostics.CodeAnalysis;
using D47.Core;
using D47.Core.Configuration;
using D47.Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace D47.Core.Tests;

/// <summary>A throwaway folder standing in for an install directory.</summary>
public sealed class TempInstall : IDisposable
{
    public TempInstall()
    {
        Root = Path.Combine(Path.GetTempPath(), "d47-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new AppPaths(Root);
        Paths.EnsureCreated();
    }

    public string Root { get; }

    public AppPaths Paths { get; }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test over.
        }
    }
}

/// <summary>
/// Reversible "encryption". Keeps the store's behaviour testable without a real DPAPI blob,
/// which is per-user and per-machine and therefore useless in CI.
/// </summary>
public sealed class ReversibleProtector : ISecretProtector
{
    public byte[] Protect(byte[] plaintext) => plaintext.Select(b => (byte)(b ^ 0x5A)).ToArray();

    public bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext)
    {
        plaintext = ciphertext.Select(b => (byte)(b ^ 0x5A)).ToArray();
        return true;
    }
}

/// <summary>Stands in for a blob written by another user or on another machine.</summary>
public sealed class NeverUnprotects : ISecretProtector
{
    public byte[] Protect(byte[] plaintext) => plaintext;

    public bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext)
    {
        plaintext = null;
        return false;
    }
}

public sealed class FakeVerbosityControl : ILogVerbosityControl
{
    private readonly Dictionary<string, LogLevel> _levels =
        Subsystems.All.ToDictionary(s => s, _ => LogLevel.Information, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, LogLevel> Levels => _levels;

    public void Set(string subsystem, LogLevel level) =>
        _levels[Subsystems.Canonical(subsystem) ?? throw new ArgumentException(subsystem)] = level;
}
