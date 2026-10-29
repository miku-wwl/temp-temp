"""Statistics, contact sheet and selected real frames for a B5 tour folder (the B4 caretaker drawings: the stride comes from their atlas).

python -I tools/art/phase52b5/analyze_b5_tour.py <tour-folder> [--frames]
Writes tour-stats.json (frame times, speed, direction and step coverage, run-phase check against distance, not visual foot contact), and with
--frames (a recorded run): contact-sheet.png (one real frame every 2 s, labelled outside the picture) and selected-frames/ (unmodified
JPEG framebuffer frames chosen by step and direction, explained in selected-frames.json).
"""
import json
import math
import shutil
import sys
from pathlib import Path

from PIL import Image, ImageDraw

folder = Path(sys.argv[1]).resolve()
tour = json.loads((folder / 'tour.json').read_text(encoding='utf-8-sig'))
samples = tour['samples']
ft = sorted(s['frameMs'] for s in samples[5:])


def pct(p):
    return ft[min(len(ft) - 1, int(p * len(ft)))]


moving = [s for s in samples if s['speed'] > 0.5]
dirs = {}
for s in samples:
    key = (s['dir'] or '-') + ('/run' if s['running'] else '/idle')
    dirs[key] = dirs.get(key, 0) + 1
# skate check: while running, the presenter's phase must advance by distance / stride (the stride is read from the atlas metadata)
atlas = json.loads((Path(__file__).resolve().parents[3] / 'game/Assets/Echoes/Art/Phase52B4/Sprites/CaretakerB4Atlas.json').read_text(encoding='utf-8'))
stride = atlas['strideMeters']
errors = []
for a, b in zip(samples, samples[1:]):
    if a['running'] and b['running'] and a['step'] == b['step']:
        d = math.hypot(b['x'] - a['x'], b['z'] - a['z'])
        if d > 1.0:
            continue
        dp = (b['runPhase'] - a['runPhase']) % 1.0
        errors.append(abs(dp - d / stride))
errors.sort()
recorded = tour.get('videoCapture', 'None') != 'None'
# minimap: the objective it pointed at over time, and whether the player's map point moved with her (a constant scale, no jumps)
timeline = []
for s in samples:
    o = s.get('objective') or '-'
    if not timeline or timeline[-1]['objective'] != o:
        timeline.append(dict(t=round(s['t'], 2), objective=o, step=s['step']))
scales = []
for a, b in zip(samples, samples[1:]):
    dw = math.hypot(b['x'] - a['x'], b['z'] - a['z'])
    if 0.02 < dw < 1.0:
        scales.append(math.hypot(b['mapX'] - a['mapX'], b['mapY'] - a['mapY']) / dw)
scales.sort()
minimap = dict(objectiveTimeline=timeline,
               mapUnitsPerMetre=dict(samples=len(scales), p05=round(scales[int(0.05 * len(scales))], 3) if scales else None,
                                     median=round(scales[len(scales) // 2], 3) if scales else None, p95=round(scales[int(0.95 * len(scales))], 3) if scales else None),
               edgeClampedFraction=round(sum(1 for s in samples if s.get('objectiveOnEdge')) / max(1, len(samples)), 3))
stats = dict(
    folder=str(folder), resolution='%dx%d' % (tour['width'], tour['height']), recordedFramebuffer=recorded, presentation=tour.get('presentation'), camera=tour.get('camera'),
    routeSeconds=round(tour['routeSeconds'], 2), metres=round(tour['travelledMetres'], 1), routePass=tour['pass'], failures=tour['failures'], events=tour['events'],
    frames=len(ft), frameMs=dict(p50=round(pct(0.5), 2), p95=round(pct(0.95), 2), p99=round(pct(0.99), 2), worst=round(ft[-1], 1), mean=round(sum(ft) / len(ft), 2)),
    fps=dict(mean=round(1000 * len(ft) / sum(ft), 1), onePercentLow=round(1000 / pct(0.99), 1)),
    speedWhileMoving=dict(mean=round(sum(s['speed'] for s in moving) / max(1, len(moving)), 2), max=round(max((s['speed'] for s in moving), default=0), 2)),
    directionSamples=dirs, steps=sorted(set(s['step'] for s in samples)),
    runPhaseVsDistance=dict(stride=stride, samples=len(errors), medianError=round(errors[len(errors) // 2], 4) if errors else None, p95Error=round(errors[int(0.95 * len(errors))], 4) if errors else None),
    note=('recorded run: framebuffer capture overhead included, not a rendering benchmark' if recorded else 'unrecorded run: no framebuffer capture; one scripted route, not a human traversal benchmark'),
    stills=tour.get('stills', []),
    minimap=minimap,
)
(folder / 'tour-stats.json').write_text(json.dumps(stats, indent=2), encoding='utf-8')
print(json.dumps({k: stats[k] for k in ('resolution', 'routeSeconds', 'metres', 'routePass', 'frameMs', 'fps', 'speedWhileMoving', 'directionSamples', 'runPhaseVsDistance')}))
print(json.dumps(minimap))

if '--frames' in sys.argv and tour['capturedFrames']:
    frames = tour['capturedFrames']
    times = [s['t'] for s in samples]

    def state(t):
        i = min(range(len(times)), key=lambda k: abs(times[k] - t)) if len(times) < 50 else None
        if i is None:
            lo, hi = 0, len(times) - 1
            while lo < hi:
                mid = (lo + hi) // 2
                if times[mid] < t:
                    lo = mid + 1
                else:
                    hi = mid
            i = lo
        return samples[i]

    # contact sheet: one real frame every 2 s, captions in a strip under each picture
    picks = []
    t = 0.0
    for f in frames:
        if f['t'] >= t:
            picks.append(f)
            t += 2.0
    cols, tw, th = 5, 384, 216
    rows = (len(picks) + cols - 1) // cols
    sheet = Image.new('RGB', (cols * tw, rows * (th + 18)), (20, 20, 24))
    for k, f in enumerate(picks):
        im = Image.open(folder / 'frames' / f['file']).convert('RGB').resize((tw, th), Image.LANCZOS)
        x, y = (k % cols) * tw, (k // cols) * (th + 18)
        sheet.paste(im, (x, y))
        s = state(f['t'])
        ImageDraw.Draw(sheet).text((x + 4, y + th + 3), '%.1f s  %s  %s%s' % (f['t'], s['step'], s['dir'] or '-', ' run' if s['running'] else ' idle'), fill=(230, 220, 190))
    sheet.save(folder / 'contact-sheet.png')
    # selected frames: one per (step, direction, running) combination, the middle frame of the longest stretch
    out = folder / 'selected-frames'
    if out.exists():
        shutil.rmtree(out)
    out.mkdir()
    groups = {}
    for f in frames:
        s = state(f['t'])
        key = (s['step'], s['dir'] or '-', 'run' if s['running'] else 'idle')
        groups.setdefault(key, []).append((f, s))
    chosen = []
    for (step, d, kind), lst in sorted(groups.items(), key=lambda kv: kv[1][0][0]['t']):
        if len(lst) < 3:
            continue
        f, s = lst[len(lst) // 2]
        name = '%05.1fs-%s-%s-%s.jpg' % (f['t'], step, d, kind)
        shutil.copyfile(folder / 'frames' / f['file'], out / name)
        chosen.append(dict(file=name, source=f['file'], t=f['t'], step=step, direction=d, running=kind == 'run', frameIndex=s['frame'], runPhase=s['runPhase'], speed=s['speed']))
    (out / 'selected-frames.json').write_text(json.dumps(dict(note='Unmodified JPEG framebuffer frames of an AUTOMATED real-player run; one per step/direction/state', frames=chosen), indent=2), encoding='utf-8')
    print('contact sheet', len(picks), 'frames; selected', len(chosen))
