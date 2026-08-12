---
title: Installing d47
---

# Installing and verifying a build

d47 ships as one file. There is no installer, no runtime prerequisite, and nothing that
asks for administrator rights.

1. Download `d47.exe` from the [releases page](https://github.com/dseelinger/d47/releases).
2. Put it wherever you like — a folder you own, not `Program Files`.
3. Run it.

## What "one file" does and does not mean

The build is a self-contained single-file publish: the .NET runtime and every managed
dependency are inside the executable, and native libraries are extracted to a temporary
folder on first run. It is not statically linked in the C sense. What you can rely on is
narrower and more useful:

- No .NET runtime needs to be installed first.
- No elevation, ever.
- Everything d47 writes lives in a `data` folder beside the executable. Move the folder,
  and your settings and secrets move with it.

## Verifying the download

Builds are unsigned, so Windows SmartScreen will likely warn on first run. Rather than ask
you to trust that, every release publishes a SHA-256 alongside the executable. Compare it:

```powershell
Get-FileHash .\d47.exe -Algorithm SHA256
```

Match the result against `d47.exe.sha256` from the same release. If it differs, do not run
the file.

## Where things are written

Everything is under `data`, beside the executable:

```text
d47.exe
data\
  settings.json        plain JSON, hand-editable, unknown keys rejected on load
  secrets.json         API keys, DPAPI-encrypted for your Windows account only
  logs\
    d47-20260812.log     human-readable
    d47-20260812.jsonl   the same events as newline-delimited JSON
```

Nothing leaves the machine. There is no analytics, no metrics endpoint and no crash
reporter. Providers that do send data off the machine — a cloud LLM, a paid voice, INARA,
web search — are individually enabled and each states what it transmits.

`secrets.json` is encrypted with Windows DPAPI scoped to your user account. Copying it to
another machine or another account leaves it undecryptable, and d47 treats an unreadable
secret as that capability being switched off rather than as an error.
