namespace D47.Core.Vr;

/// <summary>What the headset is showing.</summary>
public enum HeadsetView
{
    /// <summary>No reading, or one too old to trust.</summary>
    Unknown,

    /// <summary>The SteamVR scene.</summary>
    Vr,

    /// <summary>The streaming app's own desktop view.</summary>
    Desktop,
}

/// <summary>Which view the headset shows. A read must not block: it is called on the tick.</summary>
public interface IHeadsetViewSource
{
    HeadsetView Read();
}
