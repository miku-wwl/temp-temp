"""Assembles and zips the Phase 5.2-B4 Gate B review package (artifacts/review/phase52-b4/).

python -I tools/art/phase52b4/make_b4_review.py <evidence-root> <tests-folder> <build-dir> <build-log-dir> <editor-preview-dir>
Every image is labelled by category in its folder name and in comparison/index.html: CONCEPT_ART (the owner's style target, never game
footage), PRODUCTION_ART (what the game draws, made from the owner's B4 pack), EDITOR_PREVIEW (Unity Editor renders, diagnostics only),
REAL_PLAYER_SCREENSHOT and REAL_PLAYER_VIDEO (actual Windows player, AUTOMATED input). Never overwrites an existing package; never includes
Unity caches, the official theme recording or unrelated files.
"""
import hashlib
import html
import json
import shutil
import subprocess
import sys
import zipfile
from datetime import datetime
from pathlib import Path

from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[3]
ev = (root / sys.argv[1]).resolve()
tests = (root / sys.argv[2]).resolve()
build_dir = (root / sys.argv[3]).resolve()
build_logs = (root / sys.argv[4]).resolve()
preview = (root / sys.argv[5]).resolve()
stage = root / 'artifacts/review/phase52-b4/staging'
out_dir = root / 'artifacts/review/phase52-b4'
name = 'Echoes-Phase52-B4-Isometric-Review'
base = 'c829e65aa55bb68faac9aa7f6b720550b4b69ebf'   # the B3 tip this branch started from


def git(*a):
    return subprocess.check_output(['git', *a], cwd=root, text=True).strip()


if stage.exists():
    shutil.rmtree(stage)
stage.mkdir(parents=True)
staged, omitted = {}, []


def put(src, rel):
    src = Path(src)
    if not src.is_file():
        print('MISSING (skipped):', src)
        return None
    dst = stage / rel
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(src, dst)
    staged[str(src.resolve())] = rel
    return dst


def omit(src):
    src = Path(src)
    omitted.append('%s  %d  %s' % (hashlib.sha256(src.read_bytes()).hexdigest(), src.stat().st_size, src.relative_to(root).as_posix()))


def put_tree(src, rel, keep=lambda p: True):
    """Stages a folder; files that keep() rejects are listed with their hash in OMITTED-FILES-SHA256.txt (they stay on disk)."""
    src = Path(src)
    if not src.is_dir():
        print('MISSING folder (skipped):', src)
        return
    for p in sorted(src.rglob('*')):
        if p.is_file() and 'frames' not in p.parts and 'save' not in p.parts:
            if keep(p):
                put(p, rel + '/' + p.relative_to(src).as_posix())
            else:
                omit(p)


# ---- top level documents ---------------------------------------------------------------------------------------------------------------
put(ev / 'README_FIRST_zh.md', 'README_FIRST_zh.md')
put(ev / 'final-report.md', 'final-report.md')

# ---- motion: the real player video, contact sheet, selected frames and minimap strip (REAL_PLAYER_VIDEO) ---------------------------------
video = ev / 'player/tour720-video'
put(video / 'actual-player-b4-motion.mp4', 'motion/actual-player-b4-motion.mp4')
put(video / 'contact-sheet.png', 'motion/contact-sheet.png')
put(ev / 'minimap-strip.png', 'motion/minimap-strip.png')
put_tree(video / 'selected-frames', 'motion/selected-frames')
for f in ('capture-provenance.json', 'tour-stats.json', 'tour.json', 'frame-manifest.json', 'process-result.json', 'ffmpeg.log'):
    put(video / f, 'motion/run-data/' + f)

# ---- real player screenshots (REAL_PLAYER_SCREENSHOT) ----------------------------------------------------------------------------------
put_tree(ev / 'player/tour1080', 'real-player-screenshots/tour1080', keep=lambda p: p.suffix == '.png' or p.name in ('tour-stats.json', 'process-result.json'))
put_tree(ev / 'player/tour720-video', 'real-player-screenshots/tour720-video',
         keep=lambda p: p.name in ('dialogue-mira.png', 'inventory-portrait.png', 'pause-menu.png', 'after-b4-plaza.png', 'hero-idle-S-closest-zoom.png'))
SMOKE_KEEP = ('A1-', 'A2-', 'A3-', 'NPC', 'Clover', '03-', '05-', '06-', '13a', '15-')
put_tree(ev / 'player/smoke1080', 'real-player-screenshots/smoke1080', keep=lambda p: p.suffix == '.json' or (p.suffix == '.png' and p.name.startswith(SMOKE_KEEP)))
put_tree(ev / 'player/smoke720', 'real-player-screenshots/smoke720', keep=lambda p: p.suffix == '.json')
for tag in ('tour720-unrecorded', 'tour1080-unrecorded', 'tour1080-before-unrecorded'):
    put_tree(ev / 'player' / tag, 'performance/' + tag, keep=lambda p: p.suffix == '.json')

# ---- the art: concept (style target), production art, asset audit -------------------------------------------------------------------------
src = root / 'game/Assets/Echoes/Art/Phase52B4/Source'
art = root / 'game/Assets/Echoes/Art/Phase52B4'
audit = root / 'artifacts/art-work/phase52b4'
put(src / 'art-direction/b4_isometric_style_target.png', 'CONCEPT_ART-not-game-footage/b4_isometric_style_target.png')
put_tree(art / 'Sprites', 'PRODUCTION_ART/caretaker', keep=lambda p: p.suffix in ('.png', '.json'))
put_tree(art / 'Props', 'PRODUCTION_ART/props', keep=lambda p: p.suffix in ('.png', '.json'))
put_tree(art / 'UI', 'PRODUCTION_ART/minimap', keep=lambda p: p.suffix in ('.png', '.json'))
put(art / 'Terrain/PlazaCobble.png', 'PRODUCTION_ART/plaza/PlazaCobble.png')
put_tree(audit / 'hero', 'asset-audit/caretaker')
put_tree(audit / 'props', 'asset-audit/props')
put_tree(audit / 'audit', 'asset-audit/sources-on-backgrounds')
put_tree(preview, 'EDITOR_PREVIEW-not-player-evidence/pitch-and-occlusion', keep=lambda p: p.name == 'NOTE.txt' or p.suffix == '.png' and ('-p48-' in p.name or 'plaza' in p.name or 'workshop-front' in p.name))

# ---- comparison page ---------------------------------------------------------------------------------------------------------------------
cmp = stage / 'comparison'
sections = []


def pair(group, title, items):
    """items: list of (label, category, source path)."""
    figs = []
    for label, category, path in items:
        if path is None or not Path(path).is_file():
            continue
        key = str(Path(path).resolve())
        if key in staged:
            rel = '../' + staged[key]
        else:
            rel = '%s/%s' % (group, Path(path).name)
            dst = cmp / rel
            dst.parent.mkdir(parents=True, exist_ok=True)
            if dst.exists():
                rel = '%s/%s-%s' % (group, category.lower(), Path(path).name)
                dst = cmp / rel
            shutil.copyfile(path, dst)
        figs.append('<figure><img src="%s" loading="lazy"><figcaption><b>%s</b> &middot; %s</figcaption></figure>' % (html.escape(rel), html.escape(category), html.escape(label)))
    if figs:
        sections.append('<section><h2>%s</h2><div class="row">%s</div></section>' % (html.escape(title), ''.join(figs)))


t = ev / 'player/tour1080'
for spot, label in (('workshop-front', 'Alchemy workshop, front'), ('workshop-side', 'Alchemy workshop, side'), ('plaza', 'Plaza'), ('willow', 'Willow'), ('spring', 'Spring (S0, dormant)')):
    pair('before-after', '%s: B3 (before) vs B4 (after), same build, same place' % label,
         [('B3 presentation (before mode)', 'REAL_PLAYER_SCREENSHOT', t / ('before-b3-%s.png' % spot)), ('B4', 'REAL_PLAYER_SCREENSHOT', t / ('after-b4-%s.png' % spot))])
for spot in ('plaza', 'workshop-front'):
    pair('pitch', 'Camera pitch candidates at the %s (default 48)' % spot,
         [('pitch %d' % p, 'REAL_PLAYER_SCREENSHOT', t / ('pitch-%d-%s.png' % (p, spot))) for p in (42, 48, 54)])
pair('caretaker', 'Caretaker idle, closest zoom (S, W, E, N)', [('idle ' + d, 'REAL_PLAYER_SCREENSHOT', t / ('hero-idle-%s-closest-zoom.png' % d)) for d in 'SWEN'])
pair('caretaker', 'Caretaker walking, closest zoom (S, W, E, N)', [('walk ' + d, 'REAL_PLAYER_SCREENSHOT', t / ('hero-walk-%s-closest-zoom.png' % d)) for d in 'SWEN'])
pair('caretaker', 'Caretaker at the default zoom', [('idle ' + d, 'REAL_PLAYER_SCREENSHOT', t / ('hero-idle-%s-default-zoom.png' % d)) for d in 'SN'])
pair('caretaker', 'Source drawings vs production frames', [('Production frames at game scale (asset audit)', 'PRODUCTION_ART', audit / 'hero/game-scale-frames.png'),
                                                          ('Head-size check across directions', 'PRODUCTION_ART', audit / 'hero/onion-skin-heads.png')])
pair('occlusion', 'Occlusion: behind the painted workshop, under the willow', [('behind the workshop', 'REAL_PLAYER_SCREENSHOT', t / 'occlusion-behind-workshop.png'),
                                                                             ('behind the willow', 'REAL_PLAYER_SCREENSHOT', t / 'occlusion-behind-willow.png')])
pair('minimap', 'Minimap over the route (crops of real recorded frames, objective changing)', [('minimap strip', 'REAL_PLAYER_SCREENSHOT', ev / 'minimap-strip.png')])
pair('ui', 'UI with the minimap: dialogue, satchel, pause (1080p and 720p)', [('dialogue 1080p', 'REAL_PLAYER_SCREENSHOT', t / 'dialogue-mira.png'), ('satchel 1080p', 'REAL_PLAYER_SCREENSHOT', t / 'inventory-portrait.png'),
                                                                               ('pause 1080p', 'REAL_PLAYER_SCREENSHOT', t / 'pause-menu.png'), ('dialogue 720p', 'REAL_PLAYER_SCREENSHOT', video / 'dialogue-mira.png'),
                                                                               ('pause 720p', 'REAL_PLAYER_SCREENSHOT', video / 'pause-menu.png')])
for spot, label in (('workshop-front', 'Workshop'), ('plaza', 'Plaza'), ('spring', 'Spring')):
    pair('stages', '%s through the restoration: S0 (played) and S1-S3 (SET BY THE EVIDENCE SCRIPT, not reached by play)' % label,
         [('S0 silent valley', 'REAL_PLAYER_SCREENSHOT', t / ('after-b4-%s.png' % spot))]
         + [('%s (script)' % st, 'REAL_PLAYER_SCREENSHOT', t / ('stage-%s-set-by-script-%s.png' % (st, spot))) for st in ('S1', 'S2', 'S3')])
pair('concept', 'The owner\'s style target (CONCEPT ART, NOT GAME FOOTAGE) next to the real B4 game', [('style target, concept only', 'CONCEPT_ART', src / 'art-direction/b4_isometric_style_target.png'),
                                                                                                   ('real B4 player, workshop', 'REAL_PLAYER_SCREENSHOT', t / 'after-b4-workshop-front.png')])
page = '''<!doctype html><html lang="en"><meta charset="utf-8"><title>Echoes B4 review</title>
<style>body{font:15px/1.5 system-ui,sans-serif;background:#18201e;color:#ece6d6;margin:0;padding:0 16px 40px}h1{font-size:22px}h2{font-size:16px;margin:26px 0 8px}
.row{display:grid;grid-template-columns:repeat(auto-fit,minmax(340px,1fr));gap:12px}figure{margin:0}img{width:100%%;height:auto;display:block;border:1px solid #3c4b49}
figcaption{font-size:13px;color:#cfc8b2;padding:4px 0}.note{max-width:960px;color:#d8d0bb}b{color:#f2d48a}</style>
<h1>Echoes of the Valley &middot; Phase 5.2-B4 (high-angle 2.5D pilot) &middot; comparisons</h1>
<p class="note">Labels: <b>CONCEPT_ART</b> = the owner's style target, never game footage; <b>PRODUCTION_ART</b> = images the game draws, made from the owner's B4 pack;
<b>EDITOR_PREVIEW</b> = Unity Editor renders used for decisions, not player evidence; <b>REAL_PLAYER_SCREENSHOT</b> = unmodified framebuffer captures of the
Windows player driven by <b>automated</b> input (no human played). "Before" is the B3 presentation switched on in the same B4 build at the same place.
Captions are outside the pixels. This is the author's evidence, not an approval: <b>GATE_B_OWNER_APPROVAL=PENDING</b>.</p>
%s</html>''' % '\n'.join(sections)
cmp.mkdir(parents=True, exist_ok=True)
(cmp / 'index.html').write_text(page, encoding='utf-8')

# ---- tests and logs ----------------------------------------------------------------------------------------------------------------------
put(tests / 'tests-EditMode.xml', 'tests/EditMode.xml')
put(tests / 'tests-PlayMode.xml', 'tests/PlayMode.xml')
put(build_logs / 'Build.log', 'logs/build.log')
put(video / 'player.log', 'logs/player.log')
for tag in ('tour1080', 'tour720-unrecorded', 'tour1080-unrecorded', 'tour1080-before-unrecorded', 'smoke720', 'smoke1080'):
    put(ev / 'player' / tag / 'player.log', 'logs/player-%s.log' % tag)
put(ev / 'player/evidence-run-summary.json', 'logs/evidence-run-summary.json')

# ---- source: changed files and git summary -----------------------------------------------------------------------------------------------
head = git('rev-parse', 'HEAD')
changed = git('diff', '--name-status', base, 'HEAD')
(stage / 'source').mkdir(exist_ok=True)
(stage / 'source/changed-files.txt').write_text('base ' + base + '\nhead ' + head + '\n' + changed + '\n', encoding='utf-8')
(stage / 'source/git-change-summary.txt').write_text(git('log', '--oneline', base + '..HEAD') + '\n\n' + git('diff', '--stat', base, 'HEAD') + '\n', encoding='utf-8')
for line in changed.splitlines():
    status, _, path = line.partition('\t')
    p = root / path
    if status.startswith('D') or not p.is_file():
        continue
    if p.suffix in ('.cs', '.py', '.ps1', '.shader', '.md', '.json') and p.stat().st_size < 400_000 and '/Scenes/' not in path and '/Source/' not in path:
        put(p, 'source/files/' + path)

# ---- provenance --------------------------------------------------------------------------------------------------------------------------
for f in ('asset-manifest.md', 'AI-disclosure.md', 'license-notes.md'):
    put(root / 'tools/art/phase52b4/provenance' / f, 'provenance/' + f)
for f in ('README.zh-CN.md', 'ASSET-QA.md', 'SHA256SUMS.txt'):
    put(root / 'tools/art/phase52b4/source-docs' / f, 'provenance/owner-pack/' + f)
put(root / 'docs/10-PHASE52-B4.md', 'provenance/10-PHASE52-B4.md')
put(root / 'docs/03-RULES-CREDITS.md', 'provenance/03-RULES-CREDITS.md')

# ---- build manifest, labels, hashes, zip -------------------------------------------------------------------------------------------------
lines = ['SHA-256  size  file (build %s)' % build_dir.name]
for p in sorted(build_dir.rglob('*')):
    if p.is_file():
        lines.append('%s  %d  %s' % (hashlib.sha256(p.read_bytes()).hexdigest(), p.stat().st_size, p.relative_to(build_dir).as_posix()))
(stage / 'logs').mkdir(exist_ok=True)
(stage / 'logs/build-manifest-SHA256.txt').write_text('\n'.join(lines) + '\n', encoding='utf-8')
(stage / 'LABELS.md').write_text('''# Labels used in this package

| Label / folder | Meaning |
| --- | --- |
| `CONCEPT_ART-not-game-footage/` | The owner's B4 style target. A concept painting: never game footage, not in the build. |
| `PRODUCTION_ART/` | The images the game actually draws (caretaker atlas, painted prop cards, foliage atlas, minimap frame and base, plaza cobble), made from the owner's B4 pack by the scripts in `source/files/tools/art/phase52b4`. |
| `asset-audit/` | Audit sheets: every source on white/black/grass/earth, cleaned props, caretaker scale and head checks. |
| `EDITOR_PREVIEW-not-player-evidence/` | Unity Editor renders used to choose the camera and check occlusion. Not Windows-player evidence. |
| `real-player-screenshots/`, `motion/selected-frames/`, `motion/minimap-strip.png` | REAL_PLAYER_SCREENSHOT: unmodified captures (or crops of them) of the Windows player, AUTOMATED input. |
| `motion/actual-player-b4-motion.mp4` | REAL_PLAYER_VIDEO: the Windows player's own framebuffer frames at their real intervals, AUTOMATED input, no audio. |
''', encoding='utf-8')
if omitted:
    header = 'SHA-256  size  path (left out to keep the package reviewable; the files stay in the local artifacts/ folder)'
    (stage / 'OMITTED-FILES-SHA256.txt').write_text(header + chr(10) + chr(10).join(omitted) + chr(10), encoding='utf-8')
manifest = []
for p in sorted(stage.rglob('*')):
    if p.is_file():
        manifest.append('%s  %d  %s' % (hashlib.sha256(p.read_bytes()).hexdigest(), p.stat().st_size, p.relative_to(stage).as_posix()))
(stage / 'MANIFEST-SHA256.txt').write_text('SHA-256  size  member\n# package for commit %s\n' % head + '\n'.join(manifest) + '\n', encoding='utf-8')
out_dir.mkdir(parents=True, exist_ok=True)
zip_path = out_dir / (name + '.zip')
if zip_path.exists():
    zip_path = out_dir / (name + '-' + datetime.now().strftime('%Y%m%d-%H%M%S') + '.zip')
with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for p in sorted(stage.rglob('*')):
        if p.is_file():
            z.write(p, p.relative_to(stage).as_posix())
digest = hashlib.sha256(zip_path.read_bytes()).hexdigest().upper()
(out_dir / (zip_path.name + '.sha256.txt')).write_text(digest + '  ' + zip_path.name + '\n', encoding='utf-8')
with zipfile.ZipFile(zip_path) as z:
    assert z.testzip() is None
    print('ZIP', zip_path, 'members', len(z.namelist()), 'bytes', zip_path.stat().st_size)
print('REVIEW_ZIP_SHA256=' + digest)
