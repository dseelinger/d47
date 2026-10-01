"""Generate a story cast picture through the OpenAI Images API.

Uses the OpenAI key d47 holds (openai.apiKey in the installed app's secrets.json, then
dev-install's), or OPENAI_API_KEY when set. The key is decrypted in memory and never written out.
Writes PNGs to %TEMP%\\d47-story-images unless --out is given.

    python tools/story-image.py "prompt text" --name scholarship.vance -n 3
    python tools/story-image.py --file prompt.txt --quality low
    python tools/story-image.py --list-models
"""

import argparse
import base64
import ctypes
import ctypes.wintypes
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path

API = "https://api.openai.com/v1"
SECRET_NAME = "openai.apiKey"
SECRET_FILES = [
    Path(os.environ.get("LOCALAPPDATA", "")) / "Programs" / "d47" / "data" / "secrets.json",
    Path(__file__).resolve().parent.parent / "dev-install" / "data" / "secrets.json",
]
STYLE = (
    "Square head-and-shoulders portrait. Elite Dangerous, the 34th century. "
    "No text, no logos, no watermarks."
)


class _Blob(ctypes.Structure):
    _fields_ = [("cbData", ctypes.wintypes.DWORD), ("pbData", ctypes.POINTER(ctypes.c_char))]


def unprotect(data):
    """DPAPI CurrentUser decryption with no entropy, matching DpapiSecretProtector."""
    src = _Blob(len(data), ctypes.cast(ctypes.create_string_buffer(data, len(data)), ctypes.POINTER(ctypes.c_char)))
    dst = _Blob()
    if not ctypes.windll.crypt32.CryptUnprotectData(ctypes.byref(src), None, None, None, None, 0, ctypes.byref(dst)):
        return None
    try:
        return ctypes.string_at(dst.pbData, dst.cbData).decode("utf-8")
    finally:
        ctypes.windll.kernel32.LocalFree(dst.pbData)


def api_key():
    if os.environ.get("OPENAI_API_KEY"):
        return os.environ["OPENAI_API_KEY"]
    for path in SECRET_FILES:
        if not path.is_file():
            continue
        encoded = json.loads(path.read_text(encoding="utf-8")).get(SECRET_NAME)
        if encoded and (key := unprotect(base64.b64decode(encoded))):
            return key
    sys.exit("No OpenAI key: set one in d47's settings, or set OPENAI_API_KEY.")


def request(path, key, body=None):
    req = urllib.request.Request(
        API + path,
        data=json.dumps(body).encode() if body is not None else None,
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as e:
        try:
            message = json.load(e)["error"]["message"]
        except Exception:
            message = f"HTTP {e.code}"
        sys.exit(f"OpenAI refused the request: {message}")


def main():
    p = argparse.ArgumentParser()
    p.add_argument("prompt", nargs="?")
    p.add_argument("--file", help="read the prompt from a text file")
    p.add_argument("--name", default="story", help="file name stem, such as <story-id>.<cast-id>")
    p.add_argument("--model", default="gpt-image-2.5-flare")
    p.add_argument("--quality", default="high", choices=["low", "medium", "high", "auto"])
    p.add_argument("--size", default="1024x1024", help="WxH; gpt-image-2 and later take any multiple of 16, older models only 1024x1024, 1536x1024, 1024x1536 or auto")
    p.add_argument("-n", type=int, default=1, help="images per prompt, at most 10")
    p.add_argument("--no-style", action="store_true", help="send the prompt without the house style line")
    p.add_argument("--out", default=os.path.join(os.environ.get("TEMP", "."), "d47-story-images"))
    p.add_argument("--list-models", action="store_true", help="list the image models this key can use")
    args = p.parse_args()

    key = api_key()

    if args.list_models:
        models = request("/models", key)["data"]
        for m in sorted(m["id"] for m in models if "image" in m["id"] or "dall-e" in m["id"]):
            print(m)
        return

    prompt = Path(args.file).read_text(encoding="utf-8") if args.file else args.prompt
    if not prompt:
        p.error("give a prompt or --file")
    if not args.no_style:
        prompt = f"{prompt.strip()}\n\n{STYLE}"

    result = request("/images/generations", key, {
        "model": args.model,
        "prompt": prompt,
        "quality": args.quality,
        "size": args.size,
        "n": args.n,
    })

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    for i, item in enumerate(result["data"], 1):
        path = out / f"{args.name}-{i}.png"
        while path.exists():
            path = path.with_stem(path.stem + "x")
        path.write_bytes(base64.b64decode(item["b64_json"]))
        print(path)


if __name__ == "__main__":
    main()
