# PRD 08~10 — Fusion Dedicated Lobby 로드맵

| 항목 | 내용 |
| --- | --- |
| 문서 성격 | 상위 로드맵 (PRD 08-1 ~ 10 의 부모 문서) |
| 대상 | Unity 클라이언트 + **신규 Fusion Dedicated Server 빌드** |
| 선행 완료 | 계정 · 캐릭터 저장 PRD 01~07 (`auth-character-roadmap.md`) |
| 관련 문서 | `docs/prd/auth-character-roadmap.md`, `unity/UnderTheSea/GAME_STRUCTURE.md` (2 · 4 · 7 · 9장), `GIT_CONVENTION.md` |

이 문서는 **구현을 포함하지 않는다.** 실제 파일을 읽어 확인한 현재 상태와,
앞으로 6단계로 나눈 작업 단위 · 완료 조건 · QA 절차 · 변경 금지 범위만 담는다.

> **계정 · 캐릭터 저장 로드맵(`auth-character-roadmap.md`)과 분리한 이유**
> 저쪽은 **영구 데이터**(ASP.NET Core + MySQL), 이쪽은 **실시간 월드 상태**(Fusion)다.
> 담당과 검증 방법이 다르고, 두 갈래가 서로를 기다리지 않고 진행되어야 한다.
> 두 문서가 만나는 지점은 **PRD 09-2 와 PRD 10** 뿐이다.

---

## 0. 확정된 목표

1. **`Lobby` 는 여러 유저가 만나는 메인 광장 / 오픈월드다.** 별도 MainWorld 로 넘어가기 전 대기실이 아니다.
2. **첫 로그인 유저가 Host 가 되는 구조를 쓰지 않는다.**
3. 별도로 실행 중인 **Unity Fusion Dedicated Server** 가 Lobby 를 계속 유지한다.
4. 클라이언트는 로그인 후 `ChannelSelect` 에서 채널을 골라 **Fusion `Client`** 로 그 Lobby 세션에 접속한다.
5. **ASP.NET Core + MySQL 은 계정 · 캐릭터 · 외형의 영구 데이터 원본**이다.
6. **Fusion Dedicated Server 는 실시간 월드 상태**(위치 · 이동 · 플레이어 생성 · 외형 상태)를 동기화한다.
7. 최종적으로 Dedicated Server 가 **JWT 와 characterId 를 검증**하고 DB/API 에서 승인된 외형을 얻은 뒤,
   Fusion 으로 외형 상태를 복제한다. (PRD 10)
8. 초기 플레이테스트에서는 **클라이언트의 `CurrentCharacter` 기반 외형 전달을 임시로 허용**한다. (PRD 09-2)
9. **원격 클라이언트가 다른 유저의 외형을 REST API 로 각각 조회하는 구조는 채택하지 않는다.**
   초기안도 최종안도 아니다. (이유는 아래 0-2)
10. **외형 프리팹 자체를 네트워크로 보내지 않는다.** **안정된 파츠 키**와 피부색만 Fusion 상태로
    동기화하고, 적용은 **각 클라이언트의 로컬 카탈로그**가 한다.
    (파츠 키는 현재 프리팹 이름 문자열이다. 숫자 ID 가 아니다 — 0-3 참고)

### 0-1. 두 서버의 역할 경계

```text
┌──────────────────────────────┐        ┌──────────────────────────────┐
│  ASP.NET Core + MySQL        │        │  Fusion Dedicated Server     │
│  (server/AraAtti.Api)        │        │  (Unity 빌드, 신규)          │
├──────────────────────────────┤        ├──────────────────────────────┤
│ 영구 데이터의 원본           │        │ 실시간 월드 상태의 원본      │
│  계정 · 캐릭터 · 외형        │        │  위치 · 이동 · 접속자 목록   │
│  로그인 · JWT 발급           │        │  NetworkPlayer 스폰          │
│  느리고 드문 요청 (REST)     │        │  빠르고 잦은 tick (UDP)      │
└──────────────────────────────┘        └──────────────────────────────┘
                │                                      ▲
                │  PRD 10: 서버가 JWT · characterId 를 │
                └──────────────────────────────────────┘
                   검증하고 승인된 외형을 가져온다
```

**"외형은 어디가 원본인가" 는 단계마다 다르다.** 이것을 헷갈리면 안 된다.

| 단계 | 외형의 출처 | 신뢰 수준 |
| --- | --- | --- |
| PRD 09-2 (초기) | **클라이언트**가 자기 `CurrentCharacter` 를 서버에 알려준다 | 낮음. 플레이테스트 전용 |
| PRD 10 (최종) | **Dedicated Server**가 JWT 로 신원을 확인하고 REST 로 조회한다 | 높음 |

### 0-2. 채택하지 않는 안 — 원격 클라이언트가 각자 REST 조회

각 클라이언트가 "옆 사람이 누구지?" 를 REST 로 물어보는 구조는 **쓰지 않는다.**

| 문제 | 내용 |
| --- | --- |
| 요청 폭발 | 광장에 N 명이면 각자 N-1 번 조회한다. 20명이면 380 요청. 누가 들어올 때마다 반복된다 |
| 인증 경계 붕괴 | 남의 캐릭터를 조회하려면 `GET /api/characters/{id}` 가 **타인 것도 내주어야** 한다. PRD 05 에서 "다른 계정의 캐릭터가 절대 조회되지 않는다" 로 못박은 규칙을 뒤집는다 |
| 타이밍 | Fusion 스폰과 REST 응답이 비동기로 어긋난다. 잠깐 기본 외형으로 보이다 바뀌는 깜빡임이 생긴다 |
| 오프라인 취약 | REST 서버가 죽으면 **이미 접속한 사람들의 외형까지** 안 보인다. Fusion 세션은 멀쩡한데 화면이 깨진다 |

**대신 Fusion 상태로 복제한다.** 외형은 접속 시 1회 정해지고 거의 바뀌지 않으므로
Fusion 의 `[Networked]` 상태에 얹는 것이 가장 싸고, 늦게 들어온 사람도 자동으로 받는다. (0-3)

### 0-3. 외형 동기화 설계 — 실측 근거

`ithappy/Cute_Characters/Prefabs` 의 파츠 프리팹 **380개**를 측정한 결과:

| 항목 | 실측값 |
| --- | --- |
| 최장 프리팹 이름 | **27자** (`Female_Emotion_Surpriced_02`) |
| 슬롯 수 | **6개** (`Face` `Hair` `Shoes` `Top` `Bottom` `Accessory`) |
| 피부색 | `"#RRGGBB"` 문자열 **7자** (팔레트 인덱스가 아니다) |
| 이 Fusion 버전에서 쓸 수 있는 문자열 타입 | `NetworkString<_2 _4 _8 _16 _32 _64 _128 _256 _512>` |
| 서버 컬럼 길이 | `character_parts.prefab_name` = `VARCHAR(64)` |

→ Fusion 전송용으로는 **`NetworkString<_32>` 가 후보다** (27자 + 여유 5자).

> **DB/REST 의 `prefabName` 최대 길이는 64자 그대로 둔다.**
> 앞서 "서버 검증도 32자로 좁히자" 고 적었으나 **그렇게 하지 않는다.**
> 영속 데이터의 제약을 네트워크 전송 사정에 맞춰 좁히면, 나중에 긴 이름이 필요할 때
> 이미 저장된 데이터까지 문제가 된다. 두 계층의 제약은 분리해서 다룬다.
>
> | 계층 | 제약 | 근거 |
> | --- | --- | --- |
> | DB / REST (`prefab_name`) | **64자 유지** | `VARCHAR(64)`, `CharacterEndpoints.MaxPrefabNameLength = 64` |
> | Fusion 전송용 `networkKey` | **32자 이하** | `NetworkString<_32>` |
>
> **지금은 `prefabName` 을 그대로 `networkKey` 로 재사용할 수 있다.** 실측 최장이 27자라
> 별도 키가 필요 없다. 다만 나중에 32자를 넘는 파츠가 필요해지면
> **짧고 안정된 `networkKey` 를 별도로 도입**하면 된다. (예: 슬롯 접두어 + 일련번호)
> 그 판단 시점을 놓치지 않도록 PRD 09-1 카탈로그에 **길이 검증**을 넣는다.

**용어**: 이 문서에서 파츠를 가리키는 값은 **"안정된 파츠 키"** 다.
`"숫자 ID"` 가 아니다. 현재 그 값은 프리팹 이름 문자열이다.

> **파츠를 "배열 인덱스" 로 보내지 않는다.** PRD 01 에서 확인한 이유와 같다 —
> 인덱스는 Inspector 배열 순서라서 에셋을 정렬하면 저장된 모든 외형이 밀린다.
> 프리팹 이름은 순서와 무관하고, 이미 서버 DB 와 Unity 로컬 캐시가 같은 문자열을 쓴다.

---

## 1. 현재 상태 — 실제 파일에서 확인한 것

**결론: Lobby 는 완전히 로컬 씬이다.** Fusion 은 `ServerTestScene` 에만 격리되어 있고,
그 하나마저 `GameMode.AutoHostOrClient`(첫 접속자가 Host) 로 목표와 정반대다.

### 1-1. ChannelSelect → Lobby 는 로컬 씬 전환이다

```text
ChannelSelectController.Join()                          UI/ChannelSelectController.cs:196
  → NetworkServiceLocator.Current.Connect(nickname, serverId)             :219
      = FakeNetworkService.Connect()                    Network/FakeNetworkService.cs:110
        → 코루틴으로 0.6초 대기 후 OnConnectResult(true) 발행 (시뮬레이션)
  → HandleConnectResult(true, "")                                          :222
      → SceneFlow.FromChannelSelect() → SceneManager.LoadScene("Lobby")
```

- `INetworkService` 구현체는 **`FakeNetworkService` 하나뿐**이다.
- `FakeNetworkService.cs` 에 **Fusion 참조가 0건**이다. 채널 목록도 Inspector 하드코딩(`srv-1`, `srv-2`).
- 고른 `serverId` 는 **어디에도 쓰이지 않는다.** Fusion `SessionName` 으로 전달되지 않는다.
- `NetworkRunner.StartGame` 호출은 프로젝트 전체에서 **`FusionLauncher.cs:30` 단 한 곳**이다.

### 1-2. Lobby 씬에는 네트워크 오브젝트가 없다

`Assets/Game/Scenes/Main/CoreGames/Lobby.unity` (21,986,927 bytes)

| 항목 | 값 |
| --- | --- |
| GameObject 66개 / **PrefabInstance 5,530개** | Terrain, Water, GeNa×26, PalmTrees, Rocks, Ruins, Seaweed, OceanCollider, 조명 6개 |
| MonoBehaviour **10개** | `WeatherControl`(Synty), `ThirdPersonCamera`(ithappy), URP `Volume` · `UniversalAdditionalLightData` · `UniversalAdditionalCameraData` |
| `FusionLauncher` / `PlayerSpawner` / `PlayerInputProvider` | **0건** (GUID 검색) |
| `NetworkRunner` / `NetworkObject` / `NetworkSceneManagerDefault` | **0건** |
| `ProximityPortal`, `CaveFirelightFlicker` | 스크립트는 있으나 **씬에 배치되지 않음** |

**캐릭터가 생기는 방식이 코드가 아니다.** `P_JaeYoung.prefab`(GUID `bd936389…`) **PrefabInstance 1개**가
좌표 `(20.84, 1.2, 48.56)` 에 박혀 있고, 그 인스턴스의 `m_Camera` 가 씬 카메라에 연결되어 있다.
`P_JaeYoung` 의 컴포넌트는 `CharacterMover` · `MovePlayerInput` · `FacePicker` — **전부 로컬 스크립트, Fusion 없음.**

→ **스폰 코드가 존재하지 않는다.** Lobby 는 싱글플레이 씬이다.

### 1-3. Fusion 은 ServerTestScene 에만 있다

`Assets/Game/Scenes/Develop/GeonHee/ServerTestScene.unity` — GameObject **3개**

```text
NetworkManager   ← FusionLauncher + PlayerSpawner + NetworkSceneManagerDefault + NetworkRunner
Directional Light
Main Camera
```

```csharp
// Network/FusionLauncher.cs:30  — 프로젝트 유일의 StartGame 호출
GameMode = GameMode.AutoHostOrClient,          // ★ 목표와 정반대
SessionName = roomName,                        // "AraAtti-Test" 하드코딩 (:8)
Scene = SceneRef.FromIndex(buildIndex),        // ★ 빌드마다 인덱스가 다르다
SceneManager = GetComponent<NetworkSceneManagerDefault>()
```

`PlayerSpawner.playerPrefab` → `RawGuidValue: 3d5195ac1bf71d34095b8a2de54a1f57`
= `Game/Prefabs/NetworkTest/PlayerCapsule.prefab` (`NetworkObject` + `NetworkTransform` + `PlayerMovement`).
**원시 캡슐이고, 바닥도 맵도 없다.**

### 1-4. Scene List 가 서버와 클라이언트에서 다르다 — 가장 먼저 풀어야 할 문제

| 프로필 | Scene List |
| --- | --- |
| 클라이언트 (`ProjectSettings/EditorBuildSettings.asset`) | Boot, Title, Login, CharacterCreate, ChannelSelect, **Lobby** (6개). **ServerTestScene 없음** |
| `Assets/Settings/Build Profiles/Windows Server Test.asset`<br>(`m_BuildTarget: 19`=Win64, `m_Subtarget: 2`=**Dedicated Server**, `m_OverrideGlobalSceneList: 1`) | **ServerTestScene 하나뿐. Lobby 없음** |

`SceneRef.FromIndex(...)` 는 이 두 목록에서 **서로 다른 씬을 가리킨다.**
서버에서 ServerTestScene 은 index 0, 클라이언트에서 Lobby 는 index 5 다.
같은 세션에 붙어도 씬이 어긋난다.

**해결책은 확인했다: 이 Fusion 버전에 `SceneRef.FromPath` 가 존재한다.**
(`SceneRef.FromIndex` / `FromPath` / `None` / `RawValue`)

### 1-5. 두 클라이언트는 서로를 볼 수 없다 — 코드로 확정

수동 검증이 필요 없다.

1. 클라이언트 경로(Boot→…→Lobby)에 `NetworkRunner.StartGame` 호출 지점이 **없다.**
   유일한 호출자 `FusionLauncher` 가 클라이언트 Scene List 6개 씬 어디에도 없다.
2. `Lobby.unity` 에 `NetworkObject` 가 0개다. 동기화 대상 자체가 없다.
3. 캐릭터는 씬에 박힌 로컬 인스턴스 1개다.

**현재 실제로 멀티가 되는 유일한 경로**는 `ServerTestScene` 을 두 곳에서 실행하는 것이다.
(`AutoHostOrClient` + `SessionName "AraAtti-Test"` + Photon Cloud AppId `9389d507-…` + `PeerMode: Multiple`)

### 1-6. 재사용할 수 있는 것 / 못 하는 것

| 자산 | 위치 | 판정 |
| --- | --- | --- |
| 서버 권위 스폰 가드 | `PlayerSpawner.cs:16` `if (!Runner.IsServer) return;` | **그대로 사용 가능.** `GameMode.Server` 에서도 동작 |
| 서버 권위 이동 | `PlayerMovement.FixedUpdateNetwork()` `HasStateAuthority` + `GetInput` | **그대로 사용 가능** |
| 입력 전송 | `PlayerInputProvider.OnInput()` (WASD, `Application.isFocused` 가드) | **그대로 사용 가능** |
| 위치 동기화 | `PlayerCapsule.prefab` 의 `Fusion.NetworkTransform` | **그대로 사용 가능** |
| Dedicated Server 타깃 | `Windows Server Test.asset` `m_Subtarget: 2` | **이미 설정됨** |
| Photon Cloud | `PhotonAppSettings.asset` `AppIdFusion` 설정됨, `UseNameServer: 1` | **이미 설정됨** |
| 외형 데이터 공급원 | `ICharacterService.CurrentCharacter`, `CharacterAppearanceStore` | **그대로 사용 가능** (PRD 07 완료) |
| 파츠 카탈로그 데이터 | `Game/Prefabs/Characters/CharacterCustomization.prefab` (568KB, `catalogParts` **378개**, GUID 참조 1,633건) | 데이터는 유효하나 **UI 프리팹 안에 갇혀 있음** → PRD 09-1 에서 분리 |
| 파츠 적용 로직 | `Character/CharacterCustomizationCatalogue.cs` `TryApplyCatalogPart()` | **분리 가능성 높음.** 의존이 `slotBindings` + `equippedObjects` + `ApplySkinMaterial` 뿐이고 UI 를 직접 만지지 않음 |
| **커마 컨트롤러** | `CharacterCustomizationController.Awake()` | **재사용 불가.** `categoryButtons[i]` · `optionButtons[i]` · `previousPageButton` · `nextPageButton` · `completeButton` · `nicknameInput` 에 **null 체크 없이** 접근(`:88~102`). NetworkPlayer 에 붙이면 즉시 NRE |

---

## 2. 단계 개요와 의존 관계

| PRD | 제목 | 범위 | 서버 exe 필요 | 상태 | 브랜치(권장) |
| --- | --- | --- | --- | --- | --- |
| **08-1** | Dedicated Server 기반 검증 | Unity(테스트 씬) + Build Profile | ✓ | **완료** | `feature/gh-fusion-dedicated-server` |
| **08-2** | Lobby 네트워크 씬 전환 | Unity(Lobby 씬 · 프리팹) | ✓ | **완료** (`36e77fe`) | `feature/fusion-lobby` |
| **08-3** | ChannelSelect 실제 세션 연결 | Unity(Network 경계) | ✓ | **완료** (2026-09-13) | `feature/fusion-lobby` |
| **09-1** | 외형 카탈로그 분리 · 안정 ID | Unity(Character 폴더) | ✗ | 미착수 | `feature/sy-appearance-catalog` |
| **09-2** | NetworkPlayer 외형 복제 | Unity(NetworkPlayer) | ✓ | 미착수 | `feature/gh-appearance-replication` |
| **10** | 서버의 JWT · characterId 검증 | Unity 서버 빌드 + ASP.NET | ✓ | 미착수 | `feature/gh-server-appearance-authority` |

> 08-2 · 08-3 은 계획과 달리 **브랜치를 나누지 않고 `feature/fusion-lobby` 한 곳에서** 진행했습니다.
> 08-2 의 Lobby 네트워크 씬 위에서 곧바로 08-3 을 붙이는 편이 검증이 쉬웠기 때문입니다.

```text
08-1 ──▶ 08-2 ──▶ 08-3 ──────────────▶ 09-2 ──▶ 10
                        09-1 ─────────────┘
```

**09-1 은 08 계열과 병행할 수 있다.** 서버 없이 진행되고 담당도 다르다.
(`GAME_STRUCTURE.md` 12장 "아무도 서로를 기다리지 않는다")

**08-1 을 가장 먼저 하는 이유**: Scene 식별 문제(1-4)가 풀리지 않으면 08-2 이후 전부가
"왜 붙었는데 아무것도 안 보이지" 로 시간을 잡아먹는다. 가장 작은 변경으로 가장 큰 위험을 없앤다.

---

## 3. PRD 08-1 — Dedicated Server 기반 검증

### 목적과 범위

`ServerTestScene` 에서 **Host 없는 `GameMode.Server` 프로세스 1개 + `GameMode.Client` 2개**가
같은 세션에 붙어 캡슐이 서로 보이는 것을 확인한다. **Lobby 는 아직 건드리지 않는다.**
동시에 `SceneRef` 식별 방식을 인덱스에서 경로 기반으로 바꿔 1-4 문제를 없앤다.

### 수정 예상 파일 / 씬 / 프리팹

| 대상 | 변경 |
| --- | --- |
| `Assets/Game/Scripts/Network/FusionLauncher.cs` | `GameMode` 를 하드코딩에서 **모드 선택**으로. `SceneRef.FromIndex` → `SceneRef.FromPath` |
| (신규) `Assets/Game/Scripts/Network/FusionServerBootstrap.cs` | 서버 빌드에서 자동으로 `GameMode.Server` 로 기동. 커맨드라인 인자(`-session`, `-port`) 파싱 |
| `Assets/Settings/Build Profiles/Windows Server Test.asset` | Scene List 유지(ServerTestScene). **08-2 에서 Lobby 를 추가한다** |
| `Assets/Game/Scenes/Develop/GeonHee/ServerTestScene.unity` | `NetworkManager` 에 서버 부트스트랩 배치 |
| (조건부 신규) `Assets/Settings/Build Profiles/Fusion Client Test.asset` | **아래 "먼저 정할 것" 에서 A 안을 고른 경우에만** 만든다. `ServerTestScene` 을 포함하는 **테스트 전용** 클라이언트 프로필. 이번 문서 단계에서는 **만들지 않는다** |

참고 패턴은 Photon 공식 샘플에 있다 — `Photon/Fusion/Runtime/FusionBootstrap.cs:324`
`StartCoroutine(StartWithClients(GameMode.Server, sceneRef, 0))` 가 **로컬 클라이언트 0개인 서버**,
`:535` `InitializeNetworkRunner(_server, serverMode, NetAddress.Any(ServerPort), sceneRef, …)` 가 주소 지정 방식이다.

### ⚠ 먼저 정할 것 — 클라이언트 QA 를 어떻게 할 것인가

**클라이언트 Scene List 에 `ServerTestScene` 이 없다.** (1-4)
그래서 "클라이언트 exe 2개" QA 를 **지금 그대로는 할 수 없다.**
일반 클라이언트 `EditorBuildSettings.asset` 은 **건드리지 않는다** — 거기에 테스트 씬을 넣으면
제품 빌드에 개발용 씬이 섞인다.

**구현 시작 전에 아래 둘 중 하나를 고른다.**

| | A안 — 테스트 전용 Client Build Profile | B안 — 에디터 멀티피어만 사용 |
| --- | --- | --- |
| 방법 | `Fusion Client Test.asset` 을 새로 만들고 `m_OverrideGlobalSceneList: 1` + `ServerTestScene` 하나만 넣는다 | 서버는 exe, 클라이언트는 **에디터 Play** 로만 검증 |
| 근거 | `Windows Server Test.asset` 이 이미 같은 방식(`m_OverrideGlobalSceneList: 1`)으로 동작한다 | `NetworkProjectConfig.fusion` 의 `PeerMode: Multiple` 이라 한 에디터에서 여러 Runner 가능 |
| 장점 | **실제 빌드 환경**에서 검증된다. 클라이언트 2개를 진짜로 띄운다 | 추가 파일이 없다. 빌드 시간이 없다 |
| 단점 | 프로필 파일이 하나 늘어난다. 테스트용임을 이름으로 구분해야 한다 | 에디터에서만 되고 빌드에서 깨지는 문제를 못 잡는다. 창을 2개 띄우기 번거롭다 |
| 완료 조건 영향 | "클라이언트 exe 2개" 를 **그대로 유지**할 수 있다 | 완료 조건을 "에디터 피어 2개" 로 **낮춰야** 한다 |

> **"클라이언트 exe 2개" 를 완료 조건으로 유지하려면 A안이 필요하다.**
> 별도 Client Build Profile 없이는 그 조건을 만족시킬 방법이 없다.
> 반대로 B안을 고르면 아래 완료 조건과 QA 절차의 "exe 2개" 를 "에디터 피어 2개" 로 바꿔 적어야 한다.
>
> **이 문서에서는 결정만 기록하고 프로필 파일을 만들지 않는다.**
> PRD 08-1 세부 문서를 쓸 때 팀이 고른 안을 첫 줄에 적고 시작한다.

권장: **A안**. `-batchmode -nographics` 서버와 실제 클라이언트 exe 의 조합은
에디터에서 재현되지 않는 문제(빌드 전용 코드 스트리핑, 씬 인덱스, 경로 대소문자)를 잡아준다.
공통 규칙 6번("에디터에서만 되는 것은 Dedicated Server 가 아니다")과도 맞는다.

### 건드리면 안 되는 범위

- **일반 클라이언트 `EditorBuildSettings.asset` — 절대 건드리지 않는다.**
  테스트 씬을 제품 씬 목록에 넣지 않는다. 필요하면 위 A안의 **별도 프로필**로 푼다.
- `Lobby.unity` — 08-2 의 일이다.
- `PlayerSpawner.cs` · `PlayerMovement.cs` · `PlayerInputProvider.cs` · `NetworkInputData.cs` —
  **이미 서버 권위 구조다.** 고칠 이유가 없다. (1-6)
- `PlayerCapsule.prefab` — 이 단계의 검증 대상이므로 그대로 둔다.
- `INetworkService` · `FakeNetworkService` · `NetworkServiceLocator` — 08-3 의 일이다.
- `Account/` 폴더 전체, `server/` 전체.

### 담당 / 사전 조율

| 대상 | 담당 | 사유 |
| --- | --- | --- |
| `Network/` 폴더 | **건희** | `GAME_STRUCTURE.md` 9장 폴더 소유권 |
| `ServerTestScene.unity` | **건희** | `Scenes/Develop/GeonHee/` 본인 씬 |
| `Windows Server Test.asset` | **건희** | 서버 빌드 프로필 |

**조율 불필요.** 이 단계는 전부 건희 담당 범위 안이다.

### 완료 조건

- [ ] 서버 exe 가 `GameMode.Server` 로 기동하고, 로컬 플레이어를 스폰하지 **않는다.**
- [ ] **QA 경로(A안/B안)를 먼저 정했고, 세부 문서 첫 줄에 적었다.**
- [ ] 클라이언트 2개가 `GameMode.Client` 로 같은 `SessionName` 에 붙는다.
      (A안이면 `Fusion Client Test` 프로필로 빌드한 exe 2개, B안이면 에디터 피어 2개)
- [ ] 각 클라이언트에 **캡슐 2개**가 보이고, 한쪽에서 WASD 로 움직이면 다른 쪽에서도 움직인다.
- [ ] 서버 프로세스를 끄면 두 클라이언트가 함께 끊긴다. (Host 가 없다는 증거)
- [ ] `SceneRef.FromIndex` 가 코드에서 사라지고 `FromPath` 로 대체되었다.
- [ ] 서버 콘솔에 `OnPlayerJoined` 가 클라이언트 수만큼 찍힌다.
- [ ] 클라이언트를 하나 껐다 켜도 나머지 하나는 유지된다.

### Unity / Fusion QA 절차

1. `Windows Server Test` 프로필로 빌드 → `AraAtti-Server.exe` 생성
2. 콘솔에서 `AraAtti-Server.exe -batchmode -nographics -session lobby-ch1` 실행
   → 콘솔 로그에 `Server started` 와 세션 이름이 보이는지
3. 클라이언트 2개를 띄운다. **고른 안에 따라 방법이 다르다.**
   - **A안**: `Fusion Client Test` 프로필(= `ServerTestScene` 포함)로 빌드한 exe 를 2개 실행
   - **B안**: 에디터 Play 로 1개 + 같은 에디터에서 피어 1개 추가
     (`NetworkProjectConfig.fusion` 의 `PeerMode: Multiple` 이라 가능)
   - ⚠ **일반 클라이언트 프로필로는 이 단계를 할 수 없다.** 그 Scene List 에 `ServerTestScene` 이 없다
4. 두 클라이언트 화면에 **캡슐 2개**가 보이는지 확인
5. 한쪽에서 WASD → 다른 쪽 화면에서도 움직이는지 (`Application.isFocused` 가드 때문에 창을 클릭해 포커스를 줘야 한다)
6. 서버 exe 를 Ctrl+C → 두 클라이언트가 동시에 끊기는지
7. **역방향 확인**: 서버를 켜지 않고 클라이언트만 실행 → 접속 실패 로그가 나오고 멈추지 않는지

### 권장 커밋 단위

```text
[refactor] Fusion 씬 식별을 경로 기반으로 변경
[feat] Dedicated Server 모드 기동 추가
[chore] 서버 빌드 프로필 정리
```

### 다음 단계로 넘길 결과물

- 동작하는 서버 exe 실행 명령과 인자 규격 (`-session`, `-port`)
- `SceneRef.FromPath` 로 정리된 `FusionLauncher`
- Server / Client 모드 분기 지점 (08-2 가 Lobby 씬에 그대로 붙일 수 있는 형태)
- **확정된 클라이언트 QA 경로**(A안/B안). 08-2 이후 모든 단계가 이 방식을 그대로 쓴다.
  A안을 골랐다면 `Fusion Client Test` 프로필도 함께 넘긴다

---

## 4. PRD 08-2 — Lobby 네트워크 씬 전환

### 목적과 범위

Dedicated Server 가 **`Lobby` 씬을 로드해 유지**하고, 접속한 클라이언트마다
`NetworkPlayer` 를 스폰해 위치를 동기화한다. **외형은 아직 기본 모델 하나로 통일한다.** (09-2 의 일)

### 수정 예상 파일 / 씬 / 프리팹

| 대상 | 변경 |
| --- | --- |
| `Assets/Game/Scenes/Main/CoreGames/Lobby.unity` | `NetworkRunner` + `NetworkSceneManagerDefault` + `PlayerSpawner` 배치. **박혀 있는 `P_JaeYoung` 인스턴스 1개 제거**. 스폰 포인트 배치 |
| (신규) `Assets/Game/Prefabs/Characters/NetworkPlayer.prefab` | `NetworkObject` + `NetworkTransform` + 이동 + 캐릭터 모델 |
| `Assets/Settings/Build Profiles/Windows Server Test.asset` | Scene List 에 **Lobby 추가** |
| `ProjectSettings/EditorBuildSettings.asset` | 서버와 경로가 어긋나지 않게 확인 (`FromPath` 를 쓰면 인덱스는 무관) |
| `Assets/Game/Scripts/Network/PlayerSpawner.cs` | `playerPrefab` 을 `NetworkPlayer.prefab` 으로. 스폰 위치를 하드코딩(`player.PlayerId * 3f`)에서 스폰 포인트로 |

> **`P_JaeYoung` 을 그대로 쓸 수 없는 이유**: `NetworkObject` 가 없고, `MovePlayerInput` 이
> 로컬 입력을 직접 읽는다. Fusion 은 입력을 `OnInput` → `GetInput` 으로 받아야 하므로
> 두 입력 경로가 충돌한다. `NetworkPlayer.prefab` 은 새로 만들고 **모델만 재사용**한다.

### 건드리면 안 되는 범위

- **`Lobby.unity` 의 환경 오브젝트** — Terrain · Water · GeNa · 조명 · 프리팹 인스턴스 5,530개.
  네트워크 오브젝트 추가와 캐릭터 인스턴스 제거 **외에는 손대지 않는다.**
- `P_JaeYoung.prefab` 자체 — 씬에서 인스턴스만 빼고 **프리팹은 남긴다.** 다른 곳에서 쓸 수 있다.
- `CharacterCustomization.prefab`, `Character/` 폴더 — 09-1 의 일이다.
- `INetworkService` 계열 — 08-3 의 일이다.
- `Account/` 폴더, `server/` 전체.
- `ChannelSelectController`, `SceneFlow` — 08-3 까지 손대지 않는다.

### 담당 / 사전 조율 ⚠ 이 단계가 조율이 가장 많다

| 대상 | 담당 | 반드시 상의할 내용 |
| --- | --- | --- |
| **`Lobby.unity`** | **효진** | 22MB · 프리팹 5,530개. `GIT_CONVENTION.md` 9장 "같은 Scene 동시 수정 금지". **같은 시간에 둘이 열면 병합이 불가능하다.** 작업 창을 정하고 한 명만 연다 |
| 스폰 포인트 위치 | **효진** | 지형 높이를 아는 사람이 정해야 한다. 현재 캐릭터가 `(20.84, 1.2, 48.56)` 에 있다 |
| `ThirdPersonCamera` 연결 | **효진 ↔ 건희** | 지금은 씬 카메라가 박힌 캐릭터를 따라간다. 스폰된 `NetworkPlayer` 중 **내 것만** 따라가도록 바꿔야 한다 (`HasInputAuthority`) |
| 캐릭터 모델 선택 | **서연** | `NetworkPlayer.prefab` 에 어떤 모델을 넣을지. 09-1 의 `slotBindings` 와 맞아야 한다 |
| 서버 빌드에 Lobby 포함 | **건희** | 22MB 환경 씬을 서버가 로드한다. 서버용 경량 콜라이더 씬 분리를 **검토만** 하고 이번엔 그대로 간다 |

### 완료 조건

- [ ] 서버 exe 가 `Lobby` 씬을 로드하고 유지한다. (`-nographics` 로도 동작)
- [ ] 클라이언트 2개가 접속하면 각자 화면에 **캐릭터 2개**가 보인다.
- [ ] 이동이 서로에게 보인다. 지형 콜라이더 위를 걷는다.
- [ ] 카메라가 **자기 캐릭터만** 따라간다. (남의 캐릭터를 따라가지 않는다)
- [ ] 클라이언트 하나가 나가면 그 캐릭터만 사라지고 나머지는 유지된다.
- [ ] 씬에 박힌 `P_JaeYoung` 인스턴스가 없다. (로컬 캐릭터가 중복으로 보이지 않는다)
- [ ] 서버 콘솔에 Unity 에러가 없다. (렌더링 관련 경고는 허용)

### Unity / Fusion QA 절차

1. 서버 exe 를 `-batchmode -nographics -session lobby-ch1` 로 실행 → Lobby 로드 로그 확인
2. 클라이언트 A 실행 → 로그인 → ChannelSelect → 입장 → **자기 캐릭터 1개**만 보이는지
3. 클라이언트 B 실행 → 같은 채널 입장 → **A · B 화면 모두 캐릭터 2개**
4. A 에서 WASD 이동 → **B 화면에서 A 가 움직이는지**
5. A 의 카메라가 A 캐릭터를 따라가고, B 캐릭터를 따라가지 않는지
6. B 종료 → A 화면에서 B 캐릭터만 사라지는지
7. 서버 종료 → A 도 끊기는지, 에러 로그로 원인이 보이는지
8. **지형 확인**: 캐릭터가 물 위/지형 아래로 빠지지 않는지 (스폰 포인트 y 값 검증)

### 실행 방법 — 서버 1개 + 클라이언트 2개

**1) 빌드.** 두 빌드는 서브타깃이 다르므로 각각 따로 만든다.
`-standaloneBuildSubtarget` 을 반드시 붙인다. 서브타깃을 실행 도중에 바꾸면
스크립팅 정의가 달라져 도메인 리로드가 `-executeMethod` 를 끊는다.

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe"
$proj  = "C:\geonhee\UnderTheSea\unity\UnderTheSea"

& $unity -batchmode -quit -nographics -projectPath $proj -standaloneBuildSubtarget Server `
  -executeMethod UnderTheSea.Network.Editor.FusionTestBuilds.BuildServerFromCommandLine
& $unity -batchmode -quit -nographics -projectPath $proj -standaloneBuildSubtarget Player `
  -executeMethod UnderTheSea.Network.Editor.FusionTestBuilds.BuildClientFromCommandLine
```

에디터에서는 메뉴로도 된다.
`Tools > 아라아띠 > Fusion 서버 빌드 (Dedicated Server)` / `Fusion 클라이언트 테스트 빌드`

> ⚠ 사전 조건: Unity Hub 에서 **Windows Dedicated Server Build Support** 모듈이 깔려 있어야 한다.
> 없으면 Unity 가 **경고 없이** 일반 플레이어로 대체 빌드하고 `UNITY_SERVER` 분기가 죽는다.

> ⚠ 빌드 대상 씬은 `FusionTestBuilds.cs` 의 `TestScenePath` 상수 하나가 정한다.
> `Assets/Settings/Build Profiles/*.asset` 의 Scene List 는 이 스크립트가 읽지 않는다.
> 그 프로필들은 사람이 Build Profiles 창에서 직접 빌드할 때만 쓰인다.
> (`Windows Server Test.asset` 은 이름과 달리 Dedicated Server 프로필이 아니다.
> `m_Subtarget: 2` 가 붙어 있지만 `m_PlatformId` 가 일반 Windows Player 와 같다)

**2) 실행.** 서버를 먼저 띄우고 클라이언트를 붙인다.

```powershell
$b = "C:\geonhee\UnderTheSea\unity\UnderTheSea\Builds"

# 서버 1개 — -mode 를 주지 않는다. UNITY_SERVER 로 자동으로 Server 가 된다.
& "$b\Server\AraAtti-Server.exe" -batchmode -nographics -session lobby-ch1 -port 27015 -logFile server.log

# 클라이언트 2개 — 같은 -session 으로 붙는다.
& "$b\Client\AraAtti-Client.exe" -mode client -session lobby-ch1 -screen-width 900 -screen-height 520 -screen-fullscreen 0 -logFile c1.log
& "$b\Client\AraAtti-Client.exe" -mode client -session lobby-ch1 -screen-width 900 -screen-height 520 -screen-fullscreen 0 -logFile c2.log
```

인자는 `FusionLaunchArguments` 가 읽는다.

| 인자 | 뜻 | 기본값 |
| --- | --- | --- |
| `-session <이름>` | 붙을 방 이름. 채널 하나가 세션 하나 | FusionLauncher 의 Inspector 값 |
| `-port <번호>` | 서버가 열 포트 | 27015 |
| `-mode server\|client\|autohostorclient` | 기동 모드 강제. 빌드 종류를 이긴다 | 빌드 종류로 자동 판정 |
| `-devjoin` | 개발용 직접 접속 (Development Build 전용) | 꺼짐 |
| `-logmoves` | 위치를 0.5초마다 로그로 남긴다. 동기화 확인용 | 꺼짐 |
| `-appver <이름>` | Photon AppVersion. **같은 값을 준 사람하고만 만난다** | 없음(팀 공용) |

#### `-appver` — 사람마다 방을 갈라놓기

Photon 의 방은 `AppId + AppVersion + 지역` 안에서 **전 세계가 공유한다.** 팀이 AppId 하나를
같이 쓰므로, 세션 이름이 같으면 옆자리 사람의 서버와 내 클라이언트가 그냥 만난다.

실제로 이런 일이 있었다. 팀원이 먼저 `warriors-1` 로 DS 를 띄운 상태에서
내 DS 는 `GameIdAlreadyExists` 로 방을 못 열었고, 내 클라이언트는 조용히 **팀원의 서버**에
붙었다. 빌드가 서로 달라 네트워크 값 배치가 어긋났고 클라이언트에서 이렇게 터졌다.

    AssertException: meta.WordCount == NetworkObject.GetWordCount(instance)

화면에서는 몬스터가 투명하고, 처치 수가 안 오르고, 한 명인데 판이 시작했다.
**어디에도 "남의 서버에 붙었다" 는 말은 나오지 않는다.**

`-appver` 에 값을 주면 Photon 이 AppId 를 값마다 서로 다른 *가상 AppId* 로 갈라 놓는다.
값이 다른 사람끼리는 방 목록조차 보이지 않으므로 세션 이름이 같아도 부딪히지 않는다.

    AraAtti-Server.exe -batchmode -nographics -session warriors-1 -port 27017 -appver geonhee
    AraAtti-Client.exe -mode client -session lobby-ch1 -appver geonhee

- **서버와 클라이언트에 같은 값을 줘야 만난다.** 한쪽만 주면 서로 못 본다. 고장이 아니다.
- **인자를 주지 않으면 지금까지와 똑같다.** 팀 전체가 같은 방을 본다.
- 공용 `PhotonAppSettings.asset` 은 고치지 않는다. 복사본에만 값을 넣어 그 판에만 쓴다.
- 접속 직전 로그에 어느 쪽인지 항상 남는다.

      [FusionSessionIsolation] AppVersion "geonhee" — 같은 값을 준 사람하고만 만납니다.
      [FusionSessionIsolation] AppVersion 없음 — 팀 공용입니다. 세션 이름이 겹치면 남의 서버에 붙을 수 있습니다.

### 개발용 직접 Lobby 실행 경로

로그인 · REST · DB 를 건너뛰고 멀티플레이만 빠르게 보고 싶을 때 쓴다.
**로컬 씬만 여는 방식이 아니다.** 이 경로도 Fusion `Client` 로 Dedicated Server 에 붙고
캐릭터는 서버가 스폰한다. 일반 사용자 경로와 네트워크 구조가 같고 앞단 UI 만 건너뛴다.

**에디터에서**
1. 서버 exe 를 먼저 띄운다 (위 실행 명령)
2. `Lobby.unity` 를 열고 Play

> 2026-09-13 갱신: 예전에는 `Tools > 아라아띠 > 개발용 Lobby 접속` 메뉴를 켜야 했다.
> **그 메뉴는 없앴다.** 에디터 Play 는 언제나 `GameMode.Client` 다. 아래 08-3 "구현 결과" 참고.

**Development Build 에서**
```powershell
& "$b\Client\AraAtti-Client.exe" -devjoin -session lobby-ch1
```

끄고 켤 것이 없다. 에디터는 언제나 Dedicated Server 에 붙는다.

> Release 빌드에는 이 우회 경로가 **아예 컴파일되지 않는다.**
> `FusionDevEntry.WantsClientJoin` 이 `UNITY_EDITOR` · `DEVELOPMENT_BUILD` 밖에서는 항상 `false` 다.
> 임시 신원이나 기본 외형을 PlayerPrefs · REST · MySQL 에 쓰지도 읽지도 않는다.

### 권장 커밋 단위

```text
[feat] NetworkPlayer 프리팹 추가
[feat] Lobby 씬에 Fusion 러너 배치
[refactor] 스폰 위치를 스폰 포인트 기반으로 변경
[chore] 서버 빌드 프로필에 Lobby 씬 추가
```

### 다음 단계로 넘길 결과물

- `NetworkPlayer.prefab` (외형 파츠를 붙일 자리를 갖춘 형태 — 09-1 과 규격 합의 필요)
- Lobby 씬의 스폰 포인트 규격
- 서버가 Lobby 를 유지한다는 검증된 실행 명령

---

## 5. PRD 08-3 — ChannelSelect 실제 Fusion 세션 연결

### 목적과 범위

`FakeNetworkService` 를 **실제 Fusion 구현체**로 교체해, ChannelSelect 에서 고른 채널이
그 채널의 Lobby 세션으로 이어지게 한다. **화면 코드는 고치지 않는다.**
(PRD 06 에서 계정 서비스를 Fake→HTTP 로 갈아끼울 때와 같은 방식)

### 수정 예상 파일 / 씬 / 프리팹

| 대상 | 변경 |
| --- | --- |
| (신규) `Assets/Game/Scripts/Network/FusionNetworkService.cs` | `INetworkService` 실제 구현. `Connect(nickname, serverId)` → `runner.StartGame(GameMode.Client, SessionName: 매핑결과)` |
| (신규) `Assets/Game/Scripts/Network/ChannelCatalog.cs` | 채널 ID ↔ `SessionName` 매핑. `srv-1` → `lobby-ch1` |
| `Assets/Game/Scenes/Main/CoreGames/Boot.unity` | `FakeNetworkService` 컴포넌트를 실제 구현으로 교체 |
| `Assets/Game/Scripts/Network/INetworkService.cs` | **가능하면 변경하지 않는다.** 아래 판단 참고 |

> 위는 **착수 시점의 계획**입니다. 실제로 손댄 파일은 이보다 많습니다.
> 확정된 목록은 아래 **"구현 결과"** 를 보세요.
> 특히 `Boot.unity` 는 "컴포넌트 교체" 가 아니라 **"컴포넌트 제거"** 로 끝났습니다.

### `INetworkService` 공동 계약을 바꿔야 하는가 — 판단

**결론: 이번 단계에서는 바꾸지 않아도 된다.** 현재 시그니처로 충분하다.

| 기존 멤버 | Fusion 매핑 |
| --- | --- |
| `RequestServerList()` / `OnServerListUpdated` | `runner.JoinSessionLobby()` + `OnSessionListUpdated` 콜백 → `ServerInfo[]` 로 변환 |
| `Connect(nickname, serverId)` | `serverId` → `SessionName` 매핑 후 `StartGame(GameMode.Client, …)` |
| `OnConnectResult(bool, string)` | `StartGameResult.Ok` / `ShutdownReason` |
| `OnLobbyPlayerCountChanged(int)` | `OnPlayerJoined` / `OnPlayerLeft` 에서 `runner.ActivePlayers` 개수 |
| `Disconnect()` | `runner.Shutdown()` |

> ⚠ **`Connect` 에 characterId 를 추가하고 싶어질 것이다. 이번엔 하지 않는다.**
> `GAME_STRUCTURE.md` 4장이 이 파일을 **클라이언트↔서버 공동 합의 파일**로 지정했고,
> PRD 07 의 변경 금지 범위에도 "`Connect` 에 캐릭터 ID 를 추가하지 않는다" 가 있다.
> characterId 는 **Fusion `ConnectionToken`** 으로 보내는 것이 유력하다. (PRD 10)
> 단 그 방식은 **크기 검증 전까지 후보**다. (7장 "선행 기술 검증" 참고)
> 계약을 바꿔야 한다고 판단되면 **먼저 민화와 상의하고 문서를 고친 뒤** 코드를 고친다.

### 건드리면 안 되는 범위

- **`ChannelSelectController.cs` · `ChannelRowView.cs` — 한 줄도 고치지 않는다.**
  고쳐야 한다면 경계 설계가 틀린 것이다. PRD 06 에서 계정 서비스를 교체할 때 UI diff 가 0줄이었던 것과 같아야 한다.
- ~~`SceneFlow.cs` — 씬 전환 책임은 그대로.~~
  → **실제로는 추가가 필요했습니다.** `Lobby` 만은 Fusion 이 열기 때문에,
  `SceneFlow` 가 그 사실을 알고 스스로 비켜서야 합니다. 아래 "구현 결과" 참고.
  **씬 전환 책임 자체는 그대로** 입니다. 다른 씬은 전부 예전처럼 `SceneFlow` 가 엽니다.
- `FakeNetworkService.cs` — **삭제하지 않고 남긴다.** 서버 없이 화면 흐름을 볼 때 필요하다.
- `Lobby.unity` — 08-2 에서 끝났다.
- `Account/` 폴더, `server/` 전체.

### 담당 / 사전 조율

| 대상 | 담당 | 사유 |
| --- | --- | --- |
| `INetworkService` | **민화 ↔ 건희 공동** | 9장 지정 공동 파일. **바꾸지 않는 것이 목표지만, 바꾼다면 반드시 사전 합의** |
| `Boot.unity` | **민화** | Main 씬. 컴포넌트 교체 1건이라 작지만 씬 파일이다 |
| 채널 목록의 실제 내용 | **건희** | 서버를 몇 개 띄울지가 채널 수를 정한다 |

### 완료 조건

- [x] 채널을 고르고 입장하면 **그 채널의 세션**에 붙는다. 세션 이름이 다르면 서로 보이지 않는다.
- [x] `ChannelSelectController.cs` · `ChannelRowView.cs` **diff 가 0줄**이다.
      (`INetworkService.cs` · `FakeNetworkService.cs` · `Login.unity` 도 0줄)
- [x] 서버가 없는 채널을 고르면 실패 사유가 화면에 표시되고 **씬이 넘어가지 않는다.**
- [x] `NetworkServiceBootstrap.Active` 를 `Fake` 로 되돌리면 예전처럼 로컬 흐름으로 동작한다.
      (계획에서는 `Boot` 씬의 컴포넌트를 되돌리는 방식이었으나, **코드 한 줄로 바뀌었습니다.**)
- [x] 정상 Login 경로와 개발자 직접 경로가 **같은 세션**에 붙고, Lobby 이후 로직이 동일하다.

**이번 범위에서 뺀 것 — 후속으로 넘깁니다.**

- [ ] ChannelSelect 의 채널 목록이 **실제 Fusion 세션 목록**에서 온다.
      → 지금은 `ChannelCatalog` 고정 표입니다. 세션 목록 조회(`JoinSessionLobby`)는 붙이지 않았습니다.
- [ ] 로비 인원수가 화면에 **실제 값**으로 표시된다.
      → 실제 채널 인원수 · 서버 상태 조회와 함께 후속으로 넘깁니다.

### Unity / Fusion QA 절차

1. 서버 exe 를 **2개** 실행: `-session lobby-ch1`, `-session lobby-ch2`
2. 클라이언트 A · B 를 **채널 1** 로 입장 → 서로 보이는지
3. 클라이언트 C 를 **채널 2** 로 입장 → **A · B 에게 보이지 않는지** ★핵심
4. ~~ChannelSelect 의 인원수가 채널 1 = 2명, 채널 2 = 1명으로 보이는지~~
   → **후속으로 넘겼습니다.** 인원수는 아직 실제 값이 아닙니다
5. 서버를 하나 끄고 그 채널 입장 시도 → 실패 문구, 씬 유지
6. ~~`Boot` 컴포넌트를 Fake 로 되돌려 회귀 확인~~
   → **`NetworkServiceBootstrap.Active` 를 `Fake` 로 되돌려** 회귀 확인 (씬을 고치지 않습니다)

**실제로 돌린 QA 와 그 결과**는 아래 "구현 결과 → QA 결과" 에 정상 경로 / 개발자 직접 경로로
나누어 적어 두었습니다.

### 구현 결과 (2026-09-13 완료)

#### 1) 정상 게임 접속 경로

```text
Login
  → 서버 캐릭터 조회
  → ChannelSelect
  → 채널 선택
  → Fusion Dedicated Lobby 세션 접속
  → Fusion 이 Lobby 네트워크 씬 로드
  → NetworkPlayer 스폰 · 카메라 Snap
  → Loading Overlay 해제
```

각 단계를 누가 하는지:

| 단계 | 주체 | 근거 |
| --- | --- | --- |
| 로그인 | `HttpAuthService` | 이메일/비밀번호를 서버에 보내 검증받고 **JWT 를 발급**받는다 |
| 서버 캐릭터 조회 | `HttpCharacterService` | 발급받은 JWT 를 **`Authorization: Bearer`** 헤더로 실어 `GET /api/characters` |
| 채널 ID → 세션 이름 | `ChannelCatalog.TryGetSessionName` | 표에 없으면 `존재하지 않는 채널입니다.` 로 거부 |
| 세션 접속 | `FusionNetworkService.Connect` | `GameMode.Client` + `SessionName` + `Scene` |
| Lobby 씬 로드 | **Fusion** | `StartGameArgs.Scene = SceneRef.FromPath(Lobby.unity)` |
| 이전 화면 씬 내리기 | `SceneFlow.UnloadScreenScene` | `PeerMode.Multiple` 은 additive 로드라 이전 씬이 남는다 |
| NetworkPlayer 스폰 | 서버 (`PlayerSpawner`) | `IPlayerJoined` |
| 카메라 연결 · Snap | `LocalPlayerView.Spawned` | `HasInputAuthority` 인 피어에서만 |
| Loading Overlay 해제 | `TransitionStatus.SetReady` | **카메라 Snap 까지 끝난 뒤에만** 부른다 |

**인증은 REST 쪽 경계입니다.** Login 에서 받은 JWT 는 보호된 REST API 를 부를 때
`Authorization: Bearer` 헤더로 쓰입니다. **Fusion 세션 접속은 이 JWT 를 쓰지 않습니다.**
Dedicated Server 가 접속자의 JWT 를 검증하는 것은 **PRD 10** 입니다.
이번 단계에서 서버는 접속자의 신원을 확인하지 않습니다.

**Overlay 는 접속 성공이 아니라 화면을 넘겨도 되는 순간에 걷습니다.**
접속 성공 시점에 걷으면 사용자가 빈 바다나 날아오는 카메라를 봅니다.

접속에 실패하면 러너를 정리하고 `TransitionStatus.SetReady()` 로 되돌린 뒤
`OnConnectResult(false, 사유)` 를 보냅니다. **씬은 넘어가지 않습니다.**
`GameNotFound` 는 `서버 1 이(가) 열려 있지 않습니다.` 로 번역됩니다.

#### 2) 개발자 직접 접속 경로

로그인 · REST · DB 를 건너뛰고 **같은 Dedicated Lobby 에 붙습니다.**

- **Unity Editor**: `Lobby.unity` 를 열고 **그냥 Play**. 켜고 끌 메뉴가 없다
- **Development Build**: `-devjoin -session lobby-ch1` (Release 빌드에서는 동작하지 않는다)

| 항목 | 정상 경로 | 개발자 직접 경로 |
| --- | --- | --- |
| 앞단 UI | 거친다 | **건너뛴다** |
| 세션 이름 | 채널 선택 결과 | 실행 인자 `-session`, 없으면 Inspector 기본값 `lobby-ch1` |
| 캐릭터 외형 | (09-2 이후) 서버 캐릭터 외형 | **기본 `NetworkPlayer` 외형** |
| Lobby 이후 Runner · 플레이어 · 카메라 · 포털 로직 | **완전히 동일** | **완전히 동일** |

Lobby 진입 이후로는 두 경로가 갈라지지 않습니다. 같은 코드가 돕니다.
**그래서 개발자 직접 경로에서 확인한 동작은 정상 경로에서도 그대로 성립합니다.**

두 경로가 만나는 지점은 `FusionLauncher.Start()` 입니다.

| 상황 | `FusionLauncher` 가 하는 일 |
| --- | --- |
| 이미 도는 Runner 가 있다 (정상 경로) | 세션을 새로 열지 않고, 씬의 `PlayerSpawner` 를 **`runner.AddGlobal()`** 로 등록만 한다 |
| 도는 Runner 가 없다 (개발자 직접 · 서버) | 이 씬이 직접 세션을 연다 |
| 세션을 열 근거가 없다 (**일반 Player 빌드 한정**) | 열지 않고 `Lobby 에 바로 들어올 수 없습니다. ChannelSelect 에서 채널을 골라 주세요.` 를 띄운다 |

`AddGlobal` 이 필요한 이유: `NetworkSceneManagerDefault` 는 씬을 인수할 때
**`NetworkObject` 만** Runner 에 등록합니다. `PlayerSpawner` 는 평범한 `SimulationBehaviour` 라
자동으로 등록되지 않습니다. 그래서 Lobby 씬에 배치된 스폰 지점 배선을 살린 채
Runner 에 손수 붙여 줍니다.

**세션을 열 근거(`IsStandaloneSceneLoad`) — 에디터와 Player 빌드가 다릅니다.**

| 실행 환경 | Lobby 만 직접 열었을 때 |
| --- | --- |
| **Unity Editor** (`UNITY_EDITOR`) | **차단하지 않는다.** 개발 의도가 있는 실행으로 본다 |
| 서버 빌드 (`UNITY_SERVER`) | 차단하지 않는다. 서버는 언제나 자기가 세션을 열어야 한다 |
| Development Build + `-devjoin` | 차단하지 않는다. 개발 의도를 밝혔다 |
| 실행 인자 `-mode` 가 있다 | 차단하지 않는다. QA 실행이다 |
| **일반 Player 빌드, 위 표시 없음** | **차단하고** ChannelSelect 에서 채널을 고르라고 안내한다 |

즉 **차단은 제품 경로를 위한 안전장치이지, 개발 작업을 막는 장치가 아닙니다.**
에디터에서는 `Lobby.unity` 를 언제든 Play 할 수 있습니다.

#### 에디터 Play = Dedicated Server 개발용 Client (2026-09-13 통일)

**`GameMode` 를 고르는 분기가 하나로 줄었습니다.**

```csharp
// FusionLauncher.DetectFromBuild()
#if UNITY_SERVER
        return LaunchMode.Server;      // Dedicated Server 빌드
#else
        return LaunchMode.Client;      // 에디터 · 개발 빌드 · 일반 클라이언트 빌드 전부
#endif
```

| 실행 | `GameMode` |
| --- | --- |
| Dedicated Server 빌드 (`UNITY_SERVER`) 또는 `-mode server` | `Server` |
| **그 밖 전부 — 에디터 Play 포함** | **`Client`** |

**혼자 Host 가 되는 길은 없앴습니다.**
`LaunchMode.AutoHostOrClient` 열거값 자체를 지워 Inspector 에서 고를 수도 없습니다.
남겨 두었을 때 두 가지 문제가 있었습니다.

- 에디터에서 "되는" 것이 Dedicated Server 에서도 되는지 알 수 없었다
- 서버를 안 띄운 줄 모르고 혼자 놀다가 뒤늦게 발견했다

**서버가 없으면** Host 를 만들지 않고 접속에 실패합니다.
`FusionLauncher.DescribeClientFailure()` 가 사유를 문장으로 바꿔 `TransitionStatus.SetFailed()` 로 알립니다.

| `ShutdownReason` | 화면 문구 |
| --- | --- |
| `GameNotFound` | `Dedicated Server 를 먼저 실행하세요. "lobby-ch1" 세션이 열려 있지 않습니다.` |
| `ConnectionTimeout` · `ConnectionRefused` | `Dedicated Server 를 먼저 실행하세요. "lobby-ch1" 세션에 연결하지 못했습니다.` |
| 그 밖 | `Lobby 접속에 실패했습니다. (사유) Dedicated Server 가 실행 중인지 확인해 주세요.` |

**세션 이름 · 포트의 기본값과 바꾸는 법**

| 값 | 기본 | 정해지는 곳 |
| --- | --- | --- |
| 세션 이름 | `lobby-ch1` | `Lobby` 씬 `NetworkManager` 의 `FusionLauncher` Inspector |
| 포트 | `27015` | 같은 Inspector (서버만 사용) |

실행 인자 `-session` · `-port` 가 Inspector 를 이깁니다. 서버는 창이 없어 Inspector 로 못 바꾸기 때문입니다.
에디터 Play 에는 인자가 없으므로 **Inspector 값** 을 씁니다. 그래서 서버도 같은 이름으로 띄워야 만납니다.

> ⚠ Inspector 기본값은 원래 `AraAtti-Test` 였습니다. 채널 카탈로그(`srv-1` → `lobby-ch1`)와
> 서버 실행 명령이 쓰는 이름과 달라, 에디터 Play 가 없는 세션을 찾아 항상 실패했습니다.
> 이번에 `lobby-ch1` 로 맞췄습니다.

**팀 규칙**: `Lobby` 를 Play 해서 움직여 보는 모든 경우는 Dedicated Server 접속입니다.
씬 편집은 Play 없이 하면 되고, **플레이 모드 네트워크 QA 에는 서버 exe 가 필요합니다.**

#### 3) Network 서비스 전환

```csharp
// Assets/Game/Scripts/Network/NetworkServiceBootstrap.cs
private static readonly Implementation Active = Implementation.Fusion;   // ← 유일한 전환 지점
```

- `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 로 **하나만** 만들어 `NetworkServiceLocator` 에 등록한다
- **`NetworkServiceLocator` 에는 구현체가 정확히 하나만 등록된다.**
  등록 순서에 따라 우연히 선택되는 구조를 만들지 않는다
- Dedicated Server 프로세스에서는 채널을 고르지도 접속하지도 않으므로 **아예 만들지 않는다**
- `Boot` 씬의 `NetworkService` 오브젝트에서 **`FakeNetworkService` 컴포넌트를 제거**했다
  (오브젝트는 남아 있다. 씬 변경은 컴포넌트 한 개를 뗀 것뿐이다)
- **`FakeNetworkService.cs` 파일은 남겨 두었다.** 서버 없이 화면 흐름을 보거나
  단독 UI 테스트를 할 때 쓴다. 위 한 줄만 `Fake` 로 바꾸면 **씬을 고치지 않고** 되돌아간다

씬이 아니라 코드에 둔 이유는 두 가지입니다. 씬 파일은 병합 충돌이 가장 심하고,
`Boot` 을 거치지 않고 ChannelSelect 만 단독 실행해도 서비스가 준비되어야 하기 때문입니다.

#### 4) 채널과 씬 소유권

**채널 — 지금은 고정 카탈로그입니다.**

| 채널 ID | 화면 이름 | Fusion 세션 이름 |
| --- | --- | --- |
| `srv-1` | 서버 1 | `lobby-ch1` |
| `srv-2` | 서버 2 | `lobby-ch2` |

서버를 늘리면 `ChannelCatalog` 에 한 줄을 추가합니다. 화면 코드는 고치지 않습니다.

**씬 소유권 — `Lobby` 만 예외입니다.**

| 씬 | 여는 주체 |
| --- | --- |
| `Lobby` (Fusion 경로) | **Fusion** — `StartGameArgs.Scene` |
| 그 외 전부 | 클라이언트 — `SceneFlow` |

Fusion 경로에서 일반 `SceneFlow.LoadScene("Lobby")` 는 **실행하지 않습니다.**
접속에 성공하면 `SceneFlow.LobbyLoadedByNetwork` 가 서고 `FromChannelSelect()` 가 스스로 비켜섭니다.
두 곳이 같은 씬을 열면 Lobby 가 두 벌 생깁니다.
로그아웃 · 타이틀 복귀 시에는 이 표시를 해제해, 다음에 Fusion 없이 들어올 때 정상 동작합니다.

> ⚠ **`PeerMode.Multiple` 에서는 Client 도 Lobby `SceneRef` 를 지정해야 합니다.**
> `StartGameArgs.Scene` 을 빼면 세션은 붙고 캐릭터도 스폰되는데 **Lobby 가 로드되지 않습니다.**
> 로그에 오류가 하나도 남지 않아 찾기 어렵습니다. 실제로 여기서 한 번 막혔습니다.
> `FusionBootstrap.StartClient()` 는 Scene 을 넘기지 않지만, 그쪽은 **Single peer mode** 기준입니다.
> 우리 `NetworkProjectConfig.fusion` 은 `PeerMode: Multiple` 이라 규칙이 다릅니다.

`PeerMode.Multiple` 은 씬을 **additive 로 로드하고 이전 씬을 내리지 않습니다.**
ChannelSelect 의 Canvas 는 `Screen Space - Overlay` 라 카메라와 무관하게 계속 그려지므로,
`SceneFlow.UnloadScreenScene()` 으로 이전 화면 씬을 직접 내립니다.

**카메라 선택 — 표식으로만 고릅니다.**

`PeerMode.Multiple` 은 오브젝트를 러너 전용 씬으로 옮기므로 `scene.name` 이 경로마다 다릅니다.
(개발자 직접 실행은 `Lobby`, 정상 경로는 `FusionRunner (Client)_[Player:2]`)
그래서 **씬 이름 · `FindFirstObjectByType` 의 탐색 순서 · 루트 오브젝트 수 같은 추측 기준을 쓰지 않습니다.**
`Lobby` 의 `MainCamera` 에 붙은 `LobbyGameplayCamera` 표식이 유일한 근거입니다.
**표식이 없거나 중복이면 명확한 오류를 내고, 조용히 임의 카메라를 고르지 않습니다.**

#### 5) 이번 범위에서 하지 않은 것 — 후속

| 항목 | 지금 상태 | 어디로 |
| --- | --- | --- |
| 실제 채널 인원수 · 서버 상태 조회 | 고정 카탈로그. 인원수는 실제 값이 아니다 | 후속 |
| 자동 서버 증설 · 서버 디렉터리 · 매치메이커 | 없음. 서버 exe 를 사람이 띄우고 채널을 표에 적는다 | 후속 |
| 서버 캐릭터 외형 Fusion 동기화 | 전원 기본 `NetworkPlayer` 외형 | PRD 09-1 · 09-2 |
| Dedicated Server 의 JWT 검증 | 서버가 접속자 신원을 검증하지 않는다 | PRD 10 |
| Lobby 에서 나가기 / 로그아웃 UX | 창을 닫는 것 말고 정식 이탈 흐름이 없다 | 후속 |
| 미니게임 멀티플레이 전환 | 미니게임은 아직 네트워크에 붙지 않았다 | 후속 |

#### 실제로 손댄 파일

**신규 4개**

| 파일 | 역할 |
| --- | --- |
| `Network/NetworkServiceBootstrap.cs` | Fake ↔ Fusion 전환의 **유일한 지점** |
| `Network/FusionNetworkService.cs` | `INetworkService` 실제 구현. 접속 · 실패 사유 번역 · 이전 화면 정리 |
| `Network/ChannelCatalog.cs` | 채널 ID ↔ 세션 이름 고정 표 |
| `Network/LobbyGameplayCamera.cs` | 게임플레이 카메라 **표식 + 등록부** |

**수정 4개 + 씬 2개**

| 파일 | 변경 |
| --- | --- |
| `Core/SceneFlow.cs` | `LobbyLoadedByNetwork`, `FromChannelSelect` 분기, `UnloadScreenScene`, 복귀 시 상태 해제 |
| `Network/FusionLauncher.cs` | 실행 중 Runner 양보 · `AddGlobal` 등록 · Player 빌드의 근거 없는 단독 진입 차단 (에디터는 해당 없음) |
| `Network/LocalPlayerView.cs` | 씬 이름 기준 카메라 선택을 **표식 기준으로 교체** |
| `Network/Editor/FusionTestBuilds.cs` | Boot 부터 시작하는 정상 흐름 클라이언트 빌드 메뉴 추가 |
| `Scenes/.../Boot.unity` | `FakeNetworkService` 컴포넌트 제거 (28줄 삭제, 추가 0줄) |
| `Scenes/.../Lobby.unity` | `MainCamera` 에 `LobbyGameplayCamera` 추가 (13줄 추가, 삭제 0줄) |

**diff 0줄로 지켜진 파일**: `ChannelSelectController.cs` · `ChannelRowView.cs` ·
`INetworkService.cs` · `FakeNetworkService.cs` · `Login.unity`

**개발용 실행 규칙 통일로 추가된 변경 (2026-09-13)**

| 파일 | 변경 |
| --- | --- |
| `Network/FusionLauncher.cs` | `AutoHostOrClient` 열거값 제거 · 에디터도 `Client` · `DescribeClientFailure` 추가 |
| `Network/FusionDevEntry.cs` | 에디터 토글 제거. `-devjoin`(Development Build) 전용이 됐고, 이제 **`GameMode` 가 아니라 "Lobby 만 직접 열어도 되는가" 만** 정한다 |
| `Network/Editor/FusionDevMenu.cs` | **삭제.** 에디터가 언제나 Client 라 토글할 것이 없다 |
| `Network/Editor/FusionTestBuilds.cs` | QA 클라이언트를 **Development Build** 로 만든다. Release 로 만들면 `-devjoin` 이 조용히 무시됐다 |
| `Scenes/.../Lobby.unity` | `FusionLauncher` 의 `sessionName` 을 `AraAtti-Test` → **`lobby-ch1`** (1줄) |

#### QA 결과 — 경로별

**정상 Login 경로** (서버 1개 + 정상 흐름 클라이언트 2개, 서로 다른 계정)

| 확인 항목 | 결과 |
| --- | --- |
| Login → 캐릭터 조회 → ChannelSelect → 채널 선택 → Lobby 진입 | 두 클라이언트 모두 통과 |
| 상대 캐릭터 표시 · 이동 동기화 | 서로의 얼굴과 움직임 확인 |
| `AddGlobal` 호출 | 클라이언트 각 1회 / 서버 0회 |
| 일반 `LoadScene("Lobby")` 호출 | **0회** (네트워크가 단독 소유) |
| 카메라 경고 · 예외 | 0건 |
| 서버 미기동 시 | `서버 1 이(가) 열려 있지 않습니다.`, 씬 전환 없음 |
| 서버 프로세스의 `FusionNetworkService` 생성 | 0건 |

**개발자 직접 경로** (서버 1개 + `-mode client -devjoin` 클라이언트 2개)

| 확인 항목 | 결과 |
| --- | --- |
| 스폰 | 2/2 |
| 카메라 거리 | 5.70 m (첫 샘플부터 정상. 원점에서 날아오지 않음) |
| 화면 표시 | 캐릭터 정상 |
| `AddGlobal` 호출 | 0회 — 씬과 Runner 가 같아 Fusion 이 자동 등록 |
| 예외 | 0건 |

#### 겪은 함정 — 같은 실수를 막기 위해

| 증상 | 원인 | 대응 |
| --- | --- | --- |
| 접속 · 스폰 로그는 전부 정상인데 **Lobby 가 안 보인다** | `PeerMode.Multiple` 인데 Client 에 `StartGameArgs.Scene` 을 안 줬다 | Client 도 Lobby `SceneRef` 지정 |
| Lobby 는 로드됐는데 **ChannelSelect UI 가 계속 덮는다** | additive 로드 + `Screen Space - Overlay` Canvas | `UnloadScreenScene()` 으로 이전 씬 내리기 |
| 카메라가 **바다 한가운데에서 날아온다** | `ThirdPersonCamera` 목표 위치가 첫 입력 전까지 원점 | `SnapToPlayer()` + 대상 없을 때 정지 |
| 경로마다 **다른 카메라가 잡힌다** | 씬 이름 · 탐색 순서에 의존 | `LobbyGameplayCamera` 표식 + 등록부 |
| 옆 사람이 나가면 **내 화면이 검게 덮인다** | `Despawned` 가 남의 캐릭터에서도 불린다 | `Spawned` 에서 확정한 `isLocalPlayer` 로 가드 |
| `GameIdAlreadyExists (32766)` | `StartGameArgs.Scene` 이 현재 씬을 가리켜 재로드 → `StartGame` 중복 호출 | 세션 보유 여부 가드 |

### 권장 커밋 단위

```text
[feat] 채널 ID와 세션 이름 매핑 추가
[feat] Fusion 기반 INetworkService 구현체 추가
[chore] Boot 씬을 실제 네트워크 서비스로 전환
```

### 다음 단계로 넘길 결과물

- 채널 ↔ SessionName 매핑 규격 (PRD 10 에서 서버가 어느 채널인지 알아야 한다)
- 실제 세션에 붙는 클라이언트 경로 (09-2 가 여기에 외형을 얹는다)

---

## 6. PRD 09-1 — 외형 카탈로그 분리와 안정 ID 설계

### 목적과 범위

파츠 카탈로그를 **UI 프리팹에서 떼어내** `NetworkPlayer` 도 쓸 수 있게 만들고,
Fusion 으로 보낼 **안정된 파츠 ID / 피부색 값 규격**을 확정한다. **네트워크 코드는 없다.**

### `CharacterCustomizationController` 를 NetworkPlayer 에 붙이지 않는 이유

**붙이면 즉시 NullReferenceException 이 난다.** `Awake()` 가 UI 참조를 null 체크 없이 쓴다.

```csharp
// Character/CharacterCustomizationController.cs:88~102
categoryButtons[i].onClick.AddListener(...)      // 카테고리 탭 버튼
optionButtons[i].onClick.AddListener(...)        // 파츠 카드 버튼
previousPageButton.onClick.AddListener(...)      // 페이지 버튼
nextPageButton.onClick.AddListener(...)
completeButton.onClick.AddListener(...)          // [생성 완료]
nicknameInput.onValueChanged.AddListener(...)    // 닉네임 입력칸
```

그 밖에도 이 클래스는 **캐릭터 생성 화면 전용 책임**을 갖고 있다.

- 카테고리 전환 · 페이징 · 스크롤 (`CharacterCustomizationScroll.cs`)
- 닉네임 검증과 안내 문구 (`GetNicknameValidationMessage`, `ShowNicknameGuide`)
- 서버 저장 요청과 결과 처리 (`CharacterCustomizationPersistence.cs`)
- 878줄 + partial 3개

`NetworkPlayer` 에 필요한 것은 이 중 **"파츠 프리팹을 뼈에 붙이는 일" 하나뿐**이다.
그 로직은 `CharacterCustomizationCatalogue.cs` 의 `TryApplyCatalogPart()` 에 있고,
의존이 `slotBindings`(WearSlot→SkinnedMeshRenderer) · `equippedObjects` · `ApplySkinMaterial` 뿐이라
**떼어낼 수 있다.** UI 를 직접 만지지 않는다.

### 안정 ID / 값 설계 — 실측 근거 기반

| 항목 | 설계 | 근거 |
| --- | --- | --- |
| 슬롯 | **6개 고정**: `Face` `Hair` `Shoes` `Top` `Bottom` `Accessory` | `CharacterCustomizationPersistence.PersistedCategories` 와 동일. 서버 `AllowedSlots` 와도 동일 |
| 파츠 키 (`networkKey`) | **프리팹 이름 문자열** (`Costume_14_01`). 숫자 ID 가 아니다 | 인덱스는 배열 순서라 에셋 정렬 시 전부 밀린다 (PRD 01 에서 확인). 서버 DB `character_parts.prefab_name` · Unity `CharacterPartSnapshot.prefabName` 이 **이미 같은 문자열**이라 변환이 0이다 |
| 문자열 크기 | `NetworkString<_32>` **(후보)** | 실측 최장 **27자** (`Female_Emotion_Surpriced_02`, 파츠 380개 전수 측정). 여유 5자. 확정은 09-2 의 크기 검증 뒤에 |
| 피부색 | `"#RRGGBB"` 7자 → `NetworkString<_8>` 또는 `uint`(RGB 24bit) | 팔레트 인덱스가 아니다. 서버 `characters.skin_color` = `CHAR(7)` |

> **DB/REST 의 64자 제한은 그대로 둔다. 서버 검증 상수를 32로 낮추지 않는다.**
> 영속 데이터의 제약을 네트워크 전송 사정에 맞춰 좁히면, 나중에 긴 이름이 필요할 때
> 이미 저장된 데이터까지 문제가 된다. 두 계층을 분리한다. (0-3 참고)
>
> | 계층 | 제약 | 위치 |
> | --- | --- | --- |
> | DB / REST | **64자 유지** | `character_parts.prefab_name` `VARCHAR(64)`, `CharacterEndpoints.MaxPrefabNameLength = 64` |
> | Fusion `networkKey` | **32자 이하** | 이 단계의 카탈로그 검증이 보증한다 |
>
> **대신 카탈로그에 길이 검증을 넣는다.** 현재는 최장 27자라 `prefabName` 을
> `networkKey` 로 그대로 재사용할 수 있다. 32자를 넘는 파츠가 생기면 검증이 잡아주고,
> 그때 **짧고 안정된 별도 `networkKey`** 를 도입하면 된다. (예: 슬롯 접두어 + 일련번호)
> 그 전까지는 별도 키를 만들지 않는다 — 매핑 계층이 하나 늘어나는 비용이 이득보다 크다.

**피부색을 `uint` 로 할지 문자열로 할지**도 이 단계에서 정한다.
`uint` 가 4바이트로 훨씬 싸지만, 서버 · 로컬 캐시 · Fusion 세 곳의 표현이 갈라진다.
**문자열 유지를 권한다.** 변환 지점을 늘리지 않는 것이 이 프로젝트에서 반복해서 이득이었다.

### 수정 예상 파일 / 씬 / 프리팹

| 대상 | 변경 |
| --- | --- |
| (신규) `Assets/Game/Scripts/Character/CharacterPartCatalog.cs` | `ScriptableObject`. 슬롯별 파츠 이름 → 프리팹 매핑. 378개 카탈로그 항목을 여기로 |
| (신규) `Assets/Game/Art/.../CharacterPartCatalog.asset` | 위 ScriptableObject 인스턴스 |
| (신규) `Assets/Game/Scripts/Character/CharacterAppearanceApplier.cs` | 카탈로그 + 스냅샷 → 파츠 부착. `TryApplyCatalogPart` 로직 이관 |
| `Assets/Game/Scripts/Character/CharacterCustomizationCatalogue.cs` | 적용 로직을 Applier 로 옮기고 **위임**. 동작은 동일 |
| `Assets/Game/Prefabs/Characters/CharacterCustomization.prefab` | `catalogParts` 배열을 카탈로그 에셋 참조로 교체 |

### 건드리면 안 되는 범위

- **커마 화면의 UI 동작** — 카테고리 전환 · 페이징 · 스크롤 · 회전 버튼 · 닉네임 검증 문구.
  이 단계는 **겉보기 동작이 하나도 바뀌지 않아야 한다.**
- `CharacterCreate.unity` 씬 파일 — 저장하지 않는다.
- `CharacterAppearanceSnapshot` / `CharacterAppearanceStore` 저장 포맷 — PRD 01·07 에서 확정.
- `Account/` 폴더 전체, `Network/` 폴더 전체, `server/` 전체.
- `Completed` 이벤트 시그니처 (`CharacterCreateFlow` 가 의존).

### 담당 / 사전 조율 ⚠

| 대상 | 담당 | 사유 |
| --- | --- | --- |
| **`Character/` 폴더 전체** | **서연** | `GAME_STRUCTURE.md` 9장 폴더 소유권. 878줄 컨트롤러의 주인 |
| **`CharacterCustomization.prefab`** | **서연** | 568KB · `catalogParts` 378개 · GUID 참조 1,633건. 카탈로그를 옮기는 작업의 핵심 |
| `networkKey` 32자 제한 | **서연 ↔ 건희** | 09-2 의 `NetworkString` 크기를 정한다. **DB/REST 의 64자는 건드리지 않는다** |
| `NetworkPlayer` 의 `slotBindings` 규격 | **서연 ↔ 건희** | 08-2 의 `NetworkPlayer.prefab` 과 맞아야 한다 |

**이 단계는 서연의 사전 동의 없이 시작하면 안 된다.** 카탈로그 이동은 커마 화면 전체를 건드린다.

### 완료 조건

- [ ] `CharacterCreate` 화면의 동작이 **이전과 완전히 같다.** (파츠 선택 · 페이징 · 저장 · 복원)
- [ ] `CharacterAppearanceApplier` 를 **UI 없는 빈 GameObject + 캐릭터 모델**에 붙여 파츠가 적용된다.
- [ ] 카탈로그 에셋 하나로 슬롯 6개 · 파츠 378개를 조회할 수 있다.
- [ ] **`networkKey` 가 32자를 넘는 파츠가 카탈로그에 있으면 에디터에서 경고**가 난다.
      (DB/REST 의 64자 제한은 그대로다. 이 검증은 Fusion 전송용 키에만 적용한다)
- [ ] 이름이 중복된 파츠가 있으면 경고가 난다. (PRD 01 의 `BuildPrefabNameIndex` 와 같은 검사)
- [ ] PRD 01 회귀: `CharacterCreate` 단독 실행 → 저장 · 복원이 그대로 동작.

### Unity QA 절차

1. `CharacterCreate` 씬 Play → 파츠를 여러 개 바꿔 보고 **이전과 똑같이** 동작하는지
2. `[생성 완료]` → 저장 → 재진입 → 외형 복원 (PRD 01 회귀)
3. **빈 씬**에 캐릭터 모델 + `CharacterAppearanceApplier` 만 놓고,
   `CharacterAppearanceStore` 에 저장된 스냅샷을 적용 → **UI 없이 외형이 나오는지** ★핵심
4. 카탈로그 에셋에 32자 넘는 `networkKey` 를 임시로 넣어 경고가 나는지 → 되돌리기
   (서버에는 64자까지 저장할 수 있으므로, 이 검증은 **Unity 카탈로그 쪽에서만** 한다)
5. Console 에 새로운 Error 가 0건인지

### 권장 커밋 단위

```text
[refactor] 파츠 카탈로그를 ScriptableObject로 분리
[feat] UI 없이 외형을 적용하는 컴포넌트 추가
[chore] 파츠 이름 길이 검사 추가
```

### 다음 단계로 넘길 결과물

- `CharacterPartCatalog` 에셋 (모든 클라이언트가 **로컬로** 갖고 있는 것)
- `CharacterAppearanceApplier` (스냅샷 → 외형)
- **확정된 `networkKey` 규격과 최대 길이** (09-2 의 `NetworkString` 크기 결정 입력값)
- 카탈로그의 길이 검증 (32자 초과 시 경고)

---

## 7. PRD 09-2 — NetworkPlayer 외형 복제

### 목적과 범위

같은 채널의 모든 클라이언트가 **서로의 외형을 정확히** 보게 한다.
외형은 Fusion `[Networked]` 상태로 복제하고, 적용은 각 클라이언트의 로컬 카탈로그가 한다.

**이 단계에서는 클라이언트가 알려준 외형을 서버가 그대로 믿는다.** (초기 플레이테스트 허용)
검증은 PRD 10 에서 붙인다.

### ⚠ 선행 기술 검증 — ConnectionToken 크기 (구현 전에 반드시)

**`ConnectionToken` 으로 외형을 보내는 방식은 아직 "확정" 이 아니라 "후보" 다.**
이 Fusion 버전의 `ConnectionToken` 최대 크기와 실제 페이로드 크기를 **아직 측정하지 않았다.**
아래 3가지를 재고 나서야 방식을 확정할 수 있다.

| # | 측정 대상 | 방법 | 합격 기준 |
| --- | --- | --- | --- |
| 1 | **`ConnectionToken` 최대 바이트 수** | Fusion 문서/어셈블리 확인 + 크기를 늘려가며 접속 실패 지점 탐색 | 실측값을 문서에 기록 |
| 2 | **JWT + characterId 크기** (PRD 10 용) | 실제 발급 토큰을 `Encoding.UTF8.GetByteCount()` 로 측정 | 1번 한도 안 |
| 3 | **초기 외형 페이로드 크기** (PRD 09-2 용) | 파츠 6개(최장 27자) + 피부색 7자 + 닉네임 10자를 직렬화해 측정 | 1번 한도 안 |

**참고 실측값** (이미 확인된 것):
- 파츠 이름 최장 **27자**, 슬롯 **6개**, 피부색 `"#RRGGBB"` **7자**, 닉네임 최대 **10자**
- 단순 합산 시 외형만 약 `27×6 + 7 + 10 ≈ 179자` (UTF-8 ASCII 기준 179바이트, 구분자 별도)
- JWT 는 `sub`·`email`·`jti`·`nbf`·`exp`·`iss`·`aud` 클레임을 담아 **수백 바이트** 수준이다
  (PRD 04 에서 발급 형태 확인). 정확한 값은 **측정해야 한다**

> **2번과 3번을 동시에 넣어야 한다는 점을 놓치지 말 것.**
> PRD 10 에서는 같은 `ConnectionToken` 에 JWT 가 들어간다.
> 09-2 에서 외형으로 한도를 다 써버리면 10 에서 다시 설계해야 한다.
> **09-2 단계에서 "JWT + characterId + 외형" 을 모두 합친 최악의 크기로 검증한다.**

#### 한도를 넘으면 — 대체 경로

`ConnectionToken` 에 억지로 밀어 넣지 않는다. 잘리거나 접속이 실패한다.

```text
[대체안] 접속 후 서버 권위 핸드셰이크

 클라이언트 ── ConnectionToken(JWT + characterId 만, 작게) ──▶ 서버 접속
 서버       → NetworkPlayer 를 스폰하되 외형은 비운 상태로 둔다
 클라이언트 → 접속 완료 후 서버에 외형을 알린다 (RPC 또는 첫 입력에 실어서)
 서버       → 검증 후 [Networked] 상태에 반영 → 모든 클라이언트에 복제
```

- 이 경로에서도 **`[Networked]` 상태가 최종 전달 수단**인 것은 같다. 늦은 접속자 처리도 그대로다.
- 달라지는 것은 **초기 값을 언제 채우는가** 뿐이다.
- 단점: 스폰 직후 짧은 순간 기본 외형으로 보일 수 있다. 그 구간을 어떻게 가릴지
  (스폰을 늦출지, 기본 외형을 쓸지) 함께 정한다.
- **PRD 10 의 "인증 대기 후 스폰" 구조와 자연스럽게 합쳐진다.** 8장 참고.

> **결론**: `ConnectionToken` 방식은 **후보**다. 위 3가지 측정을 마치고
> PRD 09-2 세부 문서 첫 줄에 "측정값 / 채택한 방식" 을 적은 뒤 구현을 시작한다.

### 서버 권위 네트워크 상태 설계

```csharp
// NetworkPlayer 에 붙는 NetworkBehaviour (신규)
[Networked, Capacity(6)]
NetworkArray<NetworkString<_32>> PartNames { get; }   // 슬롯 순서 고정: Face Hair Shoes Top Bottom Accessory

[Networked] NetworkString<_8>  SkinColor { get; }     // "#RRGGBB"
[Networked] NetworkString<_16> Nickname  { get; }     // 머리 위 이름표용 (2~10자)
```

- **`[Networked]` 를 쓰는 이유**: RPC 와 달리 **늦게 접속한 사람도 자동으로 현재 값을 받는다.**
  Fusion 이 상태 스냅샷으로 보내주므로 "이미 있는 사람들의 외형" 을 따로 챙길 코드가 없다.
- **쓰기 권한은 서버(StateAuthority)만** 갖는다. 클라이언트가 직접 쓰지 못한다.
- 슬롯은 **배열 인덱스 고정 6칸**이다. 순서를 상수로 못박고 문서에 적는다.
  빈 슬롯은 빈 문자열로 둔다. (파츠를 벗은 상태)

### 접속 시 1회 동기화 / 변경 시에만 재동기화

```text
[접속]
 클라이언트 → (임시) 자기 CurrentCharacter 의 외형을 서버에 알린다
              ChannelSelect 입장 시 Fusion ConnectionToken 에 실어 보내는 방식이 **후보**다
              (PRD 10 에서 이 통로에 JWT 를 함께 실으므로 미리 맞춰 두면 재작업이 없다)
              ⚠ 크기 검증을 통과해야 확정된다. 넘으면 접속 후 핸드셰이크로 바꾼다 (위 참고)
 서버       → PlayerSpawner 가 NetworkPlayer 를 Spawn 하고 [Networked] 값을 채운다
 모든 클라 → OnChanged 감지 → 로컬 카탈로그로 파츠 적용

[변경] — 이번 범위에서는 발생하지 않는다 (커마 재진입 기능이 없다)
 서버만 [Networked] 값을 바꾼다 → 모든 클라이언트가 OnChanged 로 재적용
```

**매 tick 보내지 않는다.** 외형은 값이 바뀔 때만 전파된다. Fusion 의 델타 압축으로
변화가 없으면 대역폭을 쓰지 않는다.

### 수정 예상 파일 / 씬 / 프리팹

| 대상 | 변경 |
| --- | --- |
| (신규) `Assets/Game/Scripts/Network/NetworkPlayerAppearance.cs` | `[Networked]` 상태 + `OnChanged` → `CharacterAppearanceApplier` 호출 |
| `Assets/Game/Prefabs/Characters/NetworkPlayer.prefab` | 위 컴포넌트 + `CharacterAppearanceApplier` + `slotBindings` 배선 |
| `Assets/Game/Scripts/Network/PlayerSpawner.cs` | Spawn 직후 `[Networked]` 초기값 주입 |
| (신규) `Assets/Game/Scripts/Network/AppearanceHandshake.cs` | 클라이언트가 접속 시 외형을 전달하는 통로. **크기 검증 결과에 따라** `ConnectionToken` 방식 또는 접속 후 RPC 방식 |
| `Assets/Game/Scripts/Network/FusionNetworkService.cs` | `StartGame` 에 `ConnectionToken` 추가 (**`ConnectionToken` 방식을 택한 경우에만**) |

### 건드리면 안 되는 범위

- `Character/` 폴더 — 09-1 에서 끝났다. **여기서 다시 고치면 09-1 설계가 틀린 것이다.**
- `CharacterCustomization.prefab` — 커마 화면은 이 단계와 무관하다.
- `ICharacterService` · `IAuthService` · `Account/` 폴더 — 외형 데이터를 **읽기만** 한다.
- `server/` 전체 — PRD 10 의 일이다.
- `Lobby.unity` — 08-2 에서 끝났다. 프리팹 교체만 있으면 씬을 열지 않는다.
- `INetworkService` 시그니처 — `ConnectionToken` 은 `StartGameArgs` 쪽이라 계약 변경이 아니다.

### 담당 / 사전 조율

| 대상 | 담당 | 사유 |
| --- | --- | --- |
| `NetworkPlayer.prefab` 의 `slotBindings` | **서연 ↔ 건희** | 09-1 규격과 정확히 맞아야 파츠가 붙는다 |
| `Network/` 폴더 | **건희** | 폴더 소유권 |
| 슬롯 배열 순서 상수 | **건희 ↔ 서연** | 6칸 순서를 양쪽이 같게 못박아야 한다 |

### 완료 조건

- [ ] **선행 크기 검증 3가지를 마쳤고 측정값을 세부 문서에 기록했다.**
- [ ] 전달 방식(`ConnectionToken` / 접속 후 핸드셰이크)이 측정 결과로 **확정**되었다.
- [ ] 클라이언트 A · B 가 서로의 **정확한 외형**(파츠 6개 + 피부색)을 본다.
- [ ] **늦게 접속한 C 가 이미 있던 A · B 의 외형을 본다.** ★핵심 검증
- [ ] A 가 나갔다 다시 들어오면 같은 외형으로 보인다.
- [ ] 외형 프리팹이나 메시가 네트워크로 전송되지 **않는다.** 문자열만 오간다.
- [ ] 카탈로그에 없는 파츠 이름이 오면 **그 슬롯만 건너뛰고 경고**한다. 캐릭터가 깨지지 않는다.
- [ ] 서버가 `-nographics` 로 돌 때도 정상이다. (서버는 파츠를 렌더링하지 않는다)
- [ ] 값이 바뀌지 않을 때 대역폭 사용이 늘지 않는다. (Fusion Statistics 로 확인)

### Unity / Fusion QA 절차

0. **(선행) 크기 측정**: JWT + characterId + 외형을 모두 합친 페이로드의 바이트 수를 로그로 찍고,
   `ConnectionToken` 한도와 비교한다. 넘으면 대체 경로로 전환하고 그 사실을 문서에 적는다
1. 계정 A · B · C 를 만들고 **서로 확실히 다른 외형**으로 캐릭터를 생성 (모자 · 의상 · 피부색)
2. 서버 exe 실행 → A 접속 → 자기 외형이 맞는지
3. B 접속 → **A 화면에 B 의 외형, B 화면에 A 의 외형**이 정확히 보이는지
4. **C 접속 → C 화면에 A · B 의 외형이 보이는지** ★늦은 접속자 검증
5. A 종료 → 재접속 → B · C 화면에 A 의 외형이 다시 보이는지
6. 카탈로그에 없는 이름을 서버 DB 에 직접 넣고 접속 → 경고 1건 + 나머지 슬롯 정상
7. Fusion Statistics(`FusionStatistics`) 로 정지 상태의 대역폭이 0에 가까운지
8. 서버를 `-nographics` 로 돌려도 4번이 동작하는지

### 권장 커밋 단위

```text
[feat] NetworkPlayer 외형 네트워크 상태 추가
[feat] 접속 시 외형 전달 통로 추가
[feat] 원격 플레이어 외형 적용 구현
```

### 다음 단계로 넘길 결과물

- 동작하는 외형 복제 (단, **클라이언트를 믿는 상태**)
- **`ConnectionToken` 최대 크기 실측값**과 확정된 전달 방식 (PRD 10 이 그 위에 JWT 를 얹는다)
- 슬롯 순서 상수와 `NetworkString` 크기 확정값

---

## 8. PRD 10 — Dedicated Server 의 JWT · characterId 검증

### 목적과 범위

**클라이언트가 임의의 외형을 주장하지 못하게 만든다.**
Dedicated Server 가 접속 요청의 JWT 를 검증하고, characterId 로 REST API 를 조회해
**승인된 외형만** Fusion 상태에 넣는다.

### 이번 단계와 09-2 의 차이

```text
[PRD 09-2 — 클라이언트를 믿는다]
 클라이언트 ── "나는 모자 쓴 선원김이야" ──▶ Fusion Server ──▶ 그대로 복제
                     ↑
              여기서 아무 값이나 넣을 수 있다

[PRD 10 — 서버가 확인한다]
 클라이언트 ── ConnectionToken(JWT + characterId) ──▶ Fusion Server
                                                        │
                          ① OnConnectRequest — 즉시 판단만 (동기)
                             토큰이 있는가 / 형식이 맞는가 / 크기가 정상인가
                             아니면 → request.Refuse()
                                                        │
                          ② 접속 허용. 단 **아직 스폰하지 않는다** (인증 대기)
                                                        │
                          ③ 비동기 검증 — GET /api/auth/me → GET /api/characters
                                                        │
                            ┌───────────────┴───────────────┐
                       성공 │                               │ 실패
                            ▼                               ▼
              ④ NetworkPlayer 스폰 +            ④' 해당 PlayerRef 연결 종료
                 승인된 외형을 [Networked] 에      (Refuse 가 아니라 접속 후 퇴장)
                            │
                            ▼
                     모든 클라이언트
```

> **왜 두 단계로 나누는가**: `OnConnectRequest` 안에서 **HTTP 응답을 기다릴 수 있는지가
> 아직 확정되지 않았다.** 이 콜백은 Fusion 의 연결 수립 경로에서 동기적으로 불릴 가능성이 높고,
> 그 안에서 수백 ms 걸리는 REST 호출을 `await` 하면 연결 처리 전체가 막힌다.
> 그래서 **즉시 알 수 있는 것만 ①에서 거르고, 시간이 걸리는 검증은 ③으로 미룬다.**

| 항목 | 09-2 | 10 |
| --- | --- | --- |
| 외형의 출처 | 클라이언트가 보낸 값 | **서버가 REST 로 조회한 값** |
| 위조 가능성 | 있음 (남의 외형 · 없는 파츠 주장 가능) | **없음** |
| 신원 확인 | 없음 | JWT 서명 검증 |
| REST 의존 | 없음 | 접속 시 1회 (플레이 중에는 없음) |

### ⚠ 선행 기술 검증 — OnConnectRequest 에서 비동기가 되는가

**구현 전에 이것부터 확인한다.** 결과에 따라 아래 설계 중 하나를 고른다.

| 검증 항목 | 확인 방법 | 결과에 따른 선택 |
| --- | --- | --- |
| `OnConnectRequest` 가 `async` 를 기다려 주는가 | Fusion 어셈블리의 콜백 시그니처(반환형이 `void` 인지 `Task` 인지) + 실제로 지연시켜 접속이 유지되는지 실험 | **기다려 준다** → ①에서 바로 REST 검증 후 `Refuse()` 가능<br>**기다려 주지 않는다** → 아래 "인증 대기" 구조 사용 |
| `request.Refuse()` 를 콜백 밖에서(나중에) 부를 수 있는가 | `ConnectRequest` 가 값 타입인지, 보관했다가 쓸 수 있는지 확인 | 불가하면 **접속 후 퇴장** 경로만 쓴다 |
| 접속 후 특정 플레이어만 끊는 API | `runner.Disconnect(PlayerRef)` 존재 여부 | 없으면 대안(빈 상태로 두고 클라이언트가 스스로 나가게)을 설계 |

> 현재까지 확인된 것: `OnConnectRequest(NetworkRunner, NetworkRunnerCallbackArgs.ConnectRequest, byte[] token)`
> 시그니처가 **`void`** 로 선언되어 있다. (`RunnerEnableVisibility.cs:74` 의 빈 구현)
> **`void` 콜백은 `await` 결과를 기다려 주지 않는다** — 이 점에서 "인증 대기" 구조가 필요할 가능성이 높다.
> 다만 Fusion 이 내부적으로 지연 응답을 지원하는 별도 방법을 둘 수 있으므로 **실험으로 확정한다.**

### 두 단계 검증 구조 (비동기가 안 될 경우 — 기본안)

| 단계 | 위치 | 하는 일 | 실패 시 |
| --- | --- | --- | --- |
| ① 즉시 판단 | `OnConnectRequest` (동기) | 토큰 **존재** / 형식(점 2개로 나뉘는 JWT 모양) / **크기 상한** / characterId 파싱 | **`request.Refuse()`** — 접속 자체를 거부 |
| ② 인증 대기 | `OnPlayerJoined` | **스폰하지 않는다.** 해당 `PlayerRef` 를 대기 목록에 넣고 검증 시작 | — |
| ③ 비동기 검증 | 코루틴 / async | `GET /api/auth/me` (200/401) → `GET /api/characters` | ④' 로 |
| ④ 스폰 | 검증 성공 후 | `NetworkPlayer` 스폰 + 승인된 외형을 `[Networked]` 에 주입 | — |
| ④' 퇴장 | 검증 실패 · 타임아웃 | **해당 `PlayerRef` 만** 연결 종료 | 사유를 로그에 남긴다 |

**`Refuse()` 와 "접속 후 퇴장" 은 다른 경로다. 문서와 코드에서 구분한다.**

- **`request.Refuse()`** — `OnConnectRequest` **안에서만** 쓴다. 동기적으로 판단 가능한 것에만.
  토큰이 아예 없거나, 형식이 JWT 가 아니거나, 크기가 비정상일 때.
- **접속 후 퇴장** — 비동기 검증 실패 시. 이미 연결이 수립된 뒤이므로 `Refuse()` 를 쓸 수 없다.
  해당 `PlayerRef` 만 끊고, **다른 플레이어는 건드리지 않는다.**

**대기 중 상태를 어떻게 보이게 할 것인가**도 정해야 한다.
스폰을 미루면 그 사람은 잠시 월드에 나타나지 않는다. 다른 유저에게는 아무 영향이 없다.
본인 화면에는 "확인하는 중..." 을 보여주는 것을 권한다.

### REST 장애가 나면

| 상황 | 동작 |
| --- | --- |
| **이미 접속·스폰된 유저** | **그대로 유지된다.** Fusion 세션은 REST 와 무관하게 돈다. 이것이 0-2 에서 REST 직접 조회를 배제한 이유이기도 하다 |
| **새로 접속하려는 유저** | ③에서 실패 → ④' 퇴장. 사유가 클라이언트에 보여야 한다 |
| 검증 타임아웃 | 상한(예: 10초)을 두고 넘으면 퇴장. 무한 대기 금지 |

### 장기 검토 항목 — RS256 전환 (이번 범위 아님)

`/api/auth/me` 호출은 접속마다 REST 왕복이 한 번 필요하다.
**JWT 를 RS256(비대칭)으로 바꾸면** Dedicated Server 가 **공개키만으로 오프라인 검증**할 수 있어
REST 왕복이 사라지고, 개인키는 ASP.NET 서버에만 남는다.

> **이번 범위에서 인증 방식을 바꾸지 않는다.** 검토 항목으로만 기록한다.
> 바꾸려면 서버의 토큰 발급(PRD 04)과 키 관리가 함께 바뀌므로 별도 PRD 가 필요하다.
> 지금은 `/api/auth/me` 방식으로 간다 — **키를 Unity 빌드에 넣지 않는다는 목표는 두 방식 모두 만족한다.**

### 확인된 API 통로

| 필요 | Fusion API | 확인 |
| --- | --- | --- |
| 클라이언트가 토큰을 실어 보냄 | `StartGameArgs.ConnectionToken` | `FusionNetworkService` 에서 설정 |
| 서버가 접속 요청을 심사 | `INetworkRunnerCallbacks.OnConnectRequest(runner, request, byte[] token)` | 이 버전에 존재. `PlayerInputProvider.cs` 에 이미 빈 구현이 있다 |
| 서버가 플레이어별 토큰 조회 | `runner.GetPlayerConnectionToken(player)` | 존재 (Fusion 인스펙터가 "Has Connection Token?" 을 표시) |
| 요청 거절 | `request.Refuse()` | `OnConnectRequest` 인자 |

### 수정 예상 파일 / 씬 / 프리팹

| 대상 | 변경 |
| --- | --- |
| (신규) `Assets/Game/Scripts/Network/ServerConnectionGate.cs` | `OnConnectRequest` 에서 **즉시 판단만** (토큰 존재 · 형식 · 크기). 실패 시 `Refuse()` |
| (신규) `Assets/Game/Scripts/Network/ServerAuthGate.cs` | **인증 대기** 목록 관리. 비동기로 `/api/auth/me` → `/api/characters` 확인 후 스폰 요청 또는 퇴장 |
| (신규) `Assets/Game/Scripts/Network/ServerAppearanceResolver.cs` | 서버가 REST 로 승인된 외형 조회 |
| `Assets/Game/Scripts/Network/PlayerSpawner.cs` | **`OnPlayerJoined` 에서 즉시 스폰하지 않는다.** 인증 통과 후에만 스폰 + `[Networked]` 초기값 주입 |
| `Assets/Game/Scripts/Network/AppearanceHandshake.cs` | 외형 값 대신 **JWT + characterId** 만 보내도록 축소 |
| `server/AraAtti.Api/…` | 서버 간 조회용 경로가 필요할 수 있음. **아래 판단 참고** |

### 서버 API 를 바꿔야 하는가 — 판단

**가능하면 바꾸지 않는다.** Dedicated Server 가 **클라이언트의 JWT 를 그대로 재사용**해
`GET /api/characters` 를 호출하면 기존 API 로 충분하다. 그 토큰의 주인 캐릭터만 돌아오므로
**"남의 캐릭터를 못 본다" 규칙도 그대로 유지된다.**

바꿔야 할 수도 있는 경우:
- JWT 만료(2시간)가 긴 세션 중에 지나면 재조회가 실패한다 → 접속 시 1회만 조회하므로 실전에서는 문제없다.
- 서버가 여러 플레이어를 한 번에 조회하고 싶다 → 그때 배치 조회를 **합의 후** 추가한다.

> ⚠ **JWT 서명 키를 Unity 서버 빌드에 넣지 않는다.**
> 검증 방식을 두 가지 중에 고른다. **후자를 권한다.**
> 1. Unity 서버가 직접 서명 검증 → HS256 대칭키를 Unity 빌드에 넣어야 한다. **빌드에서 추출될 수 있다.**
> 2. Unity 서버가 그 토큰으로 `GET /api/auth/me` 를 호출 → **200 이면 유효, 401 이면 거절.**
>    키가 필요 없고, `/api/auth/me` 는 PRD 04 에서 이미 만들었다.

### 건드리면 안 되는 범위

- `Character/` 폴더, `CharacterCustomization.prefab` — 09-1 에서 끝났다.
- `Account/` 폴더의 인터페이스 시그니처 — 서버 빌드는 **읽기만** 한다.
- `server/` 의 DB 스키마와 마이그레이션 — **컬럼 추가가 필요하면 새 마이그레이션**을 만든다.
- `IAuthService` / `ICharacterService` 계약.
- 클라이언트 UI 전체 — 이 단계는 서버 쪽 변경이다.

### 담당 / 사전 조율

| 대상 | 담당 | 사유 |
| --- | --- | --- |
| `Network/` 폴더, 서버 빌드 | **건희** | 폴더 소유권 |
| `server/AraAtti.Api` | **건희** | 서버 담당 |
| JWT 검증 방식 결정 | **팀 합의** | 키를 빌드에 넣을지 여부는 보안 결정이다. `/api/auth/me` 방식 권장 |
| 서버 exe 가 REST 주소를 아는 방법 | **건희** | 커맨드라인 인자 또는 환경 변수. **하드코딩하지 않는다** |

### 완료 조건

- [ ] **`OnConnectRequest` 비동기 가능 여부를 실험으로 확정했고 결과를 문서에 적었다.**
- [ ] JWT 가 **아예 없거나 형식이 아니면** `OnConnectRequest` 에서 **`request.Refuse()`** 로 거절된다.
- [ ] 만료되거나 위조된 JWT 는 **비동기 검증에서 걸러지고 해당 PlayerRef 만 퇴장**한다.
      (`Refuse()` 경로와 퇴장 경로가 코드에서 구분되어 있다)
- [ ] 인증이 끝나기 전에는 **NetworkPlayer 가 스폰되지 않는다.**
- [ ] 검증이 상한 시간(예: 10초)을 넘으면 무한 대기하지 않고 퇴장시킨다.
- [ ] 클라이언트가 **남의 characterId** 를 보내면 접속이 거절되거나 그 캐릭터가 적용되지 않는다. ★핵심
- [ ] 클라이언트가 **카탈로그에 없는 파츠**를 주장해도 화면에 나타나지 않는다. (서버가 DB 값만 쓴다)
- [ ] 정상 클라이언트는 09-2 와 똑같이 동작한다. (회귀 없음)
- [ ] REST 서버가 죽으면 **새 접속만 실패**하고, **이미 접속·스폰된 사람들은 그대로 유지**된다.
- [ ] 한 사람의 인증 실패가 **다른 사람의 세션에 영향을 주지 않는다.**
- [ ] 서버 로그에 JWT 전문과 비밀번호가 찍히지 않는다.
- [ ] JWT 서명 키가 Unity 서버 빌드에 포함되지 않는다.

### Unity / Fusion QA 절차

1. 정상 흐름: 로그인 → 채널 입장 → 외형 정상 → 09-2 QA 4번(늦은 접속자)까지 회귀 확인
2. **토큰 없이**: `ConnectionToken` 을 비우고 접속 → **`Refuse()` 로 거절**되고 사유가 보이는지
   (서버 로그에 "즉시 거절" 로 남는지 — 비동기 경로를 타지 않아야 한다)
3. **위조 토큰**: JWT 마지막 글자를 바꿔 접속 → **접속은 되지만 스폰되지 않고 곧 퇴장**되는지
   (형식은 맞으므로 ①을 통과하고 ③에서 걸린다. 두 경로가 구분되는지 확인)
3-1. **인증 대기 확인**: REST 서버 응답을 인위적으로 늦추고 접속 → 그 사이 월드에 나타나지 않는지,
   다른 유저 화면에 아무 영향이 없는지
3-2. **타임아웃**: REST 를 응답 없이 방치 → 상한 시간 뒤 퇴장하는지 (무한 대기 금지)
4. **남의 characterId**: A 로 로그인한 뒤 B 의 characterId 를 보내 접속 → **거절 또는 A 의 외형 적용**
5. **없는 파츠 주장**: 클라이언트가 `"NoSuchPart_99"` 를 보내도 서버가 DB 값을 쓰므로 무시되는지
6. **REST 서버 종료**: 이미 접속·스폰된 A · B 는 **계속 움직이고 서로 보이는지**,
   새 접속 C 만 실패하는지 ★핵심
7. 서버 로그에 토큰 전문이 없는지 (`grep` 으로 확인)
8. 서버 빌드 파일을 `strings` 로 훑어 JWT 키가 없는지

### 권장 커밋 단위

```text
[feat] Dedicated Server 접속 인증 검증 추가
[feat] 서버가 승인된 외형을 조회해 적용
[refactor] 클라이언트 외형 전달을 토큰 기반으로 축소
[docs] 외형 권위 구조 정리
```

### 다음 단계로 넘길 결과물

- 신뢰할 수 있는 외형 복제 (클라이언트를 믿지 않는 상태)
- 서버 exe 실행 규격 (세션 · 포트 · REST 주소 인자)
- 이후 확장(인스턴스 분리 · 미니게임 매칭)이 얹힐 검증된 접속 관문

---

## 9. 전 단계 공통 규칙

1. **한 커밋에 한 작업.** (`GIT_CONVENTION.md` 3장, `[type] subject` 50자 이하)
2. MR 대상은 항상 `develop`. 리뷰어 1명 이상.
3. **`Lobby.unity` 는 한 번에 한 명만 연다.** 22MB · 프리팹 5,530개 씬은 병합이 사실상 불가능하다.
   작업 전에 팀에 알리고, 끝나면 알린다. (`GIT_CONVENTION.md` 9장)
4. **공동 관리 파일을 혼자 고치지 않는다**: `INetworkService`, `IPlayerController`,
   `IAuthService`, `ICharacterService`. 고쳤으면 같은 MR 에서 `GAME_STRUCTURE.md` 4장도 고친다.
5. Unity MR 전 체크: Console 새 Error 0건 / `.meta` 누락 없음 / `Library`·`Temp`·`Logs` 미포함 /
   타인 Develop 씬 무변경.
6. **서버 exe 검증은 반드시 `-batchmode -nographics` 로도 한다.** 에디터에서만 되는 것은
   Dedicated Server 가 아니다.
7. 각 단계는 시작 시 `docs/prd/prd-XX-*.md` 세부 문서를 만든다. 이 문서는 색인으로 유지한다.
8. 이 문서는 **Fusion 전용**이다. 계정 · 캐릭터 저장 관련 변경은
   `auth-character-roadmap.md` 에 적는다. 두 문서를 섞지 않는다.

---

## 10. 다음 단계 — PRD 08-1 의 구체적 범위

> **목표 한 문장** — `ServerTestScene` 에서 **Host 없는 서버 프로세스 1개 + 클라이언트 2개**로
> 캡슐이 서로 보이게 만들고, `SceneRef` 식별을 인덱스에서 경로 기반으로 바꾼다.

### 10-1. 왜 이것이 첫 번째인가

- **위험이 가장 큰 것을 가장 싸게 없앤다.** Scene index 불일치(1-4)는 08-2 이후 모든 단계의
  디버깅 시간을 잡아먹는다. 지금은 파일 2개만 고치면 된다.
- **조율이 필요 없다.** `Network/` 폴더 · `ServerTestScene` · 서버 빌드 프로필 전부 건희 담당이다.
  효진(Lobby) · 서연(Character) 을 기다리지 않는다.
- **캡슐로 검증한다.** 캐릭터 모델 · 외형 · 채널 매핑을 전부 빼고 "서버가 유지하고 클라이언트가
  붙는다" 만 확인한다. 실패 원인이 하나로 좁혀진다.

### 10-2. 손대는 파일 — 2개 수정 + 1개 신규

| 파일 | 변경 내용 |
| --- | --- |
| `Assets/Game/Scripts/Network/FusionLauncher.cs` | ① `GameMode.AutoHostOrClient` → 모드를 **주입받도록**<br>② `SceneRef.FromIndex(buildIndex)` → **`SceneRef.FromPath(경로)`**<br>③ `roomName` 하드코딩 → 인자로 받기 |
| (신규) `Assets/Game/Scripts/Network/FusionServerBootstrap.cs` | 서버 빌드에서 `GameMode.Server` 자동 기동.<br>`-session <이름>` · `-port <번호>` 커맨드라인 파싱.<br>인자가 없으면 기본값 + 경고 |
| `Assets/Game/Scenes/Develop/GeonHee/ServerTestScene.unity` | `NetworkManager` 에 `FusionServerBootstrap` 배치 |
| (조건부 신규) `Assets/Settings/Build Profiles/Fusion Client Test.asset` | **A안을 고른 경우에만.** 3장 "먼저 정할 것" 참고 |

### 10-3. 구현 시 지킬 것

1. **`SceneRef.FromIndex` 를 코드에서 완전히 없앤다.** 이 버전에 `SceneRef.FromPath` 가 있음을 확인했다.
   경로 문자열은 상수 한 곳에 모은다.
2. **서버는 로컬 플레이어를 스폰하지 않는다.** `PlayerSpawner` 의 `if (!Runner.IsServer) return;`
   가드는 그대로 두고, 서버 자신이 `ActivePlayers` 에 들어가지 않는 것을 로그로 확인한다.
3. **`NetAddress.Any(port)` 로 포트를 지정한다.** 공식 샘플 `FusionBootstrap.cs:535` 패턴을 따른다.
4. **클라이언트 코드는 이 단계에서 고치지 않는다.** `ChannelSelect` 는 08-3 의 일이다.
   검증은 `ServerTestScene` 을 직접 Play 하는 방식으로 한다.
5. ~~`FusionLauncher` 의 기존 동작(에디터에서 `AutoHostOrClient`)을 **선택지로 남긴다.**~~
   → **2026-09-13 에 뒤집혔다.** 선택지로 남겼더니 서버를 안 띄운 줄 모르고 혼자 Host 로 도는 일이
   반복됐다. `AutoHostOrClient` 는 열거값째 제거했다. 08-3 "구현 결과" 참고.
   서버 없이 혼자 테스트하던 흐름을 깨지 않는다.

### 10-4. PRD 08-1 완료 판정

`docs/prd/` 에 PRD 08-1 세부 문서를 만들고, 3장의 완료 조건 7개 + QA 7단계를 모두 통과시킨 뒤
`feature/gh-fusion-dedicated-server` → `develop` MR 을 올린다.

**이 단계에서 절대 하지 않는 것**

- `Lobby.unity` 수정
- `NetworkPlayer` 프리팹 제작
- 외형 관련 코드
- `INetworkService` · `FakeNetworkService` 수정
- 클라이언트 `EditorBuildSettings.asset` 수정
- `Account/` · `server/` 수정
