namespace D47.Core.Vr;

public static class VrForms
{
    extension(SurfacePlacement placement)
    {
        public VrPose AgainstTheHead() => placement.AgainstTheHead(VrPose.Origin);
    }
}
