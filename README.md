# Directive 47

Directive 47 - Optimize Inferior Systems

A Guardian-flavoured voice companion for Elite Dangerous. Windows 11, .NET 10. One widget
tree renders to both a desktop window and a SteamVR overlay.

**[Documentation](https://dseelinger.github.io/d47/)** — including [how to install and
verify a build](https://dseelinger.github.io/d47/install.html).

## Status

Phases 1-9 are complete: the foundation, the journal spine, typed and spoken conversation,
the settings surface, speaking and listening, game knowledge, proactive callouts, and the
SteamVR overlay. Phase 10 (acting on the game) is next. See [list.md](list.md) for the full
plan and [architecture.md](architecture.md) for how it is built and why.

## Building

```
dotnet build
dotnet test
dotnet publish src/D47.App -c Release
```

Requires the .NET 10 SDK. Publish produces a self-contained `d47.exe` with Whisper's
native libraries beside it under `runtimes\` — the pair ships together as `d47.zip`, with
no runtime prerequisite; releases are cut by tagging `vX.Y.Z`.
