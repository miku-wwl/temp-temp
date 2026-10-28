# 10 — Phase 5.2-B4: high-angle static 2.5D (painted props, light caretaker animation, real minimap)

2026-10-29 NZDT. Local branch `art/phase52-b4-iso`, started from the B3 tip `c829e65aa55bb68faac9aa7f6b720550b4b69ebf` (B3 and B2 are
preserved untouched; `main` was not changed). **Gate B owner approval: PENDING. World expansion: NOT allowed. Human visual and gameplay
QA: PENDING.** This document records what was built and measured; it is not an approval.

## Decision implemented

The owner asked for a higher fixed camera and a denser, warmer, painted 2.5D world in the four existing places (plaza, alchemy
workshop, willow, spring), keeping the illustrated caretaker with only light, natural motion. Trees, flowers and houses may stay static.
Gameplay, collision, interactions, quests and saves must not change; no new areas (no Phase C/D).

## Asset pack and audit

`Echoes-B4-Original-Art-Assets-20261028.zip` (SHA-256 `738B55FD…8F30B`) was unpacked outside the repository and verified against its
`SHA256SUMS.txt` (28/28). The 22 new PNGs are kept unmodified in `game/Assets/Echoes/Art/Phase52B4/Source/` (re-hashed by an EditMode
test); the pack's README, QA list and prompt are in `tools/art/phase52b4/source-docs/`. `tools/art/phase52b4/audit_assets.py` showed every
image on white, black, grass and earth and tiled the materials 2x2: the character sheets carry a red-brown haze ring, the grass clump has
haze up to 169 px from the blades, the willow 11 % far haze, the minimap frame 313 red-fringe pixels, and the stone/plaster/roof/timber
tiles show seams when repeated. Classification (per-file use and every modification: `tools/art/phase52b4/provenance/asset-manifest.md`):

* **A, used (16):** four idle drawings, four run key-pose sheets, the painted workshop, willow and spring, three foliage sheets (grass,
  wildflowers, fern), the mossy stone tile, the minimap frame.
* **B, reference only (6):** the style-target concept (never shown as game footage), the broadleaf branch and willow frond sheets, and
  the plaster, roof and timber tiles (the workshop is now the painting, and those tiles would need seam repair first).
* **C, rejected:** none.

## Caretaker: the B4 drawings on the unchanged presenter (`tools/art/phase52b4/make_b4_hero.py`)

* Alpha cleanup (`charlib.py`): haze below 35 % opacity more than 2 px from the solid figure removed, islands under 60 px removed, edge
  colour taken from the nearest solid pixel (no red or grey fringe), premultiplied resampling.
* The run sheets hold four drawn poses of unequal width: they are split by closed-mask components with nearest-owner assignment.
* Scale: 300 px per metre, 1.65 m standing; a running figure 3 % shorter, and the run sheets (drawn with slightly different head-to-body
  proportions) scaled per direction so their heads match the standing drawing of the same direction (S 0.92, W 0.97, E 0.93, N 1.06,
  judged on head overlays); feet on one ground line, pivots at the soles.
* **Idle:** 12 frames at 5 fps, a breathing loop by 2D linear-blend skinning of the natural standing drawing: the chest rises 0.8 %, the
  head follows with a slight tilt, the figure sways a fraction of a degree about the feet, hair ends and coat tails lag; the arms stay
  down as drawn.
* **Run:** 8 frames per 2.8 m loop of four steps (the controller's 4.5 m/s plays about 13 frames/s): the drawn poses (S 1-2-3-2, W and E 0-1-2-3,
  N 1-1m-3-1m where 1m mirrors the lower legs of pose 1, because the back view was drawn leading with one leg only), each followed by a
  1.5 % squash in-between for weight. The presenter advances the cycle by distance, so feet do not skate, and stops back into idle.
* E has its own drawings (nothing flips at runtime). One 4096 x 4096 atlas; after the warps, partly transparent texels take the nearest
  solid colour again and colour is bled into transparent texels (no dark halo under filtering; an EditMode test checks every edge texel).
  The atlas JSON records the SHA-256 of every source drawing.

## Painted static props (`Echoes/StaticProp25D`, `StaticProp25D`)

The workshop, willow and spring paintings are cleaned and cropped (`make_b4_props.py`) and drawn as **camera-facing cards** around an
anchor on the ground, at a fixed scale in metres per painting pixel. What makes them sit in the 3D world:

* **Depth from the painting's ground line.** Each prop knows three ground points (left end, front corner, right end of where the painted
  building meets the ground, picked from the painting's lower outline and checked by eye) mapped to world positions. Every pixel of a
  column takes the depth of its ground point, pulled slightly towards the camera, so the player in front of a wall draws over it and the
  player behind it is hidden (with the B3 silhouette pass showing where she is).
* **Shadows** come from the old 3D bodies, now invisible and set to shadows-only (the workshop's walls and roof); the yard props and
  the old spring and willow meshes are hidden.
* **Collision:** the old house's box colliders stood up to 1.7 m in front of the painted facade, so in B4 they are switched off and a
  convex prism on the painted footprint (inset 0.35 m) blocks instead; the willow keeps its trunk collider and the spring its stones.
  All interactions are distance-based and stay where they were (an EditMode test finds free standing room inside every interaction's
  range; PlayMode plays the first quest through the workshop and the spring in the B4 scene).
* **Willow canopy:** a dithered opening follows the player when she walks under or behind the canopy (`_EchoesFocus`).
* **Spring stages:** `StageVariant25D` shows a dormant painting (only the water recoloured, by script) until `ResonanceView` shows
  AwakeningStream, then the painted spring; the real stream, bloom and lighting changes of every stage are unchanged.
* The chimney smoke rises from the painted chimney.

## Ground and plaza

* **Ground plants:** 900 painted clumps (grass, wildflowers, fern; one atlas, one batched mesh, alpha-tested with depth, never sorted)
  around the four places, kept off paths, gameplay spots and footprints; the B2 low-poly plants there are hidden.
* **Plaza:** a copy of the B2 terrain (heights untouched) gets a cobble layer made seamless from the pack's mossy stone tile (RGB only, so
  URP does not read alpha as smoothness); the Heart's primitive dais, pillars and blocks get the same painted fieldstone through a
  world-space triplanar shader (`Echoes/TriplanarStone`), so the low-poly ruins read as stone without UV stretching.

## Camera

`FixedFollowCamera25D` with the B4 view: **yaw 45 degrees (fixed: the paintings show the workshop's facades at 45 degrees), pitch 48 degrees
(default; 42 and 54 were rendered in the Editor and in the player), distance 14 m (wheel 10-17 m), vertical FOV 35 degrees**. The camera
stands 10.4 m above the follow pivot (11.3 m above the ground at the default zoom). Because the paintings face the camera, 42/48/54 change
the ground's foreshortening and how much of the valley is visible, not the props; 48 was kept as the owner's default. New options (B3
defaults unchanged): collision only against the terrain (a high camera passing over roofs and canopies never jumps in), and the ending's
automatic drift becomes a sway of at most 10 degrees (painted props have no backs).

## Minimap

`MinimapController` at the top right (232 px frame at 1080p, the pack's frame after fringe cleanup; Coins/Clover moved left of it):

* The map is **baked in the Editor** from the valley itself: an orthographic north-up render of the terrain, paths, water, trees and the
  3D buildings (the painted cards, clouds and fog are excluded), warmed slightly and vignetted. Bounds -56..56 m.
* At runtime only UI transforms change: the image is turned by the camera yaw (up on the map is up on the screen) and slides so the
  player stays in the middle; the arrow turns with her facing; landmark icons (workshop, spring, windmill, garden, Heart) ride on it.
* The objective marker follows `MinimapTargets.Current(session)`, a pure function written to match the objective panel (Mira, the spring,
  the Heart, herbs, blossoms, the workshop, Wren, Clover, Alder, ore); it clamps to the window edge with an arrow when far away.

## Before mode and isolation

`Phase52B4Pilot` lists everything B4 adds, hides, restyles or swaps (props, foliage, minimap, terrain data, hero sheet and material,
plaza materials, colliders, camera view, status panel). `-echoes-b4-before` (or `Apply(true)`) shows the B3 presentation in the same
scene for matched comparisons. The original, B2 and B3 scenes contain no B4 component. Pilot saves stay isolated as before.

## Evidence tools

* `Invoke-B4.ps1 -Task Generate|Preview|Build` (one Unity process at a time; logs outside the project).
* `Phase52B4Tour` (`-echoes-b4-tour`): automated route (no teleport): idle, zoom 10/17, eight directions, ten small circles (every facing
  ten times), Mira, the workshop door, round behind the workshop, under the willow, a Dawn Blossom, the west path to the spring
  (approached until the detector offers it) and back; then stills: pitch 42/48/54, four directions idle and walking, B3/B4 pairs,
  occlusion, dialogue, satchel, pause, and stages S1-S3 **set by the script** (labelled).
* `Run-B4Tour.ps1`, `Run-B4-Evidence.ps1`, `encode_b4_video.py`, `analyze_b4_tour.py` (frame times, direction coverage, run phase against
  distance, minimap objective timeline and scale), `minimap_strip.py`, `make_b4_report.py`, `make_b4_review.py`.

## Verification

Filled in from the evidence by the final report (`artifacts/visual-qa/phase-5.2/b4/final-report.md`, also in the review package).

## Known limitations (see the report's WHAT STILL LOOKS BAD)

* Outside the four places the valley is still B2: low-poly broadleaf trees (their leaf cards look like shards from 48 degrees), the
  windmill, the garden, the stream ribbon and the Heart's crystal cube.
* The paintings are drawn for a lower view than 48 degrees; as cards they cannot change perspective with the camera, so their roofs read
  slightly flatter than the ground around them.
* The run cycle has four drawn poses per direction (eight frames with in-betweens): readable, not fluid animation.
