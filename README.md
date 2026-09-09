# Directive 47

Directive 47 — Optimize Inferior Systems

A Guardian-flavoured voice companion for Elite Dangerous. Windows 11, .NET 10. It reads the
journal the game already writes, answers out loud, and renders one panel to both a desktop window
and a SteamVR overlay.

**[Documentation](https://dseelinger.github.io/d47/)** — including [how to install and verify a
build](https://dseelinger.github.io/d47/install.html).

**[Join the Discord](https://dseelinger.github.io/d47/community.html)** — questions, bug reports
and screenshots. It is the place to reach a person; you do not need a GitHub account.

## What it does

You talk to it while you fly. It watches the journal for what just happened, answers questions
about ships, modules, engineers, blueprints and routes, calls out what is worth knowing without
being asked, and can act on the game on your say-so. Forty-five capabilities are documented one
page each under [Capabilities](https://dseelinger.github.io/d47/); the
[open issues](https://github.com/dseelinger/d47/issues) are the honest list of what is wrong with
it today.

It is one person's project with a handful of users. [CHANGELOG.md](CHANGELOG.md) records what
shipped in each release.

## Building

```
dotnet build d47.slnx -c Debug
dotnet test  d47.slnx -c Debug
dotnet publish src/D47.App -c Release
```

Requires the .NET 10 SDK; `global.json` pins the version. Publish produces a self-contained,
single-file `d47.exe` for `win-x64` with its native libraries beside it — the pair ships as
`d47.zip`, with no runtime prerequisite. There is also a per-user installer that needs no
elevation.

Eight projects: `D47.Core` holds the game logic and depends on no UI, audio hardware or vendor
SDK; six spokes each implement one seam it declares (`D47.Audio`, `D47.Knowledge`, `D47.Llm`,
`D47.Stt`, `D47.Tts`, `D47.Vr`); `D47.App` is the composition root and the UI. `CLAUDE.md` is the
working guide to the tree.

## Attribution

Directive 47 was created using assets and imagery from Elite Dangerous, with the permission of
Frontier Developments plc, for non-commercial purposes. It is not endorsed by nor reflects the
views or opinions of Frontier Developments and no employee of Frontier Developments was involved
in the making of it.

The code is MIT (see [LICENSE](LICENSE)). The Elite Dangerous game data it ships is Frontier's and
is used under their [media usage
rules](https://forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/) — a
separate thing from the code licence. [NOTICE](NOTICE) sets out which is which, which tables are
derived, and from where.
