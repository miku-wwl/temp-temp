# Echoes of the Valley

**Unity game jam entry · thatgamejam #02 · 2026**

An original, short, peaceful fantasy adventure about **bringing a silent valley back to life** through gathering, caring for a chicken, alchemy and visible restoration.

> **Silence → Awakening → Resonance → Rest**

**GATHER → CARE → CRAFT → RESTORE → WATCH THE WORLD RESPOND**

## Current truth

- **Phase 5.2-B3 (branch `art/phase52-b3-2d5d`, local only): owner-approved 2.5D pivot; owner approval PENDING.** The procedural 3D caretaker is no longer the player's visual: four-direction illustrated sprites (run and idle), cut out and animated offline from the owner-supplied AI illustrations, stand on the unchanged player root under a fixed three-quarter camera; villagers and dialogue busts use the same illustrated style. Gameplay, saves and B2's paths/foliage are kept; isolated pilot scenes only, no Phase C/D work. Details, camera constraints, AI disclosure and what still looks bad: [09](docs/09-PHASE52-B3.md).
- **Phase 5.2-B4 (branch `art/phase52-b4-iso`, local only): high-angle static 2.5D; owner Gate B approval PENDING.** A higher fixed camera (yaw 45, pitch 48, tested at 42/48/54), the owner-supplied B4 caretaker drawings (natural idle breathing, eight-frame run), painted static props for the workshop, willow and spring, painted ground plants, a cobbled plaza with fieldstone ruins, and a real minimap (baked from the valley, turned with the camera, live player arrow and objective marker). Gameplay, collision, quests and saves are unchanged; isolated pilot scenes only, no world expansion. Details, evidence and limits: [10](docs/10-PHASE52-B4.md).
- **Phase 5.2-B2 (branch `art/phase52-b2`, local only): bounded revision of the failed Gate B pilot; self-assessed PARTIAL, owner approval PENDING.** Root causes were measured first: the saved pilot terrain was 100 % Grass (the plaza, five dirt paths and the windmill trail were gone) and the tree meshes used 2.2-2.6 m leaf cards from 0.9 m height under a camera that only tests colliders. B2 replaces the hero pipeline with a painted, toon-shaded, 43-bone character (8 painted materials, IK-planted jog at the unchanged 4.5 m/s), adds camera-aware dithered foliage and restores/re-skins the original paths. It stops for independent visual review; no Phase C/D work. Details, evidence and what still looks bad: [08](docs/08-PHASE52-B2.md).

- **Phase 5.2: isolated pilot, Gate B FAIL; stopped before expansion.** The new weighted caretaker and bounded route work technically, but facial/costume finish and intrusive foliage cards do not meet the strict visual gate. The owner previously rejected RC2 art. Current evidence and owner-review boundary: [07](docs/07-PHASE52-ART-DIRECTION.md). The original scenes, controls and gameplay remain; no Phase C/D release work is authorized.

- **Phase 5.1 (historical visual overhaul): implemented; art REJECTED by owner.** Engineering results and reproducible source remain in [06](docs/06-PHASE51.md). RC1 and RC2 are preserved; nothing is pushed or uploaded.

- **Competition:** [thatgamejam #02](https://itch.io/jam/thatgamejam02), hosted by thatgamecompany.
- **Start:** 2026-10-24 20:00 NZDT; **deadline:** **2026-11-08 20:59 NZDT**. Internal upload target: 2026-11-07.
- **Phase 1 (foundation): PASS** on 2026-10-25 against its gate (23/23 EditMode, 18/18 PlayMode, Windows build, smoke test). Pushed to `origin/main` as `8849495` and `587ebf4`. Evidence: [02](docs/02-BUILD-PLAN.md#phase-1-status-2026-10-25).
- **Phase 2 (playable slice): automated gate PASS** on 2026-10-25: 111 EditMode and 49 PlayMode tests, a Windows x64 Mono development build and a scripted standalone gameplay smoke test. Pushed to `origin/main` as `f8847b6`. Evidence, design numbers and limits: [02](docs/02-BUILD-PLAN.md#phase-2-status-2026-10-25).
- **Phase 3 (full greybox): automated gate PASS** on 2026-10-26: 168 EditMode and 68 PlayMode tests, a Windows x64 Mono development build and a scripted standalone full-game smoke test (New Game to the ending, save, Continue, Continue Exploring). The whole game runs from New Game to the ending: three restorations (`ResonanceStage` 0 to 3), a closing conversation and an ending screen. Committed locally; **not pushed**. Evidence, design numbers and limits: [02](docs/02-BUILD-PLAN.md#phase-3-status-2026-10-26).
- **Phase 4 (cohesive visuals and sound): automated gate PASS** on 2026-10-26: 196 EditMode and 83 PlayMode tests, a Windows x64 Mono development build and the standalone full-game smoke (it also checks the soundscape of every stage). New: original characters and scenery, a gradient sky, post-processing, a stage-by-stage colour grade, clouds, flowing water, glowing windows, a restyled interface with a Credits screen and volume sliders, and **36 original sound files synthesised by code** (music in layers that grow with the valley, ambience, effects, an ending piece). Committed locally; **not pushed**. Evidence and limits: [02](docs/02-BUILD-PLAN.md#phase-4-status-2026-10-26).
- **Phase 5 (Windows release candidate): automated preparation PASS** on 2026-10-26. A non-development Windows x64 build (`1.0.0-rc1`, build id `008df7e74ad8443faa7db6a90d2f4776`) is packed as `EchoesOfTheValley-Windows-x64-RC1.zip` (61.7 MB, SHA-256 `EB9BFC0D9B0BA60A0E9E8367ADF3BA4EB4D4F3EFE8F0F6FCD58A2AB4FDA3112A`; in the git-ignored `artifacts/release`, **not in Git**). `VerifyRelease` read the zip, extracted it to a temporary folder, compared every file, scanned for local paths (0 found), started the **extracted** player cold and played the whole game on it to the ending (save, Continue, Continue Exploring) with a natural exit code 0, 0 error lines and no network use. **Not played or listened to by a human, not uploaded, not submitted.** The record, the itch.io copy and the human QA checklist are in [05](docs/05-RELEASE.md).
- **Manual gameplay QA: PENDING.** The owner has played RC2 and rejected its visuals; this is not a recorded complete gameplay/audio acceptance pass. The new Phase 5.2 pilot still needs owner visual review and a recorded keyboard/mouse/audio playthrough before approval.
- **Environment:** Phase 0.5 **PASS** was for a separately tested blank-template probe (see [baseline](docs/04-BASELINE.md)); Phases 1 and 2 exercised the toolchain on this project.
- **Official theme:** released; the supplied 60-second audio's dynamics have been measured and interpreted. The emotional interpretation remains a **design choice**, not a claim of direct human listening.

## What exists now (Phase 4: the whole game, with its look and sound)

A complete, short game, built entirely from code (art, textures and audio included). `MainMenu` (**Continue** / **New Game** / Quit over a dusk backdrop) leads into the `Valley`: third-person WASD + mouse in one valley with a ruined landmark, workshop cottage, pond, stream, a village garden, a hen yard and an old windmill. The loop is **gather, care, craft, restore, watch the valley change**, three times, then a short ending.

1. **Explore.** The opening objective *Discover the Heart of the Valley* (villager Mira, ruined spring, Heart) warms the light and wakes the crystal.
2. **Gather.** Press **E** at 15 renewable nodes: 6 Moonleaf Herb bushes, 5 Dawn Blossom patches, 4 Echo Ore outcrops. Each regrows (40 s, or 75 s for ore), so nothing the story needs can run out.
3. **Carry and craft.** **I** opens the satchel (8 slots, up to 9 of each item, coins). At the workshop there are three recipes, click Craft or press 1-3: *Spring Tonic* (2 herb + 1 blossom), *Chicken Feed* (1 blossom makes 3) and *Windmill Braces* (4 ore).
4. **Restore the Stream.** Mira gives the quest; pour a Spring Tonic into the ruined spring. `ResonanceStage` 0 to 1: the murky trickle becomes a wide clear stream, the basin fills, blooms open along the banks, a ring of light runs outward and the light turns golden. +25 coins, once.
5. **Restore the Garden.** Wren, the Garden Keeper, wants a fresh egg. Feed Clover the hen (15 s to lay), collect the egg, give it to Wren. Stage 1 to 2: the withered beds bloom in colour, the hen yard gets pots and bunting, pollen drifts over the garden, villagers perk up, the light warms. +30 coins, once.
6. **Restore the Windmill.** Alder, the Windmill Restorer, needs Windmill Braces: gather 4 Echo Ore, mix them at the workshop and hand them over. Stage 2 to 3: the broken windmill is mended and its sails start to turn, lanterns and flowers appear through the village, a pulse of light crosses the whole valley and the light turns to golden hour. +40 coins, once.
7. **The ending.** Mira holds a closing conversation, then the ending screen appears over the restored valley with **Continue Exploring** and **Main Menu**. The valley stays fully playable afterwards, and loading the game never replays the ending.
8. **Save.** Pause menu **Save Game**, plus an autosave at every story beat. **Continue** loads the versioned JSON save (with a backup it falls back to, and it refuses damaged data). Saves from Phase 2 (version 1) still load. **New Game** asks before it can replace an existing save.

**Sound** (all original, synthesised by code, nothing sampled): the title screen plays a soft welcome mix; in the valley a quiet bell-and-wind bed grows with each restoration: the stream adds water and a harp, the garden adds birds, wind chimes, a marimba and a pad, the windmill adds the melody and the turning sails, and the ending has its own short piece. Every action has a sound that plays once, when the world really changes. Music and Sound volume sliders are on the title screen and in the pause menu.

Not built yet: a human-verified playtest (nobody has watched or listened to it), and everything listed as out of scope in [01](docs/01-GAME-DESIGN.md).

## Exact MVP (10–15-minute goal; max 20-minute target)

One compact valley, one original protagonist, three short NPC quests, herbs/ore, a chicken and egg, three recipes, three restoration states, an ending, and local save/load. No combat, fishing, seasons, breeding, online features or large world.

The signature feature is **Valley Resonance**: each important restoration creates a clear, persistent before/after change in color, vegetation, activity and sound. The game does **not** need to be a rhythm game or ship with the official theme audio.

## Docs: five short working references

1. [01 — Game vision and musical theme](docs/01-GAME-DESIGN.md)
2. [02 — Implementation and acceptance gates](docs/02-BUILD-PLAN.md)
3. [03 — Jam rules, provenance and AI disclosure](docs/03-RULES-CREDITS.md)
4. [04 — Verified Phase 0.5 baseline](docs/04-BASELINE.md)
5. [05 — Release candidate, submission copy and human QA](docs/05-RELEASE.md)

## Unity project (Windows)

`game/` is a Unity **6000.3.27f1** Universal 3D (URP) project: Windows x64 Mono + D3D11, Input System only. It needs the verified Editor at `D:\Unity\Downloads\6000.3.27f1\Editor\Unity.exe`, a valid Personal license and free space on D:.

**Open it:** add `game/` in Unity Hub, or run `Unity.exe -projectPath game`. Open `Assets/Echoes/Scenes/MainMenu.unity` (the startup scene) and press Play.

**Commands** (from the repository root; logs, test XML, screenshots and builds go to the git-ignored `artifacts/`):

| Command | What it does |
| --- | --- |
| `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Init-Unity.ps1` | One-time creation of `game/` from Unity's own URP template; refuses to overwrite an existing project. Already done. |
| `.\tools\Invoke-Echoes.ps1 -Task Compile` | Import and compile only (fast check; exit 1 on compiler errors). |
| `.\tools\Invoke-Echoes.ps1 -Task Setup` | Compile, configure player/URP settings, and (re)generate the audio, textures, materials, prefabs and the `MainMenu` and `Valley` scenes. Idempotent: it rebuilds the scenes from code, so running it twice does not duplicate anything. Assets come out byte-identical, but Unity renumbers internal object IDs in the two scene files on every save, so `git diff` shows churn; `git checkout` the scenes if you did not mean to change them. |
| `.\tools\Invoke-Echoes.ps1 -Task TestEdit` / `-Task TestPlay` | EditMode / PlayMode tests (batch mode, results in `artifacts\logs`). |
| `.\tools\Invoke-Echoes.ps1 -Task Preview` | Render editor camera shots of both scenes (every `ResonanceStage`) and the HUD, satchel, workshop, pause, ending and title screens at 1280x720 (no Play Mode) into `artifacts\logs\preview-shots`. |
| `.\tools\Invoke-Echoes.ps1 -Task DryRun` | Rehearse the smoke scenario inside the Editor's Play Mode (batch mode) so mistakes show up before a build is spent on them. Screenshots are skipped there (no Game view), so it is only a rehearsal; the standalone `Smoke` run is the evidence. |
| `.\tools\Invoke-Echoes.ps1 -Task Build` | Windows x64 Mono development build to `artifacts\build\win64\EchoesOfTheValley.exe`. It caps Unity's compiler at 4 threads (`BEE_BUILD_THREADS`) because a full-parallel build ran out of memory on this machine. |
| `.\tools\Invoke-Echoes.ps1 -Task Smoke` | Start that build with `-echoes-smoke`: a scripted playthrough of the **whole game** (menu, opening objective, the three restorations with gathering, crafting and the hen, save and Continue, the ending, Continue Exploring, the New Game question, back to the menu) using the game's real entry points and an isolated save folder, saving screenshots and exiting with code 0 (pass) or 1. It teleports the player instead of walking and is **not** a human playtest. |
| `.\tools\Invoke-Echoes.ps1 -Task BuildRelease` / `-Task Package` / `-Task VerifyRelease` / `-Task Release` | The release candidate: a **non-development** Windows x64 build (no watermark) in `artifacts\build\win64-release`; a zip (without Burst debug folders or `.pdb` files, with the player `README.txt` from `tools\release-readme.txt` and the build machine path removed from our own assembly) in `artifacts\release`; then a check that reads every zip entry, extracts to a temporary folder, compares every file, scans for local paths, starts the extracted player cold and runs the full-game smoke test on it (up to 3 attempts, all recorded in `release-metadata.json`). `Release` does all three. Nothing under `artifacts` is committed, and nothing is uploaded. |

**Release preservation (Phase 5.1):** existing build/package destinations are now refused. For another candidate pass fresh `-BuildDirectory`, `-ReleaseDirectory`/`-ReleaseName` and an explicit `-BuildVersion`; see [06](docs/06-PHASE51.md). The old default RC1 commands no longer overwrite a verified archive.

Scenes and prefabs are **generated**: edit `game/Assets/Echoes/Scripts/Editor` (also under the **Echoes** menu in the Editor) and re-run `Setup` rather than editing `.unity`/`.prefab` files by hand.

**Controls:** WASD/arrows move relative to the camera, mouse looks, **E** interacts and advances dialogue, **I** opens the satchel, **1-3** craft at the workshop, **Esc** closes a panel or pauses (the pause menu has the volume sliders; the title screen has **Credits**). On the ending screen **Enter, E or Esc** continue exploring and **M** returns to the main menu. Saves live in the game's persistent data folder (`%USERPROFILE%\AppData\LocalLow\EchoesJam\Echoes of the Valley\echoes_save.json`, plus `.bak`, and `.unreadable` if a file the game could not read had to be moved aside); tests and the smoke run use throwaway folders. The save is JSON with an explicit `version` (now 2; version 1 files from Phase 2 are verified against their own checksum and upgraded on the next save).

## Rights and attribution

Original source code is MIT-licensed (see [LICENSE](LICENSE)). That grant does **not** apply to art, audio, fonts, third-party material, documentation or the official theme. **Do not add the official theme WAV to public GitHub.** Maintain an actual-use asset register and make an honest AI-usage disclosure at submission; see [rules](docs/03-RULES-CREDITS.md).
