namespace D47.XrLayer;

/// <summary>The Elite session the layer is attached to, and how many frames it has ended.</summary>
internal static class SessionWatch
{
    public static ulong Session { get; private set; }

    public static long FramesEnded { get; private set; }

    public static void Created(ulong session)
    {
        Session = session;
        FramesEnded = 0;
    }

    public static void FrameEnding(ulong session)
    {
        if (session == Session)
        {
            FramesEnded++;
        }
    }

    public static void Destroying(ulong session)
    {
        if (session == Session)
        {
            Session = 0;
        }
    }
}
