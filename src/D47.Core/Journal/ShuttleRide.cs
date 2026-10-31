namespace D47.Core.Journal;

public enum ShuttleKind { Unknown, Apex, Frontline }

/// <summary>A shuttle booking or ride; <c>Retreat</c> is set for a dropship that takes the Commander off the surface.</summary>
public sealed record ShuttleRide(ShuttleKind Kind, string? DestinationSystem, string? DestinationLocation, bool Retreat);

/// <summary>The held shuttle booking and the ride in progress, folded from the journal.</summary>
public sealed record ShuttleTrip(ShuttleRide? Booking, ShuttleRide? Ride)
{
    public static ShuttleTrip None { get; } = new(null, null);

    internal ShuttleTrip Apply(JournalEvent journalEvent) => journalEvent.Kind switch
    {
        "BookTaxi" => this with { Booking = Book(journalEvent, ShuttleKind.Apex) },
        "BookDropship" => this with { Booking = Book(journalEvent, ShuttleKind.Frontline) },
        "CancelTaxi" or "CancelDropship" => this with { Booking = null },
        "Embark" when journalEvent.Bool("Taxi") =>
            new ShuttleTrip(null, Booking ?? new ShuttleRide(ShuttleKind.Unknown, null, null, false)),
        "Embark" or "Disembark" or "DropshipDeploy" => new ShuttleTrip(Booking, null),
        "Location" when journalEvent.Bool("Taxi") =>
            this with { Ride = Ride ?? new ShuttleRide(ShuttleKind.Unknown, null, null, false) },
        "Location" => this with { Ride = null },
        "Died" or "LoadGame" or "Shutdown" => None,
        _ => this,
    };

    private static ShuttleRide Book(JournalEvent journalEvent, ShuttleKind kind) => new(
        kind,
        journalEvent.String("DestinationSystem"),
        journalEvent.String("DestinationLocation"),
        kind == ShuttleKind.Frontline && journalEvent.Bool("Retreat"));
}
