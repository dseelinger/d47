using D47.Donations.Store;

namespace D47.Donations.R2;

/// <summary>One object as a listing describes it.</summary>
public sealed class StoredObject(DonationKey key, long compressedBytes, DateTimeOffset lastModified)
{
    public DonationKey Key { get; } = key;

    /// <summary>What the store holds, which is the gzipped payload rather than the payload.</summary>
    public long CompressedBytes { get; } = compressedBytes;

    public DateTimeOffset LastModified { get; } = lastModified;

    /// <summary>From a HEAD — a listing carries no custom metadata — and null until one has answered.</summary>
    public string? Build { get; set; }
}
