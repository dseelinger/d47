namespace D47.Core.Journal;

/// <summary>A Commander's steps as a map draws them: lines of legs, the systems visited and the jumps counted.</summary>
/// <param name="Lines">Each run of jumps, oldest first; a <see cref="TravelArrival.Location"/> step starts a new one.</param>
/// <param name="Systems">One position per distinct system, from its latest visit.</param>
/// <param name="Jumps">The <see cref="TravelArrival.Jump"/> and <see cref="TravelArrival.CarrierJump"/> steps.</param>
public sealed record TravelPicture(
    IReadOnlyList<IReadOnlyList<StarPosition>> Lines,
    IReadOnlyList<StarPosition> Systems,
    int Jumps)
{
    public static TravelPicture Empty { get; } = new([], [], 0);

    /// <summary>The steps at or after <paramref name="since"/>, or all of them when it is null.</summary>
    public static TravelPicture Of(IReadOnlyList<TravelStep> steps, DateTimeOffset? since)
    {
        ArgumentNullException.ThrowIfNull(steps);

        var lines = new List<IReadOnlyList<StarPosition>>();
        var systems = new Dictionary<long, StarPosition>();
        List<StarPosition>? line = null;
        var jumps = 0;

        foreach (var step in steps)
        {
            if (since is { } from && step.At < from)
            {
                continue;
            }

            systems[step.SystemAddress] = step.Position;

            if (step.How == TravelArrival.Location || line is null)
            {
                line = [step.Position];
                lines.Add(line);
            }
            else
            {
                line.Add(step.Position);
            }

            if (step.How != TravelArrival.Location)
            {
                jumps++;
            }
        }

        return new TravelPicture(lines, [.. systems.Values], jumps);
    }
}
