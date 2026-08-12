---
title: d47
---

# d47

*Directive 47: Optimize Inferior Systems.*

A voice companion for Elite Dangerous on Windows 11. It reads the journal Elite already
writes, answers out loud, and renders the same panel to a desktop window and a SteamVR
overlay.

This site is the long form. In-app help is the short form: every settings row links to the
page that explains it.

## General help

- [Installing and verifying a build](install.md)

## Capabilities

Every capability d47 registers has a page here. That is enforced by a test rather than by
habit — CI fails if a registered capability has no page, or if its page does not quote the
capability's real tool schema.

| Capability | Group | What it does |
|---|---|---|
| [Diagnostics](capabilities/diagnostics.md) | Foundation | Report where d47 keeps its files, and change a subsystem's log level without a restart. |
