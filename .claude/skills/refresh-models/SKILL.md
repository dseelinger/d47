---
name: refresh-models
description: Refresh the model catalog from the providers' own pages — read model-catalog.json and the current Anthropic, OpenAI and ElevenLabs model and pricing pages, report what differs, propose the edit as a diff and write it once the maintainer acknowledges. Stops before tools/publish-models.ps1, which pushes. Use when the user invokes /refresh-models, /refresh-models anthropic, /refresh-models claude-sonnet-5-5, or says "refresh the model catalog", "new model out", "update model prices".
---

# Refresh models

A new model or a new default reaches installs by editing `src/D47.Core/Catalog/model-catalog.json`
and running `tools/publish-models.ps1`. This skill does the edit. The facts come from the providers'
own pages and are read here, with the maintainer watching; the app never scrapes them.

An argument narrows the run: a provider (`anthropic`, `openai`, `elevenlabs`) or a model id
(`claude-sonnet-5-5`). With none, cover every provider.

## Read

1. `src/D47.Core/Catalog/model-catalog.json` — the working copy.
2. The providers' current pages, fetched with `WebFetch`:
   - Anthropic models: `https://docs.claude.com/en/docs/about-claude/models/overview`
   - Anthropic pricing: `https://docs.claude.com/en/docs/about-claude/pricing`
   - OpenAI pricing: `https://platform.openai.com/docs/pricing`
   - ElevenLabs models: `https://elevenlabs.io/docs/overview/models`
   - ElevenLabs API pricing: `https://elevenlabs.io/pricing/api`

   If a page cannot be fetched or no longer says what it did, name it in the report; do not fill the
   gap from memory. Where the `claude-api` skill is available, use it as the reference for Anthropic
   ids and traits.

Prices are dollars per million tokens (ElevenLabs: dollars per thousand characters). Cartesia and
OpenAI speech entries carry no price in the catalog; check only that their ids still exist.

## Report

List what differs from the file, one line each, with the page it was read from and today's date:

- new models not in the catalog
- changed prices
- models the provider has retired or no longer lists
- whether a provider's `default` or `backgroundDefault` should move

For each new Anthropic model, give the six traits d47 sends on: `operatorSystemMessages`,
`toolSearch`, `minimumCacheablePrefix`, `basicWebSearchOnly`, `legacyThinking`, `images`. For each
new OpenAI model, give `images`, from the input modalities on its model page
(`https://developers.openai.com/api/docs/models/<id>`). `images` is whether the model takes image input.

A trait the documentation does not state is **unverified**. Leave it at the unknown-model value
(`false`, and `1024` for `minimumCacheablePrefix`) and name it in the report as unverified. Do not
guess it from a neighbouring model.

## Propose

Show the edit to `model-catalog.json` as a diff, including `published` set to today's date
(`yyyy-MM-dd`). `publish-models.ps1` refuses a date earlier than the one on the `models` branch.

Ask the maintainer which default each provider should have, with `AskUserQuestion`, naming the
current default as the first option. Do not move a default on your own reading.

**Write nothing until the maintainer acknowledges the diff in the same turn.** An acknowledgement of
an earlier diff does not cover a changed one.

## Write

1. Apply the diff to `model-catalog.json`.
2. Run:

   ```bash
   dotnet test tests/D47.Core.Tests --filter FullyQualifiedName~ModelCatalog
   ```

3. Report the result. If a test fails, show its output and stop; do not edit tests to match.
4. When the embedded copy's `default` or `backgroundDefault` changed, add a `CHANGELOG.md` entry
   under the current unreleased heading, since the next release ships that copy. A price or trait
   change that alters nothing the user sees needs none.

## Stop before publishing

`tools/publish-models.ps1` commits the file to the `models` branch and pushes it. Run it only on the
maintainer's word, in the turn they give it. Until then, say the file is written and not published.

Do not commit unless asked; the maintainer's usual rule on pushes applies.
