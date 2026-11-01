---
title: About
group: Interface
nav_order: 165
---

<!--
  The how-to band (#229). Same authoring rules as the ELI5 band below it — they are in the
  comment on engineers.md — with one addition and one subtraction.

  The class is d47-howto rather than d47-eli5, and the class decides behaviour, not just
  appearance. HelpLibrary.Band takes the first d47-eli5 div in the file, so a second band under
  that class would silently become what the in-app panel draws on this page. The docs site styles
  the two identically (main.scss extends one from the other); the app draws this band first and the How it works band after it.

  And no rationale in here. Every "because" belongs in the band below. Keeping the two apart is
  the reason there are two of them, and it is the first rule here that will be forgotten.
-->
<details class="d47-band" open>
<summary>How to use it</summary>
<div class="d47-howto"><div class="d47-frame">
<p class="intro">Two steps to what this build is and where it keeps things.</p>
<section>
<h2><span class="num">1</span> Open Updates and install.</h2>
<p class="say">Open Settings › Privacy and this install › Updates and install and find the group This install.</p>
<p class="expect">You see Version, Build and Data folder, with the version and the build stamp as values.</p>
<svg viewBox="0 0 880 308" role="img" aria-label="About">
 <rect x="20" y="16" width="840" height="268" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">This install</text>
 <rect x="44" y="70" width="792" height="42" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Version</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">0.94.0</text>
 <rect x="44" y="126" width="792" height="42" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Build</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text-muted)">0.94.0+4ebbc82</text>
 <rect x="44" y="182" width="792" height="42" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Data folder</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">beside the executable</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">Nothing in this group is a setting.</text>
</svg>
</section>
<section>
<h2><span class="num">2</span> Press what you need.</h2>
<p class="say">Press Open data folder, Changelog or Check for updates, whichever you need.</p>
<p class="expect">The folder opens, a page titled "What changed" shows, or D47 asks GitHub for a newer release.</p>
<svg viewBox="0 0 880 308" role="img" aria-label="About">
 <rect x="20" y="16" width="840" height="268" fill="var(--surface-alt)" stroke="var(--border)" stroke-width="2"/>
 <text x="44" y="52" font-size="17" font-weight="700" fill="var(--text)">This install</text>
 <rect x="44" y="70" width="792" height="42" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="98" font-size="16" fill="var(--text)">Open data folder</text>
 <text x="812" y="98" text-anchor="end" font-size="16" fill="var(--text)">settings, logs, models, your own sounds</text>
 <rect x="44" y="126" width="792" height="42" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="68" y="154" font-size="16" fill="var(--text)">Changelog</text>
 <text x="812" y="154" text-anchor="end" font-size="16" fill="var(--text)">the changelog for this build</text>
 <rect x="44" y="182" width="792" height="42" fill="var(--surface)" stroke="var(--border)" stroke-width="1.5"/>
 <text x="68" y="210" font-size="16" fill="var(--text)">Check for updates</text>
 <text x="812" y="210" text-anchor="end" font-size="16" fill="var(--text-muted)">asks GitHub whenever you press it</text>
 <text x="44" y="278" font-size="15" fill="var(--text-muted)">Buttons only a person presses.</text>
</svg>
</section>
<section>
<h2><span class="num">!</span> The one that stops people.</h2>
<p class="say">Read the build stamp on this page, not the version in the title bar.</p>
<p class="expect">Build shows the full stamp; the version in the title bar is only the release number.</p>
<svg viewBox="0 0 880 152" role="img" aria-label="The version in the title bar is not always the whole story.">
 <rect x="20" y="20" width="840" height="112" fill="var(--surface)" stroke="var(--danger)" stroke-width="2.5"/>
 <text x="440" y="62" text-anchor="middle" font-size="19" font-weight="800" fill="var(--danger)">The version in the title bar is not always the whole story.</text>
 <text x="440" y="100" text-anchor="middle" font-size="16" fill="var(--text)">A local build reports the release number there and its full stamp only here. This page is the one to read.</text>
</svg>
</section>
</div></div>
</details>

<!--
  The ELI5 band. Rules in the comment on engineers.md: no blank lines, never four spaces of
  indent, well-formed XML with no HTML entities, nothing below font-size 14, and colours are
  the nine Palette roles and nothing else.
-->
<details class="d47-band">
<summary>How it works</summary>
<div class="d47-eli5"><div class="d47-frame">
<p class="intro">This page states which build you run and where it keeps its files.</p>
<section>
<h2><span class="num">1</span> The rows state facts; only the buttons act.</h2>
<svg viewBox="0 0 880 162" role="img" aria-label="Version, Build and Data folder state; buttons act">
 <rect x="20" y="16" width="840" height="130" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">Version 0.111.0</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">Build 0.111.0+4ebbc82</text>
 <text x="440" y="124" text-anchor="middle" font-size="16" fill="var(--text-muted)">Data folder beside the executable</text>
</svg>
<p class="body">Version, Build, Data folder and Attribution only state. The buttons, such as Open data folder and Check for updates, are pressed by a person; there are no tools or spoken phrases.</p>
</section>
<section>
<h2><span class="num">2</span> Check for updates asks GitHub on demand.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="A press asks GitHub and the row reports one of four answers">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="16" fill="var(--text-muted)">You press Check for updates.</text>
 <text x="440" y="90" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">"D47 0.111.0 is the latest release."</text>
</svg>
<p class="body">It makes the same request as the startup check, whether or not that check is on. A build that is not a release has nothing to compare with, and says so.</p>
</section>
<section>
<h2><span class="num">3</span> The Build row names the exact commit.</h2>
<svg viewBox="0 0 880 128" role="img" aria-label="The build stamp names the commit">
 <rect x="20" y="16" width="840" height="96" fill="var(--surface)" stroke="var(--accent)" stroke-width="2.5"/>
 <text x="440" y="56" text-anchor="middle" font-size="18" font-weight="700" fill="var(--text)">Build 0.111.0+4ebbc82</text>
 <text x="440" y="90" text-anchor="middle" font-size="16" fill="var(--text-muted)">Quote this in a bug report.</text>
</svg>
<p class="body">Two builds of one release share a version but not a commit. <code>AboutCapability.cs</code> shows the full build string in the Build row.</p>
</section>
</div></div>
</details>

What this build is, where it keeps its files, and what changed.

Its rows are in Settings › Privacy and this install › Updates and install, group This install.

**Nothing here is a setting.** Every row states something rather than asking you to choose it, and
the buttons are things only a person presses. There are no tools and no spoken phrases: there is
nothing here for Directive 47 to do.

What the area states, on a real install:

```text
Version        0.111.0
Build          0.111.0+4ebbc82
Data folder    C:\Users\<you>\AppData\Local\Programs\d47\data
Attribution    This app is unofficial and is not endorsed by Frontier Developments plc. ...
```

#### Version {#version}

Which release this is, such as `0.111.0`. A pre-release says so after it.

**Check for updates**, under the value, asks GitHub for the latest release — the same request the
startup check makes, run on demand. It works whether or not **Check for updates at startup** is on,
and it says which of four things it found:

```text
D47 0.111.0 is available.
D47 0.110.0 is the latest release.
Could not reach GitHub, so D47 does not know whether there is a newer release.
This build is not a release, so there is nothing to compare it with.
```

The last one is a local or dev build, not a mistake — there is no tag for it to compare against.

#### Install {#install-update}

Present only while a check — either one — has found a release newer than this build. Pressing it
downloads that release, replaces this build with it, and restarts, the same as **Update now** on
the home tab: the two are one path, so accepting from either surface leaves the other with nothing
further to offer while it runs.

#### Build {#build}

The exact commit this was built from. A version alone cannot tell two builds of the same release
apart, so quote this in a bug report.

#### Data folder {#data-folder}

Where Directive 47 keeps everything it writes — settings, secrets, your checklist, the spend ledger,
your logs. It is always beside the executable and never in `%APPDATA%`, so an install is a folder you can
copy, move or delete as one thing. Open data folder opens it in Windows Explorer.

#### Attribution {#attribution}

Frontier's long-form attribution, in Frontier's own words. The `NOTICE` file and the documentation
site carry it too; this is the copy that ships inside the binary.

#### What changed {#changelog}

Press **Changelog** and a page titled "What changed" opens over the panel with the whole changelog,
newest release first. It reads with no internet.

It is the file as it stood when this build was made, so it never shows a release newer than the one
you are running. **What changed since**, beside it, has the button **Open on GitHub**, which opens
the web copy in your browser.

#### Community {#community}

Press **Open the Discord** and your browser opens D47's community page, which carries the invite.
You do not need a GitHub account to use it.

The button opens a page in the repository rather than the invite, so reissuing an invite needs no new
release.

#### Set up keys {#set-up-keys}

Press **Set up keys** and the dialog titled "Set up Directive 47" walks through the API keys again,
the same guided setup first run offers.

#### Add to Start Menu {#start-menu}

Puts a shortcut where Windows looks for one. The row is **absent once there is one**, rather than a
button that reports it already did the thing.

Press it and Directive 47 appears in the Start Menu.
