"""Phase 5.2-B5 comparison sheets from a real-player tour folder (no editing of the frames: crops and labels outside the pictures).

python -I tools/art/phase52b5/make_b5_sheets.py <tour-folder> <out-folder>
Writes, for every place of the tour, the B4 still (before mode: the same build, the same place, the same moment) beside the B5 still,
the stage strips (S0 from the matched pair, S1-S3 set by the evidence script), and the occlusion stills with the measured visible share
of the caretaker. Every picture is an unmodified REAL_PLAYER framebuffer capture, scaled; labels sit in a bar above each picture.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

folder, out = Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve()
out.mkdir(parents=True, exist_ok=True)
tour = json.loads((folder / 'tour.json').read_text(encoding='utf-8-sig'))
try:
    font = ImageFont.truetype('arial.ttf', 22)
except OSError:
    font = ImageFont.load_default()


def labelled(path, label, width):
    im = Image.open(path).convert('RGB')
    im = im.resize((width, int(im.height * width / im.width)), Image.LANCZOS)
    canvas = Image.new('RGB', (im.width, im.height + 34), (24, 24, 28))
    canvas.paste(im, (0, 34))
    ImageDraw.Draw(canvas).text((8, 6), label, fill=(240, 228, 196), font=font)
    return canvas


def row(images, gap=12):
    w = sum(i.width for i in images) + gap * (len(images) - 1)
    h = max(i.height for i in images)
    sheet = Image.new('RGB', (w, h), (12, 12, 14))
    x = 0
    for i in images:
        sheet.paste(i, (x, 0))
        x += i.width + gap
    return sheet


made = []
places = sorted({s[len('after-b5-'):] for s in tour['stills'] if s.startswith('after-b5-')})
for place in places:
    b4, b5 = folder / ('before-b4-%s.png' % place), folder / ('after-b5-%s.png' % place)
    if b4.is_file() and b5.is_file():
        row([labelled(b4, 'REAL_PLAYER  B4 (before mode, same build)  %s  S0' % place, 960), labelled(b5, 'REAL_PLAYER  B5  %s  S0' % place, 960)]).save(out / ('b4-vs-b5-%s.jpg' % place), quality=92)
        made.append('b4-vs-b5-%s.jpg' % place)
for place in ('spring', 'garden', 'plaza', 'workshop-front', 'stream', 'windmill'):
    frames = [(folder / ('after-b5-%s.png' % place), 'S0 (play)')] + [(folder / ('stage-%s-set-by-script-%s.png' % (s, place)), '%s (set by script)' % s) for s in ('S1', 'S2', 'S3')]
    if all(p.is_file() for p, _ in frames):
        row([labelled(p, 'REAL_PLAYER  B5  %s  %s' % (place, label), 720) for p, label in frames]).save(out / ('b5-stages-%s.jpg' % place), quality=90)
        made.append('b5-stages-%s.jpg' % place)
    b4s1, b5s1 = folder / ('stage-S1-set-by-script-before-b4-%s.png' % place), folder / ('stage-S1-set-by-script-%s.png' % place)
    if b4s1.is_file() and b5s1.is_file():
        row([labelled(b4s1, 'REAL_PLAYER  B4 (before mode)  %s  S1 (set by script)' % place, 960), labelled(b5s1, 'REAL_PLAYER  B5  %s  S1 (set by script)' % place, 960)]).save(out / ('b4-vs-b5-S1-%s.jpg' % place), quality=92)
        made.append('b4-vs-b5-S1-%s.jpg' % place)
    b4s3 = folder / ('stage-S3-set-by-script-before-b4-%s.png' % place)
    b5s3 = folder / ('stage-S3-set-by-script-%s.png' % place)
    if b4s3.is_file() and b5s3.is_file():
        row([labelled(b4s3, 'REAL_PLAYER  B4 (before mode)  %s  S3 (set by script)' % place, 960), labelled(b5s3, 'REAL_PLAYER  B5  %s  S3 (set by script)' % place, 960)]).save(out / ('b4-vs-b5-S3-%s.jpg' % place), quality=92)
        made.append('b4-vs-b5-S3-%s.jpg' % place)
occ = tour.get('occlusion', [])
by_still = {o['still']: o for o in occ}
names = sorted({o['still'].replace('-before-b4', '').replace('-closest-zoom', '') for o in occ})
for base in names:
    tiles = []
    for suffix in ('', '-closest-zoom'):
        for before in (True, False):
            still = base + ('-before-b4' if before else '') + suffix
            o = by_still.get(still)
            p = folder / (still + '.png')
            if o and p.is_file():
                tiles.append(labelled(p, 'REAL_PLAYER  %s  %s  zoom %.0f m  caretaker visible %.0f %%' % (o['presentation'], base.replace('occlusion-', ''), o['zoom'], 100 * o['visibleShare']), 640))
    if tiles:
        row(tiles).save(out / ('occlusion-%s.jpg' % base.replace('occlusion-', '')), quality=90)
        made.append('occlusion-%s.jpg' % base.replace('occlusion-', ''))
worst = sorted((o for o in occ if o['presentation'] == 'B5'), key=lambda o: o['visibleShare'])[:1]
for o in worst:
    labelled(folder / (o['still'] + '.png'), 'REAL_PLAYER  B5  WORST OCCLUSION MEASURED: %s  caretaker visible %.0f %%' % (o['still'], 100 * o['visibleShare']), 1920).save(out / 'worst-occlusion-b5.jpg', quality=92)
    made.append('worst-occlusion-b5.jpg')
for pair in (('ground-tiling-farthest-zoom-before-b4', 'ground-tiling-farthest-zoom'),):
    a, b = folder / (pair[0] + '.png'), folder / (pair[1] + '.png')
    if a.is_file() and b.is_file():
        row([labelled(a, 'REAL_PLAYER  B4  ground at the farthest zoom (17 m)', 960), labelled(b, 'REAL_PLAYER  B5  ground at the farthest zoom (17 m)', 960)]).save(out / 'ground-tiling-farthest-zoom.jpg', quality=92)
        made.append('ground-tiling-farthest-zoom.jpg')
(out / 'sheets.json').write_text(json.dumps(dict(source=str(folder), note='Unmodified REAL_PLAYER framebuffer stills, scaled, labels outside the pictures', sheets=made, occlusion=occ), indent=1), encoding='utf-8')
print(len(made), 'sheets')
