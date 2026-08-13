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
| [Language model](capabilities/conversation.md) | Conversation | Which model answers, where it lives, and what the session has cost. |
| [Interface](capabilities/interface.md) | Interface | Themes — including one that follows your own HUD colours — and the keys that reach D47. |
| [Privacy](capabilities/privacy.md) | Foundation | Exactly what leaves this machine, to whom, and whether it is being sent right now. |
| [Settings](capabilities/settings.md) | Foundation | What a tool call may change, and the protected set that no model can reach. |
