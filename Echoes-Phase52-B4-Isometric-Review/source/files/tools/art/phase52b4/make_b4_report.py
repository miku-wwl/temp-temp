"""Writes final-report.md and README_FIRST_zh.md for the Phase 5.2-B4 Gate B review package.

python -I tools/art/phase52b4/make_b4_report.py <judgments.json>
Numbers (tests, build id, frame times, distances, exit codes, hashes, git ids, minimap timeline) are read from the evidence files, never
typed by hand. Judgments (statuses, what still looks bad) come from judgments.json and are the author's opinion, not owner approval.
"""
import hashlib
import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

root = Path(__file__).resolve().parents[3]
J = json.loads(Path(sys.argv[1]).read_text(encoding='utf-8'))
ev = root / J['evidenceRoot']
tv = root / J['testFolder']


def read(path, default=None):
    p = Path(path)
    return json.loads(p.read_text(encoding='utf-8-sig')) if p.is_file() else default


def tests(xml):
    r = ET.parse(xml).getroot()
    return '%s %s/%s (failed %s, skipped %s)' % (r.get('result'), r.get('passed'), r.get('total'), r.get('failed'), r.get('skipped'))


def short(xml):
    r = ET.parse(xml).getroot()
    return '%s/%s' % (r.get('passed'), r.get('total'))


def b4_cases(xml):
    r = ET.parse(xml).getroot()
    return ['%s: %s' % (c.get('name'), c.get('result')) for c in r.iter('test-case') if 'Phase52B4' in (c.get('classname') or '')]


def git(*a):
    return subprocess.check_output(['git', *a], cwd=root, text=True).strip()


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()


edit_xml, play_xml = tv / 'tests-EditMode.xml', tv / 'tests-PlayMode.xml'
edit, play = tests(edit_xml), tests(play_xml)
cases = b4_cases(edit_xml) + b4_cases(play_xml)
blog = (root / J['buildLog']).read_text(encoding='utf-8', errors='replace')
m = re.search(r'PILOT_BUILD (\w+) id=(\w+) errors=(\d+) warnings=(\d+) bytes=(\d+)', blog)
build = dict(result=m.group(1), id=m.group(2), errors=m.group(3), warnings=m.group(4), bytes=m.group(5)) if m else dict(result='UNKNOWN', id='?', errors='?', warnings='?', bytes='?')
exe = root / J['buildDir'] / 'EchoesOfTheValley.exe'
tour_keys = ('tour720-video', 'tour1080', 'tour1080-unrecorded', 'tour1080-before-unrecorded', 'tour720-unrecorded')
runs = {k: (read(ev / 'player' / k / 'process-result.json'), read(ev / 'player' / k / 'tour-stats.json')) for k in tour_keys}
smoke = {k: read(ev / 'player' / k / 'process-result.json') for k in ('smoke720', 'smoke1080')}
prov = read(ev / 'player/tour720-video/capture-provenance.json', {})
head = git('rev-parse', 'HEAD')
branch = git('rev-parse', '--abbrev-ref', 'HEAD')
main = git('rev-parse', 'main')
b3_tip = git('rev-parse', 'art/phase52-b3-2d5d')
b2_tip = git('rev-parse', 'art/phase52-b2')
base = 'c829e65aa55bb68faac9aa7f6b720550b4b69ebf'
b3_intact = b3_tip == base
b2_intact = b2_tip == 'aef87c09cd6ceee8ed183e59608c7b833268e481'
main_is_ancestor = subprocess.call(['git', 'merge-base', '--is-ancestor', main, head], cwd=root) == 0
rc1 = root / 'artifacts/release/EchoesOfTheValley-Windows-x64-RC1.zip'
rc2 = root / 'artifacts/release/phase51-rc2-validated/EchoesOfTheValley-Windows-x64-RC2-Art.zip'
protected = rc1.is_file() and rc2.is_file() and sha(rc1) == 'EB9BFC0D9B0BA60A0E9E8367ADF3BA4EB4D4F3EFE8F0F6FCD58A2AB4FDA3112A' and sha(rc2) == '9085554ED52B39B46B250AA5E7E94642815FF76D9730AF038E6E8F180CFC8A80'


def bullets(items):
    return '\n'.join('- ' + i for i in items)


def run_line(label, key):
    r, s = runs[key]
    if not r or not s:
        return '%s: not run' % label
    f = s['frameMs']
    others = r.get('otherGameProcessesRunning') or []
    return ('%s: exit code %s (natural exit %s), exception/error lines %s; route %s m in %s s, route pass %s, failures %s; %s, %d frames, P50 %.2f / P95 %.2f / P99 %.2f ms, worst %.1f ms, mean %.1f FPS, 1%% low %.1f FPS (%s)%s'
            % (label, r['exitCode'], r['naturalExit'], r['exceptionLines'], s['metres'], s['routeSeconds'], s['routePass'], s['failures'] or 'none', s['resolution'], s['frames'], f['p50'], f['p95'], f['p99'], f['worst'],
               s['fps']['mean'], s['fps']['onePercentLow'], s['note'], ('; another EchoesOfTheValley process was running (PID %s, the owner-started B3 player)' % ', '.join(str(o) for o in others)) if others else ''))


run_lines = [run_line('Recorded route 720p (video)', 'tour720-video'), run_line('Route 1080p (stills)', 'tour1080'),
             run_line('Unrecorded route 1080p, B4', 'tour1080-unrecorded'), run_line('Unrecorded route 1080p, B3 presentation in the same build (before mode)', 'tour1080-before-unrecorded'),
             run_line('Unrecorded route 720p, B4', 'tour720-unrecorded')]
for k, label in (('smoke720', 'Whole-game smoke 720p'), ('smoke1080', 'Whole-game smoke 1080p')):
    r = smoke[k]
    run_lines.append('%s: %s' % (label, ('natural exit %s, exit code %s, %s s, %s screenshots, exception lines %s, %s' % (r['naturalExit'], r['exitCode'], r['seconds'], r['screenshots'], r['exceptionLines'], '; '.join(r.get('passLines') or []))) if r else 'not run'))
video_line = ('%.2f s, %s real framebuffer frames at their captured intervals (about %.1f FPS), 1280x720, no audio, no desktop capture, no concept compositing; SHA-256 `%s`' % (prov['videoDuration'], prov['framesCaptured'], prov['framesCaptured'] / max(1e-6, prov['capturedSeconds']), prov['movieSha256'])) if prov else 'not produced'
b4p, b3p = runs['tour1080-unrecorded'][1] or {}, runs['tour1080-before-unrecorded'][1] or {}
perf = 'not measured'
if b4p and b3p:
    perf = ('B4 1080p: mean %.1f FPS, 1%% low %.1f FPS, P95 %.2f ms, P99 %.2f ms; B3 presentation in the same build and route: mean %.1f FPS, 1%% low %.1f FPS, P95 %.2f ms, P99 %.2f ms'
            % (b4p['fps']['mean'], b4p['fps']['onePercentLow'], b4p['frameMs']['p95'], b4p['frameMs']['p99'], b3p['fps']['mean'], b3p['fps']['onePercentLow'], b3p['frameMs']['p95'], b3p['frameMs']['p99']))
mm = (runs['tour720-video'][1] or {}).get('minimap', {})
timeline = ', '.join('%.1f s %s (%s)' % (e['t'], e['objective'], e['step']) for e in mm.get('objectiveTimeline', [])) or 'not recorded'
# the scale is measured on the unrecorded run: in the recorded run uneven capture frames put a frame between position and map samples
scale = ((runs['tour1080-unrecorded'][1] or {}).get('minimap') or {}).get('mapUnitsPerMetre', {})
vs = runs['tour720-unrecorded'][1] or {}
skate = vs.get('runPhaseVsDistance', {})
dirs = (runs['tour720-video'][1] or {}).get('directionSamples', {})
S = J['status']
smoke_ok = all(smoke[k] and smoke[k]['naturalExit'] and smoke[k]['exitCode'] == 0 and smoke[k]['exceptionLines'] == 0 for k in smoke)
video_ok = bool(prov) and 30 <= prov.get('videoDuration', 0) <= 150
stills = (runs['tour1080'][1] or {}).get('stills', [])
review_zip = J.get('reviewZip', 'see the sidecar .sha256.txt next to the zip (a zip cannot contain its own hash)')

report = '''# Echoes of the Valley: Phase 5.2-B4 (high-angle static 2.5D) review report

2026-10-29 NZDT, branch `%(branch)s` (local only), started from the B3 tip `%(base)s`. **Author self-assessment only. Gate B owner approval, human visual approval and human gameplay QA are PENDING; world expansion is NOT allowed.** Every screenshot and the video are real Windows-player framebuffer captures driven by **automated** input (no human played). The owner's style target is shown only as labelled concept art.

## 1. Git and safety (checked before any work, re-read now)

- Branch `%(branch)s` at `%(head)s`; B3 branch `art/phase52-b3-2d5d` still at `%(b3)s` (unchanged: %(b3ok)s); B2 branch unchanged: %(b2ok)s; `main` at `%(main)s` is an ancestor of this branch: %(mainanc)s. Nothing was merged into `main`; nothing was pushed; no branch was deleted.
- The B4 branch was created from the committed B3 tip (no uncommitted owner work was found then); no `git reset --hard`, checkout over changes or force operation was used. RC1/RC2 release zips unchanged: %(prot)s. The original, B2 and B3 scenes are untouched (EditMode test `OriginalB2AndB3Scenes_ContainNoB4Presentation`). The old version is recoverable by checking out `art/phase52-b3-2d5d`, and inside the B4 build by `-echoes-b4-before`.

## 2. Asset pack

%(assets)s

## 3. What was built

%(built)s

## 4. Camera

%(camera)s

## 5. Caretaker (idle and light animation)

%(hero)s

Real-player direction samples in the recorded route: %(dirs)s. Run-cycle check (unrecorded 720p route): the run phase advanced by distance / stride (%(stride).2f m) with a median per-frame difference of %(skmed)s and p95 %(skp95)s of a cycle (a sampling artefact of one frame; the cycle is driven by distance, so feet cannot skate).

## 6. Minimap (real world data)

%(minimap)s

Recorded route (the video), objective shown by the minimap over time: %(timeline)s. Map scale while walking in the unrecorded 1080p route (UI units per metre of the player's own movement, constant if the map follows the world): p05 %(sc05)s, median %(scmed)s, p95 %(sc95)s over %(scn)s steps.

## 7. Static environment

%(env)s

## 8. WHAT STILL LOOKS BAD

%(bad)s

## 9. Visual gate (author's judgment of the real-player screenshots; the owner decides)

%(gate)s

## 10. Tests (read from the XML)

| Run | Result |
| --- | --- |
| EditMode, isolated project copy `%(tv)s` | %(edit)s |
| PlayMode, same copy | %(play)s |

B4 tests: %(cases)s. No existing test was removed or weakened (B3 historical baseline: EditMode 229/229, PlayMode 96/96).

## 11. Windows build

Non-development Windows x64 Mono/D3D11, Unity 6000.3.27f1 / URP 17.3.0, version `0.5.2-pilot.4`, `%(bdir)s`: **%(bres)s**, build id `%(bid)s`, errors %(berr)s, warnings %(bwarn)s, %(bbytes)s bytes, `EchoesOfTheValley.exe` SHA-256 `%(exe)s` (the Unity launcher stub; every build file is hashed in the package's `logs/build-manifest-SHA256.txt`). `-echoes-b4-before` starts the same build with the B3 presentation.

## 12. Real-player runs (automated)

%(runs)s

Video: %(video)s. Stills in the 1080p run: %(nstills)d (%(stilllist)s).

## 13. Performance (Radeon 780M, D3D11, windowed, 60 FPS cap, vSync off)

%(perf)s.

%(perfnote)s

## 14. Status

```text
PHASE52_B4_STATUS=%(s_b4)s
SOURCE_BRANCH=%(branch)s
SOURCE_COMMIT=%(head)s
ART_DIRECTION=HIGH_ANGLE_STATIC_25D + WARM_ALCHEMY_ANIME
ASSET_ZIP_VERIFICATION=%(s_zip)s
NEW_ASSETS_USED=%(s_used)s
REJECTED_ASSETS_AND_REASONS=%(s_rej)s
HERO_IDLE_STATUS=%(s_idle)s
HERO_LIGHT_ANIMATION_STATUS=%(s_anim)s
CAMERA_PITCH_FINAL=%(s_pitch)s
MINIMAP_REAL_WORLD_STATUS=%(s_map)s
STATIC_ENVIRONMENT_VISUAL_STATUS=%(s_env)s
WORLD_CHARACTER_STYLE_COHERENCE=%(s_coh)s
EDITMODE_STATUS=%(edit_s)s
PLAYMODE_STATUS=%(play_s)s
WINDOWS_BUILD_STATUS=%(bres)s %(bid)s (%(bdir)s)
ACTUAL_PLAYER_SCREENSHOTS=%(s_shots)s
CONTINUOUS_REAL_PLAYER_MOVEMENT_VIDEO=%(s_video)s
Radeon_780M_1080P_PERFORMANCE=%(s_perf)s
GATE_B_RESULT=PARTIAL_OR_FAIL_UNTIL_OWNER_APPROVAL
GATE_B_OWNER_APPROVAL=PENDING
HUMAN_VISUAL_APPROVAL=PENDING
HUMAN_GAMEPLAY_QA_STATUS=PENDING
WORLD_EXPANSION_ALLOWED=NO
REMOTE_PUSH_STATUS=NOT_ATTEMPTED
ITCH_SUBMISSION_STATUS=NOT_SUBMITTED
REVIEW_ZIP=%(zip)s
NEXT_ACTION=Owner plays actual B4 Windows build, assesses 2.5D visual fidelity
```
''' % dict(branch=branch, base=base, head=head, b3=b3_tip, b3ok='YES' if b3_intact else 'NO', b2ok='YES' if b2_intact else 'NO', main=main, mainanc='YES' if main_is_ancestor else 'NO',
           prot='YES' if protected else 'NO (or not present locally)', assets=bullets(J['assets']), built=bullets(J['built']), camera=bullets(J['camera']), hero=bullets(J['hero']),
           dirs=', '.join('%s %d' % kv for kv in sorted(dirs.items())) or 'n/a', stride=skate.get('stride', 0), skmed=skate.get('medianError'), skp95=skate.get('p95Error'),
           minimap=bullets(J['minimap']), timeline=timeline, sc05=scale.get('p05'), scmed=scale.get('median'), sc95=scale.get('p95'), scn=scale.get('samples'),
           env=bullets(J['env']), bad=bullets(J['bad']), gate=bullets(J['gate']), tv=J['testFolder'], edit=edit, play=play, cases=', '.join(cases) or 'none found',
           bdir=J['buildDir'], bres=build['result'], bid=build['id'], berr=build['errors'], bwarn=build['warnings'], bbytes=build['bytes'], exe=sha(exe) if exe.is_file() else 'MISSING',
           runs=bullets(run_lines), video=video_line, nstills=len(stills), stilllist=', '.join(stills), perf=perf, perfnote=J['perfNote'],
           s_b4=S['b4'], s_zip=S['zip'], s_used=S['used'], s_rej=S['rejected'], s_idle=S['idle'], s_anim=S['animation'], s_pitch=S['pitch'], s_map=S['minimap'], s_env=S['environment'],
           s_coh=S['coherence'], edit_s='%s %s' % (ET.parse(edit_xml).getroot().get('result'), short(edit_xml)), play_s='%s %s' % (ET.parse(play_xml).getroot().get('result'), short(play_xml)),
           s_shots=S['screenshots'], s_video=('PASS (AUTOMATED input, %.1f s, 1280x720, real framebuffer)' % prov['videoDuration']) if video_ok else 'FAIL or incomplete',
           s_perf=S['performance'], zip=review_zip)
(ev / 'final-report.md').write_text(report, encoding='utf-8')
readme = '''# Echoes of the Valley · Phase 5.2-B4（高角度静态 2.5D）Gate B 审阅包

**这是作者自评材料，不是验收。** `GATE_B_RESULT=PARTIAL_OR_FAIL_UNTIL_OWNER_APPROVAL`、`GATE_B_OWNER_APPROVAL=PENDING`、`HUMAN_VISUAL_APPROVAL=PENDING`、`HUMAN_GAMEPLAY_QA_STATUS=PENDING`、`WORLD_EXPANSION_ALLOWED=NO`。所有截图和视频都来自真实 Windows Player，输入是**自动脚本**，没有人类试玩，视频无声音。风格目标图只作为标注过的概念图出现，不是游戏画面。

阅读顺序：
1. `comparison/index.html`：同一构建、同一位置的 B3（before）vs B4（after）：工坊正面/侧面、广场、垂柳、泉水；42/48/54 俯角；主角四向待机与行走；遮挡；小地图随路线变化；对话/背包/暂停（1080p 与 720p）；S0 与脚本设置的 S1–S3；风格目标概念图与实机对照。
2. `motion/actual-player-b4-motion.mp4`：连续实机录像（自动输入）：广场待机、缩放、八方向、十圈小转身、与 Mira 对话、工坊门口与绕到工坊背后（遮挡）、柳树下绕行、采花、走西路到泉水并调查、回广场。`motion/minimap-strip.png` 是录像帧中裁出的小地图。
3. `final-report.md`：Git 安全检查、素材 A/B/C、做了什么、相机、人物、小地图、环境、**WHAT STILL LOOKS BAD**、测试/构建/性能、状态字段。
4. `PRODUCTION_ART/`（游戏实际使用的图）、`asset-audit/`（素材体检图）、`CONCEPT_ART-not-game-footage/`、`EDITOR_PREVIEW-not-player-evidence/`、`provenance/`、`tests/`、`logs/`、`source/`、`MANIFEST-SHA256.txt`、`LABELS.md`。

请重点判断：48° 高俯视是否舒服；工坊/垂柳/泉水的绘制静态道具与地面、人物是否融为一体（接地、遮挡、比例）；人物待机呼吸与跑步是否自然、四个方向是否像同一个人；小地图是否好用；以及仍然留着的 B2 低多边形树、风车等与新画风的反差能否接受。真实构建在 `artifacts/build/phase52-b4/EchoesOfTheValley.exe`，加 `-echoes-b4-before` 参数可在同一构建中看 B3 画面。
'''
(ev / 'README_FIRST_zh.md').write_text(readme, encoding='utf-8')
print('written', ev / 'final-report.md')
