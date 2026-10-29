"""Phase 5.2-B5 audit of the owner-supplied environment pack (measurements only; nothing is changed).

python -I tools/art/phase52b5/audit_b5_assets.py [--src folder] [--out folder]
Writes contact sheets and audit.json to artifacts/visual-qa/phase-5.2/b5/asset-audit/ (default):
  * every painted prop on white, black, grass green, earth brown and a checkerboard, with its partly transparent edge texels counted
    by colour (a red or yellow fringe is background glow left by the generator);
  * spring_S0_dormant against spring_S3_restored: silhouette overlap, the best similarity transform (SIFT matches + RANSAC) and how
    far the two drawings differ after it (the two stages must stand on the same footprint);
  * the two ground textures repeated 4 x 4 at their full size, with the wrap-edge error against the texture's own pixel-to-pixel
    variation (1.0 means an edge is as continuous as the inside; larger values are visible seams).
"""
import argparse
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
PROPS = ('spring_S0_dormant', 'spring_S3_restored', 'farm_raised_beds', 'broadleaf_tree')
GROUND = ('meadow_grass_albedo', 'tilled_soil_albedo')
BACKDROPS = {'white': (255, 255, 255), 'black': (0, 0, 0), 'grass': (96, 128, 72), 'earth': (120, 92, 64)}


def load(src, name, mode='RGBA'):
    return np.asarray(Image.open(src / (name + '.png')).convert(mode))


def on(rgba, colour):
    im = Image.fromarray(rgba)
    bg = Image.new('RGBA', im.size, tuple(colour) + (255,))
    bg.alpha_composite(im)
    return bg.convert('RGB')


def checker(rgba, cell=24):
    h, w = rgba.shape[:2]
    y, x = np.mgrid[0:h, 0:w]
    board = np.where(((x // cell + y // cell) % 2)[..., None] == 0, 200, 150).astype(np.uint8).repeat(3, -1)
    bg = Image.fromarray(board).convert('RGBA')
    bg.alpha_composite(Image.fromarray(rgba))
    return bg.convert('RGB')


def fringe(rgba):
    """Partly transparent texels (0 < a < 0.92) by colour class, and how far they lie from the solid drawing."""
    rgb = rgba[..., :3].astype(np.float32) / 255
    a = rgba[..., 3].astype(np.float32) / 255
    core = a >= 0.92
    dist = ndimage.distance_transform_edt(~core)
    soft = (a > 0.02) & ~core
    hsv = cv2.cvtColor(rgb, cv2.COLOR_RGB2HSV)
    h, s, v = hsv[..., 0], hsv[..., 1], hsv[..., 2]
    red = soft & ((h < 20) | (h > 340)) & (s > 0.45) & (v > 0.35)
    yellow = soft & (h >= 35) & (h < 70) & (s > 0.45) & (v > 0.5)
    light = soft & (s < 0.15) & (v > 0.85)
    far = soft & (dist > 3)
    return dict(softTexels=int(soft.sum()), redFringe=int(red.sum()), yellowFringe=int(yellow.sum()), lightFringe=int(light.sum()),
                softFartherThan3px=int(far.sum()), maxSoftDistancePx=float(dist[soft].max()) if soft.any() else 0.0,
                opaqueTexels=int(core.sum()), size=[int(rgba.shape[1]), int(rgba.shape[0])],
                alphaMin=int(rgba[..., 3].min()), alphaMax=int(rgba[..., 3].max())), red | yellow


def sheet(images, labels, width=360):
    cells = []
    for im in images:
        im = Image.fromarray(im) if isinstance(im, np.ndarray) else im
        scale = width / im.width
        cells.append(im.resize((width, int(im.height * scale)), Image.LANCZOS))
    H = max(c.height for c in cells) + 24
    out = Image.new('RGB', (width * len(cells) + 8 * (len(cells) - 1), H), (40, 40, 40))
    d = ImageDraw.Draw(out)
    for i, (c, label) in enumerate(zip(cells, labels)):
        out.paste(c, (i * (width + 8), 24))
        d.text((i * (width + 8) + 4, 4), label, fill=(255, 255, 255))
    return out


def registration(s0, s3, out):
    """Similarity transform taking S0 onto S3 from SIFT matches on the opaque drawing, and what remains different after it."""
    a0, a3 = s0[..., 3] > 128, s3[..., 3] > 128
    iou = float((a0 & a3).sum() / max(1, (a0 | a3).sum()))
    g0 = cv2.cvtColor(s0[..., :3], cv2.COLOR_RGB2GRAY)
    g3 = cv2.cvtColor(s3[..., :3], cv2.COLOR_RGB2GRAY)
    sift = cv2.SIFT_create(6000)
    k0, d0 = sift.detectAndCompute(g0, a0.astype(np.uint8) * 255)
    k3, d3 = sift.detectAndCompute(g3, a3.astype(np.uint8) * 255)
    matches = cv2.BFMatcher().knnMatch(d0, d3, k=2)
    good = [m for m, n in matches if m.distance < 0.72 * n.distance]
    p0 = np.float32([k0[m.queryIdx].pt for m in good])
    p3 = np.float32([k3[m.trainIdx].pt for m in good])
    M, inl = cv2.estimateAffinePartial2D(p0, p3, method=cv2.RANSAC, ransacReprojThreshold=2.5, maxIters=20000)
    inl = inl.ravel().astype(bool)
    scale = float(np.hypot(M[0, 0], M[1, 0]))
    angle = float(np.degrees(np.arctan2(M[1, 0], M[0, 0])))
    res = np.linalg.norm((p0[inl] @ M[:, :2].T + M[:, 2]) - p3[inl], axis=1)
    h, w = s3.shape[:2]
    warped = cv2.warpAffine(s0, M, (w, h), flags=cv2.INTER_LINEAR, borderValue=(0, 0, 0, 0))
    aw = warped[..., 3] > 128
    iou_after = float((aw & a3).sum() / max(1, (aw | a3).sum()))
    # where the silhouettes disagree after alignment (red: only S0, blue: only S3)
    diff = np.zeros((h, w, 3), np.uint8)
    diff[aw & a3] = (90, 90, 90)
    diff[aw & ~a3] = (230, 60, 50)
    diff[~aw & a3] = (60, 120, 240)
    Image.fromarray(diff).save(out / 'spring-silhouette-diff-after-alignment.png')
    # per-pixel colour difference inside the shared silhouette (stone should match; water, lamps and flowers are the stage change)
    both = aw & a3
    cdiff = np.abs(warped[..., :3].astype(np.int16) - s3[..., :3].astype(np.int16)).sum(-1)
    heat = np.zeros((h, w), np.uint8)
    heat[both] = np.clip(cdiff[both] * 0.6, 0, 255).astype(np.uint8)
    cv2.imwrite(str(out / 'spring-colour-diff-after-alignment.png'), cv2.applyColorMap(heat, cv2.COLORMAP_INFERNO))
    blend = (warped.astype(np.float32) * 0.5 + s3.astype(np.float32) * 0.5).astype(np.uint8)
    on(blend, (96, 128, 72)).save(out / 'spring-S0-over-S3-50pct.png')
    return dict(silhouetteIoU=round(iou, 4), siftMatches=len(good), ransacInliers=int(inl.sum()), scale=round(scale, 5), rotationDeg=round(angle, 3),
                translationPx=[round(float(M[0, 2]), 2), round(float(M[1, 2]), 2)], inlierResidualMedianPx=round(float(np.median(res)), 3),
                inlierResidualP95Px=round(float(np.percentile(res, 95)), 3), silhouetteIoUAfterAlignment=round(iou_after, 4),
                onlyS0PxAfter=int((aw & ~a3).sum()), onlyS3PxAfter=int((~aw & a3).sum()))


def seams(rgb):
    """Wrap-edge error relative to the texture's own neighbour differences, for both axes, plus large-scale luminance variation."""
    f = rgb.astype(np.float32)
    inner_x = np.abs(np.diff(f, axis=1)).mean()
    inner_y = np.abs(np.diff(f, axis=0)).mean()
    edge_x = np.abs(f[:, 0] - f[:, -1]).mean()
    edge_y = np.abs(f[0] - f[-1]).mean()
    lum = f @ np.float32([0.299, 0.587, 0.114])
    low = cv2.GaussianBlur(lum, (0, 0), lum.shape[0] / 16)
    return dict(edgeMaeX=round(float(edge_x), 2), edgeMaeY=round(float(edge_y), 2), innerNeighbourMaeX=round(float(inner_x), 2), innerNeighbourMaeY=round(float(inner_y), 2),
                seamRatioX=round(float(edge_x / inner_x), 2), seamRatioY=round(float(edge_y / inner_y), 2),
                lowFrequencyLumaStd=round(float(low.std()), 2), meanRGB=[round(float(c), 1) for c in f.reshape(-1, 3).mean(0)])


def tile4(rgb, mark=True):
    h, w = rgb.shape[:2]
    big = np.tile(rgb, (4, 4, 1))
    im = Image.fromarray(big)
    if mark:
        d = ImageDraw.Draw(im)
        for i in range(1, 4):
            d.line([(i * w, 0), (i * w, 4 * h)], fill=(255, 0, 255), width=1)
            d.line([(0, i * h), (4 * w, i * h)], fill=(255, 0, 255), width=1)
    return im


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--src', default=str(ROOT / 'game/Assets/Echoes/Art/Phase52B5/Source'))
    ap.add_argument('--out', default=str(ROOT / 'artifacts/visual-qa/phase-5.2/b5/asset-audit'))
    args = ap.parse_args()
    src, out = Path(args.src), Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    report = dict(source=str(src), props={}, ground={})
    for name in PROPS:
        rgba = load(src, name)
        info, bad = fringe(rgba)
        report['props'][name] = info
        marked = on(rgba, (0, 0, 0))
        m = np.asarray(marked).copy()
        m[ndimage.binary_dilation(bad, iterations=2)] = (255, 0, 255)
        sheet([on(rgba, c) for c in BACKDROPS.values()] + [checker(rgba), Image.fromarray(m)], list(BACKDROPS) + ['checker', 'red/yellow fringe (magenta)'], 300).save(out / (name + '-backdrops.png'))
        print(name, json.dumps(info))
    report['springRegistration'] = registration(load(src, 'spring_S0_dormant'), load(src, 'spring_S3_restored'), out)
    print('spring registration', json.dumps(report['springRegistration']))
    for name in GROUND:
        rgb = load(src, name, 'RGB')
        report['ground'][name] = seams(rgb)
        im = tile4(rgb)
        im.resize((im.width // 2, im.height // 2), Image.LANCZOS).save(out / (name + '-tiled4x4-original.jpg'), quality=92)
        # the seam crossings at full resolution (where four tiles meet)
        h, w = rgb.shape[:2]
        tile4(rgb, False).crop((w - 160, h - 160, w + 160, h + 160)).resize((640, 640), Image.NEAREST).save(out / (name + '-seam-crossing-original.png'))
        print(name, json.dumps(report['ground'][name]))
    (out / 'audit.json').write_text(json.dumps(report, indent=1), encoding='utf-8')


if __name__ == '__main__':
    main()
