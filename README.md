# Directive 47

Directive 47 - Optimize Inferior Systems

A Guardian-flavoured voice companion for Elite Dangerous. Windows 11, .NET 10. One widget
tree renders to both a desktop window and a SteamVR overlay.

**[Documentation](https://dseelinger.github.io/d47/)** — including [how to install and
verify a build](https://dseelinger.github.io/d47/install.html).

## Status

Phases 1-14 are complete: the foundation, the journal spine, typed and spoken conversation,
the settings surface, speaking and listening, game knowledge, proactive callouts, the SteamVR
overlay, acting on the game, personas and voices, the soundscape, hands-free listening, and
knowledge from outside the journal. Phase 15 (warnings that arrive in time) is next. See [list.md](list.md) for the full
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

## Attribution

Directive 47 was created using assets and imagery from Elite Dangerous, with the permission
of Frontier Developments plc, for non-commercial purposes. It is not endorsed by nor reflects
the views or opinions of Frontier Developments and no employee of Frontier Developments was
involved in the making of it.

The code is MIT (see [LICENSE](LICENSE)). The Elite Dangerous game data it ships is Frontier's
and is used under their [media usage
rules](https://forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/) — that
is a separate thing from the code licence, and [NOTICE](NOTICE) sets out which is which, which
tables are derived, and from where.
