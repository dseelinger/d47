using System.Diagnostics.CodeAnalysis;
using D47.Core.Configuration;

namespace D47.Donations.R2;

/// <summary>
/// The R2 API token, created by hand in the Cloudflare dashboard with Object Read on
/// <c>d47-donations</c> and nothing else.
/// </summary>
public sealed record R2Credentials(string AccountId, string AccessKeyId, string SecretAccessKey)
{
    public const string AccountIdName = "r2-account-id";
    public const string AccessKeyIdName = "r2-access-key-id";
    public const string SecretAccessKeyName = "r2-secret-access-key";

    public string Host => $"{AccountId}.r2.cloudflarestorage.com";

    /// <summary>Reads all three, or none: a partial credential cannot sign anything.</summary>
    public static bool TryRead(SecretStore secrets, [NotNullWhen(true)] out R2Credentials? credentials)
    {
        credentials = null;

        if (!secrets.TryGet(AccountIdName, out var account)
            || !secrets.TryGet(AccessKeyIdName, out var access)
            || !secrets.TryGet(SecretAccessKeyName, out var secret))
        {
            return false;
        }

        credentials = new R2Credentials(account, access, secret);
        return true;
    }

    public void Save(SecretStore secrets)
    {
        secrets.Set(AccountIdName, AccountId);
        secrets.Set(AccessKeyIdName, AccessKeyId);
        secrets.Set(SecretAccessKeyName, SecretAccessKey);
    }
}
