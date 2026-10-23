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
