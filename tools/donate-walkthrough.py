"""Draws the numbered highlight boxes onto the journal donation captures for docs/donate-journals.md.

Run TheJournalHistoryIsDonatedInFiveSteps first; it saves the captures and donate-boxes.json to
%TEMP%\\d47-ui-captures\\<run>. With no argument this reads the newest run.

    python tools/donate-walkthrough.py [capture-folder]
"""
import glob
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "docs", "assets", "donate-journals")

# Capture, the page's image name, and the boxes to draw in the order the page numbers them.
PLAN = [
    ("donate-2-journal.png", "1-open.png", ["tab", "journal", "button"]),
    ("donate-3-opened.png", "2-history.png", ["history"]),
    ("donate-4-history.png", "3-read.png", ["scope", "read"]),
    ("donate-5-report.png", "4-send.png", ["disclosure", "send"]),
    ("donate-6-sent.png", "5-sent.png", ["status", "forget"]),
]

CYAN = (25, 227, 255)
PAD = 7
RADIUS = 15


def newest_run():
    runs = glob.glob(os.path.join(os.environ["TEMP"], "d47-ui-captures", "*", "donate-boxes.json"))
    if not runs:
        sys.exit("No captures found. Run TheJournalHistoryIsDonatedInFiveSteps first.")
    return os.path.dirname(max(runs, key=os.path.getmtime))


def main():
    captures = sys.argv[1] if len(sys.argv) > 1 else newest_run()
    font = ImageFont.truetype(os.path.join(os.environ["WINDIR"], "Fonts", "segoeuib.ttf"), 19)

    with open(os.path.join(captures, "donate-boxes.json"), encoding="utf-8-sig") as file:
        shots = {shot["File"]: shot for shot in json.load(file)}

    os.makedirs(OUT, exist_ok=True)

    for capture, name, wanted in PLAN:
        image = Image.open(os.path.join(captures, capture)).convert("RGB")
        draw = ImageDraw.Draw(image)
        boxes = {box["Name"]: box for box in shots[capture]["Boxes"]}

        for number, key in enumerate(wanted, start=1):
            box = boxes[key]
            left, top = box["X"] - PAD, box["Y"] - PAD
            right, bottom = box["X"] + box["Width"] + PAD, box["Y"] + box["Height"] + PAD
            draw.rectangle([left, top, right, bottom], outline=CYAN, width=3)

            # A lone box needs no number.
            if len(wanted) > 1:
                cx = max(RADIUS + 2, left - 6)
                cy = max(RADIUS + 2, top - 6)
                draw.ellipse([cx - RADIUS, cy - RADIUS, cx + RADIUS, cy + RADIUS], fill=CYAN, outline=(0, 0, 0), width=2)
                draw.text((cx, cy), str(number), fill=(0, 0, 0), font=font, anchor="mm")

        image.save(os.path.join(OUT, name), optimize=True)
        print(os.path.join(OUT, name))


if __name__ == "__main__":
    main()
