using D47.Core.Hotas;

namespace D47.Core.Vr;

/// <summary>
/// The motion controllers as two stick-button devices, <see cref="LeftId"/> and <see cref="RightId"/>, with ten
/// inputs each: the six <see cref="VrButton"/>s, then the stick pushed up, down, left and right. The trigger, the
/// grip and the stick directions go to the panel or to a bound action, decided when the input goes down and kept
/// until it comes up; every other input always goes to its bound action. Callable from any thread.
/// </summary>
public sealed class MotionControllerButtons
{
    public const string LeftId = "vr:left";
    public const string RightId = "vr:right";

    public const int StickUp = 6;
    public const int StickDown = 7;
    public const int StickLeft = 8;
    public const int StickRight = 9;

    /// <summary>How many inputs each controller has.</summary>
    public const int Inputs = 10;

    /// <summary>How long after the last read of the hands they still count.</summary>
    public static readonly TimeSpan Stale = TimeSpan.FromSeconds(1);

    private readonly Lock _gate = new();
    private readonly Hand[] _hands = [new(LeftId), new(RightId)];
    private DateTimeOffset _readAt = DateTimeOffset.MinValue;

    /// <summary>Whether a stored device id is one of the motion controllers.</summary>
    public static bool IsMotionController(string? deviceId) =>
        deviceId?.StartsWith("vr:", StringComparison.Ordinal) == true;

    /// <summary>Whether the panel can take an input from its bound action.</summary>
    public static bool PanelCanTake(int input) =>
        input is (int)VrButton.Trigger or (int)VrButton.Grip or >= StickUp;

    /// <summary>
    /// One read of the hands. <paramref name="panelTakes"/> says whether the panel takes the trigger, grip and stick
    /// of a hand; it is asked only when one of them goes down. A hand of unknown side is ignored, and a hand missing
    /// from <paramref name="hands"/> lets go of everything.
    /// </summary>
    public void Observe(IReadOnlyList<VrHand> hands, Func<VrHand, bool> panelTakes, DateTimeOffset now)
    {
        lock (_gate)
        {
            _readAt = now;
            var seen = new bool[_hands.Length];

            foreach (var hand in hands)
            {
                var index = hand.Side switch
                {
                    VrSide.Left => 0,
                    VrSide.Right => 1,
                    _ => -1,
                };

                if (index < 0 || seen[index])
                {
                    continue;
                }

                seen[index] = true;
                _hands[index].Read(hand, panelTakes);
            }

            for (var index = 0; index < _hands.Length; index++)
            {
                if (!seen[index])
                {
                    _hands[index].Leave();
                }
            }
        }
    }

    /// <summary>Lets go of everything, as when there is no session or no pointing.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            foreach (var hand in _hands)
            {
                hand.Leave();
            }
        }
    }

    /// <summary>
    /// A reading for each controller in the last read of the hands, holding only the inputs that went to a bound
    /// action; nothing once that read is older than <see cref="Stale"/>.
    /// </summary>
    public IReadOnlyList<HotasReading> Poll(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (now - _readAt > Stale)
            {
                return [];
            }

            var readings = new List<HotasReading>(_hands.Length);

            foreach (var hand in _hands)
            {
                if (hand.Present)
                {
                    readings.Add(hand.Reading());
                }
            }

            return readings;
        }
    }

    /// <summary>Whether a hand holds <paramref name="button"/> for a bound action and no hand holds it for the panel.</summary>
    public bool KeepsFromThePanel(VrButton button, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (now - _readAt > Stale)
            {
                return false;
            }

            var input = (int)button;
            var bound = false;

            foreach (var hand in _hands)
            {
                if (!hand.Present || !hand.Down[input])
                {
                    continue;
                }

                if (hand.Panel[input])
                {
                    return false;
                }

                bound = true;
            }

            return bound;
        }
    }

    /// <summary>Whether the stick of the controller <paramref name="device"/> is pushed for a bound action.</summary>
    public bool KeepsTheStickFromThePanel(uint device, DateTimeOffset now)
    {
        lock (_gate)
        {
            if (now - _readAt > Stale)
            {
                return false;
            }

            foreach (var hand in _hands)
            {
                if (!hand.Present || hand.Device != device)
                {
                    continue;
                }

                for (var input = StickUp; input <= StickRight; input++)
                {
                    if (hand.Down[input] && !hand.Panel[input])
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    private static bool IsDown(VrHand hand, int input, bool was) => input switch
    {
        < StickUp => (hand.Held & (1u << input)) != 0,
        StickUp => Pushed(hand.Stick.Y, was),
        StickDown => Pushed(-hand.Stick.Y, was),
        StickLeft => Pushed(-hand.Stick.X, was),
        _ => Pushed(hand.Stick.X, was),
    };

    private static bool Pushed(float along, bool was) =>
        along >= (was ? VrStickScroll.ReleasedAt : VrStickScroll.PressedAt);

    private sealed class Hand(string id)
    {
        public bool Present { get; private set; }

        public uint Device { get; private set; }

        public bool[] Down { get; } = new bool[Inputs];

        /// <summary>Whether each input that is down went to the panel.</summary>
        public bool[] Panel { get; } = new bool[Inputs];

        /// <summary>Presses that went to a bound action, counted from the start and never reset.</summary>
        private readonly int[] _presses = new int[Inputs];

        public void Read(VrHand hand, Func<VrHand, bool> panelTakes)
        {
            if (Present && Device != hand.Device)
            {
                Leave();
            }

            Present = true;
            Device = hand.Device;
            bool? takes = null;

            for (var input = 0; input < Inputs; input++)
            {
                var down = IsDown(hand, input, Down[input]);

                if (down == Down[input])
                {
                    continue;
                }

                Down[input] = down;

                if (!down)
                {
                    Panel[input] = false;
                    continue;
                }

                Panel[input] = PanelCanTake(input) && (takes ??= panelTakes(hand));

                if (!Panel[input])
                {
                    _presses[input]++;
                }
            }
        }

        public void Leave()
        {
            Present = false;
            Array.Clear(Down);
            Array.Clear(Panel);
        }

        public HotasReading Reading()
        {
            var held = new bool[Inputs];

            for (var input = 0; input < Inputs; input++)
            {
                held[input] = Down[input] && !Panel[input];
            }

            return new HotasReading
            {
                Id = id,
                Buttons = held,
                Presses = [.. _presses],
            };
        }
    }
}
