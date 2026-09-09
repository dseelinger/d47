using System.Globalization;
using System.Security.Cryptography;
using D47.Core.Storage;

namespace D47.Core.Diagnostics.Donation;

/// <summary>
/// The one thing on a donation envelope that says two donations came from the same install (#176).
/// </summary>
public static class DonorToken
{
    /// <summary>Sixteen bytes, written as thirty-two lowercase hex characters.</summary>
    public const int Bytes = 16;

    /// <summary>How long a written token is.</summary>
    public const int Length = Bytes * 2;

    /// <summary>A fresh token.</summary>
    public static string NewToken() =>
        Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(Bytes));

    /// <summary>Whether a string is a token this build would have written.</summary>
    public static bool IsWellFormed(string? token) =>
        token is { Length: Length } && token.All(IsLowerHex);

    /// <summary>The token on this installation, or null where there is none.</summary>
    public static string? Read(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }

            var read = File.ReadAllText(file).Trim();
            return IsWellFormed(read) ? read : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable is the same answer as absent, and deliberately so: the caller's next move either way
            // is to mint one, and a donation must not fail because a file was locked.
            return null;
        }
    }

    /// <summary>The token on this installation, minting and writing one if there is none.</summary>
    public static string Ensure(string file)
    {
        if (Read(file) is { } existing)
        {
            return existing;
        }

        var minted = NewToken();
        AtomicFile.WriteAllText(file, minted + Environment.NewLine);
        return minted;
    }

    /// <summary>Withdrawal.</summary>
    /// <returns>The token that was forgotten, or null where there was nothing to forget.</returns>
    public static string? Forget(string file)
    {
        var held = Read(file);

        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return held;
    }

    /// <summary>What a Commander is told about their own token, on the privacy page.</summary>
    public static string Summarise(string? token) =>
        token is null
            ? "No donation identifier exists on this installation. One is created the first time "
              + "you donate, and never before."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Your donations are grouped under {token}. It is a random number made on this "
                + $"machine, it is not derived from your Commander name or anything else about "
                + $"you, and it is used for donations and nothing else. Forgetting it stops future "
                + $"donations joining the ones already sent.");

    private static bool IsLowerHex(char character) =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f';
}
