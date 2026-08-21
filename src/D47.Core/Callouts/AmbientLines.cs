using D47.Core.Journal;

namespace D47.Core.Callouts;

/// <summary>
/// The situations d47 will remark on unprompted. A closed set, and deliberately a small one:
/// every member has to be something the Commander is plainly doing for minutes at a time, or
/// the remark arrives about a state they have already left.
/// </summary>
public enum AmbientSituation
{
    /// <summary>Nothing worth remarking on, or not enough is known yet.</summary>
    None,

    Docked,

    /// <summary>Parked on a planet surface, still in the ship.</summary>
    Landed,

    Supercruise,

    /// <summary>In normal space and flying — between stations, in a ring, at a signal source.</summary>
    NormalSpace,

    /// <summary>Taking on fuel from a star. Distinct because it is a thing being done, not a place.</summary>
    Scooping,

    Srv,

    OnFoot,
}

/// <summary>
/// The stock ambient lines (list.md Phase 11, "Ambient Voice").
/// <para>
/// <b>One shared set rather than one per core.</b> Eleven cores times seven situations times ten
/// lines is several thousand lines of authored fiction that would go stale against the next
/// persona edit with nobody noticing. These are the floor: they play when there is no model, and
/// with no model there is no persona flavour anywhere else either, so a neutral line here is
/// consistent rather than a compromise. With a model, the core writes its own and these are
/// never heard.
/// </para>
/// <para>
/// Written flat on purpose — observations about the ship and the situation, never about the
/// Commander's judgement. An ambient remark is something you can ignore.
/// </para>
/// <para>
/// <b>And never a claim about state d47 has not read.</b> <see cref="Pick"/> is given an index and
/// nothing else — no ship, no status, no journal — so every line here has to be true of its
/// situation at <em>every</em> moment inside it. Anything asserting a transition, a sensor reading
/// or a figure is a claim nothing checked.
/// </para>
/// <para>
/// <b>Reported 2026-08-21</b>, and it is what this rule was written for: <i>"Mass lock is clear.
/// The route is ours as far as I can see"</i>, heard an hour into a crossing. Mass lock clearing is
/// a transition — true for seconds, nonsense afterwards — and "the route is ours" assumed a plotted
/// route besides. Four more lines were making the same mistake about atmosphere, gravity and cargo,
/// and all five are rewritten below.
/// </para>
/// <para>
/// <b>Hedging is the fix, not deletion.</b> "There is a queue on the pads outside. There always
/// is." reads as a remark rather than a reading, and that is what makes it honest with nothing
/// checked. A line either reads a value or does not claim one.
/// </para>
/// </summary>
public static class AmbientLines
{
    /// <summary>
    /// Which situation the Commander is in, from Status.json alone. Status rather than the
    /// journal because this is a question about how things <em>are</em>, and the journal reports
    /// what happened.
    /// </summary>
    public static AmbientSituation Situate(GameStatus status)
    {
        if (status.Has2(StatusFlags2.OnFoot))
        {
            return AmbientSituation.OnFoot;
        }

        if (status.Has(StatusFlags.InSrv))
        {
            return AmbientSituation.Srv;
        }

        // Before Supercruise, because scooping happens in supercruise and is the more specific
        // thing to be doing.
        if (status.Has(StatusFlags.ScoopingFuel))
        {
            return AmbientSituation.Scooping;
        }

        if (status.Has(StatusFlags.Docked))
        {
            return AmbientSituation.Docked;
        }

        if (status.Has(StatusFlags.Landed))
        {
            return AmbientSituation.Landed;
        }

        if (status.Has(StatusFlags.Supercruise))
        {
            return AmbientSituation.Supercruise;
        }

        // In the ship, in normal space. Checked last because InMainShip is true in almost every
        // situation above it as well.
        return status.Has(StatusFlags.InMainShip)
            ? AmbientSituation.NormalSpace
            : AmbientSituation.None;
    }

    /// <summary>How to describe the situation to a model that is writing its own line.</summary>
    public static string Describe(AmbientSituation situation) => situation switch
    {
        AmbientSituation.Docked => "docked at a station",
        AmbientSituation.Landed => "landed on a planet surface, still aboard the ship",
        AmbientSituation.Supercruise => "in supercruise, crossing a system",
        AmbientSituation.NormalSpace => "in normal space, flying",
        AmbientSituation.Scooping => "scooping fuel from a star",
        AmbientSituation.Srv => "driving an SRV on a planet surface",
        AmbientSituation.OnFoot => "out of the ship on foot",
        _ => "aboard the ship",
    };

    /// <summary>The ten stock lines for a situation, or empty for one with none.</summary>
    public static IReadOnlyList<string> For(AmbientSituation situation) => situation switch
    {
        AmbientSituation.Docked => Docked,
        AmbientSituation.Landed => Landed,
        AmbientSituation.Supercruise => Supercruise,
        AmbientSituation.NormalSpace => NormalSpace,
        AmbientSituation.Scooping => Scooping,
        AmbientSituation.Srv => Srv,
        AmbientSituation.OnFoot => OnFoot,
        _ => [],
    };

    /// <summary>
    /// One of them, chosen by an index the caller supplies. Deliberately not random: no Core
    /// component reads a clock or a random seed, and a recorded session has to replay to the
    /// same line it produced live.
    /// </summary>
    public static string? Pick(AmbientSituation situation, int index)
    {
        var lines = For(situation);

        return lines.Count == 0 ? null : lines[Math.Abs(index) % lines.Count];
    }

    private static readonly string[] Docked =
    [
        "Station power is carrying the ship. The reactor is idling for the first time in a while.",
        "Hull temperature is down to ambient. It has been a long time since it was.",
        "The pad clamps are holding. Nothing here needs your attention.",
        "Traffic control has us logged. We are somebody else's problem for a few minutes.",
        "Life support is running off the station feed. Cheaper than ours.",
        "There is a queue on the pads outside. There always is.",
        "Everything that was warm is cooling. That is the whole of the report.",
        "The ship is secure. Whatever comes next can wait until you are ready for it.",
        "No drift, no heat, no traffic. It is quiet in here.",
        "Docking computer has nothing to do. Neither, for the moment, do I.",
    ];

    private static readonly string[] Landed =
    [
        "Landing gear is taking the weight. The ground under us is holding.",
        "Whatever is out there, the hull is the only thing between it and us.",
        "The ship has settled into whatever gravity this place has.",
        "Dust on the sensors. It will clear when we lift.",
        "The horizon out there is closer than it looks. Small worlds do that.",
        "Nothing is moving within scanner range. Nothing has, for some time.",
        "Surface temperature is stable. So is ours.",
        "The ship is level. Whoever set it down did it properly.",
        "It is very still here. That is not the same as safe, but it is still.",
        "We are the only thing on this rock that was built.",
    ];

    private static readonly string[] Supercruise =
    [
        "The drive is holding steady. Nothing ahead of us for a while.",
        "Frame shift is doing the work. We are covering ground you could not walk in a lifetime.",
        // Not "mass lock is clear", which was the reported one: that is a transition, true for
        // seconds and nonsense an hour later. Saying it *at the moment it clears* would be a good
        // line and is a different feature, needing the status flag this class is not given.
        "The way ahead is as clear as anything gets out here.",
        "The star ahead is getting closer at a rate that would alarm anyone unfamiliar with this.",
        "No contacts. Just distance, being crossed.",
        "The drive note has not changed in some minutes. That is what it sounds like when it is right.",
        "Fuel is going down slowly. That is expected at this speed.",
        "There is nothing out here but us and the arithmetic.",
        "Hull is cold, drive is warm, everything is where it should be.",
        "We are travelling fast enough that the sky ahead and the sky behind are different colours.",
    ];

    private static readonly string[] NormalSpace =
    [
        "Thrusters have the ship. The drive is resting.",
        "We are moving at a speed a human could once have understood.",
        "Sensors are clear at this range. Not that range is much, down here.",
        "The hull is taking the usual small impacts. Nothing worth logging.",
        "Everything within reach is either rock or somebody else's problem.",
        "Shields are holding their charge. They have nothing to hold it against.",
        "Manoeuvring thrusters are doing more work than the main drive right now.",
        "Space here is not empty. It is only nearly empty, which is a different thing.",
        "The ship handles the way its mass says it should. It always does.",
        "No traffic on the scanner. That is either good or a matter of timing.",
    ];

    private static readonly string[] Scooping =
    [
        "Taking on fuel. Hull temperature is climbing, as it should be.",
        "The scoop is drawing well. Do not linger longer than you need to.",
        "That is a star being taken apart into something the drive can use.",
        "Fuel is coming in faster than heat is coming off. Watch the margin.",
        "The corona out there is doing most of the work. We are just holding position in it.",
        "Temperature is inside tolerance. Inside is not the same as comfortable.",
        "This is the part of the journey that costs nothing but nerve.",
        "The tank is filling. The hull is warming. Both are on schedule.",
        "Every star is a fuel depot if you are willing to get close enough.",
        "Scoop rate is steady. This is going well, which is when people stop watching.",
    ];

    private static readonly string[] Srv =
    [
        "Six wheels on regolith. The suspension is earning its mass.",
        // Not "behind us". Where the ship is depends on which way the Commander has driven since
        // they left it, and d47 has no reading that says — the SRV's own position relative to the
        // ship is in no journal event and no status flag. "Nearby" is what is actually known
        // (remediation.md, "The ship is holding position behind us").
        "The ship is holding position nearby. It will be there when you want it.",
        "This terrain would have taken a survey team a week. It is taking us an afternoon.",
        "Wheel grip is a negotiation. It always is, on a surface nobody graded.",
        "The horizon keeps arriving sooner than it should out here.",
        "Fuel cell is fine. The drive draws almost nothing at this speed.",
        "Turret is stowed. Nothing out here to point it at.",
        "Every rock down here is older than anything either of us has a word for.",
        "We are leaving tracks. They will be here long after we are not.",
        "The ride is rough. That is the ground, not the vehicle.",
    ];

    private static readonly string[] OnFoot =
    [
        "You are outside the hull. I can hear you, but I cannot do much for you out there.",
        "Suit integrity is holding. Keep an eye on it — I would rather you than the readout.",
        "The ship is where you left it. I am watching it.",
        "Oxygen is a number now. It was not a number ten minutes ago.",
        "There is very little between you and the outside. That is the arrangement you chose.",
        "Standing on a world is a strange way to see one. Most of it is under your feet.",
        "Your suit is doing the work of an entire ship, at a fraction of the size.",
        "Nothing on local scan. That is as much as I can tell you from here.",
        "It is quiet on this channel. I am still here.",
        "Take your time. The ship is not going anywhere without you.",
    ];
}
