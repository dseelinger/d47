# What voices an ElevenLabs account actually offers

**Measured 2026-08-16** against the live API. Probe:
[`spike/ElevenLabsProbe`](../../spike/ElevenLabsProbe).

This backs `list.md` Phase 19, *Spike: what voices does a new ElevenLabs account actually offer*.
The item asks four things and predicts a specific disaster: if `GET /v1/voices` is My Voices only,
a new Commander's picker is **empty** on the provider they just paid for, the per-core pairing has
nothing to pair from, and the named default that gives Warden "George" silently does not apply —
three failures that all look like "d47 is broken" and none of which the current code can tell apart
from an unreachable API.

**The disaster does not happen, the third failure is real anyway, and the item's framing missed the
thing worth worrying about.** In order.

---

## 1. The headline: the voice list needs no account at all

`GET https://api.elevenlabs.io/v1/voices` with **no `xi-api-key` header, no account, nothing** answers
`200` and 102,976 bytes of JSON holding **21 voices, every one of them `"category": "premade"`**.

```
$ curl -s https://api.elevenlabs.io/v1/voices | head -c 120
{"voices":[{"voice_id":"CwhRBWXzGAHq8TQ4Fs17","name":"Roger - Laid-Back, Casual, Resonant",
 "samples":null,"category":"premade", …
```

All three voices d47 hands out by name are in it, with exactly the ids
`tests/D47.Core.Tests/Persona/VoicePairingTests.cs` already asserts on:

| d47 names | Returned as | Voice id |
|---|---|---|
| George | `George - Warm, Captivating Storyteller` | `JBFqnCBsd6RMkjVDRZzb` |
| Matilda | `Matilda - Knowledgable, Professional` | `XrExE9yKIg1WjnnlVkGX` |
| Callum | `Callum - Husky Trickster` | `N2lVS1w4EtoT3dr4eOWO` |

The whole set, which is what a picker on a fresh key is drawing from:

```
Roger, Sarah, Laura, Charlie, George, Callum, River, Harry, Liam, Alice, Matilda,
Will, Jessica, Eric, Bella, Chris, Brian, Daniel, Lily, Adam, Bill
```

**`show_legacy=true` changes nothing.** The response is byte-identical — 102,976 both ways, the same
21 voices — so there is no parameter here for d47 to start sending. The documentation describes it as
adding the older premade set; whatever it once did, it does not do it now.

Every other voice endpoint refuses an anonymous caller: `/v2/voices`, `GET /v1/voices/{id}` and the
text-to-speech endpoint itself all answer `401 needs_authorization`. **The list endpoint is the
exception**, and that single fact answers the item's first question and disarms its first two
predicted failures.

## 2. The one line this spike could not measure, and why it matters

An anonymous call is not an authenticated call by a new account, and the two documented facts below
say the difference could be total:

> "Default voices are only available for accounts that were created before **March 2026**."
>
> "All our Default voices will expire on **December 31, 2026**, and they will no longer be
> accessible after this date."
>
> — [ElevenLabs help centre, *What are Default voices?*](https://elevenlabs.io/docs/help-center/product/voice-customization/my-voices/what-are-default-voices)

Those two sentences and the measurement in §1 cannot all be the whole truth. A caller with **no
account at all** was served the default catalogue on 2026-08-16, five months after a cutoff that is
supposed to have withheld it from accounts younger than March. The likeliest reading is that the
cutoff is a statement about **My Voices in the dashboard** rather than about the list endpoint, and
that `/v1/voices` still serves the premade catalogue to anyone. That is a reading, not a
measurement.

**Creating an account is not something this probe can do** — so the fresh-account column stays
documented rather than measured, and the probe takes `ELEVENLABS_API_KEY` so the authenticated half
can be filled in the day somebody has a new key to point at it. What the code has to do is the same
either way, because §3 is the finding that does not depend on this.

**And the expiry is the part the item did not ask about and should have.** d47's named default for
Warden is George, and George is on a published list of voices that stop existing on 2026-12-31 —
**four and a half months from this measurement**. The migration table names `Eldrin - Crisp British
Baritone` as George's replacement, `Maisie - Friendly Casual Neighbor` for Matilda and `Kellan -
Casual Friendly Speaker` for Callum. Nothing needs to move today; something needs to have moved
before the year ends, and a named default that resolves to nothing must degrade to "the model picks
one" rather than to silence.

## 3. Three failures wearing one face, which is the real bug

`ElevenLabsTtsProvider.ListVoicesAsync` returns `[]` for **all** of these:

| What happened | What the API says | What d47 returns today |
|---|---|---|
| No key stored | *nothing is sent* | `[]`, `LogDebug` |
| Key is wrong | `401` `invalid_api_key`, "Invalid API key" | `[]`, `LogWarning` |
| Network is down | connection failure | `[]`, `LogWarning` |
| Account genuinely has no voices | `200`, `{"voices":[]}` | `[]`, no log at all |

Measured shapes, which are what a caller has to read to tell them apart:

```
no header      → 401 {"detail":{"code":"unauthorized","status":"needs_authorization",
                      "message":"Neither authorization header nor xi-api-key received…"}}
bad key        → 401 {"detail":{"code":"unauthorized","status":"invalid_api_key",
                      "message":"Invalid API key"}}
```

The item is right that these are indistinguishable, and right that all three read as "d47 is
broken". They are distinguishable at the seam — the status and the `status` field say which — and
nothing above the seam is told. **This is what ships from the spike**: an empty picker has to say
*which* empty it is.

There is a second consequence, cheaper and easy to miss. d47 **short-circuits before calling** when
no key is stored, and §1 says the call would have worked. A Commander deciding whether ElevenLabs is
worth paying for could be shown the 21 voices they would get, from a request that costs nothing and
needs nothing. Not a requirement of this item; recorded because it is now known to be free.

## 4. The Voice Library is a second source, and a poor one to lean on

`GET /v1/shared-voices` is the community library. It is **also reachable anonymously, but only
barely**:

```
page_size=30, no key → 401 "You must be logged in to fetch more than 3 voices."
page_size=3,  no key → 200, 3 voices, has_more=true
```

Three at a time is a taste, not a picker. With a key it is the whole library, and none of the three
names d47 uses appear in it — the premade voices live in `/v1/voices` and the library is other
people's voices. Two documented restrictions matter more than the pagination:

- **"Voice Library voices are not available via the API to free tier users."**
- Some library voices carry a **credit multiplier** and are refused on the free plan with
  "This voice is not available for free users".

Both are quoted from
[the Voice Library documentation](https://elevenlabs.io/docs/eleven-creative/voices/voice-library).

**So the picker does not need a second source**, which is the decision this question existed to
settle. `/v1/voices` is populated, it is populated without a key, and the library adds a second
network call, a pagination protocol and a tier-dependent failure mode in exchange for voices nobody
asked for.

## 5. A library voice can be spoken by id, and adding one is a call d47 should not make

> "You can use voices from the Voice Library directly without saving them to My Voices."
>
> — [ElevenLabs, *Voice Library*](https://elevenlabs.io/docs/eleven-creative/voices/voice-library)

Documented rather than measured — synthesis needs a key, and this probe has none. It answers the
item's third question: **no add step is required before speaking**, so the fourth question is moot
in the direction that matters.

For completeness, the add call exists and is within reach of a plain key:

```
POST /v1/voices/add/{public_user_id}/{voice_id}
{ "new_name": "…" }
```

`public_user_id` comes back on every `/v1/shared-voices` entry, so d47 has both halves of the path
already. **It should still not make this call.** It writes to the Commander's account, it is not
needed to speak the voice, and a companion that quietly adds things to somebody's paid account is
doing something they did not ask for. The probe declines to run it for the same reason.

## 6. What this changes

| Question the item asked | Answer | What ships |
|---|---|---|
| What does `/v1/voices` return on a fresh account? | 21 premade voices, and **no account is needed at all** (measured anonymously; a fresh authenticated key is not measured) | Nothing. The feared empty picker is not what happens. |
| Are the premade voices in it, or only in the shared library? | **In it.** George, Matilda and Callum with the ids the tests already use | Nothing. The named default works. |
| Can a shared-library voice be spoken by id? | Yes, without adding it (documented) | Nothing — the case does not arise, because §4 says d47 has no reason to read the library. |
| Is adding one a call d47 could make? | Yes, and it should not | Nothing, deliberately. |
| *(not asked)* Can d47 tell an empty list from a broken one? | **No, and that is the real bug** | An empty voice list says which empty it is. |
| *(not asked)* How long does George exist for? | Until **2026-12-31**, published | A named default that no longer resolves degrades to the model's choice, never to silence. |

## 7. A note on where these answers came from

`help.elevenlabs.io` answers **403** to an automated fetch and renders perfectly in a browser. The
same articles are mirrored under `elevenlabs.io/docs/help-center/…` and answer 200, which is where
every quotation above was read.

That is the third costume of the same mistake `list.md` Phase 20's *Read the sources nobody has read
yet* exists to catch — 402 on the Elite wiki, 403 on the Frontier forums, 403 here. **A fetch
returning 403 is a fact about the fetcher, never about the source.** It cost nothing this time only
because the mirror was looked for.
