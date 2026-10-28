"""Crops the minimap out of real recorded frames of a B4 tour into one strip (REAL_PLAYER_SCREENSHOT crops, captions outside the pixels).

python -I tools/art/phase52b4/minimap_strip.py <recorded-tour-folder> <output.png>
Frames are chosen where the minimap's objective changes and at a few route steps (start, workshop, behind the workshop, spring, end), so
the strip shows the map scrolling and turning with the player and the objective marker moving on.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

folder = Path(sys.argv[1]).resolve()
out = Path(sys.argv[2]).resolve()
tour = json.loads((folder / 'tour.json').read_text(encoding='utf-8-sig'))
samples, frames = tour['samples'], tour['capturedFrames']


def sample_at(t):
    return min(samples, key=lambda s: abs(s['t'] - t))


def frame_at(t):
    return min(frames, key=lambda f: abs(f['t'] - t))


picks = []
last = None
for s in samples:
    if s.get('objective') != last:
        picks.append((s['t'] + 0.4, 'objective -> %s' % s.get('objective')))
        last = s.get('objective')
for step in ('idle', 'to-workshop', 'behind-workshop', 'under-willow', 'to-spring', 'back-to-plaza'):
    times = [s['t'] for s in samples if s['step'] == step]
    if times:
        picks.append((times[len(times) // 2], step))
picks.sort()
scale = tour['height'] / 1080.0
box = int(round(272 * scale))                      # frame 232 + margin 24 + a little air, at the HUD's 1080p reference
cells = []
for t, why in picks:
    f = frame_at(t)
    im = Image.open(folder / 'frames' / f['file']).convert('RGB')
    w = im.width
    crop = im.crop((w - box, 0, w, box)).resize((240, 240), Image.LANCZOS)
    s = sample_at(f['t'])
    cells.append((crop, '%.1f s  %s' % (f['t'], why), 'objective %s%s' % (s.get('objective'), ' (edge)' if s.get('objectiveOnEdge') else '')))
cols = min(6, len(cells))
rows = (len(cells) + cols - 1) // cols
sheet = Image.new('RGB', (cols * 248, rows * 280), (20, 22, 24))
d = ImageDraw.Draw(sheet)
for i, (crop, a, b) in enumerate(cells):
    x, y = (i % cols) * 248 + 4, (i // cols) * 280 + 4
    sheet.paste(crop, (x, y))
    d.text((x, y + 244), a[:38], fill=(236, 226, 196))
    d.text((x, y + 258), b[:38], fill=(200, 210, 190))
sheet.save(out)
print('minimap strip', len(cells), 'crops ->', out)
