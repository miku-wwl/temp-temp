# Echoes of the Valley: Phase 5.2-B4 (high-angle static 2.5D) review report

2026-10-29 NZDT, branch `art/phase52-b4-iso` (local only), started from the B3 tip `c829e65aa55bb68faac9aa7f6b720550b4b69ebf`. **Author self-assessment only. Gate B owner approval, human visual approval and human gameplay QA are PENDING; world expansion is NOT allowed.** Every screenshot and the video are real Windows-player framebuffer captures driven by **automated** input (no human played). The owner's style target is shown only as labelled concept art.

## 1. Git and safety (checked before any work, re-read now)

- Branch `art/phase52-b4-iso` at `cd24f521750ef49c50497e52ea4ac17f76340a6e`; B3 branch `art/phase52-b3-2d5d` still at `c829e65aa55bb68faac9aa7f6b720550b4b69ebf` (unchanged: YES); B2 branch unchanged: YES; `main` at `d9a8d12f79e17257c4808498f8c4144602cafbc3` is an ancestor of this branch: YES. Nothing was merged into `main`; nothing was pushed; no branch was deleted.
- The B4 branch was created from the committed B3 tip (no uncommitted owner work was found then); no `git reset --hard`, checkout over changes or force operation was used. RC1/RC2 release zips unchanged: YES. The original, B2 and B3 scenes are untouched (EditMode test `OriginalB2AndB3Scenes_ContainNoB4Presentation`). The old version is recoverable by checking out `art/phase52-b3-2d5d`, and inside the B4 build by `-echoes-b4-before`.

## 2. Asset pack

- `D:\Download\Echoes-B4-Original-Art-Assets-20261028.zip`, SHA-256 `738B55FDB63BB4EF328A3FAA35F6D4802E0428B9C7577458D1B9E5A14BA8F30B`, unpacked outside the repository; `SHA256SUMS.txt` verified 28/28 (22 new PNGs, 3 earlier owner references, README, QA list, prompt). The prompt inside the zip is byte-identical to the one in `D:\Download`.
- The 22 new PNGs are kept unmodified in `game/Assets/Echoes/Art/Phase52B4/Source/` (an EditMode test re-hashes every one). Audit: every image on white/black/grass/earth, materials tiled 2x2 (`asset-audit/` in the package).
- **A, used (16):** idle S/N/W/E, run key-pose sheets S/N/W/E, the painted workshop, willow and spring, grass, wildflowers and fern, the mossy stone tile (made seamless), the minimap frame (red fringe cleaned).
- **B, reference only (6):** `b4_isometric_style_target.png` (concept; never shown as game footage, not in the build); `broadleaf_branch.png` (one branch does not make a painted tree); `willow_leaf_fronds.png` (the painted willow carries its own fronds); plaster, roof and timber tiles (the workshop is now the painting; the tiles also show seams when repeated: roof 1.77/1.46, timber 2.25 in X).
- **C, rejected:** none. Per-file use and every modification: `provenance/asset-manifest.md`.

## 3. What was built

- An isolated B4 pilot scene pair (`Art/Phase52B4/Scenes`, copied from B3 by `Phase52B4Builder.Generate`) with a presentation switch (`Phase52B4Pilot`): B4 by default, the B3 presentation in the same scene with `-echoes-b4-before`.
- The owner's B4 caretaker drawings on the unchanged B3 presenter and player root: four directions, 12-frame breathing idle, 8-frame run from the drawn key poses.
- Painted static props for the workshop, the willow and the spring, standing on the ground through per-pixel depth from each painting's ground line, with real shadows from the hidden 3D bodies, a collision prism on the workshop's painted footprint, a canopy opening under the willow and a dormant/restored spring by stage.
- 900 painted ground-plant clumps around the four places; a cobbled plaza and painted fieldstone on the Heart's ruins (triplanar).
- A real minimap at the top right (baked from the valley, turned with the camera, live player arrow, landmark icons and objective marker).
- The high fixed camera (yaw 45, pitch 48, distance 14 m).

## 4. Camera

- **Final: yaw 45 degrees, pitch 48 degrees, distance 14 m (wheel 10-17 m), vertical FOV 35 degrees; 10.4 m above the follow pivot, about 11.3 m above the ground at the default zoom.** The pitch is the angle below the horizon (`Quaternion.Euler(pitch, yaw, 0)` looking down); B3 was 30, so the camera is higher, not lower.
- Pitch 42, 48 and 54 were rendered in the Editor (diagnostic) and in the player (`comparison/pitch`). The paintings face the camera, so the pitch changes the ground's foreshortening and how far ahead the player sees, not the props; 42 shows more ground ahead, 54 is closer to a map view and makes the paintings' lower drawing angle more obvious. 48 is kept as the owner's default.
- Yaw is fixed at 45 because the paintings show the workshop's facades at 45 degrees to the view; with any other yaw the painted house would stand skewed to the world's paths. WASD stays screen-relative (W = up the screen), no free orbit.
- Collision against the terrain only (the high camera passing over roofs and canopies never jumps in; tested in PlayMode behind the workshop); the ending's automatic drift is limited to a 10 degree sway (painted props have no backs). The B3 scene keeps its own camera (tested).

## 5. Caretaker (idle and light animation)

- Idle: the owner's natural standing drawings (arms down) for S, N, W and E, cleaned of the red-brown haze ring, 12-frame breathing loop at 5 fps (chest 0.8 %, head follows, slight sway, hair ends and coat tails lag).
- Run: the four drawn poses of each direction split from the key-pose sheets, scaled so the head matches the standing drawing, with 1.5 % squash in-betweens: 8 frames per 2.8 m loop (about 13 frames/s at the unchanged 4.5 m/s), advanced by distance (no skating), back to idle on stopping. The back view mirrors the lower legs of one pose because that sheet was drawn leading with one leg.
- One player root, collider, interaction root, speed and save identity unchanged (tests). E has its own drawings; nothing flips at runtime.
- Ten full turns are in the recorded video (small circles on the plaza, every facing ten times); the four directions read as the same character to the author, but the owner's eye decides (the sheets were generated per direction and differ in small details such as the strap).

Real-player direction samples in the recorded route: E/idle 65, E/run 408, N/idle 205, N/run 243, S/idle 204, S/run 194, W/idle 260, W/run 529. Run-cycle check (unrecorded 720p route): the run phase advanced by distance / stride (2.80 m) with a median per-frame difference of 0.0 and p95 0.0013 of a cycle (a sampling artefact of one frame; the cycle is driven by distance, so feet cannot skate).

## 6. Minimap (real world data)

- Top right, 232 px frame at 1080p (the pack's frame after fringe cleanup), Coins/Clover moved to its left, the quest panel unchanged; checked in dialogue, satchel and pause stills at 1080p and 720p.
- The map is the valley itself: an orthographic north-up render of the terrain, paths, water, trees and buildings baked in the Editor (clouds, fog and the painted cards excluded), 112 x 112 m. At runtime only UI transforms move: it turns by the camera yaw so up on the map is up on the screen, slides to keep the player centred, the arrow turns with her facing, five landmark icons ride on it.
- The objective marker follows `MinimapTargets.Current(session)`, written to match the objective panel; it clamps to the window edge with a pointer when the target is far. PlayMode test: the map point moves by the player's own world movement in the camera's orientation, and the marker moves on after talking to Mira.

Recorded route (the video), objective shown by the minimap over time: 0.0 s Mira (idle), 43.4 s Spring (interact), 75.1 s Heart (interact). Map scale while walking in the unrecorded 1080p route (UI units per metre of the player's own movement, constant if the map follows the world): p05 3.591, median 3.684, p95 3.791 over 3159 steps.

## 7. Static environment

- Workshop: the painted apothecary (stone base, timber frame, red roof, awning, potion counter, flower boxes, lanterns, ivy) replaces the box house; its 3D walls and roof stay invisible as the shadow caster; the yard's low-poly cauldron, table, barrels and sign are hidden (the painting has its own counter, barrels and sign). E interaction unchanged.
- Willow: the painted willow (trunk, roots, fronds, flowers) on the old trunk collider, with a dithered opening around the player under the canopy.
- Spring: the painted ruined spring; S0 shows a dormant painting (the water recoloured), S1 and later the clear painted spring with the unchanged 3D stream, bloom and lighting.
- Plaza: cobbles from the pack's mossy stone on the B2 terrain copy, and the Heart's primitive dais, pillars and blocks in painted fieldstone; the low-poly plant cones on the dais are hidden.
- Ground plants: 900 painted grass, wildflower and fern clumps around the four places, alpha-tested (no sorting, no grey rectangles).

## 8. WHAT STILL LOOKS BAD

- **The rest of the valley is still B2/B3 low-poly** and the high camera shows it: broadleaf trees whose leaf cards read as dark shards from above (visible at the edges of the spring, workshop-back and willow shots), the windmill, the garden, the grey-green stream ribbon, and the Heart's floating crystal, a plain blue cube. Next to the paintings this is the largest style gap.
- **Occlusion by a B2 tree at the spring (stage S1 and later):** the pink bloom tree that appears beside the spring path when the stream is restored is low-poly B2 foliage without a canopy opening; when the caretaker stands under it (as in `stage-S1/S2/S3-set-by-script-spring`), only her head shows, and the leaves are too close for the silhouette pass. The painted willow handles this; that tree does not.
- The paintings were drawn for a lower view than 48 degrees: their roofs and the workshop's footprint read slightly flatter than the ground around them, and as cards they keep the same perspective at 42 and 54.
- The workshop's footprint is a skewed parallelogram on the ground (the painting's isometric projection does not match the camera's), so the collision prism is skewed too; walking along the facades is fine, but the corners are a little generous or tight.
- The caretaker's run has four drawn poses per direction: readable steps, not fluid animation; the back view's mirrored legs are the weakest frames. Small detail differences between the per-direction drawings are visible at the closest zoom.
- The ground between the painted clumps is the B2 terrain texture: flatter and less painterly than the props.
- The plaza's fieldstone is the cobble texture on primitive shapes: it reads as stone, not as carved ruins; shadows of the obelisk are large.
- The silent valley (S0) is deliberately grey-green and hazy; the warm look appears from S1 on (stills set by the script).

## 9. Visual gate (author's judgment of the real-player screenshots; the owner decides)

- Camera: higher and still easy to steer (screen-relative WASD, fixed yaw, wheel zoom); not harder to operate in the author's runs. Owner to judge comfort.
- Minimap: real (baked from the world, live player and objective); not a picture.
- Occlusion: behind the painted workshop the player shows as a silhouette through the roof, and under the painted willow the canopy opens around her; but at stage S1 and later the B2 bloom tree beside the spring path hides her when she stands under it (see WHAT STILL LOOKS BAD). Not severe on the S0 route; a real defect for later stages.
- Character: not a sliding sticker (distance-driven steps, idle breathing), but four-pose runs are basic.
- Style: the three painted places and the caretaker match each other; the surrounding low-poly trees and buildings do not. This is the main reason the author would not call the visual gate passed.
- Regression: EditMode 244/244 and PlayMode 100/100 in an isolated copy (the B4 PlayMode test plays the first quest through the workshop and the spring, saves and continues in the B4 scene); the whole-game smoke (three restorations, ending, save and Continue) passes in the B4 build at 720p and 1080p. Performance: no measurable change at the 60 FPS cap (section 13).

## 10. Tests (read from the XML)

| Run | Result |
| --- | --- |
| EditMode, isolated project copy `artifacts/visual-qa/phase-5.2/logs/b4-tests-4` | Passed 244/244 (failed 0, skipped 0) |
| PlayMode, same copy | Passed 100/100 (failed 0, skipped 0) |

B4 tests: B4Atlas_FramesDoNotOverlap_AndTransparentTexelsCarryBledColour: Passed, B4RunCycle_StrideMatchesTheControllerSpeed_AtAReadableRate: Passed, B4Scene_KeepsOnePlayerRoot_WithTheB4DrawingsOnTheUnchangedPresenter: Passed, B4Sheet_HasFourDirections_TwelveIdleAndEightRunFrames_AtOneConsistentHeight: Passed, B4Sources_AreTheUnmodifiedOwnerSuppliedFiles: Passed, B4Terrain_IsACopyWithACobbledPlaza_AndTheB2TerrainIsUnchanged: Passed, HighFixedCamera_UsesTheDefaultPitchWithinTheTestedRange_AndOnlyAGentleEndingSway: Passed, Minimap_IsARealBakedMapOfTheValley_TurnedWithTheCamera: Passed, MinimapTargets_FollowTheObjectivePanel: Passed, OriginalB2AndB3Scenes_ContainNoB4Presentation: Passed, PaintedCardsAndFoliage_AreAlphaTestedWithDepth_SoTheyNeverDependOnSorting: Passed, PaintedProps_StandOnTheirLandmarks_AsVisualOnlyCards: Passed, PlazaStones_TakeThePaintedFieldstone_AndBeforeModeRestoresTheirMaterials: Passed, ReplacedLandmarks_KeepTheirGameplayColliders: Passed, WorkshopCollision_FollowsThePaintedFootprint_AndEveryInteractionStaysReachable: Passed, B4Scene_RestoresTheStreamThroughTheWorkshopAndTheSpring_AndContinueKeepsIt: Passed, BeforeAndAfter_SwitchOnlyThePresentation: Passed, HighCamera_StaysFixed_AndDoesNotJumpInBehindThePaintedWorkshop: Passed, Minimap_FollowsThePlayer_AndPointsAtTheCurrentObjective: Passed. No existing test was removed or weakened (B3 historical baseline: EditMode 229/229, PlayMode 96/96).

## 11. Windows build

Non-development Windows x64 Mono/D3D11, Unity 6000.3.27f1 / URP 17.3.0, version `0.5.2-pilot.4`, `artifacts/build/phase52-b4`: **Succeeded**, build id `3ff366c7acba45d89c6af80ffd29c988`, errors 0, warnings 0, 303983411 bytes, `EchoesOfTheValley.exe` SHA-256 `F083B0E879A9298CDCF9933A8F3B4F2B013E13F7D3E51221FC0D756B79E28807` (the Unity launcher stub; every build file is hashed in the package's `logs/build-manifest-SHA256.txt`). `-echoes-b4-before` starts the same build with the B3 presentation.

## 12. Real-player runs (automated)

- Recorded route 720p (video): exit code 0 (natural exit True), exception/error lines 0; route 231.7 m in 81.84 s, route pass True, failures none; 1280x720, 2103 frames, P50 38.00 / P95 47.55 / P99 72.07 ms, worst 117.0 ms, mean 25.7 FPS, 1% low 13.9 FPS (recorded run: framebuffer capture overhead included, not a rendering benchmark); another EchoesOfTheValley process was running (PID 63528, the owner-started B3 player)
- Route 1080p (stills): exit code 0 (natural exit True), exception/error lines 0; route 230.9 m in 81.39 s, route pass True, failures none; 1920x1080, 4870 frames, P50 16.67 / P95 16.70 / P99 17.28 ms, worst 110.5 ms, mean 59.8 FPS, 1% low 57.9 FPS (unrecorded run: no framebuffer capture; one scripted route, not a human traversal benchmark); another EchoesOfTheValley process was running (PID 63528, the owner-started B3 player)
- Unrecorded route 1080p, B4: exit code 0 (natural exit True), exception/error lines 0; route 230.8 m in 81.43 s, route pass True, failures none; 1920x1080, 4878 frames, P50 16.67 / P95 16.69 / P99 16.91 ms, worst 56.0 ms, mean 59.9 FPS, 1% low 59.1 FPS (unrecorded run: no framebuffer capture; one scripted route, not a human traversal benchmark); another EchoesOfTheValley process was running (PID 63528, the owner-started B3 player)
- Unrecorded route 1080p, B3 presentation in the same build (before mode): exit code 0 (natural exit True), exception/error lines 0; route 230.9 m in 81.55 s, route pass True, failures none; 1920x1080, 4888 frames, P50 16.67 / P95 16.68 / P99 16.86 ms, worst 38.5 ms, mean 59.9 FPS, 1% low 59.3 FPS (unrecorded run: no framebuffer capture; one scripted route, not a human traversal benchmark); another EchoesOfTheValley process was running (PID 63528, the owner-started B3 player)
- Unrecorded route 720p, B4: exit code 0 (natural exit True), exception/error lines 0; route 230.9 m in 81.36 s, route pass True, failures none; 1280x720, 4880 frames, P50 16.67 / P95 16.69 / P99 16.84 ms, worst 20.3 ms, mean 60.0 FPS, 1% low 59.4 FPS (unrecorded run: no framebuffer capture; one scripted route, not a human traversal benchmark); another EchoesOfTheValley process was running (PID 63528, the owner-started B3 player)
- Whole-game smoke 720p: natural exit True, exit code 0, 75.1 s, 40 screenshots, exception lines 0, [Echoes][SMOKE] PASS: all steps passed in 70 s, 40 screenshots
- Whole-game smoke 1080p: natural exit True, exit code 0, 79.9 s, 40 screenshots, exception lines 0, [Echoes][SMOKE] PASS: all steps passed in 75 s, 40 screenshots

Video: 82.00 s, 2050 real framebuffer frames at their captured intervals (about 25.0 FPS), 1280x720, no audio, no desktop capture, no concept compositing; SHA-256 `3a94b720f7434a919f12a8c760aa759d174bed07e53c982a6bcad4aec27a2fb3`. Stills in the 1080p run: 42 (pitch-42-workshop-front, pitch-42-plaza, pitch-48-workshop-front, pitch-48-plaza, pitch-54-workshop-front, pitch-54-plaza, hero-idle-S-closest-zoom, hero-idle-S-default-zoom, hero-idle-W-closest-zoom, hero-idle-W-default-zoom, hero-idle-E-closest-zoom, hero-idle-E-default-zoom, hero-idle-N-closest-zoom, hero-idle-N-default-zoom, hero-walk-S-closest-zoom, hero-walk-W-closest-zoom, hero-walk-E-closest-zoom, hero-walk-N-closest-zoom, after-b4-workshop-front, before-b3-workshop-front, after-b4-workshop-side, before-b3-workshop-side, after-b4-plaza, before-b3-plaza, after-b4-willow, before-b3-willow, after-b4-spring, before-b3-spring, occlusion-behind-workshop, occlusion-behind-willow, dialogue-mira, inventory-portrait, pause-menu, stage-S1-set-by-script-workshop-front, stage-S1-set-by-script-plaza, stage-S1-set-by-script-spring, stage-S2-set-by-script-workshop-front, stage-S2-set-by-script-plaza, stage-S2-set-by-script-spring, stage-S3-set-by-script-workshop-front, stage-S3-set-by-script-plaza, stage-S3-set-by-script-spring).

## 13. Performance (Radeon 780M, D3D11, windowed, 60 FPS cap, vSync off)

B4 1080p: mean 59.9 FPS, 1% low 59.1 FPS, P95 16.69 ms, P99 16.91 ms; B3 presentation in the same build and route: mean 59.9 FPS, 1% low 59.3 FPS, P95 16.68 ms, P99 16.86 ms.

The unrecorded runs are the measurement (the recorded run includes synchronous framebuffer readback and JPEG encoding). An owner-started B3 player (PID 63528, launched without arguments on 2026-10-28 20:43) was open during the runs and was not closed by the author, so absolute frame times include its load; B4 and the B3 presentation were measured under the same conditions. B3's own 1080p unrecorded route in October 28's B3 review: mean 60.0 FPS, P99 16.69 ms (a different route, without that extra process). Headroom above the 60 FPS cap, VRAM, peak memory, other GPUs: NOT VERIFIED.

## 14. Status

```text
PHASE52_B4_STATUS=PARTIAL
SOURCE_BRANCH=art/phase52-b4-iso
SOURCE_COMMIT=cd24f521750ef49c50497e52ea4ac17f76340a6e
ART_DIRECTION=HIGH_ANGLE_STATIC_25D + WARM_ALCHEMY_ANIME
ASSET_ZIP_VERIFICATION=PASS (SHA-256 738B55FD...8F30B; SHA256SUMS 28/28)
NEW_ASSETS_USED=16 (idle S/N/W/E, run key poses S/N/W/E, workshop, willow, spring, grass, wildflowers, fern, mossy stone, minimap frame)
REJECTED_ASSETS_AND_REASONS=none rejected; 6 reference-only: style target (concept), broadleaf_branch (no painted tree to build), willow_leaf_fronds (willow painting has its own), plaster/roof/timber tiles (workshop is the painting; seams when tiled)
HERO_IDLE_STATUS=IMPLEMENTED (owner drawings, 12-frame breathing loop); owner visual approval PENDING
HERO_LIGHT_ANIMATION_STATUS=PARTIAL (idle breathing + 8-frame run from 4 drawn poses per direction, distance-driven; basic)
CAMERA_PITCH_FINAL=48 (yaw 45, distance 14 m, FOV 35; 42/54 compared)
MINIMAP_REAL_WORLD_STATUS=PASS (baked from the valley, live player arrow and objective; automated tests)
STATIC_ENVIRONMENT_VISUAL_STATUS=PARTIAL (workshop, willow, spring, plaza upgraded; surrounding B2 low-poly trees/windmill remain)
WORLD_CHARACTER_STYLE_COHERENCE=PARTIAL (painted props and caretaker coherent; surrounding low-poly scenery is not)
EDITMODE_STATUS=Passed 244/244
PLAYMODE_STATUS=Passed 100/100
WINDOWS_BUILD_STATUS=Succeeded 3ff366c7acba45d89c6af80ffd29c988 (artifacts/build/phase52-b4)
ACTUAL_PLAYER_SCREENSHOTS=PASS (42 real Windows-player stills at 1080p and 42 at 720p, AUTOMATED input; S1-S3 stills set by the script and labelled)
CONTINUOUS_REAL_PLAYER_MOVEMENT_VIDEO=PASS (AUTOMATED input, 82.0 s, 1280x720, real framebuffer)
Radeon_780M_1080P_PERFORMANCE=PASS_AT_60FPS_CAP (B4 1080p unrecorded: mean 59.9 FPS, P99 16.91 ms, 1% low 59.1; B3 presentation same build and route: 59.9 / 16.86 ms / 59.3; headroom above the cap NOT VERIFIED; an owner-started B3 player was open in the background)
GATE_B_RESULT=PARTIAL_OR_FAIL_UNTIL_OWNER_APPROVAL
GATE_B_OWNER_APPROVAL=PENDING
HUMAN_VISUAL_APPROVAL=PENDING
HUMAN_GAMEPLAY_QA_STATUS=PENDING
WORLD_EXPANSION_ALLOWED=NO
REMOTE_PUSH_STATUS=NOT_ATTEMPTED
ITCH_SUBMISSION_STATUS=NOT_SUBMITTED
REVIEW_ZIP=artifacts/review/phase52-b4/Echoes-Phase52-B4-Isometric-Review.zip (its SHA-256 is in the sidecar .sha256.txt: a zip cannot contain its own hash)
NEXT_ACTION=Owner plays actual B4 Windows build, assesses 2.5D visual fidelity
```
