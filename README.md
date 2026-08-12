# d47

Directive 47 - Optimize Inferior Systems

A Guardian-flavoured voice companion for Elite Dangerous. Windows 11, .NET 10. One widget
tree renders to both a desktop window and a SteamVR overlay.

**[Documentation](https://dseelinger.github.io/d47/)** — including [how to install and
verify a build](https://dseelinger.github.io/d47/install.html).

## Status

Phase 1 (foundation) is complete: settings and secret stores, logging with runtime
per-subsystem verbosity, the capability registry, and the documentation gate. See
[list.md](list.md) for the full plan and [architecture.md](architecture.md) for how it is
built and why.

## Building

```
dotnet build
dotnet test
dotnet publish src/D47.App -c Release
```

Requires the .NET 10 SDK. Publish produces one self-contained `d47.exe` with no runtime
prerequisite; releases are cut by tagging `vX.Y.Z`.
