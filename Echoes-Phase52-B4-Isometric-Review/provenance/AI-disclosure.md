# AI disclosure for Phase 5.2-B4 (thatgamejam #02 requires disclosure of AI-generated material)

**AI-generated art in the B4 pilot.** The B4 caretaker drawings (idle and run poses, four directions), the painted workshop, willow and
spring, the ground plants (grass, wildflowers, fern), the plaza stone texture and the minimap frame are **original AI-generated
illustrations supplied by the game's owner** in `Echoes-B4-Original-Art-Assets-20261028.zip` (the pack does not name the generator; the
owner should state it in the release disclosure). Scripts cleaned their alpha, cropped, scaled and split them, and animated the caretaker
lightly (2D warping for breathing, squash in-betweens for the run); no human painted over them. The villagers and dialogue busts are
unchanged from B3 (OpenAI image generation, owner-supplied).

**AI-assisted code and documentation.** Anthropic Claude Code (this session reported the model Claude Opus 5.5) wrote, under the
developer's direction: `tools/art/phase52b4/*` (audit, character and prop pipelines, evidence scripts), the `Echoes/StaticProp25D`,
`Echoes/Foliage25D` and `Echoes/TriplanarStone` shaders, `StaticProp25D`, `StageVariant25D`, `Phase52B4Pilot`, the minimap
(`MinimapController`, `MinimapTargets`), the B4 builder, the Editor preview and the automated evidence tour, the B4 tests, `docs/10` and
the review package.

**Not AI-generated / unchanged.** Gameplay systems, save format, quests, the 36 synthesised WAV files and the Unity built-in font are
unchanged by B4. No third-party art, Atelier Ryza material, marketplace pack, stock image, motion data or font was used. The owner's two
reference game screenshots mentioned in the pack were not supplied and are not in the repository.

**In-game credits.** The B4 pilot menu (version `0.5.2-pilot.4`) states that these illustrations are original AI-generated art supplied
by the owner and that the pipelines, shaders, camera, minimap and scene builder were made with Anthropic Claude Code.

**Owner action before any release:** name the image generator, confirm this wording and approve the illustrations as release art.
