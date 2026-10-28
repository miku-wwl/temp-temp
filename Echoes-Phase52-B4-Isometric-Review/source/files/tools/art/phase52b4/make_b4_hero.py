"""Phase 5.2-B4 caretaker sprites from the owner-supplied B4 drawings (natural standing poses and run key poses).

python tools/art/phase52b4/make_b4_hero.py
Outputs:
  game/Assets/Echoes/Art/Phase52B4/Sprites/CaretakerB4Atlas.png + .json   frames, ground pivots, scale, stride (same format as B3)
  artifacts/art-work/phase52b4/hero/                                      review sheets (game scale, turnaround, idle-vs-run scale check)

What is done to the drawings, and nothing else: background glow and fringe removed, cropped, scaled so every direction has the same
height, aligned on the torso and the ground, an idle breathing loop by gentle 2D warping, run "down" in-betweens by a 1.5 % vertical
squash, and (back view only) one run frame whose lower legs are mirrored so the right foot also lifts. No pixel is drawn.

Run steps (each frame is one step; the planted foot alternates):
  S  [1, 2, 3, 2]   right knee up, left knee up, right knee high, left knee up
  W  [0, 1, 2, 3]   legs reaching, legs gathered, reaching, gathered (the sheet's own order)
  E  [0, 1, 2, 3]   as W
  N  [1, 1m, 3, 1m] left foot kicks up, right foot (mirrored legs of 1), left again (3), right again
"""
import hashlib
import json
import sys
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parent / 'phase52b3'))
import charlib as C  # noqa: E402
import rig  # noqa: E402

ROOT = HERE.parents[2]
OUT = ROOT / 'game/Assets/Echoes/Art/Phase52B4/Sprites'
WORK = ROOT / 'artifacts/art-work/phase52b4/hero'
PX_PER_M = 300.0
CHAR_H_M = 1.65
IDLE_H = int(round(PX_PER_M * CHAR_H_M))
RUN_HEIGHT_RATIO = 0.97                 # a running figure (bent knees, forward lean) is a little shorter than standing
# the run sheets were drawn with slightly different head-to-body proportions than the standing poses; these factors make the heads the
# same size as in the standing pose of the same direction (judged on head-size overlays, artifacts/art-work/phase52b4/hero/)
RUN_SCALE_ADJ = {'S': 0.92, 'W': 0.97, 'E': 0.93, 'N': 1.06}
RUN_STEPS = {'S': [1, 2, 3, 2], 'W': [0, 1, 2, 3], 'E': [0, 1, 2, 3], 'N': [1, '1m', 3, '1m']}
IDLE_FRAMES, IDLE_FPS = 12, 5.0
STRIDE_M = 2.8                          # metres per loop of four steps (0.7 m per step, about 6.4 steps/s at 4.5 m/s)
SQUASH = 0.985


def torso_anchor(rgba):
    a = rgba[..., 3] > 128
    ys = np.where(a.any(1))[0]
    top, bottom = int(ys.min()), int(ys.max())
    h = bottom - top
    band = a[top + int(0.12 * h):top + int(0.45 * h)]
    xs = np.where(band)[1]
    return float(xs.mean()), top, bottom


def mirror_legs(rgba, cut_frac=0.585, half_width_frac=0.17):
    """Back view: the lower legs mirrored about the body axis below the petticoat, drawn behind everything else."""
    h, w = rgba.shape[:2]
    a = rgba[..., 3] > 30
    rgb = rgba[..., :3].astype(int)
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    lum = 0.299 * r + 0.587 * g + 0.114 * b
    cloth = ((lum > 150) & (b > 100)) | ((g > r + 5) & (b > r - 5)) | ((r > 100) & (r > 1.6 * g))
    ys, xs = np.where(a[int(.2 * h):int(.5 * h)])
    axis = xs.mean()
    cut = int(cut_frac * h)
    region = np.zeros_like(a)
    region[cut:, max(0, int(axis - half_width_frac * w)):int(axis + half_width_frac * w)] = True
    legs = a & region & ~cloth
    legs = ndimage.binary_fill_holes(ndimage.binary_closing(legs, iterations=3)) & region & a
    lab, n = ndimage.label(legs)
    if n:
        sizes = ndimage.sum(legs, lab, range(1, n + 1))
        legs = np.isin(lab, [i + 1 for i, s in enumerate(sizes) if s > 800])
    rest = rgba.copy()
    rest[legs] = 0
    src = rgba.copy()
    src[~legs] = 0
    xs_new = np.clip(np.round(2 * axis - np.arange(w)).astype(int), 0, w - 1)
    mirrored = src[:, xs_new]
    top = C.premul(rest)
    under = C.premul(mirrored)
    return C.unpremul(top + under * (1 - top[..., 3:4]))


def idle_frames(rgba):
    """Breathing: chest rises, head follows, the figure sways a fraction of a degree about the feet; hair ends and coat tails lag."""
    pad = 24
    v = rig.premultiply(rig.pad(rgba, pad, pad, pad, pad))
    h, w = v.shape[:2]
    cx, top, bottom = torso_anchor(rig.pad(rgba, pad, pad, pad, pad))
    H = bottom - top
    feet = (cx, bottom)
    waist = top + 0.42 * H
    neck = top + 0.17 * H
    sk = rig.Skeleton([rig.Bone('root', feet), rig.Bone('chest', (cx, waist), 'root'), rig.Bone('head', (cx, neck), 'chest'), rig.Bone('sway', (cx, neck), 'root')])
    chest = rig.band_y(h, w, waist + 0.06 * H, waist - 0.04 * H)
    head = rig.capsule(h, w, (cx, top + 0.02 * H), (cx, neck), 0.10 * H, 0.04 * H)
    # hair ends and coat tails: away from the body's centre line, upper or lower body
    side = rig.band_x(h, w, cx + 0.16 * H, cx + 0.24 * H) + rig.band_x(h, w, cx - 0.16 * H, cx - 0.24 * H, invert=True)
    side = np.clip(side, 0, 1) * (rig.band_y(h, w, top + 0.10 * H, top + 0.20 * H) * rig.band_y(h, w, top + 0.92 * H, top + 0.80 * H))
    W = [np.full((h, w), 0.2, np.float32), chest * 1.0, head * 1.6, side * 1.4]
    wts = rig.normalise(W)
    frames = []
    for i in range(IDLE_FRAMES):
        p = 2 * np.pi * i / IDLE_FRAMES
        s, c = np.sin(p), np.cos(p)
        pose = {'root': dict(rot=0.30 * c), 'chest': dict(sy=1 + 0.008 * s, dy=-1.1 * s), 'head': dict(rot=0.5 * np.sin(p - 0.7), dy=-0.6 * s),
                'sway': dict(dx=1.6 * np.sin(p - 1.1))}
        frames.append(C.unpremul(rig.warp(v, wts, sk.matrices(pose))))
    return frames


def squash(rgba, anchor):
    """The same drawing pressed down 1.5 % about the ground point: the 'down' beat after each step."""
    ax, ay = anchor
    m = np.array([[1.008, 0, ax * (1 - 1.008)], [0, SQUASH, ay * (1 - SQUASH)]], np.float32)
    f = C.premul(rgba)
    out = cv2.warpAffine(f, m, (rgba.shape[1], rgba.shape[0]), flags=cv2.INTER_CUBIC, borderMode=cv2.BORDER_CONSTANT, borderValue=(0, 0, 0, 0))
    return C.unpremul(np.clip(out, 0, 1))


def place(frames, anchors, ground_pad=4):
    """All frames of a direction on one canvas: torso centre and ground line aligned; returns frames, pivot (x from left, y from bottom)."""
    left = max(ax for ax, _ in anchors)
    right = max(f.shape[1] - ax for f, (ax, _) in zip(frames, anchors))
    up = max(gy for _, gy in anchors)
    down = max(f.shape[0] - gy for f, (_, gy) in zip(frames, anchors))
    W = int(np.ceil(left + right)) + 2
    H = int(np.ceil(up + down)) + ground_pad
    out = []
    for f, (ax, gy) in zip(frames, anchors):
        canvas = np.zeros((H, W, 4), np.uint8)
        x0 = int(round(left - ax))
        y0 = int(round(up - gy))
        canvas[y0:y0 + f.shape[0], x0:x0 + f.shape[1]] = f
        out.append(canvas)
    # crop to the union of all frames
    alpha = np.max(np.stack([f[..., 3] for f in out]), 0)
    ys, xs = np.where(alpha > 4)
    x0, x1, y0, y1 = max(xs.min() - 2, 0), xs.max() + 3, max(ys.min() - 2, 0), ys.max() + 3
    out = [f[y0:y1, x0:x1] for f in out]
    pivot = (left - x0, (y1 - y0) - (up - y0))
    return out, pivot


def run_anchor(rgba, head_ref):
    """Torso centre; the ground under the lowest foot, unless that would put the head much lower than standing (a flight frame floats)."""
    cx, top, bottom = torso_anchor(rgba)
    ground = bottom
    head_height = ground - top
    if head_height < head_ref * 0.95:
        ground = top + head_ref * 0.95
    return cx, ground


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    WORK.mkdir(parents=True, exist_ok=True)
    directions = {}
    report = {}
    for d in 'SWEN':
        idle = C.clean_alpha(C.load('characters/idle/hero_idle_%s.png' % d))
        x0, x1, y0, y1 = C.bbox(idle, 128)
        idle = idle[max(0, y0 - 4):y1 + 4, max(0, x0 - 4):x1 + 4]
        idle_scale = IDLE_H / float(y1 - y0)
        idle = C.resize(idle, idle_scale)
        idle_loop = idle_frames(idle)
        sheet = C.load('characters/run-keyposes/hero_run_%s.png' % d)
        poses = [C.clean_alpha(p) for p in C.split_poses(sheet, 4)]
        crops = []
        for p in poses:
            a0, a1, b0, b1 = C.bbox(p, 8)
            crops.append(p[max(0, b0 - 4):b1 + 4, max(0, a0 - 4):a1 + 4])
        heights = sorted([C.bbox(c, 128)[3] - C.bbox(c, 128)[2] for c in crops], reverse=True)
        run_scale = IDLE_H * RUN_HEIGHT_RATIO / float(np.mean(heights[:2])) * RUN_SCALE_ADJ[d]
        crops = [C.resize(c, run_scale) for c in crops]
        steps = []
        for s in RUN_STEPS[d]:
            steps.append(mirror_legs(crops[int(str(s)[0])]) if str(s).endswith('m') else crops[int(s)])
        run = []
        run_anchors = []
        for st in steps:
            an = run_anchor(st, IDLE_H * RUN_HEIGHT_RATIO)
            run += [st, squash(st, an)]
            run_anchors += [an, an]
        idle_anchors = []
        for f in idle_loop:
            cx, top, bottom = torso_anchor(f)
            idle_anchors.append((cx, bottom))
        frames, pivot = place(idle_loop + run, idle_anchors + run_anchors)
        directions[d] = dict(idle=frames[:IDLE_FRAMES], run=frames[IDLE_FRAMES:], pivot=pivot)
        report[d] = dict(idleScale=round(idle_scale, 4), runScale=round(run_scale, 4), runSteps=[str(s) for s in RUN_STEPS[d]], canvas=list(frames[0].shape[1::-1]), pivot=[round(float(v), 1) for v in pivot])
        print(d, report[d])
    # pack
    items = [(d, kind, i, f) for d, dd in directions.items() for kind in ('idle', 'run') for i, f in enumerate(dd[kind])]
    order = sorted(range(len(items)), key=lambda k: -items[k][3].shape[0])
    W = 4096
    x = y = shelf = 0
    rects = {}
    for k in order:
        h, w = items[k][3].shape[:2]
        if x + w + 2 > W:
            x, y, shelf = 0, y + shelf + 2, 0
        rects[k] = (x, y, w, h)
        x += w + 2
        shelf = max(shelf, h)
    H = 1 << int(np.ceil(np.log2(y + shelf + 2)))
    atlas = np.zeros((H, W, 4), np.uint8)
    for k, (ax, ay, w, h) in rects.items():
        atlas[ay:ay + h, ax:ax + w] = C.bleed(C.decontaminate(items[k][3]))
    Image.fromarray(atlas).save(OUT / 'CaretakerB4Atlas.png', optimize=True)
    sources = {}
    for d in 'SWEN':
        for kind in ('idle/hero_idle_%s.png', 'run-keyposes/hero_run_%s.png'):
            rel = 'characters/' + kind % d
            sources[rel] = hashlib.sha256((C.SRC / rel).read_bytes()).hexdigest()
    meta = dict(
        note='Phase 5.2-B4 caretaker: owner-supplied B4 drawings (natural standing poses and run key poses), cleaned, scaled, aligned; idle = breathing warp; run = drawn steps with 1.5 % squash in-betweens; back view: lower legs of run pose 1 mirrored for the right-foot steps.',
        atlas=[W, H], pixelsPerMeter=PX_PER_M, strideMeters=STRIDE_M, runFrames=8, idleFrames=IDLE_FRAMES, idleFps=IDLE_FPS,
        directions={d: dict(pivot=[float(directions[d]['pivot'][0]), float(directions[d]['pivot'][1])], size=list(map(int, directions[d]['idle'][0].shape[1::-1]))) for d in directions},
        frames=[dict(dir=items[k][0], kind=items[k][1], index=items[k][2], x=rects[k][0], y=H - rects[k][1] - rects[k][3], w=rects[k][2], h=rects[k][3]) for k in sorted(rects)],
        sources=sources, report=report)
    (OUT / 'CaretakerB4Atlas.json').write_text(json.dumps(meta, indent=1), encoding='utf-8')
    print('atlas', W, H, 'frames', len(items))
    review(directions)


def review(directions):
    # game-scale sheet: every frame at about the size the B4 camera shows her (240 px tall at 1080p)
    target = 240.0 / IDLE_H
    tiles = []
    for d in 'SWEN':
        dd = directions[d]
        for kind in ('idle', 'run'):
            for i, f in enumerate(dd[kind]):
                if kind == 'idle' and i % 3:
                    continue
                im = Image.fromarray(f)
                im = im.resize((max(1, int(im.size[0] * target)), max(1, int(im.size[1] * target))), Image.LANCZOS)
                bg = Image.new('RGBA', (170, 280), (104, 140, 84, 255))
                dr = ImageDraw.Draw(bg)
                dr.rectangle((0, 262, 170, 280), fill=(170, 145, 105, 255))
                px, py = dd['pivot'][0] * target, (f.shape[0] - dd['pivot'][1]) * target
                bg.alpha_composite(im, (int(85 - px), int(262 - py)))
                dr.text((3, 3), '%s %s %d' % (d, kind, i), fill=(255, 255, 0, 255))
                tiles.append(bg.convert('RGB'))
    cols = 12
    rows = (len(tiles) + cols - 1) // cols
    sheet = Image.new('RGB', (170 * cols, 280 * rows))
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % cols) * 170, (i // cols) * 280))
    sheet.save(WORK / 'game-scale-frames.png')
    # turnaround and scale check: idle of every direction and the first run step, same scale, same ground line
    tiles = []
    for d in 'SWEN':
        dd = directions[d]
        for f in (dd['idle'][0], dd['run'][0], dd['run'][2]):
            im = Image.fromarray(f)
            bg = Image.new('RGBA', (360, 560), (104, 140, 84, 255))
            px, py = dd['pivot'][0], f.shape[0] - dd['pivot'][1]
            bg.alpha_composite(im, (int(180 - px), int(540 - py)))
            dr = ImageDraw.Draw(bg)
            dr.line([(0, 540), (360, 540)], fill=(255, 255, 255, 255))
            dr.line([(0, 540 - IDLE_H), (360, 540 - IDLE_H)], fill=(255, 220, 0, 255))
            tiles.append(bg.convert('RGB'))
    sheet = Image.new('RGB', (360 * 6, 560 * 2))
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % 6) * 360, (i // 6) * 560))
    sheet.save(WORK / 'turnaround-scale-check.png')
    # onion skin: standing pose and each run step at 50 %, aligned on the ground pivot; the heads should coincide in size
    tiles = []
    for d in 'SWEN':
        dd = directions[d]
        base = Image.fromarray(dd['idle'][0])
        for i in (0, 2, 4, 6):
            f = Image.fromarray(dd['run'][i])
            bg = Image.new('RGBA', (420, 260), (104, 140, 84, 255))
            for im, alpha in ((base, 0.55), (f, 0.55)):
                im2 = im.copy()
                im2.putalpha(Image.fromarray((np.asarray(im2.split()[3], np.float32) * alpha).astype(np.uint8)))
                bg.alpha_composite(im2, (int(210 - dd['pivot'][0]), int(500 - (im.size[1] - dd['pivot'][1])) - 260 + 20))
            tiles.append(bg.convert('RGB'))
    sheet = Image.new('RGB', (420 * 4, 260 * 4))
    for i, t in enumerate(tiles):
        sheet.paste(t, ((i % 4) * 420, (i // 4) * 260))
    sheet.save(WORK / 'onion-skin-heads.png')
    print('review sheets in', WORK)


if __name__ == '__main__':
    main()
