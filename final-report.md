# Echoes of the Valley: Phase 5.2-B5 (painted environment) review report

2026-10-29 NZDT, branch `main` (main-only workflow). **Engineering PASS (automated); visual stop rule 3 FAIL; overall PARTIAL. Gate B5 owner approval, human visual approval and human gameplay QA are PENDING; world expansion is NOT allowed.** Gameplay screenshots and the video are real Windows-player framebuffer captures driven by **automated** input (no human played); source art and Editor diagnostics are separately labelled and are not gameplay evidence. S1-S3 comparison stills are reached by the evidence script and say so in their names. "B4" in a comparison is the B4 presentation switched on in the same B5 build at the same place (`-echoes-b5-before`).

## 1. Git and safety

- Branch `main`, HEAD `3d5ae5dfbeff10bddc9e6ccce8e7e6dc4f138292`, `origin/main` `3d5ae5dfbeff10bddc9e6ccce8e7e6dc4f138292` (in sync: YES). B5 started from `f712fd7` (B4 on main). Local branches: `main` (no branch was created). No force push, no history rewrite, no reset, no stash.
- Gameplay/build source commit: `e4e4e0fa2e6dd9cece55af9f3cf2d1206cad8e75`. HEAD above includes finalization tooling/documentation only after that source commit. The exact pre-test inputs and finalization checks are in `tests/source-inputs-SHA256.json` and `logs/finalization-audit.json` in the review package. The original test snapshot differed in nine generated assets after its tests; it was preserved, and a fresh full test run was required to record the exact inputs.
- Commits for B5:
```
3d5ae5d docs: finalize B5 evidence with exact-input tests and honest visual gates
e4e4e0f fix: B5 canopy opening covers the whole caretaker; one warm-grey plaza stone
94d8c1a art: Phase 5.2-B5 painted environment pilot from the owner's B5 pack
132fd1f fix: store the pilot terrain data byte-exact in git
```
- RC1 release zip unchanged: YES. The original, B2, B3 and B4 scenes contain no B5 component (EditMode test `OriginalB2B3AndB4Scenes_ContainNoB5Presentation`). B4 is recoverable by `git checkout f712fd7`, and inside the B5 build by `-echoes-b5-before`.

## 2. Baseline audit: what B4 showed in the real player (before mode stills of this build, the same places as B5)

- **Old low-poly trees and the windmill's surroundings:** every tree outside the four B4 places was still a B2 low-poly tree whose leaf cards read as dark shards from 48 degrees (`before-b4-stream`, `before-b4-hen-yard`, `before-b4-windmill`, the left edge of `before-b4-spring`); the windmill approach had low-poly bushes and flower cones (`before-b4-windmill`).
- **Large monotone grass:** outside the B4 foliage rings the ground was the flat B1 green with a faint blotch pattern (`before-b4-garden`, `before-b4-hen-yard`, the right half of `before-b4-workshop-front`); seen from 48 degrees it is a large, even teal-green field.
- **No real farmland:** the garden was four flat grey soil boxes with stick stalks inside a fence (`before-b4-garden`); the beds had no collision (the player walked through them).
- **The spring as an isolated sticker cut by the old stream:** the painted B4 spring stood on the ground, but the B2 stream ribbon (a flat grey-blue slab, 2.9 m wide when restored) ran out of the painted rim with a straight edge, and B1 reeds, cattails and blue crystal plants stood round it (`before-b4-spring`, `before-b4-stream`). The B4 spring also has an invisible blocker: B1's fallen lintel at (-20.6, 12.2) under the front edge of the painting stopped the B4-presentation route for 9 s on the way down the stream bank (`tour1080-before-unrecorded`); B5 switches off the three old stones outside its painted rim.
- **Canopies hiding the caretaker:** from S1 on, a low-poly bloom tree beside the spring path hid her when she stood under it (`stage-S1-set-by-script-before-b4-spring`); the B2 trees have no canopy opening at all (`occlusion-behind-broadleaf-*-before-b4`).
- Terrain layers and anchors (from the Editor survey, `survey-b4/survey.txt`): Grass, Meadow, Path and Stone (B1, 7 m tiles) under the B2 RouteMeadow / RouteDirt and the B4 PlazaCobble; interaction anchors: Mira (14.2, 5.4), workshop (25, 8) door +1.5 m, spring (-21, 16) range 3.8 m, Heart (0, 0), garden (16, -14) with Wren at its west gate, hen yard (22, -5), windmill (6, 32) with Alder; 6 herbs, 5 blossoms, 4 ore. B5 risk to quests, saves and navigation: none by design (no gameplay object moves, the terrain heights are copied, movement has no NavMesh, saves store no presentation); the B5 colliders (beds, scarecrow pole, spring footprint, wider trunks) are checked by the EditMode reachability test and the recorded route.

## 3. Asset pack (A / B / C)

- `D:\Download\Echoes-B5-Environment-Art-Pack.zip`, SHA-256 `F2D5A0EE6508CBA61301A6189664C82571B8D79B73CAB57BEED868695FEE9675`, unpacked into an empty folder outside the repository; `SHA256SUMS.txt` verified 9/9. The six PNGs are kept unmodified in `game/Assets/Echoes/Art/Phase52B5/Source/` (an EditMode test re-hashes them); the pack's documents are in `tools/art/phase52b5/source-docs/`.
- **A, used (6 of 6):** both spring paintings (four registered stages), the meadow grass and the tilled soil (made seamless, graded), the farm painting (16 plants and one boulder cut out; a withered recolour of every plant), the broadleaf tree (87 painted trees). **B, reference only:** the farm painting's own bed frames and planks (drawn for a lower view and another layout: the beds are 3D on the real garden). **C, rejected:** none. Per-file use and every modification: `provenance/asset-manifest.md`.
- Audit findings that shaped the work: the S0 painting still has two waterfalls and a full pool (so stage 0 is drained by script); the two spring paintings register to under a pixel; the ground textures' wrap edges were 1.22-1.71 times as different as neighbouring texels; every prop had yellow-red background glow at its edge (up to 20 px).

Spring registration (SIFT similarity, S0 onto S3): 1181 matches, 1105 inliers, scale 0.9994, rotation -0.010 deg, shift -0.50, -0.42 px, residual median 0.64 px, p95 1.65 px.

Ground texture seams (opposite edges against neighbouring texels; 1.0 = as continuous as the inside):

| Texture | Source X / Y | Final X / Y |
| --- | --- | --- |
| MeadowA | 1.224 / 1.343 | 1.04 / 0.965 |
| MeadowB | 1.224 / 1.343 | 0.968 / 1.037 |
| TilledSoil | 1.705 / 1.414 | 0.914 / 1.001 |

## 4. What was built

### P0-A Spring
- One card, one footprint, one interaction area for all stages: the four paintings are slices of one Texture2DArray on `Echoes/StageProp25D` (the B4 ground-line depth: in front of the rim the caretaker draws over it, behind the arch she is hidden). `StagePainting25D` shows the sum of the valley's stage blends, so the change runs with the game's own transition (a PlayMode test sees an in-between value) and a loaded save shows its stage at once. Nothing is set for screenshots at runtime.
- Stage 0 (Silence): the S0 painting drained by script (pool floor re-toned to dry stone with a damp middle and one murky puddle, floating leaves brown, the two falls clone-filled from the rock beside them, a thin dark trickle down the upper fall, plants a little wilted, lanterns unlit). Stage 1 (Awakening): the S3 water, falls and crystals, the rest half way between the paintings, lanterns still unlit. Stage 2: 85 % S3, lanterns unlit. Stage 3 (Rest): the S3 painting, lanterns lit, saturation x 0.94.
- Grounding: collision is a prism on the painted footprint (three old stones outside the new rim stop blocking); a soft contact band and a ragged ring of damp soil meet the rim; the old fountain particles (which sprayed from the middle of the painted pool) are hidden.
- The stream: the trickle (S0) and the wide stream (S1+) keep their objects and ResonanceView's switch; their material is painted water in the spring's colours (deeper middle, soft banks, slow streaks scrolled by the existing WaterFlow), fading in where it leaves the rim. The B1 reeds, cattails and the blue crystal plants near the spring are hidden; the stage-1 bloom saplings that stood in front of the painted spring became painted flower clumps.

### P0-B Ground and farmland
- Seams: a minimum-error cut through a wrap-around overlap band (no cross-fade, no resampling) makes both textures tileable at 1024 x 1024: wrap ratio about 1.0 in both directions (table above; an EditMode test measures it). Import: RGB only (no smoothness from alpha), DXT1, mipmaps, trilinear, 8x anisotropic, repeat.
- Terrain: a copy of the B4 terrain (heights untouched, collision the same), six layers instead of nine: two gradings of the meadow mixed by a slow noise (lush and drier patches), the B2 painted dirt for paths, stone on the slopes, the B4 cobble warmed for the plaza, tilled soil in the garden walkways and the spring's damp ring. 5.5 / 6.3 m tiles: no shimmer at the farthest zoom in the real player (`ground-tiling-farthest-zoom`).
- Ground plants: 1,400 painted clumps (the B4 foliage atlas, three sizes, tinted drier in the dry patches) where B4 had none, kept off paths, gameplay spots, interaction points, trunks, beds and water; painted flowers where the B1 flower patches were; the 520 B1 grass tufts, the path stones and the 14 low-poly boulders are replaced (the boulders by a painted mossy stone on their own colliders).
- Farmland: four 3D raised beds on the garden's real layout (warm painted timber, corner posts with rope), now solid (box colliders; a PlayMode test walks into one); soil boxes in the tilled-soil texture (dry while dormant, rich in bloom). 16 plants cut from the farm painting stand on the beds: the lush set under the garden's Bloom root, a withered recolour under its Dormant root, so the garden's own stage (2) decides which shows and the bloom pops up bed by bed. The beds are not harvestable in this game (gathering is at the herb and blossom spots), so no painted crop can contradict a harvest; the gathering spots keep their ResourceNode and switch between a painted plant and a cut stump with the node's own timer (a PlayMode test gathers one and waits for it to grow back).

### P0-C Trees, occlusion and the caretaker
- 87 painted broadleaf cards on the roots of the B2 low-poly trees (24 on the distant ridge), sized to the old tree and smaller near the places the player works in, mirrored and tinted per tree; one draw call. The old trees stay as shadow casters (shadow-only) and their trunk colliders stay, widened to the painted 0.3 m trunk. The part of a card below its ground point follows the ground, so the painted roots are not cut along a straight line.
- Occlusion: a near tree opens an ordered-dither window round the caretaker when she is behind it (soft edge, never the whole tree); the B4 willow keeps its own opening and the workshop its silhouette pass. The stage-1 bloom saplings by the spring path (B4's worst occluder) are flower clumps in B5; the stage-3 saplings are small painted trees. Visible share of the caretaker is measured in the real player (section 5).
- The caretaker is B4's, unchanged: drawings, 12-frame idle, 8-frame run advanced by distance at the unchanged 4.5 m/s (run phase against distance in section 6), no new frames.

### P1 Overall look, minimap and UI
- Colour: the silent valley is lit grey-green instead of blue at the same muted level (S1-S3 unchanged), the meadow is a warmer, painted yellow-green with drier patches, the plaza stone and the garden and hen-yard timber are warm; the Heart's crystal cube is a cut crystal.
- Bounded to the existing places and paths; nothing added to the world's extent, no quest or interaction changed.
- Minimap: re-baked from the B5 valley with the B4 bounds, so it still shows the real terrain, paths, water, beds and buildings, and the live player arrow, landmarks and objective marker are B4's code (PlayMode test and the route's objective timeline). UI overlap checked in the dialogue, satchel and pause stills at 1080p.

## 5. Occlusion, measured in the real player

Share of the caretaker the player sees, measured on the real framebuffer (consecutive frames with and without her card, against the same with every other renderer off; the hidden-silhouette marker switched off). "Visible or marked" counts the silhouette marker too (lower threshold). For B5 the count with the painted trees' opening switched off shows what the opening contributes.

| Still | Presentation | Zoom | Visible | Visible without the canopy opening | Visible or marked |
| --- | --- | --- | --- | --- | --- |
| occlusion-behind-workshop | B5 | 14 m | 0 % | 0 % | 100 % |
| occlusion-behind-workshop-closest-zoom | B5 | 10 m | 0 % | 0 % | 100 % |
| occlusion-behind-workshop-before-b4 | B4 | 14 m | 0 % | - | 92 % |
| occlusion-behind-workshop-before-b4-closest-zoom | B4 | 10 m | 0 % | - | 92 % |
| occlusion-behind-willow | B5 | 14 m | 83 % | 83 % | 85 % |
| occlusion-behind-willow-closest-zoom | B5 | 10 m | 84 % | 84 % | 85 % |
| occlusion-behind-willow-before-b4 | B4 | 14 m | 81 % | - | 85 % |
| occlusion-behind-willow-before-b4-closest-zoom | B4 | 10 m | 81 % | - | 85 % |
| occlusion-behind-broadleaf-henyard | B5 | 14 m | 69 % | 0 % | 89 % |
| occlusion-behind-broadleaf-henyard-closest-zoom | B5 | 10 m | 73 % | 0 % | 89 % |
| occlusion-behind-broadleaf-henyard-before-b4 | B4 | 14 m | 23 % | - | 97 % |
| occlusion-behind-broadleaf-henyard-before-b4-closest-zoom | B4 | 10 m | 14 % | - | 99 % |
| occlusion-behind-broadleaf-stream | B5 | 14 m | 85 % | 0 % | 99 % |
| occlusion-behind-broadleaf-stream-closest-zoom | B5 | 10 m | 84 % | 0 % | 99 % |
| occlusion-behind-broadleaf-stream-before-b4 | B4 | 14 m | 64 % | - | 99 % |
| occlusion-behind-broadleaf-stream-before-b4-closest-zoom | B4 | 10 m | 72 % | - | 99 % |

Worst B5 case: `occlusion-behind-workshop` (0 % visible). The faint hidden-silhouette pass is not counted as visible.

## 6. Caretaker and minimap

Direction samples in the recorded route: E/idle 47, E/run 194, N/idle 84, N/run 90, S/idle 99, S/run 164, W/idle 116, W/run 260. Run cycle against distance (unrecorded 1080p): median per-frame difference 0.0, p95 0.0028 of a cycle (stride 2.8 m; driven by distance). This checks animation phase, not visual foot contact; sliding and comfort still need owner review. The caretaker's drawings, speed (4.5 m/s) and stride are B4's, unchanged.

Minimap during the recorded route (objective over time): 0.0 s Mira (idle), 26.8 s Spring (interact), 73.7 s Heart (interact). Map scale while walking (unrecorded 1080p): p05 3.568, median 3.684, p95 3.792 UI units per metre over 3336 steps (constant: the map follows the world).

## 7. Stop rules (any FAIL means B5 cannot pass)

| # | Rule | Result | Evidence |
| --- | --- | --- | --- |
| 1 | Real control less readable than B4 (owner's play decides) | UNKNOWN | player-final/tour1080-unrecorded/tour.json: 242.3 m route PASS; pitch 42/48/54 stills are present. Owner camera/control usability NOT VERIFIED. |
| 2 | Minimap fake or out of sync | PASS | player-final/tour1080-video/tour-stats.json (objective timeline) and PlayMode XML Minimap_ShowsTheB5Bake_AndStillFollowsThePlayerAndTheObjective; genuine bake and live map tracking PASS. |
| 3 | Caretaker badly hidden by canopies or buildings, or grounding not believable | FAIL | player-final/tour1080-unrecorded/tour.json and sheets/worst-occlusion-b5.jpg: behind workshop at zoom 14 and 10 the body is 0% visible, marked share 100%; severe body occlusion remains, inherited from B4. Painted-tree opening improves visibility to 69-85%, but does not clear this stop rule. |
| 4 | Animation sliding or inconsistent directions | UNKNOWN | player-final/tour1080-unrecorded/tour-stats.json: phase/distance median error 0, p95 0.0028 cycles, stride 2.8 m. This verifies the phase driver only; visual foot sliding and animation consistency need owner review. |
| 5 | B5 vegetation / spring / farm clash more with the caretaker and the B4 house | UNKNOWN | sheets/b4-vs-b5-*.jpg and motion/actual-player-b5-motion-AUTOMATED_INPUT.mp4: painted surroundings with retained low-poly windmill/ruins/hen yard. Art consistency is not owner-approved. |
| 6 | Alchemy, chicken, windmill, save/continue or stage regressions | PASS | tests/EditMode.xml and tests/PlayMode.xml; player-final/smoke1080/process-result.json and player.log: 1080p full-game smoke PASS, three restorations, ending, save and Continue; 0 error/exception lines. No baseline tests changed. |
| 7 | Clearly worse performance or serious VRAM / loading problems on the same 780M, 1080p, 60 FPS cap, same build and route | UNKNOWN | player-final/tour1080-unrecorded/tour-stats.json: B5 mean 60.0 FPS, P95 16.69 ms, P99 16.84 ms. B4 before-mode mean 59.6, P95 16.69, P99 16.86, but that route FAILED at the stream collider (exit 1). Same build, 1080p, 60 FPS cap; whole-route equivalence and performance parity UNKNOWN. |

## 8. Tests (read from the XML)

| Run | Result |
| --- | --- |
| EditMode, isolated project copy `artifacts/visual-qa/phase-5.2/logs/b5-finalization-verified` | Passed 262/262 (failed 0, skipped 0) |
| PlayMode, same copy | Passed 106/106 (failed 0, skipped 0) |

B4 baseline: EditMode 244, PlayMode 100; no existing test was removed or weakened. B5 tests: B5Scene_KeepsTheB4CaretakerCameraAndPlayerRoot: Passed, B5Sources_AreTheUnmodifiedOwnerSuppliedFiles: Passed, B5Terrain_KeepsTheB4Heights_AndUsesThePaintedLayers: Passed, BeforeMode_RestoresTheB4Presentation_InTheSameScene: Passed, EveryInteraction_StaysReachable_WithTheB5Collision: Passed, GardenBeds_StandOnTheRealGardenLayout_WithCollision_AndThePlantsFollowTheGardensOwnStage: Passed, GatheringSpots_KeepTheirNodes_WithPaintedReadyAndCutVisuals: Passed, GroundTextures_ImportAsOpaqueRepeatingMipmappedDxt1_WithAnisotropy: Passed, GroundTextures_WrapWithoutSeams_InBothDirections: Passed, Minimap_IsTheB5ValleyBaked_AndBeforeModeShowsTheB4Bake: Passed, OriginalB2B3AndB4Scenes_ContainNoB5Presentation: Passed, PaintedCards_AreAlphaTestedWithDepth_AndTheirTexturesKeepCoverageInTheMipmaps: Passed, PaintedTextures_TransparentTexelsCarryBledColour_NoDarkOrWhiteHalo: Passed, PaintedTrees_StandOnTheOldTrees_WhoseTrunksStillBlock_AndWhoseShadowsStay: Passed, SilentValleyLight_IsLessBlue_InB5_AndTheB4LightIsKeptForBeforeMode: Passed, SpringCard_ShowsTheShownStage_OnTheSpringsLandmark: Passed, SpringFootprint_IsSolid_AndOnlyOldStonesOutsideThePaintingStopBlocking: Passed, SpringStages_AreFourRegisteredPaintingsOnOneFootprint: Passed, B5Scene_RestoresTheStream_TheSpringPaintingFollowsTheStage_AndContinueKeepsIt: Passed, GatheringAHerb_SwapsThePaintedPlantForItsCutStump_AndTheTimerBringsItBack: Passed, Minimap_ShowsTheB5Bake_AndStillFollowsThePlayerAndTheObjective: Passed, PlayerBehindAPaintedTree_StaysVisibleThroughTheCanopysOpening: Passed, RaisedBeds_StopThePlayer_InB5_AndNotInTheB4BeforeMode: Passed, SilentValleyLight_IsTheB5One_AndBeforeModeRestoresTheB4Light: Passed.

## 9. Windows builds

- B5: non-development Windows x64 Mono/D3D11, Unity 6000.3.27f1 / URP 17.3.0, version `0.5.2-pilot.5`, `artifacts/build/phase52-b5-final`: **Succeeded**, build id `8dcf20bf30374cc1b676ad6c2df2fd78`, errors 0, warnings 0, 302863838 bytes; `EchoesOfTheValley.exe` SHA-256 `F083B0E879A9298CDCF9933A8F3B4F2B013E13F7D3E51221FC0D756B79E28807` (the Unity launcher stub; every build file is hashed in the package's `logs/build-manifest-SHA256.txt`).
- B4 (rebuilt on 2026-10-29 from `132fd1f`, the B4 content of `f712fd7`, after the earlier B4 build folder disappeared): Succeeded, build id `d8511a8a8a31430fa61a06c378e8bf23`, errors 0, warnings 0, `artifacts/build/phase52-b4`.

## 10. Real-player runs (automated)

- Recorded route 1080p (video): exit code 0 (natural exit True), exception/error lines 0, 8.4 s to the ready flag; route 243.3 m in 83.18 s, route pass True, failures none; 1920x1080, 1049 frames, P50 76.94 / P95 93.00 / P99 129.22 ms, worst 164.2 ms, mean 12.6 FPS, 1% low 7.7 FPS; GPU memory in use (shared, integrated GPU) mean 422.5 MB, max 441.4 MB (recorded run: framebuffer capture overhead included, not a rendering benchmark)
- Unrecorded route 1080p, B5: exit code 0 (natural exit True), exception/error lines 0, 6.6 s to the ready flag; route 242.3 m in 81.93 s, route pass True, failures none; 1920x1080, 4916 frames, P50 16.67 / P95 16.69 / P99 16.84 ms, worst 21.1 ms, mean 60.0 FPS, 1% low 59.4 FPS; GPU memory in use (shared, integrated GPU) mean 442.8 MB, max 462.9 MB (unrecorded run: no framebuffer capture; one scripted route, not a human traversal benchmark)
- Unrecorded route 1080p, B4 presentation (before mode, same build): exit code 1 (natural exit True), exception/error lines 0, 8.5 s to the ready flag; route 243.1 m in 92.08 s, route pass False, failures ['walk down the stream bank under the trees: did not reach (-21.40, 11.20) (stuck at (-20.1, 0.0, 12.6))']; 1920x1080, 5489 frames, P50 16.67 / P95 16.69 / P99 16.86 ms, worst 225.6 ms, mean 59.6 FPS, 1% low 59.3 FPS; GPU memory in use (shared, integrated GPU) mean 355.7 MB, max 371.9 MB (unrecorded run: no framebuffer capture; one scripted route, not a human traversal benchmark)
- Whole-game smoke 1080p: natural exit True, exit code 0, 80.9 s, 40 screenshots, exception lines 0, [Echoes][SMOKE] PASS: all steps passed in 76 s, 40 screenshots

Video: 83.32 s, 1054 captured framebuffer frames (about 12.66 captured FPS), 1055 encoded frames, encoded average rate 26375/2084 FPS (variable frame rate), 1920x1080, no audio, no desktop capture, no concept compositing; SHA-256 `08cb8c141f3a17d420f35afbe0114ac369a4db6b3c02d929ab321be34b04865e`. Full decode: PASS. Actual GUI playback: NOT VERIFIED (owner pressed Escape to stop Computer Use before this file was opened).. The video frame rate is the framebuffer capture rate (each frame is read back and JPEG-encoded during the run), not the game's frame rate: the game's frame times are measured in the unrecorded runs.

## 11. Performance (Radeon 780M, D3D11, windowed 1920 x 1080, 60 FPS cap, vSync off; same build, attempted same route)

| Measure | B4 presentation (before mode) | B5 |
| --- | --- | --- |
| Runs (1080p, unrecorded, attempted same route) | 1 | 1 |
| Mean FPS | 59.6 | 60.0 |
| 1 % low FPS | 59.3 | 59.4 |
| P95 frame time (ms) | 16.69 | 16.69 |
| P99 frame time (ms) | 16.86 | 16.84 |
| Worst frame (ms) | 225.6 | 21.1 |
| GPU 3D engine utilisation, mean (%) | 43.1 | 39.6 |
| GPU memory in use (shared), mean (MB) | 355.7 | 442.8 |
| GPU memory in use (shared), max (MB) | 371.9 | 462.9 |
| Process private memory, max (MB) | 941.4 | 1024.9 |
| Launch to tour ready, incl. menu and scene load (s) | 8.46 | 6.61 |

PARTIAL COMPARISON. One unrecorded run per presentation in the same build, Radeon 780M, 1920x1080, 60 FPS cap, vSync off. B5 completed 242.3 m in 81.93 s; B4 attempted the same route but got stuck at its old spring-bank collider, exited 1, and lasted 92.08 s. Full-route timing and scene exposure differ: the table preserves observations, not proof of performance parity. B5 averaged 60.0 FPS with P95 16.69/P99 16.84 ms. Shared GPU memory averaged 442.8 MB vs 355.7 MB (+87.1 MB); these are process counters on an integrated GPU, not a full VRAM profile. Recorded route capture/JPEG readback reduced measured throughput to 12.6 FPS; the video averages 12.66 encoded FPS. No recapture or performance retest was needed for finalization; repeatability and uncapped headroom remain NOT VERIFIED.

## 12. WHAT STILL LOOKS BAD

- **The windmill is still the B2 low-poly tower** (white drum, plank sails, crates and barrels round it): the pack has no windmill painting, and next to the painted trees it is now the largest style gap (`after-b5-windmill`, `stage-S3-set-by-script-windmill`).
- **The plaza ruins are primitives with a stone texture** (dais discs, cylinder pillars, box obelisk); B5 only re-grades the stone to one warm grey. From the high camera they read as textured shapes, not carved ruins (`after-b5-plaza`).
- **One tree painting, 87 times:** mirrored and tinted per tree, but where three or four stand together the repeated silhouette and root knot are visible (`after-b5-stream`). The trees are cards; their shadows are the old low-poly trees' shadows, which do not match the painted crowns exactly.
- **The canopy opening is an ordered dither:** behind a painted tree the caretaker shows through a regular dot screen inside a soft ellipse, readable but visibly technical at 1080p (`occlusion-behind-broadleaf-henyard`). Behind the workshop she is still only B4's faint silhouette marker (measured as hidden in section 5).
- **Still low-poly:** the hen yard and the hen, the stage-3 lantern posts (white sphere lanterns), the villagers' 3D props, the Heart's crystal (now a cut crystal, still flat-shaded), the windmill's crates and barrels.
- **Garden plants are cut-outs from a painting drawn for a lower view:** on the 3D beds they stand up like small cards rather than lying in the soil; the withered set is a recolour, not drawn dead plants (`after-b5-garden`).
- **The stream is a painted ribbon, not painted banks:** the clear water (S1+) has soft edges and moving streaks but no drawn stones or bank lip; the S0 trickle reads as a dark damp line.
- **The drained S0 spring is a script edit of the S0 painting:** at the closest zoom the clone-filled falls and the drawn trickle are plainer than the hand-painted stones.
- **One boulder painting, 14 times,** cut on a soft ellipse: on open meadow at the farthest zoom it reads as a round sticker (`ground-tiling-farthest-zoom`).
- **The run animation is B4's** (four drawn poses per direction, eight frames): readable, not fluid; B5 did not change the caretaker.
- **The recorded video runs at the framebuffer capture rate** (about 12.7 frames/s at 1080p, each frame read back and JPEG-encoded), so motion in the video looks choppier than in the game, which ran at the 60 FPS cap in the unrecorded runs.
- **The silent valley (S0) is muted on purpose** (grey-green, hazy): the B5 colours come into their own from S1 on.

## 13. Not verified

- Any human play: all input is automated; readability, comfort and the feel of the canopy opening need the owner's play (stop rule 1).
- Sound: not listened to (B5 changes no audio).
- Other GPUs, resolutions other than 1920 x 1080, fullscreen mode, frame rates above the 60 FPS cap (the cap was kept, so headroom is not measured).
- Repeatability of the performance comparison: one unrecorded B5 run and one B4 run (the finalisation set), not a series.
- Long sessions (memory over hours) and a release package of B5 (B5 is a pilot build, not a release candidate).
- Separate 720p whole-game smoke: NOT RUN on the final build. The 1080p smoke briefly changes to 720p for a performance sample; that is not a full 720p playthrough.
- Actual GUI playback: NOT VERIFIED. The owner pressed physical Escape to stop Computer Use before the B5 video was opened; all further UI actions stopped. Full video decode and frame hashes are verified separately.

## 14. Deferred visual observations (no further phase implemented or authorized here)

- Owner art for the remaining B2 pieces, in the same style and for the 48-degree view: the windmill (broken and mended, as two registered stage paintings like the spring), the Heart's ruins (dais, obelisk, crystal), the hen yard with the hen, and lantern posts.
- Two or three more tree paintings (a smaller tree, a blossom tree, a bush) and two more boulders, so groups of trees and stones stop repeating.
- A painted stream bank (stones and reeds drawn along both banks) so the stream joins the spring's style.
- Garden plants drawn from above at 48 degrees (or a bed painting in the garden's real four-bed layout) with a drawn withered set.
- A smoother canopy opening (a soft alpha fade with a depth pre-pass, or an outline of the caretaker) instead of the dither screen, and the same for the workshop.
- Keep B5 only if the owner's play confirms readability; otherwise the B4 presentation is one switch away (`-echoes-b5-before`) or one revert away (commit `f712fd7`).

## 15. Status

```text
PHASE52_B5_STATUS=PARTIAL
SOURCE_BRANCH=main
SOURCE_COMMIT=e4e4e0fa2e6dd9cece55af9f3cf2d1206cad8e75
FINAL_COMMIT=3d5ae5dfbeff10bddc9e6ccce8e7e6dc4f138292
ORIGIN_MAIN=3d5ae5dfbeff10bddc9e6ccce8e7e6dc4f138292
B5_ASSET_ZIP_VERIFICATION=PASS (SHA-256 F2D5A0EE...FEE9675; SHA256SUMS 9/9)
B5_ASSETS_USED=6/6 (spring S0, spring S3, meadow grass, tilled soil, farm beds (plants and a stone; bed frames reference only), broadleaf tree)
B5_ASSETS_REJECTED=none
SPRING_STAGES_STATUS=IMPLEMENTED (4 registered stage paintings on one footprint, S0 drained by script, follows the real stage; PlayMode-tested through the first quest, save and Continue); owner visual approval PENDING
GROUND_SEAMLESS_STATUS=PASS for seams (wrap ratio 0.91-1.04 after repair, EditMode-tested; 6 terrain layers on the B4 heights); look PENDING owner
FARMLAND_STATUS=IMPLEMENTED (3D beds with collision on the real garden layout, PlayMode-tested; painted plants follow the garden's own Dormant/Bloom roots; no harvestable crops exist in this game); owner visual approval PENDING
TREES_AND_OCCLUSION_STATUS=PARTIAL (87 painted trees with real shadows and trunk collision; caretaker 69-85 % visible behind them through the opening, B4 14-72 % at the same spots; hidden behind the workshop with B4's silhouette marker)
CHARACTER_ANIMATION_STATUS=UNCHANGED_B4 (no new drawings; distance-driven run)
GLOBAL_LOOK_STATUS=PARTIAL (painted ground, trees, spring and garden sit with the B4 paintings; windmill, plaza ruins, hen yard and lanterns still low-poly)
MINIMAP_REAL_WORLD_STATUS=PASS (re-baked from the B5 valley, live player and objective; automated tests)
EDITMODE_STATUS=Passed 262/262
PLAYMODE_STATUS=Passed 106/106
WINDOWS_BUILD_STATUS=Succeeded 8dcf20bf30374cc1b676ad6c2df2fd78 (artifacts/build/phase52-b5-final)
FULL_GAME_SMOKE=PASS (1920x1080, three restorations, ending, save and Continue, 0 exception lines); standalone 720p smoke NOT RUN
REAL_PLAYER_SCREENSHOTS=PASS (85 real Windows-player stills per 1080p route run, B4 and B5 at the same places, AUTOMATED input; S1-S3 set by script and labelled)
CONTINUOUS_REAL_PLAYER_MOVEMENT_VIDEO=PASS (AUTOMATED_INPUT, 83.3 s, real framebuffer)
Radeon_780M_1080P_PERFORMANCE=PARTIAL (B5 completed route at 60.0 FPS, P99 16.84 ms; B4 observed 59.6 FPS, P99 16.86 ms but route exit 1 at inherited collider; no parity claim)
STOP_RULES=1 UNKNOWN, 2 PASS, 3 FAIL (workshop occlusion), 4 UNKNOWN, 5 UNKNOWN, 6 PASS (automated), 7 UNKNOWN (B4 route failed)
GATE_B5_RESULT=PARTIAL_OR_FAIL_UNTIL_OWNER_APPROVAL
GATE_B5_OWNER_APPROVAL=PENDING
HUMAN_VISUAL_APPROVAL=PENDING
HUMAN_GAMEPLAY_QA_STATUS=PENDING
WORLD_EXPANSION_ALLOWED=NO
REMOTE_PUSH_STATUS=PASS (HEAD equals origin/main; see delivery-identifiers.json for live remote verification)
ITCH_SUBMISSION_STATUS=NOT_SUBMITTED
REVIEW_ZIP=artifacts/review/phase52-b5/Echoes-Phase52-B5-Painted-Environment-Review.zip (its SHA-256 is in the sidecar .sha256.txt: a zip cannot contain its own hash) and three parts under 256 MiB in artifacts/review/phase52-b5/split/
NEXT_ACTION=Owner plays the B5 Windows build (and -echoes-b5-before for B4) and decides Gate B5
```
