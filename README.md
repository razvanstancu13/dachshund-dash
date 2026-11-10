# Dachshund Dash

Side-view 3D endless runner (Unity 6000.5.10f1, URP) - a black & tan dachshund dashing through night streets, alleys
and backyards. All art and audio were made with the local asset pipeline (LAGP, `C:\AI`); picks and sources are
listed at the end.

The repository holds the Unity project only. The design docs (`docs/`: GDD, asset list, reference art, media) and the
helper scripts (`tools/`) are kept locally next to it and are not versioned; the sections below that mention them
apply to a local working copy.

## Play
- **WebGL**: serve `Builds/WebGL` with any static server, e.g.
  `C:\AI\ComfyUI\python_embeded\python.exe -m http.server 8765 --directory Builds/WebGL` -> http://127.0.0.1:8765
- **Windows (test build)**: `Builds/Windows/DachshundDash.exe`
- **Android**: needs the Android Build Support module in Unity Hub; then menu *Dachshund Dash > Build Android*.

## Controls
| | Keyboard | Touch |
|---|---|---|
| Jump / crouch (hold) | Up / Down | right diamond, top / bottom |
| Lane left (far) / right (near) | Left / Right | right diamond, left / right |
| Pause | Esc / P | II button |
| View: side / behind the dog (remembered) | Tab | VIEW button (next to II) |

Power-ups start the moment they are collected; collecting another replaces the running one. The active one is
shown left of the bone counter with a ring that runs down with its time.

Cheats: **Z** invincible, **X** slow, **C** fly, **V** double jump (as if collected), **Backspace** half speed on / off.

## Local scripts (`tools/`, not versioned; each ends with one `OK: ...` / `FAIL: ...` line; the Unity editor must be closed for builds)
```
tools\build.ps1 -Target Android -Publish     # build the APK and put it on the phone link (...:8443/DachshundDash.apk)
tools\build.ps1 -Target WebGL -Publish       # build WebGL and serve it on the phone link
tools\build.ps1 -Target Windows              # test player
tools\serve_phone.ps1 [-Stop]                # serve Builds\WebGL on the tailnet (https://<machine>:8443/)
tools\record_gif.ps1 [-Seed 7 -Seconds 30 -Skip 240 -Clean -NoBuild -Debug Enemies]   # autopilot GIF -> docs\media\gameplay.gif
tools\compare_shots.ps1 -A "<args>" -B "<args>"   # the same moments with two command-line settings, side by side
tools\cutscene\make_cutscene.ps1 -Stage keyframes [-Shots s4_hiss]   # LAGP keyframe candidates; set "pick" in shots.json
tools\cutscene\make_cutscene.ps1 -Stage sfx [-Cues hiss,yelp]         # sound cue candidates; set "pick" in shots.json
tools\cutscene\make_cutscene.ps1 [-Stage all|videos|assemble]        # Wan shots -> StreamingAssets\intro.mp4 + Audio\intro_sfx.ogg
tools\cutscene\make_cutscene.ps1 -Stage videos -Shots s5_flee -Takes 543   # one take at a time (fits a foreground run)
```
The intro cutscene (`IntroCutscene`, shots and prompts in the local `tools/cutscene/shots.json`) plays once per launch before
the menu; any key / click / tap skips it, `-nointro` (and `-ddcapture`) leaves it out.
Build logs go to `Builds\logs\`.

Live editor control: the project has Unity's Pipeline package (`com.unity.pipeline`), so with the editor open the
official Unity CLI drives it, e.g. `unity status`, `unity command` (lists the editor's commands), `unity test .`.

## Build and test (batch mode, by hand)
```
set U="C:\Program Files\Unity\Hub\Editor\6000.5.10f1\Editor\Unity.exe"
%U% -batchmode -projectPath . -executeMethod DachshundDash.EditorTools.ProjectBuilder.Setup -quit
%U% -batchmode -projectPath . -executeMethod DachshundDash.EditorTools.ProjectBuilder.BuildWebGL -quit
%U% -batchmode -projectPath . -executeMethod DachshundDash.EditorTools.ProjectBuilder.BuildWindows -quit
%U% -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/EditMode.xml
%U% -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults Logs/PlayMode.xml
```
`Setup` (also run by every build) makes the template materials, helper textures, the two scenes and the player
settings. Visual check: `DachshundDash.exe -ddcapture <folder>` plays menu, highscores and a long invincible run
through all three areas and saves screenshots. The local `tools/webgl_shot.py` drives the WebGL build in headless
Chrome (type, keys, clicks, screenshots, console log).

## How it is built
- Everything is created in code at runtime: each scene holds one root component (`MenuRoot`, `GameRoot`).
- `Assets/Resources`: `Models/<name>.fbx` + `<name>_tex.png` (the game builds matte Simple Lit materials from
  them; `tools/extract_textures.py` pulls the textures out of the FBX files), `Textures/` (ground, parallax),
  `UI/` (skin, icons, logo, menu background - 9 / 3-slice borders set by `ImportRules`), `Audio/`, `Fonts/`.
- Tunables: `GameConfig` (defaults in code; a `Resources/GameConfig.asset` overrides them).
- Level: hand-made patterns (`Level/Pattern.cs`, 2 m slots x 3 lanes) in 5 difficulty tiers, checked by
  `PatternValidator` (edit-mode test); `Level` spawns them ahead, pools and recycles everything.
- Lane surfaces: each biome stretch picks one surface combination (far, middle, near), e.g. street cobble-asphalt-
  cobble, alley cobble-grass-cobble, backyard grass-grass-grass; a lane that changes surface blends over its first 4 m
  (`trans_<a>_<b>.png`, made by `tools/make_transitions.py`). Surfaces decide content: cars and pipes on asphalt;
  hydrants, dumpsters and cardboard boxes on cobble; bushes, hedges, molehills / moles, play tunnels and fallen trees
  on grass. A mole in a pattern on a non-grass lane becomes a low obstacle (same jump). Extra enemies are scattered
  into free cells (10 % rising to 28 %; cats from 40 m, crows from 80 m) and, in the first 300 m, extra low / crouch props.
- Crouch obstacles (pipe, box, tunnel, fallen tree) are taller than the jump and solid on top: crouch (slide) or switch.
- Rendering: one perspective camera with a narrow lens. The 2D backdrop is flat UI images on a screen-space canvas at
  the far end of its view (screen-aligned, so drawn like an orthographic view; a 1080 px high canvas): the far skyline and the
  per-biome near houses scroll at 5.4 / 48.6 px per metre; the near houses reach about a third up the sky above the
  horizon (where the 3D ground ends on screen) and the far skyline is raised to show above them; the moon (cut out of
  the far painting by the local `tools/split_moon.py`) sits between them and drifts slowly across the
  sky over the run without looping.
- Behind view (`CameraRig.SetView`): a wide lens just above and behind the dog, following its lane and partly its
  height. The near-house paintings become 3D facades lining both sides of the street (right side half a tile on, so
  the sides never mirror); the far skyline and moon fill the sky from just below eye level.
- Lane lines (`LaneLines`): four long glowing strips that follow the dog; the two around the dog's lane glow brighter
  and thicker with light pulses running ahead; upright and screen-sized in the side view, flat in the behind view.
- Readability at night: enemies and bones use the `LitGlow` template (Simple Lit + a little emission) and a soft
  additive halo card behind them (`Art.Halo`). Moles hide under a faint soil decal (`Textures/mole_hint.png`, made
  from a cut-out by `tools/make_decal.py`). Cats come in orange / brown / white coats (`tools/recolor.py`).
- Crows hover over any lane, caw when they spot the dog and lock onto the dog's lane when they dive. Enemies turn to
  face the dog (a charging cat looks where it runs). A crash shakes the camera and flashes a red screen frame.
- Post-processing (`PostFx`, game and menu cameras): a night-film grade - neutral tonemapping, moonlit-blue shadows /
  lamp-warm highlights, bloom on the glowing bits, vignette, fine grain, slight chromatic fringe and a soft far
  background (Gaussian depth of field), SMAA. Moods follow the run: speed (vignette, fringe), Slow (desaturated,
  violet edges), Shield (gold edges, more bloom), Fly (wider lens), crash (red jolt + flash), dying / result
  (drains colour), pause (dims). Phones get a light tier (no grain / depth of field / fringe, FXAA). The base values
  are the constants at the top of `PostFx.cs`; `Resources/PostFx.asset` (made by `Setup`) only keeps the shader
  variants in builds. Command line: `-nopostfx` (off, for comparisons with `tools\compare_shots.ps1`), `-postfxlow`.
- Night air (`AmbientFx`): drifting motes around the view - warm dust in the street, cool ash in the alley,
  blinking fireflies (bright enough to bloom) over the backyards.
- WebGL: lit materials use URP *Simple Lit*; strict shader variant matching is off (a missing variant falls back to
  the nearest one instead of showing pink).

## Asset picks (C:\AI\outputs\dachshund)
Key art `00_keyart/keyart_03`. Models from `07_level` (straightened with `--level base --drop_floating`), rigs in
`08_rig` (dog, cat: quadruped rig; crow: hand-written parts spec `crow_spec.json`, clip Flap; mole: static, animated
in code). 2D from `05_2d` (exact picks: `tools/copy_assets.ps1`), audio version 1 of each, music and
ambience looped in `06_audio/loops`; round 2 (menu theme, jazzy synth game music, crash / caw / jet sounds) in
`09_round2`. Font: Luckiest Guy (Apache 2.0).
