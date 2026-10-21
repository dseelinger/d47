namespace D47.Core.Journal;

/// <summary>The body the Commander is at and where their own ship sits on it.</summary>
/// <param name="Body">The <c>(SystemAddress, BodyId)</c> set by <c>ApproachBody</c> and <c>Touchdown</c>.</param>
/// <param name="Since">When the event that set <paramref name="Body"/> was written.</param>
/// <param name="Ship">The landing point, with radius 0; <see cref="SurfaceFix.MetresTo"/> takes the other point's radius.</param>
public sealed record SurfacePoints(
    (long SystemAddress, int BodyId)? Body,
    DateTimeOffset? Since,
    SurfaceFix? Ship)
{
    public static SurfacePoints None { get; } = new(null, null, null);

    public SurfacePoints Apply(JournalEvent journalEvent)
    {
        switch (journalEvent.Kind)
        {
            case "ApproachBody":
                return AtBody(journalEvent) is { } approached
                    ? this with { Body = approached, Since = journalEvent.Timestamp }
                    : this;

            case "LeaveBody":
                return None;

            case "Touchdown" when !IsAnotherShip(journalEvent):
                var landed = AtBody(journalEvent) is { } body
                    ? this with { Body = body, Since = journalEvent.Timestamp }
                    : this;

                return journalEvent.Double("Latitude") is { } latitude && journalEvent.Double("Longitude") is { } longitude
                    ? landed with { Ship = new SurfaceFix(latitude, longitude, 0) }
                    : landed;

            case "Liftoff" when !IsAnotherShip(journalEvent):
                return this with { Ship = null };

            default:
                return this;
        }
    }

    private static bool IsAnotherShip(JournalEvent journalEvent) =>
        journalEvent.Bool("Taxi") || journalEvent.Bool("Multicrew");

    private static (long, int)? AtBody(JournalEvent journalEvent) =>
        journalEvent.Long("SystemAddress") is { } address && journalEvent.Int("BodyID") is { } bodyId
            ? (address, bodyId)
            : null;
}
