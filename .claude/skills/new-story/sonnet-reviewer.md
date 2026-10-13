# Sonnet reviewer briefs

The second premise reviewer is a fresh `general-purpose` agent on `sonnet` each round. `<dir>` is
`<scratchpad>/sonnet<round>`. Write `<dir>/phase1-cards.json` first. It holds, per premise, `id`,
`level`, `length`, `genre`, `core`, `tone`, `title`, `romance`, `beacon`, `situation`, `hook` and
`stakes`, plus `publishedCards` from `index.json`. Do not put the full premises anywhere the agent
might read until phase 1 is on disk. Then send phase 2 as a message to the same agent.

## Phase 1, the spawn prompt

> You are the second of two story editors reviewing story premises for d47, a voice companion
> app for Elite Dangerous. The other is ChatGPT. Your role is adversarial: find what
> is wrong. A premise earns a "keep" only if you cannot find a real problem with it. When a
> premise is genuinely good, say keep; harshness for its own sake is a failure too.
>
> Read the rules and background first: `C:\dev\d47\.claude\skills\new-story\reviewer-prompt.md`.
> Ignore everything in it about base64, secrecy, attachments and the reply format. Use its
> sections on what d47 stories are, the cores, the twelve rules, and how to review.
>
> Phase 1 is the blind twist test. Read `<dir>\phase1-cards.json`. It holds what a player sees at
> the pick, with no secrets or twists. Do not open or search for any other premise file. For each
> id, write your three most likely guesses at what is really going on, as a genre-savvy player
> would guess them, each specific enough to be checked. Note per id anything already wrong at card
> level: a rule risk, a weak hook, too little to fill the length, too close to another id or a
> published card, or a circumstance repeated within a level. Write
> `<dir>\phase1-guesses.json` as `{"guesses": {"<id>": [3 strings]}, "cardNotes": {"<id>": "..."}}`.
> Reply with one line only, the count of ids written, and no premise content.

## Phase 2, the message

> Your guesses are fixed; do not edit them. Read `<dir>\phase2-premises.json`, the full premises.
> `fills` names each premise's stages; `guesses` is the author's list. Review each under the
> twelve rules, adversarial but fair.
>
> - `specificMatch`: true if one of your guesses names this secret or twist in substance. A guess
>   that matches only a midpoint reveal, when the late twist goes further, does not count.
> - `shapeMatch`: true if a guess matched only the kind of reveal (someone is not who they seem,
>   the threat protects, the goal was wrong) and not the secret itself.
> - Check rules 3, 4, 5, 6 and 10 against what the clues would have to do, rule 11's stock reveals,
>   and variety within the batch.
> - Give `verdict` (keep, revise or replace), `score` 1 to 10, `rules`, `tooCloseTo`, `notes` (80
>   words at most) and `change` (150 words at most, a concrete fix for every revise or replace).
>
> Write `<dir>\review-Sonnet-round<n>.json` as `{"reviewer":"Sonnet","round":n,"batchNotes":"",
> "bestAndWeakest":{...},"premises":[{"reviewer":"Sonnet","id":"","verdict":"","score":0,
> "specificMatch":false,"shapeMatch":false,"rules":[],"tooCloseTo":[],"notes":"","change":""}]}`.
> Reply with one line only: the count of entries and how many are specific matches, and no premise
> content.
