"""Shared image helpers for the Phase 5.2-B4 character and prop pipelines (no drawing: only alpha cleanup, cropping, scaling, warping)."""
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
SRC = ROOT / 'game/Assets/Echoes/Art/Phase52B4/Source'


def load(rel):
    return np.asarray(Image.open(SRC / rel).convert('RGBA'))


def clean_alpha(rgba, haze_alpha=0.35, haze_distance=2.0, min_island=60, solid=0.92):
    """Removes background glow and specks and decontaminates edge colours.

    * semi-transparent pixels that are weak (alpha < haze_alpha) and more than haze_distance px from the solid figure are background
      glow, not drawing: they become fully transparent;
    * small islands far from the figure are removed;
    * every remaining partly transparent pixel takes the colour of the nearest solid pixel, so no red, grey or light fringe survives.
    """
    rgb = rgba[..., :3].astype(np.float32)
    a = rgba[..., 3].astype(np.float32) / 255.0
    core = a >= solid
    if not core.any():
        return rgba.copy()
    dist, (iy, ix) = ndimage.distance_transform_edt(~core, return_indices=True)
    a = np.where((a < haze_alpha) & (dist > haze_distance), 0.0, a)
    lab, n = ndimage.label(a > 0.04)
    if n > 1:
        sizes = ndimage.sum(np.ones_like(a), lab, range(1, n + 1))
        touching = np.zeros(n + 1, bool)
        touching[np.unique(lab[ndimage.binary_dilation(core, iterations=2) & (lab > 0)])] = True
        for i, s in enumerate(sizes, start=1):
            if s < min_island and not touching[i]:
                a[lab == i] = 0
    edge = (a > 0) & (a < solid)
    rgb[edge] = rgb[iy[edge], ix[edge]]
    out = np.dstack([np.clip(rgb, 0, 255), np.clip(a * 255 + 0.5, 0, 255)]).astype(np.uint8)
    out[a <= 0] = 0
    return out


def bbox(rgba, threshold=8):
    ys, xs = np.where(rgba[..., 3] > threshold)
    return int(xs.min()), int(xs.max()) + 1, int(ys.min()), int(ys.max()) + 1


def split_poses(rgba, count, close=6, min_area=20000):
    """The `count` largest figures of a key-pose sheet, each with every stray strand assigned to the nearest figure.
    Returns full-sheet-size RGBA layers (other figures removed), left to right."""
    a = rgba[..., 3] > 40
    lab, n = ndimage.label(ndimage.binary_closing(a, iterations=close))
    sizes = ndimage.sum(a, lab, range(1, n + 1))
    keep = [i + 1 for i in np.argsort(sizes)[::-1][:count]]
    centroids = ndimage.center_of_mass(a, lab, keep)
    keep = [k for _, k in sorted(zip([c[1] for c in centroids], keep))]
    # every visible pixel goes to the figure whose (closed) mask is nearest
    dists = []
    for k in keep:
        dists.append(ndimage.distance_transform_edt(lab != k))
    owner = np.argmin(np.stack(dists, 0), 0)
    layers = []
    for i, k in enumerate(keep):
        layer = rgba.copy()
        layer[owner != i] = 0
        layers.append(layer)
    return layers


def premul(rgba):
    f = rgba.astype(np.float32) / 255.0
    f[..., :3] *= f[..., 3:4]
    return f


def unpremul(f):
    a = f[..., 3:4]
    rgb = np.where(a > 1e-4, f[..., :3] / np.maximum(a, 1e-4), 0)
    return (np.clip(np.concatenate([rgb, a], -1), 0, 1) * 255 + 0.5).astype(np.uint8)


def resize(rgba, scale):
    """Area-filtered downscale in premultiplied space (no dark or light edges)."""
    import cv2
    f = premul(rgba)
    h, w = f.shape[:2]
    out = cv2.resize(f, (max(1, int(round(w * scale))), max(1, int(round(h * scale)))), interpolation=cv2.INTER_AREA)
    return unpremul(out)


def decontaminate(rgba_u8, solid=235):
    """Partly transparent texels take the colour of the nearest solid texel (a warp's resampling leaves near-zero-alpha texels whose
    un-premultiplied colour is black); alpha is kept."""
    a = rgba_u8[..., 3]
    core = a >= solid
    if not core.any():
        return rgba_u8
    idx = ndimage.distance_transform_edt(~core, return_distances=False, return_indices=True)
    out = rgba_u8.copy()
    edge = (a > 0) & ~core
    out[..., :3][edge] = rgba_u8[idx[0], idx[1], :3][edge]
    return out


def bleed(rgba_u8):
    """Copies the nearest opaque colour into transparent texels so bilinear filtering and mipmaps never pull in black or white."""
    a = rgba_u8[..., 3]
    solid = a > 8
    if not solid.any():
        return rgba_u8
    idx = ndimage.distance_transform_edt(~solid, return_distances=False, return_indices=True)
    out = rgba_u8.copy()
    out[..., :3] = rgba_u8[idx[0], idx[1], :3]
    out[..., 3] = a
    return out


def on(rgba, colour=(70, 90, 70)):
    im = Image.fromarray(rgba)
    bg = Image.new('RGBA', im.size, tuple(colour) + (255,))
    bg.alpha_composite(im)
    return bg.convert('RGB')
