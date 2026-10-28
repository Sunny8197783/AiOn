# 〈경보 이전〉 Unity 포트

Three.js 게임(`safe-route/dist/`)을 Unity C#로 옮긴 프로젝트입니다. 장면·프리팹·에셋 없이 코드만으로 게임 전체를 만듭니다.
포팅 규칙과 구조는 `PORTING.md`에 있습니다.

## 열기

1. Unity Hub → **Add project from disk** → 이 `unity/` 폴더 선택 → 에디터 **6000.5.3f1**로 엽니다.
2. 처음 열면 패키지(URP 17.5, Input System 1.19, Newtonsoft JSON, Test Framework)를 받고 컴파일한 뒤, 에디터 스크립트가 한 번만 다음을 만듭니다.
   - `Assets/Scenes/Main.unity`: 빈 장면이며 빌드 목록 맨 앞에 들어갑니다 (`Assets/Editor/ProjectSetup.cs`).
   - Always Included Shaders: URP Lit/Unlit, URP Particles Lit/Unlit, Skybox/Procedural, `Gyeongbo/UnlitNoFog`.
   - `Assets/Settings/URP.asset`, `URP_Renderer.asset`(SSAO 포함), `URP_VolumeProfile.asset`. 이 파이프라인을 Graphics 설정과 모든 Quality 레벨에 지정합니다 (`Assets/Scripts/URP/Editor/UrpSetup.cs`).
   - 이미 만들어져 있으면 아무것도 하지 않습니다. Input System이 백엔드 전환 후 에디터 재시작을 묻는다면 **Yes**를 누르세요 (Old·New·Both 어느 설정이든 동작합니다).
3. `Assets/Scenes/Main.unity`를 열고 **Play**를 누릅니다. 게임은 `Runtime/App/Boot.cs`가 코드로 만들기 때문에 다른 장면에서 Play해도 시작됩니다.

> 아직 Unity 안에서 한 번도 실행하지 않았습니다. 지금까지는 아래 오프라인 검사(컴파일과 로직 테스트)로만 확인했습니다. 처음 열 때 Console에 `[setup]` 경고가 나오는지 확인하세요.

## 흐름

시작 화면 → 새로 시작(체험 모드 선택, 조작법) 또는 이어하기 → 프롤로그 → 준비(prep) → 그날(dday) → 버티기(endure) → 갈림길(crossroads) → 대피(evac) → 생존 기록(ending) → 시작 화면.
레벨은 apartment, town, shelter, mart, store, office 여섯 개입니다. 각 레벨은 `LevelRegistry`에 스스로 등록됩니다.
저장 파일은 `Application.persistentDataPath`의 `save.json`(체크포인트)과 `settings.json`입니다. 장이 시작될 때마다 자동으로 저장됩니다.

## 조작

| 키 | 동작 |
|---|---|
| W A S D · 방향키 | 이동 |
| 마우스 | 둘러보기 (화면을 클릭하면 커서가 잠기고, Esc로 풀림) |
| Shift | 달리기 (기력 소모) |
| Space | 점프 · 낮은 담 넘기 / 오르기 |
| 클릭 · Q | 주먹 (손에 든 게 있으면 던지기) |
| G | 돌·병 줍기 / 던지기 |
| C | 엎드리기·웅크리기 |
| E · Enter | 살피기 · 대화 · 사용 |
| F | 손전등 (정전 후) |
| V | 1인칭 / 3인칭 |
| X | 구급함 |
| B | 분필로 바닥에 적기 (다음 회차에 보임) |
| P / Tab / I / M | 휴대폰 / 수첩 / 소지품 / 지도 |
| H | 조작법 |
| Esc | 일시정지 · 설정 · 저장 · 메인 메뉴 |

## 임시 구현 (`ponytail:` 주석)

Unity를 열지 않고 옮겼기 때문에, 품질 작업에서 바꿀 부분을 코드에 `ponytail:` 주석으로 표시해 두었습니다. 전체 목록은 `grep -rn "ponytail:" Assets`로 볼 수 있습니다. 종류별로 묶으면 다음과 같습니다.

- **렌더링·셰이더**
  - 하늘(Sky shader, 밤하늘, 구름)은 그라디언트 큐브맵으로 대신했습니다.
  - 빠진 기능: eye adaptation, 셰이더 프리컴파일, 저시력 블러(어두운 막으로 대신함), clearcoat, 나무 흔들림(sway) 셰이더, 1인칭 몸 clipping plane, 손전등 빛줄기의 fresnel.
  - `roughnessMap`, `alphaMap`, `polygonOffset` 등 URP Lit에 대응하는 것이 없는 재질 옵션은 무시합니다.
  - 포인트 라이트 감쇠는 역제곱만 씁니다. 그림자 bias는 변환하지 않았습니다.
  - 그레이딩은 URP 기본 override(`UrpPost`)에 맞췄습니다.
- **인스턴싱**: three.js `instanceColor`는 색마다 인스턴스 메시 하나로 나눴습니다 (나무, 상품, 차량, 바리케이드). 쉘터 소품은 복사본으로 구웠습니다. NoteCheck 경로 점은 점마다 GameObject 하나입니다.
- **스프라이트·글자**
  - 월드 글자는 OS 글꼴과 `Paint`(CPU 캔버스)로 그립니다. 품질 작업에서 TMP와 함께 들어갈 한글 SDF 글꼴로 바꿉니다.
  - 그림자 블러, 외곽선 글자, 정확한 글자 메트릭은 근사치입니다.
  - 빌보드는 카메라를 향하는 쿼드입니다. NoteCheck 라벨에는 안개가 적용됩니다.
- **UI (UI Toolkit)**
  - 테마 스타일시트가 없어 모든 컨트롤을 인라인 스타일로 그립니다.
  - `gap`과 `grid`는 margin으로 대신했습니다. vh/vw 값은 한 번만 읽습니다.
  - 점선·이중 테두리는 실선으로, 그라디언트 제목은 흰색으로, 비컨 링은 깜빡임으로 그립니다.
  - 시작 화면 스카이라인은 640px, 약 20fps로 그립니다. 소화기·지도 미니게임 캔버스도 CPU로 그립니다.
  - 필기 애니메이션과 backdrop blur는 없습니다.
- **소리·접근성**
  - 저시력 모드의 음성 읽기는 TTS가 없어 `Debug.Log`로 대신합니다. 스크린리더 대신 숨김 라벨에 내용을 넣습니다.
  - 리버브는 Schroeder 근사입니다.
  - 인쇄(`window.print`)는 `survival-record.txt`를 저장한 뒤 그 파일을 엽니다.
- **게임플레이**
  - 군중이 도망칠 때 직선으로 움직여 벽을 통과합니다 (navmesh 미사용).
  - 폭발 잔해는 저장되지 않습니다. 터치 드래그 시점 조작은 없습니다.
  - 일시정지 → 메인 메뉴는 월드·UI·소리를 정리한 뒤 GameApp을 새로 만듭니다. 이전 장(chapter)의 작업은 멈춘 채 버려집니다.

## 오프라인 검사 (Unity 없이)

저장소 루트에서 실행합니다.

```bash
bash "unity/Tools~/check.sh"
```

설치된 에디터의 DLL을 참조해 csc로 모든 어셈블리를 컴파일하고, .NET에서 Logic 테스트를 돌립니다. 마지막 줄에 `check ok`가 나와야 합니다.
`Assets/Scripts/URP/**`와 `Assets/Scripts/InputSys/**`는 패키지 소스가 Unity 안에만 있어서 이 검사에서 컴파일하지 않습니다.
