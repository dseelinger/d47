using System.Runtime.InteropServices;

namespace D47.Tts;

/// <summary>How many intra-op threads Chatterbox gets on this chip.</summary>
internal static class PerformanceCores
{
    public const int Cap = 8;

    private const int RelationProcessorCore = 0;

    /// <summary>
    /// The count of physical cores in the highest efficiency class, capped at <see cref="Cap"/>. On a
    /// single-class chip that is every physical core.
    /// </summary>
    public static int ThreadsFor(IReadOnlyCollection<byte> efficiencyClassPerCore)
    {
        if (efficiencyClassPerCore.Count == 0)
        {
            return Math.Clamp(Environment.ProcessorCount, 1, Cap);
        }

        var fastest = efficiencyClassPerCore.Max();

        return Math.Min(efficiencyClassPerCore.Count(c => c == fastest), Cap);
    }

    /// <summary>The threads for this machine, read from Windows.</summary>
    public static int ForThisMachine() => ThreadsFor(EfficiencyClasses());

    /// <summary>One entry per physical core, its <c>EfficiencyClass</c>; empty where Windows does not answer.</summary>
    private static List<byte> EfficiencyClasses()
    {
        uint length = 0;
        GetLogicalProcessorInformationEx(RelationProcessorCore, 0, ref length);

        if (length == 0)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal((int)length);

        try
        {
            if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length))
            {
                return [];
            }

            var classes = new List<byte>();
            var offset = 0;

            // SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX: Relationship (4), Size (4), then
            // PROCESSOR_RELATIONSHIP: Flags (1), EfficiencyClass (1), ...
            while (offset < length)
            {
                var size = Marshal.ReadInt32(buffer, offset + 4);

                if (size <= 0)
                {
                    break;
                }

                if (Marshal.ReadInt32(buffer, offset) == RelationProcessorCore)
                {
                    classes.Add(Marshal.ReadByte(buffer, offset + 9));
                }

                offset += size;
            }

            return classes;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationship, nint buffer, ref uint length);
}
