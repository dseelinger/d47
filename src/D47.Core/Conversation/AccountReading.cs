namespace D47.Core.Conversation;

/// <summary>A provider whose own account figures d47 can read.</summary>
public enum AccountProvider
{
    ElevenLabs,
    Deepgram,
}

/// <summary>Reads what one provider reports about the Commander's account.</summary>
public interface IAccountReader
{
    AccountProvider Provider { get; }

    Task<AccountReading> ReadAsync(CancellationToken cancellationToken);
}

/// <summary>What one read of a provider's account found.</summary>
public abstract record AccountReading
{
    private protected AccountReading()
    {
    }

    /// <summary>No key is stored for the provider, so nothing was asked.</summary>
    public sealed record NoKey : AccountReading;

    /// <summary>The provider answered with an error status, and its own message when it gave one.</summary>
    public sealed record Refused(int Status, string? Message) : AccountReading;

    /// <summary>The request did not get an answer.</summary>
    public sealed record Unreachable(string Message) : AccountReading;
}

/// <summary>How often an ElevenLabs character quota starts again.</summary>
public enum RefreshPeriod
{
    Monthly = 1,
    Quarterly = 3,
    HalfYearly = 6,
    Annual = 12,
}

/// <summary>How far usage may go past the character limit: not at all, a number of characters, or without limit.</summary>
public readonly record struct LimitExtension
{
    private LimitExtension(long? characters) => Characters = characters;

    /// <summary>Usage stops at the limit.</summary>
    public static LimitExtension Stops { get; } = new(0);

    /// <summary>Usage continues past the limit with no ceiling.</summary>
    public static LimitExtension Unlimited { get; } = new(null);

    /// <summary>Usage may continue this many characters past the limit.</summary>
    public static LimitExtension UpTo(long characters) =>
        characters >= 0 ? new(characters) : throw new ArgumentOutOfRangeException(nameof(characters), characters, "An extension cannot be negative.");

    /// <summary>The characters allowed past the limit; null when unlimited.</summary>
    public long? Characters { get; }

    public bool IsStop => Characters == 0;

    public bool IsUnlimited => Characters is null;
}

/// <summary>An amount of money in the currency the provider named.</summary>
public readonly record struct Money(decimal Amount, string Currency);

/// <summary>What ElevenLabs reports about the account's character quota.</summary>
public sealed record ElevenLabsFigures(
    long CharactersUsed,
    long CharacterLimit,
    DateTimeOffset? NextReset,
    RefreshPeriod? Refresh,
    LimitExtension Extension,
    Money Overage) : AccountReading;

/// <summary>One Deepgram project's remaining credit.</summary>
public sealed record DeepgramBalance(string Project, decimal Amount, string Units);

/// <summary>What Deepgram reports: the credit left on each project.</summary>
public sealed record DeepgramCredit(IReadOnlyList<DeepgramBalance> Projects) : AccountReading;
