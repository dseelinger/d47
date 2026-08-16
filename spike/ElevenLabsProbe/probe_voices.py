"""What voices an ElevenLabs account actually offers, and which of them need one.

Answers, against the live service: what `GET /v1/voices` returns, whether the premade
voices d47 names as defaults are in it, whether the Voice Library is a second source,
whether a shared voice can be spoken by id, and which of these calls need a key at all.

Runs with no key and reports what the service gives an anonymous caller. Set
ELEVENLABS_API_KEY to run the authenticated half as well - the half that answers what a
*new* account sees, which is the question and the one an anonymous call cannot settle.
Prints; writes nothing.
"""
import json
import os
import sys
import urllib.error
import urllib.request

HOST = "https://api.elevenlabs.io"
UA = "d47-spike/0.1 (+https://github.com/dseelinger/d47)"

# The three d47 hands out by name: Warden's default, and two the pairing tests assert on.
NAMED = ("George", "Matilda", "Callum")

KEY = os.environ.get("ELEVENLABS_API_KEY")


def call(path, key=None, method="GET", body=None):
    """Status, parsed body (or raw head), and how many bytes came back."""
    headers = {"User-Agent": UA}
    if key:
        headers["xi-api-key"] = key
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"

    request = urllib.request.Request(HOST + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=60) as response:
            raw = response.read()
            status = response.status
    except urllib.error.HTTPError as error:
        raw = error.read()
        status = error.code
    except Exception as error:  # noqa: BLE001 - a probe reports rather than raises
        return None, {"ERROR": str(error)}, 0

    try:
        return status, json.loads(raw), len(raw)
    except ValueError:
        # Audio, or an error page. The head is enough to tell which.
        return status, {"RAW": raw[:80].hex()}, len(raw)


def named_in(voices):
    """Which of the three d47 names are present, matched the way VoicePairing matches.

    Not equality: the same voice is "George" on one account and "George - Warm,
    Captivating Storyteller" on another, which is the join d47 already relaxes.
    """
    found = {}
    for want in NAMED:
        for voice in voices:
            name = (voice.get("name") or "")
            if name == want or name.startswith(want + " "):
                found[want] = voice.get("voice_id")
                break
    return found


def describe(label, voices):
    print(f"  {label}: {len(voices)} voices")
    categories = {}
    for voice in voices:
        categories[voice.get("category")] = categories.get(voice.get("category"), 0) + 1
    print(f"    by category: {categories}")
    print(f"    of the three d47 names: {named_in(voices)}")


def question_one():
    """What does the list endpoint return, and does it need an account at all?"""
    print("\n1. GET /v1/voices")

    status, body, size = call("/v1/voices")
    print(f"  no key at all -> {status}, {size} bytes")
    if status == 200:
        describe("anonymous", body.get("voices", []))
        for voice in body.get("voices", []):
            labels = voice.get("labels") or {}
            print(f"      {voice['voice_id']} | {voice['name']} | {voice.get('category')} "
                  f"| {labels.get('accent')} {labels.get('gender')}")
    else:
        print(f"    {json.dumps(body)[:200]}")

    # show_legacy is documented as adding the older premade set. Worth one call, because
    # if it changes nothing there is no parameter for d47 to start sending.
    status, legacy, size = call("/v1/voices?show_legacy=true")
    print(f"  no key, show_legacy=true -> {status}, {size} bytes")
    if status == 200:
        describe("anonymous, legacy shown", legacy.get("voices", []))

    status, bogus, _ = call("/v1/voices", key="sk_not_a_real_key_0000")
    print(f"  a key that is not a key -> {status}: {json.dumps(bogus)[:160]}")

    if KEY:
        status, mine, size = call("/v1/voices", key=KEY)
        print(f"  the key in ELEVENLABS_API_KEY -> {status}, {size} bytes")
        if status == 200:
            describe("this account", mine.get("voices", []))
            for voice in mine.get("voices", []):
                print(f"      {voice['voice_id']} | {voice['name']} | {voice.get('category')}")
    else:
        print("  no ELEVENLABS_API_KEY set, so the account half is unanswered")


def question_two():
    """Are the premade voices only in the shared library?"""
    print("\n2. GET /v1/shared-voices - the Voice Library as a second source")

    # Both sizes, because the boundary is the finding: an anonymous caller is allowed a
    # taste of the library and not the library, and a picker cannot be built on a taste.
    for size_asked in (30, 3):
        status, body, size = call(f"/v1/shared-voices?page_size={size_asked}", key=KEY)
        print(f"  page_size={size_asked} ({'with key' if KEY else 'no key'}) -> {status}, {size} bytes")
        if status == 200:
            voices = body.get("voices", [])
            print(f"    {len(voices)} voices on the first page, has_more={body.get('has_more')}")
            for voice in voices[:5]:
                print(f"      {voice['voice_id']} | owner {voice['public_owner_id'][:12]}… | {voice['name']}")
            print(f"    of the three d47 names: {named_in(voices)}")
        else:
            print(f"    {json.dumps(body)[:200]}")

    # v2 is the documented successor and takes voice_type=default, which is the direct
    # way to ask "what does this account get for free".
    status, body, size = call("/v2/voices?voice_type=default&page_size=30", key=KEY)
    print(f"  GET /v2/voices?voice_type=default ({'with key' if KEY else 'no key'}) -> {status}, {size} bytes")
    if status == 200:
        describe("default voices", body.get("voices", []))
    else:
        print(f"    {json.dumps(body)[:200]}")


def question_three():
    """Can a shared-library voice be spoken by id, without adding it first?"""
    print("\n3. Speaking a voice that is not in the account")

    status, shared, _ = call("/v1/shared-voices?page_size=1", key=KEY)
    if status != 200 or not shared.get("voices"):
        print("  could not reach the shared library; skipping")
        return

    candidate = shared["voices"][0]
    print(f"  candidate: {candidate['name']} ({candidate['voice_id']})")

    for label, voice_id in (("a premade voice", "JBFqnCBsd6RMkjVDRZzb"),
                            ("a shared-library voice", candidate["voice_id"])):
        status, body, size = call(
            f"/v1/text-to-speech/{voice_id}?output_format=pcm_24000",
            key=KEY,
            method="POST",
            body={"text": "Directive forty seven.", "model_id": "eleven_multilingual_v2"})
        print(f"  POST tts, {label} ({'with key' if KEY else 'no key'}) -> {status}, {size} bytes")
        if status != 200:
            print(f"    {json.dumps(body)[:200]}")


def question_four():
    """Is adding a shared voice a call d47 could make?"""
    print("\n4. POST /v1/voices/add/{public_user_id}/{voice_id}")

    status, shared, _ = call("/v1/shared-voices?page_size=1", key=KEY)
    if status != 200 or not shared.get("voices"):
        print("  could not reach the shared library; skipping")
        return

    candidate = shared["voices"][0]

    if not KEY:
        # Deliberately still called. What an unauthenticated caller is told is the shape
        # d47 would have to read, and it costs nothing to find out.
        status, body, _ = call(
            f"/v1/voices/add/{candidate['public_owner_id']}/{candidate['voice_id']}",
            method="POST",
            body={"new_name": "d47 probe"})
        print(f"  no key -> {status}: {json.dumps(body)[:200]}")
        print("  set ELEVENLABS_API_KEY to answer this one; it writes to the account, so it is opt-in")
        return

    print("  not run: this one adds a voice to the account, which a probe should not do")
    print(f"  the call would be POST /v1/voices/add/{candidate['public_owner_id']}/{candidate['voice_id']}")
    print("  with body {\"new_name\": \"…\"}, and needs only the same xi-api-key header")


def main():
    print(f"ElevenLabs voice probe — key {'present' if KEY else 'absent'}")
    question_one()
    question_two()
    question_three()
    question_four()
    return 0


if __name__ == "__main__":
    sys.exit(main())
