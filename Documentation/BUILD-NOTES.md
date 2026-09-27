# Build notes: release 0.8.47-r2

This public repository is a snapshot of the verified **0.8.47-r2** release of *Stefanie & Fernando*. Day-to-day development happened in a private working repository started from the course's Week 4 platformer checkpoint. That repository also holds the internal coordination notes, which are not published here.

## Sources

| Item | Source |
|---|---|
| Game code, scenes, art and audio (this snapshot) | private commit `412cf74`, the Windows release source |
| WebGL player | private commit `8b7051c`: same game code, plus build-time records and a reversible Web compilation fallback in the editor build script |
| Web page template (`Assets/SF/Editor/SFWebShell.html`) | private commit `7f2a853` ("polish1"): load percentage, startup state, a recoverable load-error panel and keyboard hints. The game payload is byte-identical to the 0.8.47-r2 Web release. |

## Settings

- Unity **6000.6.0f1**, Universal Render Pipeline (2D).
- **Build scene:** `Assets/SF/Scenes/Curitiba.unity`, the only scene in the build settings. The class checkpoint scene is kept at `Assets/Scenes/SampleScene.unity`.
- **Windows:** 64-bit player with the full-resolution (native 4K) artwork.
- **WebGL:**
  - IL2CPP with OptimizeSize;
  - WebAssembly code optimisation for disk size, without link-time optimisation;
  - gzip compression **with decompression fallback**, so any static host can serve it without special headers;
  - data caching on.
  
  The release download is about 490 MB (467 MiB), and most of it is the full-quality art.
- **Menu entries** (`Assets/SF/Editor/SFBuild.cs`):
  - **SF → 1. Import selected art** regenerates `Assets/SF/Resources/SF` from `Assets/SF/ArtSource`;
  - **SF → 3. Validate gameplay rules and art** runs the editor checks;
  - **SF → Build Windows** and **SF → Build Web** make release builds.

## Automated checks

The game includes opt-in, in-player test suites. They start from command-line arguments and write results to a `QA` folder next to the project:

| Argument(s) | What it checks |
|---|---|
| `-sfCourseRules` | 131 rules of the Curitiba course (hazards, pickups, revive, extraction, win/lose) |
| `-sfCourse -sfRoute` | Plays the Curitiba course start to finish with normal commands |
| `-sfCourse -sfChapterCheck [-sfChapter 1\|2]` | Chapter data validation and chapter fixtures (Rio, Al Anbar) |
| `-sfChapter 1\|2 -sfRoute` | Route bot plays the Rio / Al Anbar chapter to extraction |
| `-sfChapterFlow` / `-sfChapterSelect` | Chapter progression, JLTV interlude and chapter select |
| `-sfCourse -sfFeel` | Hit-stop, boss cards, atmosphere and enemy variety |
| `-sfSmoke`, `-sfControls`, `-sfGamepad`, `-sfTouch`, `-sfCombat`, `-sfDrive` and more | Smoke run, input, combat, drive and other system suites |
| `-sfNoChapters` | Kill switch: runs the course as the original single chapter |

**Verification of 0.8.47-r2 before release:**
- 58 native suite runs, 35 editor assertions, 99 art-mapping checks and 8 local-browser gates;
- archive and extracted-file hash checks.

The Web "polish1" page was re-checked against the same payload:
- course rules 131/131;
- complete Rio and Al Anbar runs with normal commands (won in 60.6 s and 65.4 s, all three pieces of intel, both heroes);
- a cold load from a plain server that sends no compression headers.

Automated checks complement, but do not replace, playing the game by hand.
