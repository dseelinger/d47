namespace D47.Core.Callouts;

public static class CalloutForms
{
    extension(AmbientLines)
    {
        public static string? Pick(AmbientSituation situation, int index)
        {
            var lines = AmbientLines.For(situation);

            return lines.Count == 0 ? null : lines[Math.Abs(index) % lines.Count];
        }
    }

    extension(ContinuityCallout callout)
    {
        public string Compose(DateTimeOffset now) => callout.Compose(now, null);
    }
}
