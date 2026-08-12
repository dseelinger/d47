using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using D47.Core.Storage;
using Microsoft.Extensions.Logging;

namespace D47.Core.Configuration;

/// <summary>
/// Secrets, encrypted at rest, kept apart from settings so this store can shrug where the
/// settings store must shout (architecture.md §5 D6). Values are write-only from the
/// outside: callers can enumerate <see cref="Names"/> and set values, and only the code
/// that needs a secret reads one.
/// </summary>
public sealed class SecretStore
{
    private readonly AppPaths _paths;
    private readonly ISecretProtector _protector;
    private readonly ILogger<SecretStore> _logger;
    private readonly Dictionary<string, string> _ciphertext = new(StringComparer.OrdinalIgnoreCase);

    public SecretStore(AppPaths paths, ISecretProtector protector, ILogger<SecretStore> logger)
    {
        _paths = paths;
        _protector = protector;
        _logger = logger;
        Reload();
    }

    /// <summary>Secret names only. Never the values — that is the write-only property.</summary>
    public IReadOnlyCollection<string> Names => _ciphertext.Keys;

    public bool Has(string name) => TryGet(name, out _);

    public bool TryGet(string name, [NotNullWhen(true)] out string? value)
    {
        value = null;

        if (!_ciphertext.TryGetValue(name, out var encoded))
        {
            return false;
        }

        byte[] blob;
        try
        {
            blob = Convert.FromBase64String(encoded);
        }
        catch (FormatException)
        {
            _logger.LogWarning("Secret {Name} is not valid base64; treating it as absent", name);
            return false;
        }

        if (!_protector.TryUnprotect(blob, out var plaintext))
        {
            _logger.LogWarning(
                "Secret {Name} could not be decrypted for the current user; treating it as absent. " +
                "This is expected if {File} was copied from another machine or user account",
                name,
                _paths.SecretsFile);
            return false;
        }

        value = Encoding.UTF8.GetString(plaintext);
        return true;
    }

    public void Set(string name, string value)
    {
        _ciphertext[name] = Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(value)));
        Flush();
        _logger.LogInformation("Stored secret {Name}", name);
    }

    public bool Remove(string name)
    {
        if (!_ciphertext.Remove(name))
        {
            return false;
        }

        Flush();
        _logger.LogInformation("Removed secret {Name}", name);
        return true;
    }

    private void Reload()
    {
        _ciphertext.Clear();

        if (!File.Exists(_paths.SecretsFile))
        {
            return;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(_paths.SecretsFile));

            if (stored is null)
            {
                return;
            }

            foreach (var (name, encoded) in stored)
            {
                _ciphertext[name] = encoded;
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Deliberately not fatal. Every secret being absent turns capabilities off,
            // which is a state the app already has to handle.
            _logger.LogWarning(ex, "Secret store at {Path} is unreadable; starting with no secrets", _paths.SecretsFile);
        }
    }

    private void Flush() =>
        AtomicFile.WriteAllText(
            _paths.SecretsFile,
            JsonSerializer.Serialize(_ciphertext, new JsonSerializerOptions { WriteIndented = true }));
}
