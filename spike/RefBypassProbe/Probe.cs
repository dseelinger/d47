using Avalonia.Platform.Surfaces;

// Compiles ONLY if the ref-assembly guard is bypassed.
sealed class BypassSurface : IPlatformRenderSurface
{
    public bool IsReady => true;
}
