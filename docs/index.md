---
# The site is called Directive 47, so the home page cannot also be: the header already
# carries the name, and repeating it puts "Directive 47 | Directive 47" in the browser tab
# and a nav entry beside a logo that already links here.
title: Overview
group: General help
nav_order: 0
---

*Directive 47: Optimize Inferior Systems.*

A voice companion for Elite Dangerous on Windows 11. It reads the journal Elite already
writes, answers out loud, and renders the same panel to a desktop window and a SteamVR
overlay.

This site is the long form. In-app help is the short form: every settings row links to the
page that explains it.

## Where everything is

**The nav on the left is the whole site**, grouped the way the settings panel is grouped —
Foundation, Ship, Knowledge, Conversation, Voice, Interface, Acting on the game.

Two pages are worth reading before any capability page:

- [Installing](install.md) — getting a build, verifying it, and where it keeps its files
- [Talking to Directive 47](conversation.md) — the two answer paths, running with no model, and
  what each turn reports

## Capabilities

Every capability D47 registers has a page under **Capabilities** in the nav. That is enforced by
tests rather than by habit — CI fails if a registered capability has no page, if its page does
not quote the capability's real tool schema, if the page is filed under a different group from
the capability, or if any published page is unreachable from the nav.

There used to be a table here listing them. It listed sixteen of the thirty-three, which is what
a hand-maintained copy of a generated list does given a few months — so the nav, which is built
from each page's own front matter and checked against the registry, is the list now.

## Attribution

{{ site.frontier_attribution }}

Directive 47's own code is MIT-licensed. The Elite Dangerous game data it ships — ship and module
figures, blueprints, material names, engineer locations — is Frontier's, used under their
[media usage rules](https://forums.frontier.co.uk/threads/elite-dangerous-media-usage-rules.510879/),
and is not covered by that licence.
