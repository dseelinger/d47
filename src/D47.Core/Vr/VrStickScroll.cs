using D47.Core.Interface;

namespace D47.Core.Vr;

/// <summary>Turns a pushed stick on the pointing hand into line scrolls, repeating while it stays pushed.</summary>
public sealed class VrStickScroll
{
    /// <summary>A stick pushed at least this far up or down starts scrolling.</summary>
    public const float PressedAt = 0.6f;

    /// <summary>A scrolling stick keeps scrolling until it falls inside this.</summary>
    public const float ReleasedAt = 0.4f;

    /// <summary>Time between repeats while the stick stays pushed.</summary>
    public static readonly TimeSpan RepeatEvery = TimeSpan.FromMilliseconds(150);

    private uint _device;
    private int _direction;
    private DateTimeOffset _next;

    /// <summary>
    /// The scroll to make this tick, or null. Only the hand whose ray is on the panel counts, and nothing
    /// scrolls while <paramref name="suppressed"/>.
    /// </summary>
    public PanelScrollStep? Step(IReadOnlyList<VrHand> hands, uint? pointing, bool suppressed, DateTimeOffset now)
    {
        if (suppressed || pointing is not { } device)
        {
            _direction = 0;
            return null;
        }

        var y = 0f;

        foreach (var hand in hands)
        {
            if (hand.Device == device)
            {
                y = hand.Stick.Y;
                break;
            }
        }

        var was = _device == device ? _direction : 0;
        var direction = DirectionOf(y, was);

        if (direction == 0)
        {
            _direction = 0;
            return null;
        }

        if (direction == was && now < _next)
        {
            return null;
        }

        _device = device;
        _direction = direction;
        _next = now + RepeatEvery;
        return direction > 0 ? PanelScrollStep.LineUp : PanelScrollStep.LineDown;
    }

    private static int DirectionOf(float y, int was)
    {
        if (was != 0 && y * was >= ReleasedAt)
        {
            return was;
        }

        return y >= PressedAt ? 1 : y <= -PressedAt ? -1 : 0;
    }
}
