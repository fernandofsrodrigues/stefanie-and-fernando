# Stefanie & Fernando

A 2D / 2.5D partner platformer made in **Unity 6** for GSD 551 (Tools & Techniques of Programming, University of Illinois). Two heroes, one mission: recover the intel, clear the route, and reach extraction **together**.

**▶ Play in your browser:** [Unity Play](PLAY_URL_HERE) · **Portfolio page:** [fernando.org/portfolio](https://www.fernando.org/portfolio)

Version **0.8.47-r2** · Unity **6000.6.0f1** · Windows (native 4K) and WebGL

---

## The game

You play as **Stefanie** or **Fernando**. The hero you are not controlling becomes an AI partner who follows you, climbs with you and fights beside you. Switch leads at any moment.

The mission has three chapters:

| Chapter | Setting | Highlights |
|---|---|---|
| 1. **After the Rain** | Curitiba at night | Electrical hazards, raised platforms, Vesper at the extraction |
| 2. **Climb to the Redeemer** | Rio de Janeiro | Rooftops stacked three high up to the Redeemer terrace; boss: The Broker |
| Interlude | Desert convoy | A drive in the team's JLTV (skippable) |
| 3. **The Unfinished Tower** | Al Anbar | Girders, welding arcs and steam vents; mid-boss Cantilever, final boss The Architect |

Each chapter has three pieces of intel, timed hazards, checkpoints, a boss and a shared extraction. Finish a chapter to unlock NEXT CHAPTER, or pick any chapter from the course menu (keys **1 / 2 / 3**).

### Original feature: reciprocal partnership

The partner is part of the rules, not a skin:

- **Switch leads** (Tab). The other hero becomes an AI partner with its own position and a flanking behaviour, who waits for live hazards and climbs only when the leader does.
- **Revive, don't respawn.** A downed hero stays down until their partner revives them: free up close (E), or from a distance with a **V assist** that spends a shared *bond* meter (35 bond, 6-second cooldown).
- **Lose together.** The mission fails only when both heroes are down.
- **Win together.** Extraction requires all three pieces of intel, the boss stopped, and **both** heroes at the exit.

## Controls

| Action | Keyboard / mouse | Controller |
|---|---|---|
| Move / change depth lane | A D / W S | Left stick |
| Run | Hold Shift | Hold left-stick click |
| Jump | Space | A (bottom face) |
| Recover intel / revive / extract | E | D-pad Up |
| Punch / fire | J or left mouse (F fires) | RT |
| Kick / reload | K or right mouse / R | X (left face) |
| Guard / aim | L / right mouse when armed | LB |
| Switch lead | Tab | D-pad Left |
| Partner assist (spends bond) | V | D-pad Right |
| Pause / help | P or Esc / H | Start / View |
| Choose chapter (course menu) | 1 / 2 / 3 | Chapter buttons |
| Next chapter / retry | Enter | Select the button |

## Open the project

1. Install **Git LFS** (`git lfs install`), then clone this repository. Art and audio are stored with Git LFS.
2. Open the folder with **Unity 6000.6.0f1** (Unity Hub → Add project from disk).
3. Open `Assets/SF/Scenes/Curitiba.unity` and press Play. The course menu offers **CLASS PLATFORMER** and the three chapters.

The original classroom scene from the Week 4 checkpoint is kept at `Assets/Scenes/SampleScene.unity`.

## How it is built

- **Shared motor.** The class `PlayerMovement` Rigidbody2D motor was extended to take commands from outside, so the player, the AI partner and every enemy use the same movement and jump code. Platforms are one-way `PlatformEffector2D` decks.
- **Game manager.** `SFGame` owns the state machine (menu, playing, paused, won, lost), the partnership rules, hazards on the simulation clock, collectibles, checkpoints and extraction.
- **Data-driven chapters.** `SFCourseChapter` describes each chapter's platforms, hazards, pickups, cast, checkpoints, boss and extraction. `SFChapterLevel` builds whichever chapter is selected.
- **Partner and enemy AI.** Formation following, flanking, tier-to-tier climbing with waypoints, deck guards and a vertical catch-up when a partner is stranded.
- **Presentation.** Hit-stop on melee impacts, boss knockout slow motion, boss title cards and per-chapter atmosphere. All of it respects the *Camera impact motion* setting (reduced motion).
- **Tests.** Opt-in in-player QA suites are launched with `-sf…` arguments: course rules, routes, controls, chapters, flow, chapter select, feel, combat, drive and more. Release 0.8.47-r2 passed 58 native suite runs, 35 editor assertions, 99 art-mapping checks and 8 local browser gates before packaging.

| Folder | Contents |
|---|---|
| `Assets/SF/Runtime` | Game code (C#) |
| `Assets/SF/Editor` | Art importer, build and verification tools |
| `Assets/SF/ArtSource` | Source art with per-file provenance records |
| `Assets/SF/Resources` | Imported sprites, audio and the in-game credits |
| `Assets/SF/AudioLicenses` | Audio licences and source records |
| `Documentation` | [Build notes](Documentation/BUILD-NOTES.md), audio credits and asset provenance records |

To rebuild the imported sprites from `Assets/SF/ArtSource`, use **SF → 1. Import selected art** in the Unity menu. Release builds use **SF → Build Windows** and **SF → Build Web**. See the [build notes](Documentation/BUILD-NOTES.md) for the exact release sources and settings.

## Credits

**Game design and direction:** Rod ([fernando.org](https://www.fernando.org)). Built on Irin Berry's GSD 551 Week 4 platformer checkpoint.

**Development assistance:** AI coding assistants (OpenAI Codex and Anthropic Claude) helped write, test and review the code under Rod's direction. The character and background art was created with AI image-generation tools from Rod's direction and supplied references. Provenance is recorded next to each source file in `Assets/SF/ArtSource`.

**Music:** *Cylinder One*, *Cylinder Seven* and *Cylinder Nine*, written, produced and performed by **Chris Zabriskie**, from the album *Cylinders* (2014), © 2014 Chris Zabriskie. Licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/) ([chriszabriskie.com/cylinders](https://chriszabriskie.com/cylinders/)). Changes: decoded to stereo PCM, peak attenuation, Unity encoding, runtime fades and looping. *Pressure* by yd ([CC0](https://opengameart.org/content/pressure)). The artists do not endorse this game.

**Sound effects:**
- [The Free Firearm Sound Library](https://opengameart.org/content/the-free-firearm-sound-library) (CC0);
- [Impact Sounds](https://kenney.nl/assets/impact-sounds) by Kenney (CC0);
- [Punch](https://opengameart.org/content/punch) by Iwan "qubodup" Gabovitch (CC0);
- [Car engine loop](https://opengameart.org/content/car-engine-loop-96khz-4s) by qubodup ([CC BY 3.0](https://creativecommons.org/licenses/by/3.0/));
- [Car Tire Squeal Skid Loop](https://opengameart.org/content/car-tire-squeal-skid-loop) by audible-edge (Tom Haigh), loop by qubodup (CC BY 3.0);
- [CarDoorSfx](https://opengameart.org/content/cardoorsfx) and [Car Engine Start Up 02](https://opengameart.org/content/car-engine-start-up-02) by looneybits (CC0);
- [Motorbike Idling](https://freesound.org/people/lmbubec/sounds/119455/) by lmbubec (CC0).

Full per-file records are in `Assets/SF/AudioLicenses` and the in-game **MUSIC & CREDITS** screen.

**Tileset:** [Aekiro Platformer Tileset](Assets/Art/Environment/Platformer%20Tileset/readme.txt) (free, no attribution required).

All organisations, groups and characters in the game are fictional. Combat is fictional entertainment, not real-world training.

## License

© 2026 Rod / fernando.org. All rights reserved. Third-party assets listed above keep their own licences.
