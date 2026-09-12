"""Writes the key layout of every Stream Deck profile named Directive 47 Development.

One profile per device: the MK.2 gets PAGE_1 and PAGE_2, Stream Deck Mobile gets MOBILE_PAGE_1.
Create the profile in the app first for a new device, then run this.

Stream Deck must be closed: it holds profiles in memory and writes them back on exit, so an edit
made while it runs is lost. Run gen_tiles.py first, or pass --tiles to run it here.

    python tools/deck/apply_profile.py --tiles

Format notes worth keeping in front of you, all of them learned the hard way and recorded in
.claude/skills/stream-deck/SKILL.md:

- every controller needs "Type": "Keypad", or the actions are silently discarded
- a key image path is relative to the *page* folder, not the profile root
- Pages.Default must name a page that is not in Pages.Pages
"""

import argparse
import json
import os
import subprocess
import sys
import uuid

PROFILE_NAME = 'Directive 47 Development'
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DECK = os.path.join(REPO, 'tools', 'deck')

ALPHA = '0123456789ABCDEFGHIJKLMNOPQRSTVW'


def folder_for(page_uuid):
    """Stream Deck names a page folder from the page UUID: base32hex with U removed."""
    v = int.from_bytes(uuid.UUID(page_uuid).bytes, 'big') << 2
    return ''.join(ALPHA[(v >> (5 * i)) & 31] for i in range(26))[::-1] + 'Z'


def _action(action_uuid, name, plugin_name, plugin_uuid, settings, image):
    return {
        'ActionID': str(uuid.uuid4()),
        'LinkedTitle': False,
        'Name': name,
        'Plugin': {'Name': plugin_name, 'UUID': plugin_uuid, 'Version': '1.0'},
        'Settings': settings,
        'State': 0,
        # The label is baked into the image, so Stream Deck draws no title of its own.
        'States': [{
            'FontFamily': 'Arial', 'FontSize': 12, 'FontStyle': 'Bold', 'FontUnderline': False,
            'Image': 'Images/' + image + '.png', 'OutlineThickness': 2,
            'ShowTitle': False, 'Title': '', 'TitleAlignment': 'bottom', 'TitleColor': '#ffffff',
        }],
        'UUID': action_uuid,
    }


def run(script, image):
    return _action('com.elgato.streamdeck.system.open', 'Open', 'Open',
                   'com.elgato.streamdeck.system.open',
                   {'path': os.path.join(DECK, script)}, image)


def send(text, image):
    """Types into whatever window has focus, then presses Enter."""
    return _action('com.elgato.streamdeck.system.text', 'Text', 'Text',
                   'com.elgato.streamdeck.system.text',
                   {'Hotkey': {'KeyModifiers': 0, 'QTKeyCode': 33554431, 'VKeyCode': -1},
                    'isSendingEnter': True, 'isTypingMode': False, 'pastedText': text}, image)


# Row 0 opens a session, row 1 types into the session that has focus, row 2 runs the app and
# cuts releases. Both release scripts follow the run to the end on their own.
PAGE_1 = {
    '0,0': run('triage.cmd', 'triage'),
    '1,0': run('coordinator.cmd', 'coord'),
    '2,0': run('architect.cmd', 'architect'),
    '3,0': run('issue-worker.cmd', 'issue'),
    '4,0': run('review.cmd', 'review'),

    '0,1': send('/desktop', 'desktop'),
    '1,1': send('push', 'push'),
    '2,1': send('/wrap-up', 'wrapup'),

    '0,2': run('test-drive.cmd', 'testdrive'),
    '1,2': run('restart-test-drive.cmd', 'restart'),
    '3,2': run('release-patch.cmd', 'patch'),
    '4,2': run('release-minor.cmd', 'minor'),
}

# Nothing navigates here. A major release is deliberate: tools\release.ps1 -Major.
PAGE_2 = {}

MOBILE_MODEL = 'VSD2/WiFi'

# The free Mobile tier: six keys, three columns by two rows, shown on the phone as three rows of two.
MOBILE_PAGE_1 = {
    '0,0': run('claude.cmd', 'claude'),
    '0,1': send('/desktop', 'desktop'),

    '1,0': send('push', 'push'),

    '2,0': run('release-patch.cmd', 'patch'),
    '2,1': run('release-minor.cmd', 'minor'),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--tiles', action='store_true', help='regenerate the key images first')
    args = ap.parse_args()

    root = os.path.join(os.environ['APPDATA'], 'Elgato', 'StreamDeck', 'ProfilesV2')
    profiles = []
    for entry in sorted(os.listdir(root)):
        manifest = os.path.join(root, entry, 'manifest.json')
        if os.path.isfile(manifest):
            top = json.load(open(manifest, encoding='utf-8'))
            if top.get('Name') == PROFILE_NAME:
                profiles.append((os.path.join(root, entry), top))
    if not profiles:
        sys.exit('No profile named {0} in {1}'.format(PROFILE_NAME, root))

    for profile, top in profiles:
        print('{0} ({1})'.format(os.path.basename(profile), top['Device']['Model']))
        apply(profile, top, args.tiles)


def apply(profile, top, tiles):
    pages = top['Pages']['Pages']
    default = top['Pages']['Default']
    if top['Device']['Model'] == MOBILE_MODEL:
        layouts = dict(zip(pages, (MOBILE_PAGE_1,)))
    else:
        layouts = dict(zip(pages, (PAGE_1, PAGE_2)))
    layouts[default] = {}

    if tiles:
        dirs = [os.path.join(profile, 'Profiles', folder_for(p), 'Images') for p in layouts]
        subprocess.check_call([sys.executable, os.path.join(DECK, 'gen_tiles.py')] + dirs)

    for page_uuid, actions in layouts.items():
        page_dir = os.path.join(profile, 'Profiles', folder_for(page_uuid))
        manifest = os.path.join(page_dir, 'manifest.json')
        data = json.load(open(manifest, encoding='utf-8'))
        data['Controllers'] = [{'Type': 'Keypad', 'Actions': actions}]
        json.dump(data, open(manifest, 'w', encoding='utf-8'), indent=2, ensure_ascii=False)

        for coord, action in actions.items():
            path = action['Settings'].get('path')
            if path and not os.path.isfile(path):
                sys.exit('Missing launcher for {0}: {1}'.format(coord, path))
            image = action['States'][0]['Image']
            if not os.path.isfile(os.path.join(page_dir, image)):
                sys.exit('Missing image for {0}: {1}'.format(coord, image))
        print('{0}: {1} keys'.format(folder_for(page_uuid), len(actions)))


if __name__ == '__main__':
    main()
