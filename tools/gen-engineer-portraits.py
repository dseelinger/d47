#!/usr/bin/env python3
"""Regenerate d47's engineer portraits.

Not part of the build and never run by CI. Its output is committed under
`assets/engineers/` and shipped beside the exe; the app never fetches a picture:

    python tools/gen-engineer-portraits.py <folder>

The pictures are the page images of the engineers' articles on the Elite Dangerous Wiki
(Fandom), saved by hand from each article into <folder> under the wiki's own file names (the
wiki's image host refuses scripted downloads). FILES names the picture of each engineer in
`src/D47.Core/Knowledge/Engineers.tsv`. For each engineer the script writes two files named
by the engineer's id:

    <id>.jpg        the whole picture, long edge at most 1024 px
    <id>.face.jpg   a 256 px square crop around the face

Nothing is written unless every engineer in the table has a picture and a crop. The pictures
are Frontier's in-game imagery; the wiki's CC BY-SA licence covers its text, not these. See
NOTICE.

CROPS holds each engineer's square crop, id: (x, y, side) in source pixels. A face detector
placed the crops and the maintainer approved all 38 of them by eye.
"""

import csv
import io
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
TABLE = ROOT / "src" / "D47.Core" / "Knowledge" / "Engineers.tsv"
OUT = ROOT / "assets" / "engineers"

LONG_EDGE = 1024
FACE = 256
QUALITY = 88

FILES = {
    300000: "Didi_Vaterman.webp",
    300010: "Bill_Turner.webp",
    300030: "Broo_Tarquin.webp",
    300040: "The_Sarge.webp",
    300050: "Zacariah_Nemo.webp",
    300080: "Liz_Ryder.webp",
    300090: "Hera_Tani.webp",
    300100: "FelicityFace.webp",
    300110: "Ram_Tah.webp",
    300120: "Lei_Cheung.webp",
    300130: "Petra-Olmanova-Elite-Dangerous.webp",
    300140: "Bris_Dekker-0.webp",
    300150: "Marsha-Hicks-Elite-Dangerous.webp",
    300160: "Elvira_Martuuk.webp",
    300180: "DwellerFace.webp",
    300200: "Marco_Qwent.webp",
    300210: "Selene_Jean.webp",
    300220: "Professor_Palin.webp",
    300230: "Lori_Jameson.webp",
    300250: "Juri_Ishmaak.webp",
    300260: "Tod_McQuinn.webp",
    300270: "Tiana_Fortune.webp",
    300280: "Mel-Brandon-Elite-Dangerous.webp",
    300290: "Etienne-Dorn-Elite-Dangerous.webp",
    300300: "Engineer_Chloe_Sedesi.webp",
    400001: "Jude_Navarro.webp",
    400002: "Domino_Green.webp",
    400003: "Hero_Ferrari.webp",
    400004: "Kit_Fowler.webp",
    400005: "Wellington_Beck.webp",
    400006: "Terra_Velasquez.webp",
    400007: "Uma_Laszlo.webp",
    400008: "Oden_Geiger.webp",
    400009: "Yarden_Bond.webp",
    400010: "Baltanos.webp",
    400011: "Eleanor_Bresa.webp",
    400012: "Rosa_Dayette.webp",
    400013: "Yi_Shen.webp",
}

CROPS = {
    300000: (49, 0, 763),  # Didi Vatermann, 1024x768
    300010: (126, 0, 768),  # Bill Turner, 1024x768
    300030: (130, 0, 768),  # Broo Tarquin, 1024x768
    300040: (52, 0, 384),  # The Sarge, 512x384
    300050: (133, 0, 768),  # Zacariah Nemo, 1024x768
    300080: (113, 0, 768),  # Liz Ryder, 1024x768
    300090: (85, 0, 748),  # Hera Tani, 1024x768
    300100: (208, 9, 247),  # Felicity Farseer, 704x256
    300110: (127, 0, 768),  # Ram Tah, 1024x768
    300120: (85, 0, 768),  # Lei Cheung, 1024x768
    300130: (260, 0, 253),  # Petra Olmanova, 668x253
    300140: (67, 0, 480),  # Colonel Bris Dekker, 640x480
    300150: (257, 0, 252),  # Marsha Hicks, 670x252
    300160: (35, 0, 198),  # Elvira Martuuk, 260x198
    300180: (715, 0, 768),  # The Dweller, 2104x768
    300200: (135, 0, 768),  # Marco Qwent, 1024x768
    300210: (164, 0, 768),  # Selene Jean, 1024x768
    300220: (141, 0, 768),  # Professor Palin, 1024x768
    300230: (166, 0, 768),  # Lori Jameson, 1024x768
    300250: (73, 0, 768),  # Juri Ishmaak, 1024x768
    300260: (130, 0, 768),  # Tod 'The Blaster' McQuinn, 1024x768
    300270: (151, 0, 768),  # Tiana Fortune, 1024x768
    300280: (146, 0, 252),  # Mel Brandon, 669x252
    300290: (162, 0, 254),  # Etienne Dorn, 670x254
    300300: (249, 0, 339),  # Chloe Sedesi, 894x339
    400001: (48, 0, 210),  # Jude Navarro, 284x210
    400002: (35, 0, 210),  # Domino Green, 284x210
    400003: (36, 0, 210),  # Hero Ferrari, 284x210
    400004: (38, 0, 210),  # Kit Fowler, 284x210
    400005: (35, 0, 210),  # Wellington Beck, 284x210
    400006: (45, 0, 210),  # Terra Velasquez, 284x210
    400007: (37, 0, 210),  # Uma Laszlo, 284x210
    400008: (20, 0, 210),  # Oden Geiger, 284x210
    400009: (36, 0, 210),  # Yarden Bond, 284x210
    400010: (13, 0, 210),  # Baltanos, 284x210
    400011: (36, 0, 210),  # Eleanor Bresa, 284x210
    400012: (36, 0, 210),  # Rosa Dayette, 284x210
    400013: (45, 0, 210),  # Yi Shen, 284x210
}


def engineers():
    rows = []

    with open(TABLE, encoding="utf-8", newline="") as handle:
        lines = [line for line in handle if not line.startswith("#")]

    for row in csv.DictReader(lines, delimiter="\t", quoting=csv.QUOTE_NONE):
        rows.append((int(row["id"]), row["name"]))

    return rows


def flatten(image):
    if image.mode in ("RGBA", "LA", "P"):
        image = image.convert("RGBA")
        base = Image.new("RGB", image.size, (0, 0, 0))
        base.paste(image, mask=image.getchannel("A"))
        return base

    return image.convert("RGB")


def whole(image):
    longest = max(image.size)

    if longest <= LONG_EDGE:
        return image

    scale = LONG_EDGE / longest

    return image.resize(
        (round(image.width * scale), round(image.height * scale)), Image.LANCZOS
    )


def face(image, engineer_id):
    x, y, side = CROPS[engineer_id]

    if x < 0 or y < 0 or x + side > image.width or y + side > image.height:
        raise ValueError(f"the crop {CROPS[engineer_id]} is outside the {image.width}x{image.height} picture")

    return image.crop((x, y, x + side, y + side)).resize((FACE, FACE), Image.LANCZOS)


def encode(image):
    buffer = io.BytesIO()
    image.save(buffer, "JPEG", quality=QUALITY, optimize=True, progressive=True)

    return buffer.getvalue()


def main():
    if len(sys.argv) != 2:
        print("usage: gen-engineer-portraits.py <folder of saved pictures>", file=sys.stderr)
        return 2

    source = Path(sys.argv[1])
    rows = engineers()
    outputs = {}
    failed = []

    for engineer_id, name in rows:
        try:
            if engineer_id not in FILES or engineer_id not in CROPS:
                raise ValueError("no picture or crop in the script")

            with Image.open(source / FILES[engineer_id]) as opened:
                image = flatten(opened)

            outputs[f"{engineer_id}.jpg"] = encode(whole(image))
            outputs[f"{engineer_id}.face.jpg"] = encode(face(image, engineer_id))
        except Exception as error:
            failed.append(f"{name}: {error}")
            continue

        print(f"{engineer_id} {name}: {image.width}x{image.height}")

    if failed:
        print("\n".join(failed), file=sys.stderr)
        print("Nothing written.", file=sys.stderr)
        return 1

    OUT.mkdir(parents=True, exist_ok=True)

    for file, data in outputs.items():
        (OUT / file).write_bytes(data)

    print(f"Wrote {len(outputs)} files to {OUT}")

    return 0


if __name__ == "__main__":
    sys.exit(main())
