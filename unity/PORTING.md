# 〈경보 이전〉 Unity port — porting contract

Source of truth: the Three.js game in `safe-route/dist/` (JS) and its tests in `safe-route/tests/`.
This Unity project is a faithful port, written without opening Unity (the dev laptop cannot run it).
The quality pass (real models, baked lighting, art) happens later in Unity; this port keeps the
whole game playable and structured so that pass can swap pieces in.

## Versions and packages
- Unity **6000.5.3f1**, URP 17.5, UI Toolkit (runtime, built in code), Input System 1.19 + legacy input
  (both supported via `GameInput`), Newtonsoft JSON 3.2.2, Test Framework 1.7.
- C# 9 on .NET Standard 2.1. No `record`, no `init`, no file-scoped namespaces, no global usings,
  no C# 10+ features. Namespaces start with `Gyeongbo`.

## Verify without Unity
`bash "unity/Tools~/check.sh"` compiles every assembly with csc against the installed editor's DLLs
and runs the Logic tests on .NET (via `Tools~/nunit/NUnitShim.cs`). It must print `check ok`.
`Assets/Scripts/URP/**` and `Assets/Scripts/InputSys/**` are not compiled by it (package source only
exists inside Unity) — keep those folders small and plain.

## Assemblies (asmdef) and folders
| Assembly | Folder | References |
|---|---|---|
| `Gyeongbo.Logic` (noEngineReferences) | `Assets/Scripts/Logic` | — (pure C#; ports `game/*.js`) |
| `Gyeongbo.Runtime` | `Assets/Scripts/Runtime` | Logic, Newtonsoft (precompiled) |
| `Gyeongbo.URP` | `Assets/Scripts/URP` | Runtime, URP runtime + core runtime |
| `Gyeongbo.InputSys` | `Assets/Scripts/InputSys` | Runtime, Unity.InputSystem |
| `Gyeongbo.Editor` (Editor only) | `Assets/Editor` | Runtime, Logic |
| `Gyeongbo.Tests.EditMode` (Editor only) | `Assets/Tests/EditMode` | Logic, NUnit, TestRunner |

Runtime never references URP or Input System types directly. URP and InputSys register
implementations into Runtime hooks from `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]`
(e.g. `Post.Backend = new UrpPost()`, `GameInput.Backend = new InputSystemBackend()`).
Runtime falls back to a plain implementation (legacy `Input` under `ENABLE_LEGACY_INPUT_MANAGER`,
no post) when no backend registered.

## Boot — no hand-written scenes or assets
- `Assets/Editor` holds a one-time `[InitializeOnLoad]` setup that creates `Assets/Scenes/Main.unity`
  (empty scene + build settings entry) and the URP pipeline asset/renderer/volume profile if missing.
  URP-typed editor code lives in `Assets/Scripts/URP/Editor` (asmdef `Gyeongbo.URP.Editor`).
- `Runtime/App/Boot.cs` creates the whole game from code with
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]`, so pressing Play in any
  scene runs the game. No prefabs, no `.unity`/`.asset` YAML written by hand. Unity generates `.meta`.
- Materials: `Mat.Lit(...)` uses `Shader.Find("Universal Render Pipeline/Lit")`, falling back to
  `"Standard"`. Builds must keep these shaders (the editor setup adds them to Always Included).

## Coordinates (the one rule that must never drift)
JS/Three.js is right-handed; Unity is left-handed. The port **mirrors X**:
- position `(x, y, z)` in JS → `new Vector3(-x, y, z)` in Unity  → use `J.V(x, y, z)`
- yaw `rotation.y = a` (radians) → `Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0)` → `J.Yaw(a)`
- full Euler `rotation.set(x, y, z)` (XYZ order) → `J.Euler(x, y, z)` =
  `AngleAxis(x°, right) * AngleAxis(-y°, up) * AngleAxis(-z°, forward)`
- JS `Math.atan2(dx, dz)` yaw → in Unity space use `Mathf.Atan2(dx, dz)` on Unity vectors, which equals `-jsYaw`.
- JS forward `+z` stays Unity forward `+z`, so `transform.forward`, `LookRotation` work naturally.
- 2D map data (`game/townmap.js` x/z) stays in **JS numbers inside Logic**; convert only at the Unity
  boundary with `J.Map(x, z)` → `new Vector3(-x, 0, z)` and `J.FromUnity(v)` → JS `(x, z)`.
- Custom meshes ported from JS geometry: mirror X of every vertex and normal (`-x`), keep the same
  triangle index order (the mirror and Unity's opposite winding cancel), keep UVs as-is.
- Text and textures must read correctly from the side the JS camera saw them; `Kit` planes and signs
  handle this — always build them through `Kit`, never with `GameObject.CreatePrimitive(Quad)` directly.
Put `J` (static class) in `Runtime/Engine/J.cs`.

## Async story flow
JS `async` story code becomes C# `async Task` / `async Task<T>` on Unity's main thread
(Unity's SynchronizationContext). Frame/time waits: `await Awaitable.NextFrameAsync(ct)`,
`await Awaitable.WaitForSecondsAsync(s, ct)`. UI that resolves on a click returns `Task<T>` backed by
a `TaskCompletionSource<T>`. Soft-fail is `throw new Fail(type, detail)` caught by the chapter runner
(same as JS). Never `async void` except Unity event entry points that catch and log every exception.

## Text and fonts
- UI: UI Toolkit `VisualElement`s built in code (ports of the DOM builders). One `UIDocument` with a
  `PanelSettings` created at runtime. Korean font: `FontAsset.CreateFontAsset(family, "Regular")`
  trying `"Malgun Gothic"`, `"Apple SD Gothic Neo"`, `"Noto Sans CJK KR"`, `"NanumGothic"` in order,
  applied with `style.unityFontDefinition = FontDefinition.FromSDFFont(fa)` at the root.
  CSS (`style.css`, `ui/*.css`) ports to inline styles or a USS string loaded at runtime.
- World text (signs, labels): `Kit` draws the board into a texture and adds the text with a
  `TextMesh` using `Font.CreateDynamicFontFromOSFont(...)` (same family list) or bakes it into the
  texture. `ponytail:` mark it — the quality pass replaces it with TMP + a bundled Korean SDF font.

## Procedural textures
JS draws textures on `<canvas>`. Port drawing code against `Paint` (`Runtime/Engine/Paint.cs`): a
CPU canvas over `Color32[]` with canvas-style coordinates (origin top-left, y down) and the subset the
JS uses (fillRect, strokeRect, circles/arcs, lines, linear/radial gradients, alpha blending, text via
the font atlas or skipped, speckle/noise helpers, `NormalFrom(height)`), then `ToTexture(repeat, sRGB)`.

## Save, settings, audio
- Save/settings: JSON (Newtonsoft) in `Application.persistentDataPath` (`save.json`, `settings.json`),
  same shape and the same validation as JS `readSnap`. localStorage-only conveniences use `PlayerPrefs`.
- Audio: JS synthesises with WebAudio; port to `AudioClip.Create` PCM generation + `AudioSource`s.

## Porting rules
- Port behaviour, data and numbers faithfully — every story line, fact, item, number and branch.
  Korean text is copied verbatim. Facts keep their sources (item 9: no unverified claims).
- Keep each file under 500 lines; split by responsibility, mirroring JS file names
  (`levels/town-park.js` → `Runtime/Levels/Town/TownPark.cs`).
- Leave no `NotImplementedException` in owned files when done. If something truly cannot be ported
  without Unity (e.g. needs an imported asset), implement a working stand-in and mark it `// ponytail:`.
- Edit only the files you own. A needed change to another owner's signature goes in your report as a
  contract request; do not edit their file.
- Run `bash "unity/Tools~/check.sh"` before every commit; commit per finished file group.

## Contract
The C# skeleton is in place: every JS module has its C# file with the public API as stubs
(`NotImplementedException("port: <js file>:<function>")`). Thirteen porters fill them in parallel.

### Groups and files (paths under `Assets/Scripts/`)
| Group | Owns | Ports |
|---|---|---|
| 1 logic | `Logic/*` (State, StateTypes, Items, Facts, Drills, Debrief, Endings, Relay, Routes, TownMap), `Assets/Tests/EditMode/*` | `game/*.js`, `tests/*.test.mjs` |
| 2 engine | `Runtime/Engine/*` (GameEngine=core.js, Kit, Mat, Geo, Geom, Controls+Player, Actions, Sky, Presets, Post, GameInput, Fallbacks; J, Paint, PaintColor, PaintText already written), `URP/**`, `InputSys/**`, `Assets/Editor/**` | `engine/core, kit, controls, actions, geom, sky, presets, post` |
| 3 people | `Runtime/People/*` (Person=people.js, Crowd, PeopleAnim, PeopleBody, PeopleClips, PeopleGeo, PeopleOutfit, PeopleProps) | `engine/people*.js`, `engine/crowd.js` |
| 4 systems | `Runtime/Systems/*` (Fx, GameAudio=audio.js, Cine, Cutscenes, Danger, Echo) | `engine/fx.js`, `audio.js`, `story/cine, cutscenes, danger, echo` |
| 5 apartment | `Runtime/Levels/Apartment/*` | `levels/apartment*.js` |
| 6 townA | `Runtime/Levels/Town/` TownGround, TownBuildings, TownFronts, TownFences, TownPlan, TownUtil, TownPark, TownProps | same-named `levels/town-*.js` |
| 7 townB | `Runtime/Levels/Town/` Town, TownCrowd, TownEvents, TownShelter, TownStrike, TownTraffic | `levels/town.js`, `town-crowd, events, shelter, strike, traffic` |
| 8 shelter | `Runtime/Levels/Shelter/*` (ShelterTex.cs holds `ShelterTextures` + the `ShelterTex` result) | `levels/shelter*.js` |
| 9 commerce | `Runtime/Levels/Commerce/*` (Mart, Store, CommerceGoods, Office) | `levels/mart, store, commerce-goods, office` |
| 10 uiCore | `Runtime/UI/*` except `UI/Minigames` (UiRoot, UiStyle, Dom, MiniDom, Dialog, Hud, Menu, DebriefUi, Phone, Notebook, Planner) | `ui/dom, mini-dom, dialog, hud, menu, debrief, phone, notebook, planner`, `style.css`, `ui/relay.css`, `index.html` |
| 11 uiGames | `Runtime/UI/Minigames/*` (Bag, Compose, Docs, Extinguisher, Grab3d, Radio, RouteMap, MinigameStyle, NoteCheck) | `ui/bag, compose, docs, extinguisher, grab3d, radio, routemap`, `ui/minigames.css`, `notecheck.js` |
| 12 storyA | `Runtime/App/*` (GameApp=app.js, Boot, AppUi), `Runtime/Story/` StoryRuntime, World, Common, Prologue, Prep, PrepActions | `app.js`, `story/runtime, world, common, prologue, prep, prep-actions` |
| 13 storyB | `Runtime/Story/` Dday, Seoyeon, Endure, Evac | `story/dday, seoyeon, endure, evac` |
| shared | `Runtime/Core/*`, every `*.asmdef`, `Tools~/check.sh` (integrator only, frozen) | Level contract, ctx, facades |

Not ported in this pass: `dev.js`, `dev.html`. Not portable to EditMode: `clips.test.mjs`, `echo.test.mjs` (they
test Runtime code the test assembly cannot reference); state.test's JSON parse cases belong to `Core.GameJson`,
so the Logic port tests `State.Load(Game)` / `Validate` / `Game.Clone()` instead.

### What is frozen
- A public member that another group calls is frozen (the Core layer, Engine `GameEngine/Kit/Mat/Geo/Geom/
  Player/Actions/Post/GameInput/J/Paint`, `IPerson` implementations, `Systems` entry points, every level main
  builder and the town A/B APIs, `UiRoot/UiStyle/MiniDom/Hud/Menu/DebriefUi/Dialog/Phone/Notebook/Planner`, the
  minigame `Open`/`Write`/`Shop3d`/`Bag3d`/`NoteCheck` entry points, `StoryRuntime`, `Common`, `PrepActions`,
  `World`, chapter `Run` methods). Need a change? Put a contract request in your report.
- Types and methods used only inside one group (apartment `AptBuilder`, people internals, shelter/commerce
  sub-builders and their result classes) are that group's to reshape. Add files and private helpers freely
  inside your folders (split `GameApp` into `App/*.cs` as it grows).

### Conventions
- Namespaces: `Gyeongbo.Logic`, `.Core`, `.Engine`, `.People`, `.Systems`, `.Levels` (all levels), `.UI` (both UI
  groups), `.Story`, `.App`, `.URP`, `.InputSys`, `.Editor`. No type is named like a namespace segment (that breaks
  name lookup), hence `GameEngine`, `GameApp`, `GameAudio`, `StoryRuntime`, `Person`; `UnityEngine.Rect` clashes, hence `RectXZ`.
- JS module -> static class named after the file; `foo(x)` in `bar.js` -> `Bar.Foo(x)`. A name the class already
  uses becomes `Run` (chapters), `Show` / `Open` (UI functions), `Write` (compose), `SettingsMenu` (menu.settings).
- JS object literals that cross modules (G and its parts, level data, kit `At`/`Xform`, fx options, crowd options,
  UI options/results, Logic results) are classes with public fields named exactly like the JS keys, so keys and
  numbers copy verbatim; C# keywords get `@` (`@checked`, `@short`, `@base`, `@out`, `@unchecked`, `@lock`, `@ref`).
  Behaviour objects use PascalCase members: `ctx.toast()` -> `ctx.Toast()`, `engine.player.x` -> `engine.Player.X`.
  `Paint` keeps the Canvas 2D names (`g.fillStyle = "#3f3e3c"; g.fillRect(...)`).
- JS `undefined` and `null` are both C# `null`. Detail bags (`ctx.event(type, {...})`) are
  `Dictionary<string, object>`; build them with `Details.Of("key", value, ...)`, read with `GameEvent.Str/Num/Bool`.
- Logic numbers are `double` (JS numbers), `int` where JS validates integers (day, minute, money, counts);
  Runtime is `float`; cast at the boundary.
- Coordinates: all gameplay data is JS space (walk, solids, points, spawns, anchors, crowd paths, `Player.X/Y/Z/Yaw`,
  `IPerson.LookAt`, fx options, `Geom`). Only Transforms see Unity space: `J.SetPos/SetYaw/SetRot/Pos/GetYaw`,
  `J.V`, `J.LookAt`; meshes come from `Geo` (built in JS space with three.js parameters, `ToMesh()` mirrors X).
  Level roots sit at the origin; children use local positions (JS `position` is local too).
- Scene graph: `J.Group(name)`, `parent.Add(child, ...)` (extension in `J`), `Geo.Obj(geo, mat)` for `new THREE.Mesh`,
  `Kit.Box/Cyl/Plane/Slab/RBox/Instanced/Merge`. Colours: `J.Hex(0xRRGGBB)`, `J.Css("#rgb" | "rgba(...)")`.
  Materials only through `Mat.Lit/Unlit(MatSpec)` or `Kit.Mat/Surface/Facade`; runtime changes through
  `Mat.SetColor/SetEmissiveIntensity/SetOpacity/SetTiling`. Texture repeat lives on the material (`MatSpec.repeat`).
  Lights through `Kit.PointLight/SpotLight` (three.js -> Unity units in one place) and `Kit.Lit` groups; as in JS
  the light count never changes at runtime. Redrawn canvases: `paint.Upload(texture)` (JS `needsUpdate = true`).
- Async: chapters and UI are `Task` / `Task<T>` on the main thread; UI resolves through a `TaskCompletionSource`;
  waits with `Awaitable.NextFrameAsync` / `WaitForSecondsAsync`. Chapters are `Task<string> Run(Ctx ctx)` and
  resolve `null` except the ending. Soft fail: `throw new Fail(type, detail)` or `ctx.Fail(type, detail)`; inside a
  `Ctx` subclass write `Gyeongbo.Core.Fail` for the type.
- Keys are JS `KeyboardEvent.code` strings ("KeyF", "Escape", "Space") everywhere: `GameInput.Held/Down`,
  `GameEngine.OnKey(KeyPress{ code, key })`.

### Core API (`Runtime/Core`)
- `Ctx` (abstract; `Story/StoryRuntime` implements, `App/GameApp` creates):
  services `engine` (GameEngine), `hud` (Hud), `ui` (IUi), `audio` (IAudio), `settings`, `cine` (Cine), `G`;
  run state `resume, rate, outdoorEnv, indoorEnv, power, levelStates, lightSource, trail, seoyeonAsk,
  speedOverride, profileSpeed, deathCause, roaming, snap`;
  `Refresh, SetLevelState(level, key, value), Goto(level, spawn|Spawn, env), Objective(title, body, steps),
  Toast(text, label, ms), Caption(text, speaker, ms), Event(type, details), Note(text, label, place),
  Payoff(prep, moment), Stat(stat, delta), Pass(minutes), FactLine(id), Modal(fn), Say(speaker, lines, choices, role),
  Card(CardOptions), Skip(from, to, text...), Checkpoint(key), Fail(type, detail), Roam(RoamOptions) -> object,
  Near(level, x, z, r) (a roam tick), TownPlace(id) / TownSpawn(id) (JS ctx.place / ctx.spawn), Load(json),
  Boom(x, z, size), SetPlayer(preset)`. `RoamOptions { on: id | "level:id" | "*" -> handler, doors, deadline,
  rate, tick, shelter }`: a handler or tick returning `null` keeps roaming (JS `undefined`).
- `Level { id, group, env, walk, solids, points, spawns, npcs, states, setState, update(dt, t, FrameCtx), state,
  anchors }`; data types `WalkZone{ x0,x1,z0,z1, y, ramp{ axis, y0, y1 } }`, `Solid{ ..., y0, y1, off, id }`,
  `Point{ id, label, kind, to, x, y?, z, r = 1.6, enabled, desc(ctx) }`, `Spawn{ x, y?, z, yaw? }`,
  `Anchor{ x, y, z, target?, look? }`, `XZ`, `RectXZ`, `FrameCtx{ camera, player, env, power, reduced }`.
- `LevelRegistry.Register(id, LevelBuilder)` / `Has` / `Get` / `ORDER`. Each level's main class registers itself:
  `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Register() =>
  LevelRegistry.Register("mart", BuildMart);` (`LevelBuilder = (Kit, IPeople, IFx) -> Task<Level>`).
- `Sv.Str/Num/StrList/Map/StrMap/IntMap/StrListMap/Plain`: read `setState` values, which are C# values from story
  code or JSON tokens from a loaded save (town `closures`, `gates`, `noticeText`; apartment `stock`; ...).
- Facades: `IPerson` (Group, Mesh, Bone, Height, Seat, Anim, SetAnim, Play, SetProp, LookAt, Update, OnHit,
  Hostile, ...), `IPeople` (CreatePerson, CreateCrowd), `ICrowd` (+ `CrowdOptions`, `CrowdModeOptions`), `IFx`
  (Fire/Smoke/Rain/Dust/ExtinguisherSpray + option classes), `IAudio` (+ `Played` event for the deaf profile's
  cues), `IUi` (+ every UI option/result type in `UiTypes.cs`).
- `Settings` (+ `Load()` / `Save()`, settings.json), `Profile` (experience mode body), `Snapshot` (+ `Read(raw)`:
  readSnap validation), `SaveFile{ index, G, snap }`, `GameJson.Save/Load` (state.js save/load with Newtonsoft;
  loaded detail bags become plain CLR data), `SaveStore.ReadRaw/Write/Clear` (save.json, atomic write),
  `Store.Get/Set` (PlayerPrefs, for localStorage-only conveniences such as echoes), `GameEvents.Ui(name)` /
  `GameEvents.UiSound` (JS 'gyeongbo:ui'), `Fail`.

### How things flow
- Boot: `App/Boot` (AfterSceneLoad) adds `GameApp`, whose `Start` awaits `Run()` (app.js boot) and logs anything that
  escapes. `GameApp` creates `Settings.Load()`, `GameAudio`, `GameEngine.Create`, `UiRoot.Create()`, `Hud.Create`,
  `AppUi`, `StoryRuntime.Create`, `Actions`, `Danger`, `Echo`, `Cine`, then runs `GameApp.CHAPTERS`.
- Settings: the pause menu returns an edited copy -> the app applies it (engine setters, `audio.SetVolume/SetMute/
  SetBus`, `UiRoot.SetTextScale/SetReduceMotion/SetLowVision`) -> `settings.Save()`.
- UI: story calls `ctx.ui.X(...)` (IUi). `App/AppUi` forwards to `Dialog.Talk/Card/TimeSkip`, `Phone.Show/
  EmergencyAlert`, `Notebook.Show/Inventory`, `Planner.Show`, `Grab3d.Shop3d/Bag3d(engine)`, `Radio.Open`,
  `Extinguisher.Open`, `Docs.Open`, `RouteMap.Open`, `DebriefUi.FailCard/ChapterCard/Ending`. Only the app calls
  `Menu.*`; only Seoyeon calls `Compose.*` and `NoteCheck`. Elements are UI Toolkit built by `Dom/MiniDom.El`;
  CSS classes become `UiStyle.Register` rules applied by `UiStyle.AddClass` (runtime cannot parse USS); the
  minigames register theirs in `MinigameStyle`, relay.css rules are registered by `UiStyle.RegisterBase`.
- Sounds: UI code raises `GameEvents.Ui(name)`; the app forwards `GameEvents.UiSound` to `audio.Ui`.
- Save: `persist.checkpoint(key)` = `State.Checkpoint(G, key)` + `ctx.snap = snapshot()` + `SaveStore.Write`;
  `snapshot()` drops non-JSON values (the office dashboard `Paint`). Continue = `raw = SaveStore.ReadRaw()` ->
  `GameJson.Load(raw["G"].ToString())` (throws on a bad save: start fresh) -> `Snapshot.Read(raw["snap"])`.
