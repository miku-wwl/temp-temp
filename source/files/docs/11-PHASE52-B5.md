# 11 — Phase 5.2-B5: painted environment (spring stages, painted trees, garden beds, meadow and soil)

2026-10-29 NZDT, on `main` (main-only workflow). **Gate B5 owner approval: PENDING. World expansion: NOT allowed. Human visual and
gameplay QA: PENDING.** This document records what was built and measured; it is not an approval. B4 stays the baseline it was (Gate B
PARTIAL, not approved); B5 is an isolated pilot scene built on top of it.

## Decision implemented

The owner supplied six AI-assisted environment paintings (`Echoes-B5-Environment-Art-Pack.zip`) and asked to replace what still looked
like B2 around the B4 caretaker, workshop and willow: the low-poly trees, the flat green ground, the missing farmland, the spring that
read as a sticker cut by the old stream, and canopies that hid the caretaker. The B4 camera (yaw 45, pitch 48), the caretaker drawings,
the real minimap, the world's extent and all gameplay stay as they were; no new area (no Phase C/D).

## Asset pack and audit

The pack (SHA-256 `F2D5A0EE…FEE9675`) was unpacked outside the repository and verified against its `SHA256SUMS.txt` (9/9). The six PNGs
are kept unmodified in `game/Assets/Echoes/Art/Phase52B5/Source/` (re-hashed by an EditMode test); the pack's README, QA notes and prompt
are in `tools/art/phase52b5/source-docs/`. Findings that shaped the work (`tools/art/phase52b5/audit_b5_assets.py`, per-file use:
`tools/art/phase52b5/provenance/asset-manifest.md`):

* `spring_S0_dormant.png` is **not dry** (two waterfalls, a full pool): the game's silent spring is a dry basin with a murky trickle.
* The two spring paintings share one design: after a SIFT similarity registration they agree to about a pixel (scale 0.9994, rotation
  -0.01 deg, median residual 0.64 px).
* The ground textures do **not** wrap (opposite edges 1.22-1.71 times more different than neighbouring texels).
* The farm painting's beds are drawn for a lower view, in a layout that is not the garden's, and fully grown.
* Every painted prop has yellow-red background glow at its edges (up to 20 px from the drawing).

## What B5 changes (all presentation; `Phase52B5Pilot` lists every change, `-echoes-b5-before` shows B4 in the same scene)

* **Spring:** one camera-facing card (`Echoes/StageProp25D`, the B4 ground-line depth) with four registered stage paintings in a
  Texture2DArray: stage 0 drained from the S0 painting by script (pool re-toned to a dry floor, falls clone-filled, a thin murky trickle,
  wilted plants, lanterns unlit); stages 1 and 2 with the S3 water and the S0 lanterns; stage 3 the S3 painting. `StagePainting25D`
  follows the sum of the valley's stage blends, so the painting changes over the same seconds as the light and a loaded save shows its
  stage at once. Collision is a prism on the painted footprint (inset 0.35 m); three old stones that stood outside the new rim stop
  blocking. A soft contact band and a ragged ring of damp soil meet the rim; the old fountain particles are hidden.
* **Stream:** the trickle and the wide stream keep their objects (ResonanceView still switches them); their material becomes painted
  water in the spring's colours (`Echoes/PaintedWater`: deeper middle, soft banks, slow streaks scrolled by the existing `WaterFlow`),
  fading in where it leaves the painted rim.
* **Trees:** 87 painted broadleaf cards (`Echoes/PaintedCard25D`) on the roots of the low-poly trees, sized to the old tree (smaller near
  the places the player works in), mirrored and tinted per tree; the old trees keep their real shadow (shadow-only) and their trunk
  colliders, widened to the painted 0.3 m trunk. The part of a card below its ground point follows the ground (no straight cut through
  the roots). Near trees open a soft-edged, dithered upright ellipse round the caretaker when she is behind them (her whole figure, never the
  whole tree). The four stage-3 saplings become small
  painted trees; the four stage-1 saplings round the spring (which would hide the painted spring) become painted flowers.
* **Garden:** 3D raised beds on the garden's real layout, with colliders (the B4 frames had none), painted timber and tilled-soil
  boxes; 16 plants cut from the farm painting stand on the beds, lush under the garden's own Bloom root and withered under its Dormant
  root, so stage 2 decides which set shows and the bloom pops up bed by bed. The scarecrow's pole now blocks. No bed is harvestable in this
  game (gathering is at the herb and blossom spots), so no painted crop can contradict a harvest.
* **Gathering spots:** the six herbs and five Dawn Blossoms keep their `ResourceNode`; their ready and cut visuals become painted plants,
  switched by the node's own timer.
* **Ground:** a copy of the B4 terrain (heights untouched) with six layers: the seam-repaired meadow in two gradings mixed by a slow noise,
  the B2 painted dirt, stone, the B4 cobble warmed, and the tilled soil (garden walkways, the spring's rim). 1,400 painted ground clumps
  and painted flowers where the low-poly flower patches were; the low-poly grass tufts, path stones and reeds near the spring are hidden;
  the 14 boulders become painted mossy stones on their own colliders.
* **Light:** the silent valley (before the Heart wakes) is lit grey-green instead of blue at the same muted level
  (`ValleyLighting.SetMutedBase`); the awakened and restored looks are unchanged.
* **Plaza and details:** one warm-grey stone (B4's cobble graded by luminance) for the plaza's cobble layer and its dais, pillars and
  blocks; the Heart's crystal cube becomes a cut crystal on the same renderer; the hen yard's fence and the lantern posts take the
  garden's timber.
* **Minimap:** re-baked from the B5 valley with the same bounds, so its scale, markers and live behaviour are unchanged.

## Not changed

Gameplay, quests, interactions and their ranges, saves, NavMesh-free movement, the caretaker drawings and their animation, the camera,
the workshop and willow paintings, the B4 ground plants, the UI. The original, B2, B3 and B4 scenes contain no B5 component.

## Verification

**Engineering: PASS (automated). Visual gate: FAIL on stop rule 3 (workshop occlusion). Overall B5: PARTIAL; owner approval PENDING.**

The game inputs remain exactly commit `e4e4e0fa2e6dd9cece55af9f3cf2d1206cad8e75`; finalization changes only documentation and audit/evidence
tools. All 12 outstanding handoff files were copied outside the repository with verified hashes before editing, to
`D:\workshop\oct\echoes-of-the-valley-finalization-backup-20261029-2154`. Earlier builds, reports and recordings were retained.

| Check | Observed result / local evidence |
| --- | --- |
| Fresh EditMode / PlayMode | **262/262 / 106/106 PASS**, zero failed/skipped/inconclusive, both Unity exits 0. `artifacts/visual-qa/phase-5.2/logs/b5-finalization-verified/`; `source-inputs-SHA256.json` records all 1,741 input files before tests regenerate assets in the disposable copy. |
| Baseline tests | All 64 files under the B4 baseline's test folders are unchanged; B5 adds 18 EditMode and 6 PlayMode cases. No tests removed or weakened. |
| Existing final Windows build | **Succeeded**, ID `8dcf20bf30374cc1b676ad6c2df2fd78`, 0 errors / 0 build warnings, non-development Windows x64 Mono/D3D11, version `0.5.2-pilot.5`. `artifacts/build/phase52-b5-final`; `artifacts/visual-qa/phase-5.2/b5/logs/build-final-e4e4e0f/Build.log`. The original-project build ran after the source commit; current Unity inputs still match that commit. |
| Full-game standalone smoke | **1080p PASS**, exit 0, 40 screenshots, no error/exception lines: New Game, NPCs, crafting, hen, farming/gathering, S0-S3, ending, save and Continue. `b5/player-final/smoke1080/`. A separate 720p full-game run was **NOT RUN**; the 1080p smoke's brief 720p performance sample does not count as one. |
| Real Player movement / stills | Automated-input B5 route **PASS**, 242.3 m / 81.93 s unrecorded; 85 real 1080p stills. Existing motion MP4 **83.32 s, 1920x1080, 1,055 encoded frames, 12.66 average encoded FPS (VFR)**, 1,054 captured source frames. No audio. S1-S3 comparison stills explicitly say `set-by-script`. |
| Video limits | Frame hashes and full decode are checked by `verify_b5_delivery.py`. **Actual GUI playback NOT VERIFIED**: the owner stopped Computer Use with physical Escape before the B5 file was opened, and all UI actions stopped. This is not human playtesting or audio approval. |
| Performance comparison | **PARTIAL**: B5 60.0 mean FPS, P95 16.69 / P99 16.84 ms. B4 before mode observed 59.6 FPS, P95 16.69 / P99 16.86 ms, but failed its stream-bank route (exit 1, inherited collider), so full-route parity is **UNKNOWN**. Same build, 1080p, 60 FPS cap, Radeon 780M; one unrecorded run each. Shared GPU memory averaged 442.8 vs 355.7 MB. Video FPS is capture throughput, not game FPS. |
| Severe occlusion | **FAIL**: at default and closest zoom the caretaker's body is 0% visible behind the workshop, with only the silhouette marker. Inherited from B4; not fixed or waived here. Painted-tree opening yields 69-85% visible. `b5/sheets/worst-occlusion-b5.jpg` and `player-final/tour1080-unrecorded/tour.json`. |

The seven stop rules are **1 UNKNOWN (camera comfort), 2 PASS (genuine minimap), 3 FAIL (workshop occlusion), 4 UNKNOWN (visual foot
sliding), 5 UNKNOWN (style judgment), 6 PASS (automated gameplay/save regression), 7 UNKNOWN (complete performance comparison)**.
No owner playtest was performed. Successful tests do not clear the visual gate.

Known weaknesses: B2 windmill, primitive plaza ruins, repeated tree/boulder silhouettes, dithered canopy opening, low-poly hen yard,
upright garden cut-outs and simplified stream banks. They are documented, with no new visual development or B6 work.

The complete report is `artifacts/visual-qa/phase-5.2/b5/final-report.md`. `finalization-audit.json` records current checks;
`delivery-identifiers.json` records build source, final commit and live remote verification. The review ZIP and three independently
extractable parts are in `artifacts/review/phase52-b5/`, with SHA-256 sidecars, all-member hash verification and checked comparison-page
links. Art provenance and AI disclosure remain in `tools/art/phase52b5/provenance/` and `docs/03-RULES-CREDITS.md`.
