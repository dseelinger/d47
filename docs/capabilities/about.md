---
title: About
group: Interface
nav_order: 144
---

What this build is, where it keeps its files, and what changed.

It is the bottom of the settings page and the bottom of the nav, which is where something read
once belongs. Until 0.76.0 it was a button in the footer beside **Open data folder**, on the
reasoning that both answer *where is this thing and what is it*. That reasoning was fine and the
placement was not: a Commander looking for the version looks down the list of settings areas, so
that is where it is now — and the footer button is **gone** rather than kept beside it, because two
ways in that can drift is exactly the kind of thing this project keeps writing rules about.

**Nothing here is a setting.** Every row states something rather than asking you to choose it, and
the buttons are things only a person presses. There are no tools and no spoken phrases: there is
nothing here for Directive 47 to do.

What the area states, on a real install:

```text
Version        0.76.0
Build          0.76.0+4ebbc82
Data folder    C:\Program Files\d47\data
Attribution    This app is unofficial and is not endorsed by Frontier Developments plc. ...
```

#### Version {#version}

Which release this is — `0.76.0`, and the same string the title bar carries.

#### Build {#build}

The exact commit this was built from, and **the reason this area exists**. A version alone cannot
tell two builds of the same release apart, and a bug report without it is a bug report about a
binary nobody can identify. Select it and paste it.

#### Data folder {#data-folder}

Where Directive 47 keeps everything it writes — settings, secrets, your checklist, the spend ledger,
your logs. It is always beside the executable and never in `%APPDATA%`, so an install is a folder
you can copy, move or delete as one thing.

#### Attribution {#attribution}

Frontier's long-form attribution, in Frontier's own words, because their media usage rules supply
the sentence and ask that it be somewhere a person can find it. The `NOTICE` file and the
documentation site carry it too; this is the copy that ships inside the binary, which is the only
one a Commander who visits neither will ever see.

#### What changed {#changelog}

**The whole changelog, from inside this build**, newest release first. It opens in a window over the
panel and it reads with **no internet at all** — which is the one thing the button it replaces could
never do, because that one opened a browser.

It is the file as it stood when this build was made, so it can never show a release *newer* than the
one you are running. That is what **Open on GitHub** is for, beside it: the web copy points at the
branch rather than at a tag, precisely so that a Commander one release behind can read the entry
they came for.

Both survive on purpose. The offline one answers when there is no network; the online one answers
about a version this build has never heard of.

#### Community {#community}

Opens the community page, which carries the invite to the Discord — where questions get
answered by a person, and where a bug report reaches somebody who can fix it. You do not need a
GitHub account to use it.

**The button opens the page rather than the invite**, and that is deliberate. A `discord.gg` link
compiled into a build is permanent: revoke that invite and every copy already installed has a dead
button, fixable only by shipping a release. The page is a file in the repository, so reissuing an
invite is a commit and every build ever installed follows it.

#### Set up keys {#set-up-keys}

Walks through the API keys again — the same guided setup first run offers.

It is here because **keys get rotated and revoked**, so the state that triggers the guide is one a
working install can come back to. Without it, declining the first-run offer once would make that
decision permanent, which is a poor property for an offer.

#### Add to Start Menu {#start-menu}

Puts a shortcut where Windows looks for one. The row is **absent once there is one**, rather than a
button that reports it already did the thing.

The same reasoning as the row above: the first-run prompt is a convenience, and without a permanent
way in, saying no once would be irreversible.
