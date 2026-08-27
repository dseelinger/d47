# Directive 47

Directive 47 - Optimize Inferior Systems

A Guardian-flavoured voice companion for Elite Dangerous. Windows 11, .NET 10. One widget
tree renders to both a desktop window and a SteamVR overlay.

**[Documentation](https://dseelinger.github.io/d47/)** — including [how to install and
verify a build](https://dseelinger.github.io/d47/install.html).

**[Join the Discord](https://dseelinger.github.io/d47/community.html)** — questions, bug
reports and screenshots. It is the place to reach a person; you do not need a GitHub account.

## Status

**It is built and it is flown.** Phases 1 to 54, 57 and 58 have shipped — the journal spine,
typed and spoken conversation, the settings surface, speaking and listening, game knowledge,
proactive callouts, the SteamVR overlay, acting on the game, personas and voices, the
soundscape, hands-free listening, knowledge from outside the journal, ships and modules,
engineers and blueprints, routing, the build gauges, adventures, and per-role voices.

That is a description rather than a boast: it is one person's project, it has a handful of
users, and the [open issues](https://github.com/dseelinger/d47/issues) are the honest list of
what is wrong with it today.

See [list.md](list.md) for what has shipped and what each phase covers,
[issues labelled `phase`](https://github.com/dseelinger/d47/issues?q=is%3Aissue+is%3Aopen+label%3Aphase)
for what is planned but not built yet,
[docs/plans/build-order.md](docs/plans/build-order.md) for what is being built next and why,
[CHANGELOG.md](CHANGELOG.md) for what changed in each release, and
[architecture.md](architecture.md) for how it is built.

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
