using System.Diagnostics.CodeAnalysis;
using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace D47.Core.Configuration;

/// <summary>Encryption at rest for secrets. Abstracted so tests need no real DPAPI blob.</summary>
public interface ISecretProtector
{
    byte[] Protect(byte[] plaintext);

    /// <summary>
    /// False rather than throwing: a blob that will not decrypt is a secret that is
    /// unavailable, which reads downstream as a capability being off.
    /// </summary>
    bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext);
}

/// <summary>
/// DPAPI with <see cref="DataProtectionScope.CurrentUser"/> — no key management, and the
/// reason the install is per-user (architecture.md §5 D6).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSecretProtector : ISecretProtector
{
    public byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);

    public bool TryUnprotect(byte[] ciphertext, [NotNullWhen(true)] out byte[]? plaintext)
    {
        try
        {
            plaintext = ProtectedData.Unprotect(ciphertext, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return true;
        }
        catch (CryptographicException)
        {
            // Different user, or the file was copied from another machine.
            plaintext = null;
            return false;
        }
    }
}
