"""Writes the key layout of the Directive 47 Development Stream Deck profile.

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

PROFILE_UUID = '2CFD100A-59FE-4ADF-82B0-A12855B1A0B2'
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


def goto(page_index, image):
    # Plugin.UUID is the plugin id and differs from the action id for this one.
    return _action('com.elgato.streamdeck.page.goto', 'Go to Page', 'Pages',
                   'com.elgato.streamdeck.page', {'PageIndex': page_index}, image)


PAGE_1 = {
    '0,0': run('triage.cmd', 'triage'),
    '1,0': run('coordinator.cmd', 'coord'),
    '2,0': run('architect.cmd', 'architect'),
    '3,0': run('issue-worker.cmd', 'issue'),
    '4,0': send('/desktop', 'desktop'),

    '0,1': run('review.cmd', 'review'),
    '1,1': run('prose.cmd', 'prose'),
    '2,1': send('commit and push', 'ship'),
    # /neural-voice off is the one that stops the speaking. /neural-voice none keeps the voice
    # and only drops the session name.
    '3,1': send('/neural-voice', 'voice_on'),
    '4,1': send('/neural-voice off', 'voice_off'),

    '0,2': run('build.cmd', 'build'),
    '1,2': run('ticking.cmd', 'ticking'),
    '2,2': run('test-drive.cmd', 'testdrive'),
    '3,2': run('restart-test-drive.cmd', 'restart'),
    '4,2': goto(2, 'release'),
}

PAGE_2 = {
    '0,0': run('release-patch.cmd', 'patch'),
    '1,0': run('release-minor.cmd', 'minor'),
    '2,0': run('release-major.cmd', 'major'),
    '3,0': run('watch-release.cmd', 'watch'),

    '0,2': send('/wrap-up', 'wrapup'),
    '4,2': goto(1, 'back'),
}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--tiles', action='store_true', help='regenerate the key images first')
    args = ap.parse_args()

    profile = os.path.join(os.environ['APPDATA'], 'Elgato', 'StreamDeck', 'ProfilesV2',
                           PROFILE_UUID + '.sdProfile')
    if not os.path.isdir(profile):
        sys.exit('Profile not found: ' + profile)

    top = json.load(open(os.path.join(profile, 'manifest.json'), encoding='utf-8'))
    pages = top['Pages']['Pages']
    default = top['Pages']['Default']
    layouts = dict(zip(pages, (PAGE_1, PAGE_2)))
    layouts[default] = {}

    if args.tiles:
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
