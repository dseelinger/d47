---
# The site is called Directive 47, so the home page cannot also be: the header already
# carries the name, and repeating it puts "Directive 47 | Directive 47" in the browser tab
# and a nav entry beside a logo that already links here.
title: Overview
---

*Directive 47: Optimize Inferior Systems.*

A voice companion for Elite Dangerous on Windows 11. It reads the journal Elite already
writes, answers out loud, and renders the same panel to a desktop window and a SteamVR
overlay.

This site is the long form. In-app help is the short form: every settings row links to the
page that explains it.

## General help

- [Installing and verifying a build](install.md)
- [Talking to Directive 47](conversation.md) — the two answer paths, running with no model, and what each turn reports

## Capabilities

Every capability D47 registers has a page here. That is enforced by a test rather than by
habit — CI fails if a registered capability has no page, or if its page does not quote the
capability's real tool schema.

| Capability | Group | What it does |
|---|---|---|
| [Diagnostics](capabilities/diagnostics.md) | Foundation | Report where D47 keeps its files, and change a subsystem's log level without a restart. |
| [Journal](capabilities/journal.md) | Foundation | Report the Commander's current system, body and docking state from the journal. |
| [Galaxy search](capabilities/galaxy.md) | Knowledge | Look up systems, stations and bodies, and work out how far apart two of them are. |
| [Route planning](capabilities/routes.md) | Knowledge | Plot a neutron route, a Road to Riches loop, or a trade run. |
| [Specifications](capabilities/specifications.md) | Knowledge | What a hull or a module can do, from a table built out of the community's own data. |
| [Engineers](capabilities/engineers.md) | Knowledge | Where each engineer is, what they grade, and how far along you are with them. |
| [Engineering](capabilities/engineering.md) | Knowledge | What a blueprint costs and changes, and how the roll on a fitted module actually went. |
| [Community goals](capabilities/community-goals.md) | Knowledge | What community goals are running, what tier they have reached, and how you are doing in them. |
| [Checklists](capabilities/checklists.md) | Knowledge | One list of what you are working on — your own lines, your ship builds and your construction sites. |
| [Exobiology](capabilities/exobiology.md) | Knowledge | Plot a circuit through known biology, and read back what your own surface scan found. |
| [System names](capabilities/system-names.md) | Knowledge | What a system's name says about it — its sector, its boxel, and the mass code that sizes it. |
| [Colonisation](capabilities/colonisation.md) | Knowledge | What your construction sites still need, what is left to haul, and which nearby systems have the bodies your next colony wants. |
| [Language model](capabilities/conversation.md) | Conversation | Which model answers, where it lives, and what the session has cost. |
| [Interface](capabilities/interface.md) | Interface | Themes — including one that follows your own HUD colours — and the keys that reach D47. |
| [Privacy](capabilities/privacy.md) | Foundation | Exactly what leaves this machine, to whom, and whether it is being sent right now. |
| [Settings](capabilities/settings.md) | Foundation | What a tool call may change, and the protected set that no model can reach. |

## Attribution

{{ site.frontier_attribution }}

Directive 47's own code is MIT-licensed. The Elite Dangerous game data it ships — ship and module
figures, blueprints, material names, engineer locations — is Frontier's, used under their
[media usage rules](https://forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/),
and is not covered by that licence.
