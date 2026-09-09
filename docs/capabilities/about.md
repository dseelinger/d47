---
title: About
group: Interface
nav_order: 144
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and that is load-bearing rather than cosmetic.
  HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under that class
  would silently become what the in-app panel draws on this page. The docs site styles the two
  identically (main.scss extends one from the other); the app sees only the one below.

  And no rationale in here. Every "because" belongs in the band below. That separation is the
  whole point of there being two, and it is the thing that will erode first.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to what this build is and where it keeps things.</p>
<section>
<h2><span class="num">1</span> Scroll to the bottom of Settings.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="About">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">About</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Version</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">0.94.0</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Build</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">0.94.0+4ebbc82</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Data folder</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">beside the executable</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">The bottom of the settings page and the bottom of the nav. Nothing here is a setting.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Press what you need.</h2>
<svg viewBox="0 0 880 308" role="img" aria-label="About">
 <rect x="20" y="16" width="840" height="268" rx="8" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">About</text>
 <rect x="44" y="70" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Open data folder</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">settings, logs, models, your own sounds</text>
 <rect x="44" y="126" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">What changed</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">the changelog for this build</text>
 <rect x="44" y="182" width="792" height="42" rx="6" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Check for updates</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">asks GitHub, once, at startup</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">Buttons only a person presses. There is nothing here for the AI to do.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<svg viewBox="0 0 880 152" role="img" aria-label="The version in the title bar is not always the whole story.">
 <rect x="20" y="20" width="840" height="112" rx="8" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">The version in the title bar is not always the whole story.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">A local build reports the release number there and its full stamp only here. This page is the one to read.</text>
</svg>
</section>
</div></div>
</details>

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
