# 로비 심장 제단 봉헌 시스템 설계

> 이 문서는 **설계안**입니다. 이 단계에서 코드는 한 줄도 고치지 않았습니다.
> 작성 기준일 2026-09-20, 브랜치 `feature/hj-lobby-altar`, 기준 커밋 `82b8cd4e`.
>
> 문서 안의 모든 클래스명·경로·키·API 는 **실제 저장소에서 확인한 것만** 적었습니다.
> 확인되지 않은 것은 `없음` 또는 `미정(TBD)` 으로 적고, 추측으로 채우지 않았습니다.

---

## 0. 시작하기 전에 — 먼저 바로잡아야 할 전제 세 가지

설계에 들어가기 전에, 요청서의 전제와 실제 저장소가 다른 부분이 세 군데 있습니다.
이걸 모르고 구현을 시작하면 "있는 걸 재사용" 하려다 없는 걸 찾게 됩니다.

### (1) 화면의 `섬 회복도` HUD 와 `[F] 조각 봉헌` 은 **구현된 UI 가 아니라 컨셉 아트**입니다

요청서에 첨부하신 로비 화면은 코드가 만든 화면이 아니라 아래 이미지 파일입니다.

```text
art/lobby-ui/island-restoration-hud-v1.png          "섬 회복도 68%", "심장 조각 18 / 30"
art/lobby-ui/island-restoration-gameplay-v2.png
art/lobby-ui/island-restoration-gameplay-v3.png     "[F] 조각 봉헌", "심장 제단", 시스템 메시지
```

근거 — 다음 문자열은 저장소 전체(`Assets/Game`, `art`, `docs`, `design`, `server`)에서
**단 한 번도 발견되지 않습니다.**

```text
"봉헌"       0건
"회복도"      0건
"제단"       3건 — 전부 채팅 더미 문구다 (아래)
```

"제단" 3건은 모두 채팅 예시 문구입니다. 기능이 아닙니다.

| 파일 | 내용 |
|---|---|
| `unity/UnderTheSea/Assets/Game/Scripts/Network/PlayerSpeechBubble.cs:202` | `Preview("심장 제단 퀘스트 같이 하실 분 구해요...")` |
| `unity/UnderTheSea/Assets/Game/Scripts/UI/LobbyChatLine.cs:10` | 주석 안의 화면 예시 |
| `unity/UnderTheSea/Assets/Game/Scripts/UI/LobbyChatView.cs:286` | 데모용 더미 채팅 한 줄 |

즉 **상단 회복도 HUD 도, 상호작용 안내 HUD 도, 봉헌 UI 도 아직 존재하지 않습니다.**
이 시스템은 "기존 HUD 에 값을 연결" 하는 일이 아니라 **처음부터 만드는** 일입니다.

### (2) 인벤토리 시스템이 **없습니다**

`inventory`, `ItemData`, `ItemSO`, `itemId` 로 `Assets/Game` 과 `server` 를 전부 검색한 결과
**0건**입니다. 아이템 ScriptableObject 도, 인벤토리 컴포넌트도, DB 테이블도 없습니다.

가장 가까운 것은 `RewardService` 하나인데, 이것은 인벤토리가 아닙니다. 3장에서 자세히 봅니다.

### (3) 제단은 **씬에 놓인 장식물**이고, 스크립트가 하나도 붙어 있지 않습니다

`P_HeartAltar` 프리팹은 `Lobby` 씬에 실제로 배치되어 있습니다.

```text
프리팹   Assets/Game/Prefabs/HeartAltar/P_HeartAltar.prefab   (guid e30da2f93a3ba0045b1768d5821b8913)
씬       Assets/Game/Scenes/Main/CoreGames/Lobby.unity        PrefabInstance &108088768276666623
이름     HeartAltar
위치     (34.54, 22.90, 138.89)   씬 루트 직계
```

프리팹 안의 자식은 다음과 같고, **전부 `m_IsActive: 1` (켜져 있음)** 입니다.

```text
P_HeartAltar
├─ PlatformBase          BoxCollider
├─ PlatformTier2         BoxCollider
├─ Pedestal
├─ Stairway              BoxCollider
├─ HeartCrystal
├─ AltarBeam             ← 파란 기둥 연출에 쓸 자리가 이미 있다
├─ Light_Crystal
└─ Light_Monolith
```

머티리얼도 이미 있습니다 — `M_Altar_Beam_01.mat`, `M_Altar_Crystal_01.mat`.

**MonoBehaviour 는 0개입니다.** Trigger Collider 도 없습니다(BoxCollider 3개 모두 일반 충돌체).
그래서 12장의 VFX 설계는 "새로 만든다" 가 아니라 **"이미 있는 `AltarBeam` 을 껐다 켠다"** 가 됩니다.

---

## 1. 현재 프로젝트 구조 요약

### 1.1 기술 스택 (실제 확인)

| 영역 | 실제 | 근거 |
|---|---|---|
| Unity | **6000.5.9f1** | `unity/UnderTheSea/ProjectSettings/ProjectVersion.txt:1` |
| 렌더 파이프라인 | **URP** (`PC_RPAsset` / `Mobile_RPAsset`) | `ProjectSettings/GraphicsSettings.asset:49`, `Assets/Settings/` |
| VFX Graph | **없음.** `com.unity.modules.particlesystem` 만 있음 | `Packages/manifest.json:30` |
| 실시간 네트워크 | **Photon Fusion 2, Dedicated Server 방식** | `Assets/Game/Scripts/Network/FusionLauncher.cs:13-21` |
| 입력 | **Both** (레거시 + 새 Input System 동시 활성) | `ProjectSettings/ProjectSettings.asset:950` → `activeInputHandler: 2` |
| 계정 서버 | **ASP.NET Core Minimal API + EF Core + MySQL 8.4 + JWT(HS256)** | `server/AraAtti.Api/Program.cs`, `Data/AraAttiDbContext.cs:23` |
| 어셈블리 | Fishing 만 asmdef 분리. **나머지는 전부 `Assembly-CSharp`** | `Assets/Game/Scripts/Fishing/**/*.asmdef` 외 없음 |

### 1.2 "서버" 가 두 개입니다 — 이 설계의 핵심 갈림길

이 프로젝트에는 **성격이 완전히 다른 서버 프로세스가 두 개** 있습니다.
"서버 권위로 처리한다" 고 할 때 **어느 서버를 말하는지** 가 이 설계 전체를 가릅니다.

```text
┌──────────────────────────────────────────────────────────────────┐
│ ① AraAtti.Api        ASP.NET Core + MySQL                        │
│                                                                  │
│    · 영속 저장소다. users / characters / character_parts         │
│    · JWT 로 "이 요청이 누구인지" 를 안다 (sub = users.id)        │
│    · 지금 쓰는 곳: 회원가입 · 로그인 · 캐릭터 조회/생성 뿐       │
│    · 게임 플레이 중에는 호출하지 않는다                          │
└──────────────────────────────────────────────────────────────────┘
┌──────────────────────────────────────────────────────────────────┐
│ ② Fusion Dedicated Server    Unity 서버 빌드 (AraAtti-Server.exe) │
│                                                                  │
│    · 채널마다 프로세스가 하나씩 (lobby-ch1, lobby-ch2)           │
│    · StateAuthority 를 가진 진짜 권위 서버다                     │
│    · 이동 · 채팅 · 외형을 여기서 검증한다                        │
│    · ⚠ DB 를 모른다. JWT 도 모른다. 플레이어가 누구인지 모른다   │
└──────────────────────────────────────────────────────────────────┘
```

**②가 플레이어의 DB 신원을 모른다**는 점이 이 설계에서 가장 중요한 제약입니다. 근거:

`FusionLauncher.cs:208-217` 의 `StartGameArgs` 에는 `AuthValues` 도
`ConnectionToken` 도 설정하지 않습니다. `OnCustomAuthenticationResponse` 는 빈 구현입니다
(`FusionNetworkService.cs:435`). 즉 Fusion 서버가 아는 것은 `PlayerRef` (세션 안의 번호) 뿐이고,
그 번호가 `users.id` 몇 번인지 알 길이 **현재 없습니다.**

Fusion 서버가 플레이어에 대해 아는 유일한 신원 정보는 닉네임 하나이고,
그것도 클라이언트가 RPC 로 제출한 값을 길이만 잘라서 저장한 것입니다
(`NetworkPlayerIdentity.cs:97-120`).

→ 이 제약의 결론은 **8장**에서 다룹니다.

### 1.3 씬 흐름

```text
Boot → Title → Login → (캐릭터 없으면) CharacterCreate → ChannelSelect → Lobby
                                                                           │
                                     F 로 포탈 진입 ──▶ MineBoot / ShipCoopBoot / WarriorsBoot
```

`Lobby` 씬은 **Fusion 이 로드**합니다. 클라이언트가 직접 `LoadScene` 하지 않습니다
(`GAME_STRUCTURE.md` 82행 "정상 게임 접속 경로 — `Lobby` 씬은 Fusion 이 로드합니다").

이 때문에 **로비 UI 는 씬에 올려 두지 않는 것이 이 프로젝트의 관례**입니다. 6장에서 다룹니다.

### 1.4 Lobby 씬에 실제로 붙어 있는 우리 스크립트

씬 YAML 의 GUID 를 `Assets/Game/Scripts` 의 `.cs.meta` 와 대조한 결과, **5종뿐**입니다.

```text
FusionLauncher                 세션 시작
PlayerSpawner                  캐릭터 스폰 (서버만)
DedicatedServerSceneCleanup    서버 빌드에서 불필요한 것 제거
LobbyGameplayCamera            카메라
MiniGamePortal      × 5        미니게임 입구
```

`LobbyChatView` · `LobbyTutorial` · `ProximityPortal` 은 씬에 없습니다.
앞의 둘은 **런타임에 `Resources` 에서 만들어 올립니다** (6.2절).

---

## 2. 현재 Lobby 키 입력 전체 조사

제단 키를 고르기 위한 조사입니다. **저는 키를 확정하지 않습니다.** 판단 재료만 정리합니다.

### 2.1 조사 방법

```text
레거시   Input.GetKey / GetKeyDown / GetKeyUp / KeyCode. / GetAxis / GetButton
새 시스템 Keyboard.current / Mouse.current / Key. / InputAction / PlayerInput
에셋     Assets/InputSystem_Actions.inputactions
설정     ProjectSettings/InputManager.asset, ProjectSettings/ProjectSettings.asset
씬       Lobby.unity 안의 직렬화된 키 값 (interactKey)
```

`Assets/Game/Scripts/**/Editor/**` 는 제외했습니다(에디터 전용, 런타임에 안 돕니다).

### 2.2 Input System 구조 — 먼저 알아야 할 것

**① 프로젝트는 Old 와 New 를 동시에 켜 둔 상태입니다.**

```text
ProjectSettings/ProjectSettings.asset:950   activeInputHandler: 2   (= Both)
```

그래서 `Input.GetAxis("Mouse X")` 와 `Keyboard.current.fKey` 가 한 프로젝트 안에 섞여 있습니다.
실제로 섞여 있습니다 — 카메라는 레거시, 이동은 새 시스템입니다.

**② `.inputactions` Action Map 은 우리 코드가 쓰지 않습니다.**

```text
Assets/InputSystem_Actions.inputactions   (guid 052faaac586de48259a63d0c4782560b)

이 GUID 를 참조하는 곳
  ProjectSettings/ProjectSettings.asset      (프로젝트 기본 액션으로 등록만 되어 있음)
  ProjectSettings/EditorBuildSettings.asset
  Assets/Game 안에는 참조가 0건
```

`PlayerInput` 컴포넌트를 붙인 프리팹도, `InputActionReference` 필드도 `Assets/Game` 에 없습니다.
**즉 제단 입력도 Action Map 을 만들 필요 없이 `Keyboard.current[Key.X]` 한 줄이면 됩니다.**
그게 `MiniGamePortal` 이 이미 하고 있는 방식입니다.

**③ 레거시 InputManager 축은 기본값 그대로입니다.**

`ProjectSettings/InputManager.asset` 에 우리가 추가한 축은 없습니다
(Horizontal / Vertical / Fire1~3 / Jump / Mouse X / Mouse Y / Mouse ScrollWheel / Submit / Cancel / Debug*).
이 중 Lobby 에서 실제로 읽는 것은 `Mouse X` · `Mouse Y` · `Mouse ScrollWheel` 뿐입니다.

### 2.3 키 입력 전체 표

**Lobby 에서 실제로 도는 것** (굵게 표시)

| 키 | 현재 기능 | 사용 Scene | 스크립트 | 코드 위치 | Lobby 충돌 가능성 |
|---|---|---|---|---|---|
| **W A S D** | 캐릭터 이동 | **Lobby** | `PlayerInputProvider` | `Network/PlayerInputProvider.cs:29-32` | **확정 사용** |
| **W A S D** | 튜토리얼 이동 단계 판정 | **Lobby** | `LobbyTutorial` | `UI/LobbyTutorial.cs:508-511` | **확정 사용** |
| **Space** | 점프 (`LobbyButton.Jump`) | **Lobby** | `PlayerInputProvider` | `Network/PlayerInputProvider.cs:39` | **확정 사용** |
| **Space** | 튜토리얼 점프 단계 판정 | **Lobby** | `LobbyTutorial` | `UI/LobbyTutorial.cs:501` | **확정 사용** |
| **L/R Shift** | 달리기 (`LobbyButton.Sprint`) | **Lobby** | `PlayerInputProvider` | `Network/PlayerInputProvider.cs:42-43` | **확정 사용** |
| **F** | **미니게임 포탈 입장** | **Lobby** | `MiniGamePortal` | `Lobby/MiniGamePortal.cs:53, 87-90` | **확정 사용 — 아래 2.4 참고** |
| **Esc** | 채팅 입력칸 포커스 해제 | **Lobby** | `LobbyChatView` | `UI/LobbyChatView.cs:150` | **조건부** (채팅 입력 중에만) |
| **Enter** | 채팅 전송 | **Lobby** | `LobbyChatView` (`TMP_InputField.onSubmit`) | `UI/LobbyChatView.cs:97` | **조건부** (입력칸 포커스 중에만) |
| **마우스 우클릭 드래그** | 카메라 회전 | **Lobby** | `LocalPlayerView` | `Network/LocalPlayerView.cs:392-398` | **확정 사용** |
| **마우스 휠** | 카메라 확대/축소 | **Lobby** | `LocalPlayerView` | `Network/LocalPlayerView.cs:400` | **확정 사용** |
| **마우스 좌클릭** | UI 전용 (일부러 비워 둠) | **Lobby** | `LocalPlayerView` 주석 | `Network/LocalPlayerView.cs:386-390` | **UI 가 쓴다** |

**다른 Scene 에서 쓰지만 Lobby 에서는 안 도는 것**

| 키 | 기능 | Scene | 스크립트 | 코드 위치 | Lobby 충돌 |
|---|---|---|---|---|---|
| E | 낚시 — 텐션 ↑ | Fishing* | `KeyboardFishingInputSource` | `Fishing/Runtime/Infrastructure/KeyboardFishingInputSource.cs:28` | 없음 |
| Q | 낚시 — 텐션 ↓ | Fishing* | 위와 같음 | `:29` | 없음 |
| T | 낚시 — 낚싯대 리센터 | Fishing* | 위와 같음 | `:40` | 없음 |
| R | 낚시 — 릴 감기 | Fishing* | 위와 같음 | `:52` | 없음 |
| F | 낚시 — 후킹 | Fishing* | 위와 같음 | `:51` | 없음(다른 씬) |
| Space | 낚시 — 캐스팅 | Fishing* | 위와 같음 | `:43, 50` | 없음 |
| ↑ ↓ ← → | 낚시 조준 / 무쌍 2P 이동 | Fishing*, Warriors | `KeyboardFishingInputSource:33-38`, `IoT/KeyboardPlayerController.cs:165-168` | | 없음 |
| J | 광산 — 힌트 | Mine | `MineInputProvider` | `MiniGames/Mine/Net/MineInputProvider.cs:73` | 없음 |
| C | 광산 — 블록 복구 | Mine | 위와 같음 | `:72` | 없음 |
| K | 배 — 망치질/발사 | ShipCoop | `IoT/KeyboardPlayerController.cs:178` | | 없음 |
| V | 무쌍 — 면버튼 | Warriors | `IoT/KeyboardPlayerController.cs:156` | | 없음 |
| 1 2 3 / Numpad1-3 | 무쌍 — 동작 입력 | Warriors | `MiniGames/Warriors/Net/WarriorsInputProvider.cs:51-55` | | 없음 |
| X | 무쌍 — 찌르기 | Warriors | `IoT/KeyboardPlayerController.cs:195` | | 없음 |
| Esc | 일시정지 / 튜토리얼 닫기 / 메뉴 | Warriors, ShipCoop, Title | `WarriorsPauseControl.cs:166`, `ShipCoopTutorialView.cs:154`, `StartMenuController.cs:305` | | 조건부 |
| Tab | 로그인 화면 칸 이동 | Login | `UI/LoginScreenController.cs:177` | | 없음 |
| W S ↑ ↓ Enter Space | 시작 메뉴 조작 | Title | `UI/StartMenuController.cs:261-287` | | 없음 |
| **F1** | 디버그 HUD 토글 | Mine, ShipCoop, 공통결과, Fishing | `MineDebugHud.cs:36`, `ShipCoopDebugHud.cs:25`, `MatchResultTestRig.cs:49`, `FishingDebugUI.cs:100` | | 없음 |
| F3 | 낚시 디버그 UI | Fishing* | `FishingDebugUI.cs:100` | | 없음 |
| F9 | 배 개발자 모드 | ShipCoop | `ShipCoopDevMode.cs:98` | | 없음 |
| ` (Backquote) | 개발자 모드 토글 | ShipCoop, Warriors | `ShipCoopDevMode.cs:95`, `WarriorsDevMode.cs:35` | | 없음 |
| `- = [ ] ; ' , . / \` `P` `R` `0` `1` | 배 개발자 치트 | ShipCoop | `ShipCoopDevMode.cs:240-308` | | 없음 |
| `- [ ] 0` | 무쌍 개발자 치트 | Warriors | `WarriorsDevMode.cs:80-92` | | 없음 |

\* 낚시(Fishing)는 현재 `Scenes/Develop/Yongju/FishingScenes/` 의 개발 씬에만 있습니다.
`Scenes/Main` 에 편입되지 않았고, Lobby 에 낚시 진입 포탈도 없습니다.
다만 씬 목록에 `FishingPier` 오브젝트가 있으므로 **나중에 로비에 낚시가 붙을 가능성**은 있습니다.
2.5절의 "향후 충돌 가능성" 은 이 점을 반영했습니다.

### 2.4 `F` 에 대한 사실 확인 — 컨셉 아트가 맞긴 한데, 이미 쓰고 있습니다

요청서에서 "HUD 에 `[F] 조각 봉헌` 이 보여도 코드로 확인하라" 고 하셨습니다. 확인 결과:

**`F` 는 이미 Lobby 에서 사용 중입니다. 그것도 5곳에서.**

```csharp
// Assets/Game/Scripts/Lobby/MiniGamePortal.cs:52-53
[Tooltip("누를 키. Lobby 이동은 WASD 만 쓰므로 F 와 겹치지 않는다.")]
[SerializeField] private Key interactKey = Key.F;
```

씬에 직렬화된 값도 전부 F 입니다 (`Key.F` == 20):

```text
Assets/Game/Scenes/Main/CoreGames/Lobby.unity
  123808:  interactKey: 20      config → dd6f57d8...
  141962:  interactKey: 20      config → 5500890e...
  151601:  interactKey: 20      config → 184fa0a6...
  307542:  interactKey: 20      config → dd6f57d8...
  476106:  interactKey: 20      config → 5500890e...
```

다만 — **거리로 갈라져 있습니다.** `MiniGamePortal` 은 `interactDistance: 4` (4m) 안에서만
키를 읽습니다(`MiniGamePortal.cs:77, 85`). 제단과 포탈의 실제 거리를 씬에서 계산해 봤습니다.

```text
HeartAltar          (34.54, 22.90, 138.89)

Entrance_Mine       (38.1,  4.0,  53.6)    수평거리 약 85 m
Entrance_ShipCoop   (-17.9, 3.8, 199.8)    수평거리 약 80 m
Entrance_Warriors   (-37.5, 2.0,  70.2)    수평거리 약 99 m
```

⚠ 나머지 `MiniGamePortal` 2개는 프리팹 인스턴스 안에 있어 씬 YAML 만으로 월드 좌표를 풀지
못했습니다. **F 를 최종 선택하신다면 Unity 에디터에서 그 둘의 위치를 직접 확인해 주세요.**

### 2.5 제단 상호작용 키 후보

**2~4개만 제시하고, 추천 키를 확정하지 않습니다.** 최종 선택은 사용자 몫입니다.

#### 후보 A — `E`

```text
키:              E
현재 충돌:        없음. Lobby 에서 E 를 읽는 코드가 0건이다.
향후 충돌 가능성:  중간. 낚시가 E 를 텐션 올리기로 쓴다
                 (KeyboardFishingInputSource.cs:28). 낚시는 지금 개발 씬에만 있지만
                 Lobby 에 FishingPier 오브젝트가 있어 나중에 로비에 들어올 수 있다.
                 다만 그때도 "로비를 걸어다닐 때" 와 "낚시 중" 은 상태가 다르므로
                 실제 충돌이 되려면 낚시를 로비 씬 안에서 하도록 만들어야 한다.
장점:            장르 관례상 가장 자연스럽다. 플레이어가 설명 없이 누른다.
                 F(포탈) 와 E(제단) 로 "입장" 과 "상호작용" 이 손가락으로 구분된다.
주의점:          낚시가 로비에 들어오는 시점에 다시 한 번 표를 봐야 한다.
```

#### 후보 B — `F` 재사용 (거리로 중재)

```text
키:              F
현재 충돌:        있음. MiniGamePortal 5개가 전부 F 다.
                 다만 셋의 위치를 실측한 결과 제단에서 80m 이상 떨어져 있고,
                 포탈의 반응 거리는 4m 다. 한 프레임에 둘이 같이 반응할 수 없다.
                 ⚠ 나머지 포탈 2개 위치는 에디터에서 확인 필요 (2.4절).
향후 충돌 가능성:  높음. "로비에 F 상호작용을 하나 더 놓는다" 가 반복되면
                 언젠가 두 개가 4m 안에 들어온다. 그때 어느 쪽이 먹는지는
                 컴포넌트 Update 순서에 달리고, 그건 보장되지 않는다.
장점:            컨셉 아트(island-restoration-gameplay-v3.png)의 "[F] 조각 봉헌" 과
                 그대로 맞는다. 아트 수정이 필요 없다.
                 플레이어에게 "로비에서 뭔가 하려면 F" 라는 규칙 하나만 가르치면 된다.
주의점:          이 길을 고르면 **거리 중재를 우연에 맡기지 말고 코드로 못박아야 한다.**
                 "가장 가까운 상호작용 대상 하나만 반응한다" 는 공통 규칙을 세우고
                 MiniGamePortal 과 제단이 그것을 함께 쓰게 해야 한다.
                 → 이건 설계 범위가 커진다. 5.4절에 별도로 정리했다.
```

#### 후보 C — `G`

```text
키:              G
현재 충돌:        없음.
향후 충돌 가능성:  없음. 저장소 전체(Assets/Game, IoT 포함)에서 G 를 읽는 코드가 0건이다.
                 IOT_INPUT.md 의 장치 키 배치표에도 G 는 없다.
장점:            가장 안전하다. 어떤 미니게임이 로비에 들어와도 안 부딪힌다.
                 WASD 에서 손가락이 닿는 거리다.
주의점:          관례가 아니라 플레이어가 안내 HUD 를 보고 배워야 한다.
                 안내 HUD 가 반드시 잘 보여야 한다(5장에서 설계).
```

#### 후보 D — `R`

```text
키:              R
현재 충돌:        없음. Lobby 에서 R 을 읽는 코드가 0건이다.
향후 충돌 가능성:  중간. 낚시의 릴 감기(KeyboardFishingInputSource.cs:52),
                 배 개발자 모드 치트(ShipCoopDevMode.cs:292) 가 R 을 쓴다.
                 후자는 개발용이라 실제 충돌은 아니다.
장점:            E · F 다음으로 흔한 상호작용 키다. WASD 바로 위라 손이 편하다.
주의점:          "Reload / Restart" 로 오해할 수 있다.
                 나중에 로비에 다시하기·새로고침 성격의 기능이 생기면 R 을 뺏길 수 있다.
```

### 2.6 최종 키 — **`E` 로 확정** (2026-09-20)

```text
ALTAR_INTERACT_KEY = Key.E        후보 A 채택
```

확정에 따른 결과:

- **`MiniGamePortal.cs` 를 고치지 않습니다.** 5.4절의 "가장 가까운 것 하나만 반응" 공통 규칙이
  통째로 필요 없어졌습니다. F 를 골랐다면 해야 했을 작업입니다.
- **포탈(F) 과 제단(E) 이 손가락으로 구분**됩니다. 같은 키로 두 가지 일이 일어나지 않습니다.
- ⚠ **컨셉 아트의 `[F] 조각 봉헌` 문구는 `[E] 조각 봉헌` 으로 고쳐야 합니다.**
  `art/lobby-ui/island-restoration-gameplay-v3.png` 입니다. 실제 HUD 는 어차피 새로 만들므로
  아트 이미지 자체를 다시 뽑을 필요는 없고, **HUD 안의 키 문구만** 맞추면 됩니다.
- ⚠ 남은 위험은 **낚시**뿐입니다. `KeyboardFishingInputSource.cs:28` 이 E 를 텐션 올리기로 씁니다.
  낚시는 지금 `Scenes/Develop/Yongju/` 의 개발 씬에만 있고 로비에 진입로가 없습니다.
  다만 로비에 `FishingPier` 오브젝트가 있으므로, **낚시를 로비 씬 안에서 하도록 만드는 순간**
  이 표를 다시 봐야 합니다. 그때도 "걸어다니는 중" 과 "낚시 중" 은 상태가 다르므로
  상태 분기로 풀 수 있습니다.

값은 **상수 한 곳**에만 두고 인스펙터에서 바꿀 수 있게 합니다.
`MiniGamePortal.cs:53` 이 이미 그 모양이므로 그대로 따릅니다.

```csharp
[Tooltip("제단과 상호작용할 키. Lobby 키 표는 docs/prd/lobby_altar_inventory_system_design.md 2장.")]
[SerializeField] private Key interactKey = Key.E;
```

이 문서 나머지에서는 계속 `ALTAR_INTERACT_KEY` 로 적습니다. 값은 위 한 줄에서만 정해집니다.

---

## 3. 현재 Inventory 구조 분석

### 3.1 결론: 인벤토리가 없습니다

검색 결과 (`Assets/Game` + `server`, `*.cs`):

```text
inventory        0건
ItemData         0건
ItemSO           0건
itemId           0건
아이템            0건 (기능 문맥)
```

DB 에도 없습니다. 테이블은 `users` · `characters` · `character_parts` 셋뿐입니다
(`server/AraAtti.Api/Data/AraAttiDbContext.cs:30-34`).

### 3.2 가장 가까운 것 — `RewardService`

`Assets/Game/Scripts/MiniGames/Common/RewardService.cs`

```csharp
public static class RewardService
{
    private static readonly HashSet<string> OwnedFragments = new();

    public static int OwnedCount => OwnedFragments.Count;

    public static float RecoveryRatio =>
        TotalFragmentCount <= 0 ? 0f : OwnedFragments.Count / (float)TotalFragmentCount;

    public static int TotalFragmentCount { get; set; } = 3;

    public static bool Grant(string fragmentId) { ... }             // HashSet.Add
    public static void ApplyAuthoritativeGrant(string fragmentId)   // 서버 확정분 반영용 자리
    public static void LoadFrom(IEnumerable<string> fragmentIds)    // 로그인 직후 채울 자리
}
```

**이것은 인벤토리가 아닙니다.** 세 가지가 다릅니다.

| | `RewardService` 현재 | 봉헌 시스템에 필요한 것 |
|---|---|---|
| 자료구조 | `HashSet<string>` — **종류**를 센다 | **개수**를 센다 (127개, 50개 차감) |
| 의미 | 미니게임 3종 클리어 도장 3개 | 스택 가능한 재화 |
| 최대치 | `TotalFragmentCount = 3` | 1000 (전체 서버 합산) |
| 차감 | **없음.** `Remove` 가 없다 | 필수 |
| 수명 | **프로세스 메모리.** 플레이모드 나가면 사라진다 | DB 영속 |
| 권위 | 클라이언트. `MatchFlowController.cs:175` 가 로컬에서 `Grant` | 서버 |

클래스 주석이 이 상황을 이미 인정하고 있습니다:

```text
/// 지금은 실행 중에만 기억한다(플레이 모드를 나가면 사라진다). 서버 저장이
/// 생기면 Grant · Has · LoadFrom 안쪽만 서버 호출로 갈아끼우면 된다.
```

`MiniGameResultOverlay.cs:27` 에도 같은 말이 있습니다 — **"아직 아이템도 인벤토리도 없는 단계라"**.

### 3.3 그래서 어떻게 할 것인가

`RewardService` 를 **고쳐서 재사용하지 않습니다.** 역할이 다릅니다.
"세 미니게임을 다 깼다" 는 도장과 "조각 127개를 들고 있다" 는 소지품은 다른 개념이고,
전자는 결과 화면(`ResultPanelPresenter.cs:119`)이 이미 쓰고 있습니다.

**신규 `PlayerInventory` 를 만들되, `RewardService` 와 같은 모양(정적 캐시 + 이벤트)으로 만듭니다.**
그래야 팀원이 새 개념을 배우지 않습니다.

```text
RewardService        "어느 미니게임을 깼는가"     3종 도장     ← 그대로 둔다
PlayerInventory      "무엇을 몇 개 들고 있는가"   수량 캐시     ← 새로 만든다  (서버가 원본)
AltarState           "전체 봉헌량이 얼마인가"     전역 상태     ← 새로 만든다  (서버가 원본)
```

셋 다 `static` + `event Changed` 형태로 통일합니다. `RewardService.Changed` 와 같은 모양입니다.

⚠ `RewardService.RecoveryRatio` 라는 이름이 "섬 회복도" 와 겹칩니다.
**둘은 다른 값입니다.** 13장에서 정리합니다.

---

## 4. 바다의 심장 조각 데이터 구조

### 4.1 현재 상태

문자열 `"바다의 심장 조각"` 은 코드에 두 군데 있습니다.

```text
Assets/Game/Scripts/MiniGames/Common/MiniGameConfig.cs:60
    [SerializeField] private string rewardName = "바다의 심장 조각";

Assets/Game/Scripts/MiniGames/Common/Editor/CommonMatchSceneBuilder.cs:547
    TMP_Text rewardName = Label(reward.transform, "Name", "바다의 심장 조각", ...);
```

아이콘도 있습니다:

```text
Assets/Game/Art/MiniGames/Warriors/UI/SeaHeartGem.png
    (CommonMatchSceneBuilder.cs:62 의 GemPath 상수가 가리킨다)
```

식별자는 `MiniGameConfig.fragmentId` 입니다 (`MiniGameConfig.cs:66`):

```csharp
/// 조각을 구분하는 열쇠. 같은 조각을 두 번 주지 않기 위한 것이라
/// 게임마다 서로 달라야 한다. 예: "sword", "mining", "ship".
[SerializeField] private string fragmentId = "sword";
```

### 4.2 의미 충돌 — 반드시 먼저 정해야 할 것

현재 기획과 요청하신 기획이 **같은 이름으로 다른 것**을 가리킵니다.

```text
현재        "바다의 심장 조각" = 미니게임 3종의 고유 수집품 3개 (sword / mining / ship)
                                  3개 다 모으면 회복 100%

요청서      "바다의 심장 조각" = 스택 가능한 재화, 1000개 모아 제단에 봉헌
```

**이건 제가 판단할 문제가 아니라 기획 결정입니다.** 두 갈래 중 하나를 골라 주셔야 합니다.

#### 갈래 1 — 조각을 재화로 통일한다 (권장)

```text
미니게임 클리어 → sea_heart_fragment 를 N개 준다 (점수/등급에 비례)
                   ↓
             인벤토리에 누적
                   ↓
             제단에 봉헌 → 전체 1000
```

`MiniGameConfig.fragmentId` 의 "게임마다 달라야 한다" 는 규칙을 버리고
세 게임 모두 같은 아이템을 다른 **수량**으로 주게 됩니다.
`RewardService` 는 "클리어 도장" 용도로 이름만 남기거나 그대로 둡니다.

- 장점: 컨셉 아트 · 요청서 · UX 가 전부 맞아떨어진다. 구조가 하나다.
- 단점: `MatchFlowController.CompleteMiniGame` 의 보상 처리 경로를 손봐야 한다.

#### 갈래 2 — 둘을 분리한다

```text
sword / mining / ship        고유 수집품 3종   (RewardService, 기존 그대로)
sea_heart_fragment           봉헌용 재화       (PlayerInventory, 신규)
```

- 장점: `RewardService` 와 결과 화면의 "이미 보유 중" 분기를 그대로 둘 수 있다.
- 단점: 플레이어 눈에 "바다의 심장 조각" 이 두 개로 보인다. 이름을 갈라야 한다.
- ⚠ **초안에서 이 안의 장점을 과장했다.** 갈래 2 를 택해도 재화를 지급할 경로는 똑같이
  필요하고, 그 경로는 결국 `MatchFlowController.CompleteMiniGame` 을 지난다.
  거기에 `RewardService` 유지와 이름 변경이 더해지므로 **갈래 2 가 일이 더 많다.**

### 4.2.1 **결정 #1 — 갈래 1 (재화로 통일) 확정** (2026-09-20)

지급 규칙도 함께 확정되었습니다.

```text
미니게임 한 판을 성공으로 클리어 → sea_heart_fragment 1개
실패                              → 0개
같은 게임을 다시 깨도             → 또 1개   ("이미 보유 중" 이 사라진다)
                                             ⚠ 단, 섬 회복이 진행 중일 때만 (결정 #8)
```

#### 따라오는 변화

| 대상 | 전 | 후 |
|---|---|---|
| 조각의 정체 | 고유 수집품 3종 (`sword`/`mining`/`ship`) | 스택 재화 1종 (`sea_heart_fragment`) |
| 지급 | `RewardService.Grant()` → `HashSet.Add` | 서버가 수량 +1 |
| 한 사람 평생 최대 | **3개** | 제한 없음 (섬 회복이 진행 중인 동안) |
| 결과 화면 | "획득!" / "이미 보유 중" / "획득 실패" | 클리어면 "바다의 심장 조각 +1", 실패면 보상 칸을 숨긴다 |
| `MiniGameConfig.fragmentId` | 게임마다 달라야 한다 | **이 규칙이 사라진다.** 4.2.3 참고 |

#### 4.2.2 ⚠ 산수를 먼저 보셔야 합니다

**클리어 1회 = 1개**이므로 목표 1000 이 실제로 얼마나 걸리는지 계산해 두어야 합니다.

```text
4인 파티가 한 판을 깬다  →  참가자 각자 1개  →  세상에 4개가 들어온다

1000개 ÷ 4개 = 250판
한 판 3분 + 매칭·로딩 2분 ≈ 5분
250판 × 5분 = 약 21시간   (4명이 쉬지 않고)
```

**시연에서는 도달할 수 없는 숫자입니다.** 다만 이건 문제가 아니라 **이미 해결되어 있습니다** —
`targetOffering` 이 하드코딩이 아니라 **DB 컬럼**이기 때문입니다 (11.2절).

```text
기획값   altar_state.target_offering = 1000     문서와 UI 는 이 값을 기준으로 쓴다
시연값   UPDATE altar_state SET target_offering = 20 WHERE id = 1;
         → 코드 배포 없이, Unity 재빌드 없이 바꾼다
```

권장: **스키마 기본값은 1000 으로 두고, 시연 직전에 DB 에서 낮춥니다.**
코드 어디에도 1000 을 적지 않습니다.

> 나중에 "한 판에 여러 개" 로 바꾸고 싶어지면 `MiniGameConfig` 에
> `fragmentRewardAmount` 필드를 더하면 됩니다. 지금은 **상수 1** 로 두고,
> 필드조차 만들지 않는 편이 단순합니다 (`CLAUDE.md` §2).

#### 4.2.3 `RewardService` 와 `fragmentId` 는 어떻게 되는가

`RewardService` 의 `HashSet` 은 이제 조각 보유량과 무관해집니다. **그렇다고 지우지 않습니다.**

```text
할 것    MatchFlowController 가 RewardService.Grant 로 보상을 판정하던 것을 끊는다
         ResultPanelPresenter 의 "이미 보유 중" 분기를 없앤다
         RewardService 주석의 "로비의 회복 게이지가 듣는다" 를 지운다 (13.3절)

안 할 것 RewardService 클래스 자체를 지우지 않는다
         MiniGameConfig.fragmentId 필드를 지우지 않는다
```

⚠ 끊고 나면 `RewardService` 를 부르는 곳이 `MatchResultTestRig`(디버그 리그)와
`FakeNetworkService` 만 남습니다. 사실상 죽은 코드가 되지만, **이번 작업 범위 밖이라
지우지 않습니다.** (`CLAUDE.md` §3 — "기존 죽은 코드는 요청 없이 지우지 않는다")
"어느 게임을 깬 적이 있는가" 를 나중에 도감·업적으로 살릴 여지도 있습니다.

`MiniGameConfig.fragmentId` 는 `"sword"` / `"mining"` / `"ship"` 그대로 둡니다.
보상 결정에는 더 이상 쓰이지 않고, **어느 게임에서 온 클리어인지 서버에 알릴 때**
그대로 쓸 수 있습니다 (`reward_claims.game_id` 로 들어갑니다).

### 4.3 아이템 정의 제안

프로젝트의 기존 명명 규칙을 먼저 확인했습니다.

```text
서버 DB 컬럼      snake_case      users.password_hash, characters.slot_index
서버 slot 값      PascalCase      "Face", "Hair", "Shoes", "Top", "Bottom", "Accessory"
                                  (CharacterEndpoints.cs:52-55)
Unity 조각 id     lower snake     "sword", "mining", "ship"  (MiniGameConfig.cs:66)
Unity 채널 id     kebab           "srv-1", "lobby-ch1"       (ChannelCatalog.cs:58-59)
```

조각 id 는 `MiniGameConfig.fragmentId` 의 소문자 규칙을 따릅니다.

```text
itemId        sea_heart_fragment
displayName   바다의 심장 조각
icon          Assets/Game/Art/MiniGames/Warriors/UI/SeaHeartGem.png
stackable     true
maxStack      없음 (제한하지 않는다)
```

**ScriptableObject 를 만들 것인가?** — 지금은 만들지 않기를 권합니다.

아이템이 **한 종류뿐**입니다. `ItemData` SO 하나에 `CreateAssetMenu` 를 달고
인벤토리를 `Dictionary<ItemData, int>` 로 만들면, 실제로 얻는 것 없이
"SO 를 씬/프리팹에 물려야 하는" 배선 부담만 늘어납니다.
`CLAUDE.md` §2 "단일 사용 코드에 추상화를 만들지 않는다" 에 걸립니다.

대신 **상수 한 곳**으로 시작합니다.

```csharp
// Assets/Game/Scripts/Inventory/ItemIds.cs (신규, 파일 하나)
public static class ItemIds
{
    /// <summary>바다의 심장 조각. 제단에 봉헌하는 재화. 서버 item_id 와 같은 문자열이어야 한다.</summary>
    public const string SeaHeartFragment = "sea_heart_fragment";
}
```

아이템이 3종 이상으로 늘어나는 것이 **확정되면** 그때 SO 로 승격합니다.

---

## 5. 제단 Interaction 설계

### 5.1 기존 상호작용 시스템 조사 결과 — 재사용할 "공통 규약" 은 없습니다

먼저 찾아봤습니다. `MiniGamePortal.cs:27-31` 의 주석이 이미 같은 조사를 해 두었습니다.

```text
⚠ Lobby 에는 공통 상호작용 규약이 없다. 확인해 보니 ProximityPortal 은
   거리에 따라 연출만 바꾸고 입력을 읽지 않으며, TaskBase 계열은 ShipCoop 안쪽
   규약이라 IPlayerController(IoT) 에 묶여 있다.
```

제가 다시 확인한 결과도 같습니다.

| 후보 | 재사용 가능? | 이유 |
|---|---|---|
| `ProximityPortal` (`Lobby/ProximityPortal.cs`) | ✗ | 거리로 ARPG 포탈 이펙트만 켜고 끈다. 입력을 안 읽는다. 게다가 Lobby 씬에 붙어 있지도 않다 |
| ShipCoop `TaskBase` 계열 | ✗ | `IPlayerController`(IoT 장치 추상화)에 묶여 있다. 로비 플레이어에는 그 컴포넌트가 없다 |
| `MiniGamePortal` | **◎ 패턴을 그대로 베낀다** | 거리 + `Keyboard.current[key]` + `LocalPlayer.Transform`. 로비에서 검증된 유일한 방식 |

**결론: `MiniGamePortal` 의 구조를 그대로 따릅니다.** 새 규약을 만들지 않습니다.

### 5.2 Local Player 판별 — 이미 정답이 있습니다

요청서 3장에서 "다른 플레이어가 Trigger 에 들어왔다고 내 UI 가 열리면 안 된다" 고 하셨습니다.
이 프로젝트는 그 문제를 **`UnderTheSea.Network.LocalPlayer` 정적 등록소**로 이미 풀었습니다.

```csharp
// Assets/Game/Scripts/Network/LocalPlayer.cs
public static Transform Transform => current != null ? current.transform : null;
```

등록은 `LocalPlayerView.Spawned()` 에서, **`HasInputAuthority` 를 가진 피어에서만** 일어납니다
(`LocalPlayer.cs:64-71` 이 아닌 것은 경고와 함께 거부).

```text
내 클라이언트        LocalPlayer.Transform  →  내 캐릭터
남의 클라이언트      LocalPlayer.Transform  →  그 사람의 캐릭터 (내 것이 아님)
Dedicated Server     LocalPlayer.Transform  →  null (화면이 없으니 UI 도 안 연다)
```

그래서 **Trigger Collider 를 쓰지 않습니다.** 거리 계산을 씁니다.

```csharp
Transform local = LocalPlayer.Transform;
if (local == null) return;                         // 아직 접속 중 / 전용 서버
Vector3 gap = local.position - transform.position;
gap.y = 0f;                                        // 계단 위라 높이를 무시해야 한다
bool near = gap.sqrMagnitude <= radius * radius;
```

Trigger 를 안 쓰는 이유 세 가지:

1. `MiniGamePortal` · `ProximityPortal` 이 둘 다 거리 방식이다. 섞으면 두 규약이 생긴다.
2. Trigger 는 상대 쪽에 Rigidbody 가 있어야 한다. 네트워크 캐릭터 프리팹의 구성에 의존하게 된다.
3. **Trigger 는 남의 캐릭터도 들어온다.** `OnTriggerEnter` 안에서 다시 `HasInputAuthority` 를
   확인해야 하는데, 그건 `LocalPlayer` 를 쓰는 것보다 복잡하다.

제단은 **계단 위**에 있으므로 `ignoreHeight = true` 가 특히 중요합니다
(`MiniGamePortal.cs:48-49` 가 같은 이유로 기본값 `true`).

### 5.3 제단 오브젝트 구조

```text
HeartAltar                          ← Lobby.unity 의 기존 PrefabInstance (그대로 둔다)
└─ P_HeartAltar (prefab)
   ├─ PlatformBase   BoxCollider    기존
   ├─ PlatformTier2  BoxCollider    기존
   ├─ Pedestal                      기존
   ├─ Stairway       BoxCollider    기존
   ├─ HeartCrystal                  기존
   ├─ AltarBeam                     기존 ← 12장에서 이걸 껐다 켠다
   ├─ Light_Crystal                 기존
   ├─ Light_Monolith                기존
   │
   └─ AltarInteraction              ★ 신규 (빈 GameObject 1개)
      ├─ AltarInteraction.cs        ★ 신규 — 거리 판정 + 키 입력 + 안내 토글
      └─ AltarVfxController.cs      ★ 신규 — AltarBeam / Light_Monolith 를 켜고 끈다
```

⚠ **프리팹 수정은 사용자가 Unity Editor 에서 합니다.** `CLAUDE.local.md` §1 에 따라
저는 `.prefab` / `.unity` / `.meta` 를 직접 옮기거나 고치지 않습니다.

⚠ `AltarInteraction` 의 위치는 **계단 위 `Pedestal` 근처**로 잡아야 합니다.
프리팹 루트(`P_HeartAltar` 원점)에 두면 계단 아래에서도 반응합니다.
반경은 포탈과 같은 `4m` 로 시작하고, 에디터의 `OnDrawGizmosSelected` 와이어 구를 보며 조정합니다
(`MiniGamePortal.cs:160-164` 와 같은 코드를 씁니다).

### 5.4 (해당 없음) `ALTAR_INTERACT_KEY = F` 를 골랐다면 필요했을 작업

> **키가 `E` 로 확정되어 이 절의 작업은 하지 않습니다.** (2.6절)
> 나중에 로비에 상호작용이 늘어나 같은 키를 나눠 쓰게 되면 그때 다시 꺼내 보라고 남겨 둡니다.

F 를 골랐다면 **"가장 가까운 것 하나만 반응한다"** 를 코드로 못박아야 했습니다.

```text
문제:  포탈과 제단이 둘 다 F 를 읽는다. 둘 다 4m 안에 들어온 프레임에서
       어느 쪽이 먼저 Update 를 도는지는 Unity 가 보장하지 않는다.
       → "가끔 제단을 눌렀는데 미니게임으로 들어가는" 버그가 된다.

해결:  LobbyInteractables 정적 등록소 하나를 만든다.
         · 상호작용 후보가 자기를 등록한다 (거리와 함께)
         · 매 프레임 가장 가까운 하나만 "활성" 으로 표시한다
         · 활성인 것만 안내를 띄우고 키를 읽는다
       MiniGamePortal 이 이 등록소를 쓰도록 고쳐야 한다. (기존 파일 수정)
```

`E` 를 고르셨으므로 **이 작업은 통째로 사라졌습니다.** 대신 컨셉 아트의 `[F]` 문구를
`[E]` 로 고쳐야 합니다 (2.6절).

### 5.5 상태 흐름

```text
[범위 밖]
   │  거리 ≤ radius  &&  LocalPlayer.Transform != null
   ▼
[범위 안 — 안내 표시]
   │  안내 HUD 켜짐:  "[ALTAR_INTERACT_KEY] 조각 봉헌"
   │
   ├── 키 입력 ──────────────▶ [봉헌 UI 열림]
   │                                │
   │                                ├── 봉헌 성공/실패 → UI 유지, 수량만 갱신
   │                                ├── 취소 / Esc     → [범위 안] 으로 복귀
   │                                └── 범위 이탈       → UI 닫힘 (아래 정책)
   │
   └── 범위 이탈 ────────────▶ [범위 밖], 안내 HUD 꺼짐
```

**범위를 벗어났을 때 열려 있던 UI 를 닫을 것인가** — 닫는 쪽을 권합니다.

- UI 가 열려 있는 동안 이동 입력을 막으면(6.4절) 사실 걸어나갈 수가 없습니다.
- 다만 **서버 응답을 기다리는 중**에는 닫지 않습니다. 응답을 받아 결과를 보여준 뒤 닫습니다.
  그렇지 않으면 "봉헌했는지 안 했는지 모르는" 상태가 됩니다.
- 넉아웃·밀림 등으로 튕겨나가는 경우를 대비해 **닫는 거리는 여는 거리보다 크게** 잡습니다
  (`ProximityPortal.cs:24-26` 의 `openDistance 6` / `closeDistance 8` 과 같은 히스테리시스).

---

## 6. Inventory HUD 설계

> HUD 이미지는 사용자가 따로 주신다고 하셨으므로, **디자인은 만들지 않습니다.**
> 여기서는 **어떤 요소가 필요하고, 그것들이 코드와 어떻게 이어지는지**만 정합니다.

### 6.1 필요한 UI 요소와 대응하는 데이터

| UI 요소 | 타입 | 값의 출처 | 비고 |
|---|---|---|---|
| 아이콘 | `Image` | `SeaHeartGem.png` | 이미 있는 에셋 |
| 아이템명 | `TMP_Text` | 상수 `"바다의 심장 조각"` | |
| 현재 보유 수량 | `TMP_Text` | `PlayerInventory.Get(sea_heart_fragment)` | **서버가 채운 캐시** |
| 봉헌할 수량 | `TMP_Text` 또는 `TMP_InputField` | 로컬 UI 상태 | **범위는 `1 ~ maxOfferAmount`** |
| `-` / `+` | `Button` | | `+` 는 `maxOfferAmount` 에서 멈춘다. 길게 누르면 가속 |
| `MAX` | `Button` | **`maxOfferAmount`** | ⚠ 보유량이 아니다. 11.1절 |
| 봉헌 확인 | `Button` | | 요청 중 · `maxOfferAmount == 0` 이면 `interactable = false` |
| 취소 | `Button` | | |
| 오류 메시지 | `TMP_Text` | 서버 `code` + `message` (+ `remainingToTarget`) | 9.2절의 문구 조합 규칙 |
| **남은 칸 안내** | `TMP_Text` | `remainingToTarget` | **`maxOfferAmount < 보유량` 일 때만 띄운다.** 아래 |
| (전체 진행) | `Slider` / `TMP_Text` | `AltarState.TotalOffered / Target` | 선택 — 13장 HUD 와 겹칠 수 있다 |

#### 남은 칸 안내 문구

플레이어의 보유량보다 제단의 남은 칸이 적을 때만 보여 줍니다. 그때만 사용자가
**"왜 내가 가진 만큼 못 고르지?"** 라고 느끼기 때문입니다.

```text
조건   maxOfferAmount < myFragments      (즉 remainingToTarget 이 병목일 때)
문구   "섬 회복까지 {remainingToTarget}개만 더 필요합니다."

예     보유 5, 남은 칸 1  →  "섬 회복까지 1개만 더 필요합니다."   MAX = 1
       보유 5, 남은 칸 3  →  "섬 회복까지 3개만 더 필요합니다."   MAX = 3
       보유 5, 남은 칸 9  →  (안내 없음)                          MAX = 5
```

`maxOfferAmount == 0` 이면 안내를 회복 완료 문구로 바꾸고 봉헌 버튼을 잠급니다.

```text
"섬 회복이 완료되어 더 이상 봉헌할 수 없습니다."
```

### 6.2 UI 를 어디에 두는가 — 씬에 올리지 않습니다

이 프로젝트에는 **명확한 관례**가 있습니다. `LobbyChatInstaller.cs:10-22` 가 이유를 적어 뒀습니다.

```text
왜 씬에 올려 두지 않는가. LobbyTutorial 과 같은 이유다.
  · Fusion 은 세션을 시작하면서 씬을 러너 전용 씬으로 인수한다. 그때 없어지거나 옮겨진다
  · 큰 Lobby 씬을 건드리지 않아도 된다 (CONVENTION.md 3장)
```

`Lobby.unity` 는 **22 MB** 입니다. 여러 명이 동시에 고치면 병합 지옥이 됩니다.
`CONVENTION.md` §3 "Main Scene 규칙" 도 같은 말을 합니다.

**그래서 채팅·튜토리얼과 똑같은 방식을 씁니다.**

```text
프리팹     Assets/Game/Resources/AltarOfferingUI.prefab     ★ 신규
설치기     AltarOfferingInstaller.cs                        ★ 신규
           [RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]
           LocalPlayer.Registered += 내 캐릭터가 생기면 만든다
           SceneManager.sceneLoaded += 로비가 아니면 숨긴다
           DontDestroyOnLoad
```

⚠ `LobbyChatInstaller.cs:33-37` 의 방어를 반드시 베껴야 합니다.

```csharp
// 화면이 없는 프로세스(Dedicated Server 등)에는 UI 를 만들지 않는다.
if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
```

⚠ `LobbyChatInstaller.cs:44-65` 의 `try/catch` 도 베껴야 합니다.
이 콜백은 `LocalPlayerView.Spawned` **한가운데**서 불리고, 그 뒤에 카메라를 붙이는 코드가 있습니다.
여기서 예외가 새면 **카메라가 안 붙어 "Lobby에 접속 중..." 에서 영영 멈춥니다.** 실제로 겪은 사고입니다.

### 6.3 수량 입력 방식 비교

| 방식 | 장점 | 단점 | 이 프로젝트에서 |
|---|---|---|---|
| **`TMP_InputField` 단독** | 큰 수(127개)를 한 번에 | 문자·음수·소수·공백 전부 방어해야 함. **키보드 포커스가 이동 입력과 충돌** | 위험 |
| **`-` / `+` 버튼 단독** | 검증 불필요. 항상 유효한 정수 | 100개 봉헌하려면 100번 누른다 | 단독으로는 부족 |
| **`Slider`** | 범위가 시각적. 항상 유효 | 정확히 50 을 맞추기 어렵다. 보유량 0일 때 모양이 애매 | 보조용 |
| **`- / + / MAX` + 읽기 전용 표시** | 검증 불필요, 빠름, 이동 입력과 안 부딪힘 | 임의의 수(37개)를 넣기 번거로움 | **권장** |
| **`- / + / MAX` + `TMP_InputField`** | 위 장점 + 직접 입력 가능 | InputField 포커스 관리 필요 | 차선 |

**권장: `- / + / MAX` + 읽기 전용 수량 표시로 시작합니다.**

⚠ **결정 #1 로 이 선택이 더 분명해졌습니다.** 클리어 1회당 조각 1개이므로, 플레이어가
한 번에 들고 있는 수량은 보통 **한 자리 수**입니다. 컨셉 아트의 "보유량 127" 같은 상황은
당분간 오지 않습니다. 그러면 `TMP_InputField` 로 숫자를 직접 치는 일이 거의 없고,
실제로 가장 많이 눌리는 버튼은 **`MAX`** 가 됩니다.

이유는 **키보드 포커스 문제**입니다. 이 프로젝트에서 `TMP_InputField` 는
`ChatFocus` 를 통해 이동 입력을 멈춰야만 안전합니다.

```csharp
// LobbyChatView.cs:94-95
input.onSelect.AddListener(_   => ChatFocus.Begin());
input.onDeselect.AddListener(_ => ChatFocus.End());
```

`ChatFocus.Typing` 이 켜지면 `PlayerInputProvider.cs:25` 가 이동을 통째로 0 으로 보냅니다.
껐다 켜는 책임을 지킬 곳이 하나 더 늘어납니다.
`ChatFocus.cs:18-19` 가 경고하듯, **켠 쪽이 안 끄면 영영 못 움직이게 됩니다.**

`+` 버튼은 **길게 누르면 가속**(0.4초 후 초당 10 → 50)하게 하면 100개도 2초면 됩니다.

> 나중에 `TMP_InputField` 를 추가하신다면 `ChatFocus.Begin/End` 연결은 **필수**입니다.
> 채팅이 이미 정확히 그렇게 하고 있으니 그 코드를 그대로 베끼면 됩니다.

### 6.4 UI 가 열려 있는 동안의 입력 정책

```text
이동 (WASD/Space/Shift)   막는다.  단, ChatFocus 를 지금 모양 그대로 쓰면 버그가 난다.
                                    반드시 6.4.1 을 먼저 읽는다.

카메라 (우클릭 드래그)     막지 않는다.  LocalPlayerView 는 ChatFocus 를 보지 않는다.
                                    막고 싶으면 LocalPlayerView 를 고쳐야 하는데,
                                    막을 이유가 딱히 없다. 그대로 둔다

Esc                       UI 를 닫는다.
                          ⚠ 채팅 입력 중이면 채팅이 먼저 먹는다 (LobbyChatView.cs:150)
                          ⚠ 6.4.1 로 바꾸고 나면 제단 UI 자신도 ChatFocus 보유자다.
                            그래서 `ChatFocus.Typing == false` 를 조건으로 쓰면
                            **제단 UI 의 Esc 가 영영 안 먹는다.** "나 말고 다른 보유자가
                            있는가" 를 물어야 한다 → 6.4.1 의 HeldByOther(this)

E (제단)                  UI 가 열려 있으면 무시한다. 토글로 만들지 않는다.
                          닫는 길은 Esc · 취소 버튼 · 범위 이탈 셋이다

F (포탈)                  신경 쓸 것 없다. 키가 다르고(결정 #0) 제단과 80m 떨어져 있다

채팅                      제단 UI 를 연 채 채팅을 켜고 꺼도 이동은 계속 막힌다.
                          6.4.1 의 보유자 집합이 그것을 보장한다.
                          (지금 구조 그대로면 이동이 풀린다 — 그것이 6.4.1 의 결함 1)
```

### 6.4.1 ⚠ `ChatFocus` 는 지금 모양으로 재사용할 수 없습니다

초안에서 저는 "제단 UI 를 열 때 `ChatFocus.Begin()`, 닫을 때 `End()`" 라고 적었습니다.
**실제 코드를 확인해 보니 그대로 하면 깨집니다.**

#### 결함 1 — 플래그가 하나뿐이라 뒤에 끈 쪽이 이깁니다

```csharp
// ChatFocus.cs:24-35 — 참조 계수가 없는 단순 bool 이다
public static bool Typing { get; private set; }
public static void Begin() { Typing = true; }
public static void End()   { Typing = false; }
```

```text
제단 UI 열림      Begin()  → Typing = true      이동 막힘
채팅칸 클릭       Begin()  → Typing = true      (LobbyChatView.cs:94)
채팅 바깥 클릭    End()    → Typing = false     (LobbyChatView.cs:95)
제단 UI 는 아직 열려 있다                       ← 그런데 이동이 다시 살아난다
```

제단 UI 를 띄운 채 캐릭터가 걸어갑니다. 걸어서 범위를 벗어나면 UI 가 닫힙니다.

#### 결함 2 — 단순 참조 계수로 바꾸면 **기존 채팅 코드가 깨집니다**

`LobbyChatView` 의 `End()` 호출 세 곳 중 **둘이 짝이 맞지 않습니다.**

```text
:94  onSelect    → Begin()      짝 맞음
:95  onDeselect  → End()        짝 맞음
:144 OnDisable   → End()        ⚠ Begin 을 부른 적 없어도 무조건 부른다
:262 Unfocus()   → DeactivateInputField() 가 onDeselect 를 쏴 End() 가 불리고
                   그 **직후에 End() 를 또 부른다**   ⚠ 한 번의 Begin 에 End 두 번
```

여기에 `int counter` 를 넣으면 카운트가 음수로 내려가고, 그러면 제단 UI 가 걸어 둔
잠금까지 같이 풀립니다. **참조 계수는 답이 아닙니다.**

#### 해결 — 누가 잠갔는지를 기억하는 집합

```csharp
// ChatFocus.cs — 이렇게 바꾼다
private static readonly HashSet<object> holders = new();

public static bool Typing => holders.Count > 0;

public static void Begin(object owner) { if (owner != null) holders.Add(owner); }
public static void End(object owner)   { if (owner != null) holders.Remove(owner); }
```

`HashSet.Add` · `Remove` 는 **몇 번을 불러도 결과가 같습니다.**

```text
End 를 두 번 불러도    두 번째는 아무 일도 안 한다   → :262 가 그대로 돌아간다
Begin 없이 End 해도    아무 일도 안 한다             → :144 가 그대로 돌아간다
채팅이 풀어도          제단이 걸어 둔 것은 남는다     → 결함 1 이 사라진다
```

**기존 채팅 코드의 짝 안 맞는 호출이 저절로 안전해집니다.** 그 코드를 고칠 필요가 없습니다.

#### 고쳐야 하는 곳 (3파일, 호출부 4곳)

```text
UI/ChatFocus.cs              위 구현으로 교체 + 클래스 주석 갱신
UI/LobbyChatView.cs:94,95,144,262   인자에 this 를 넘긴다
Network/PlayerInputProvider.cs:25   ⚠ 고치지 않는다. ChatFocus.Typing 을 그대로 읽는다
```

⚠ **이름은 `ChatFocus` 로 둡니다.** 이제 "글자를 치는 중" 보다 "게임플레이 입력을 막는
화면이 떠 있음" 에 가까워져 이름이 정확하지 않지만, 바꾸면 `PlayerInputProvider` 까지
건드려야 합니다. 그리고 이 클래스의 원래 주석이 확장을 이미 예고하고 있습니다 —

> `ChatFocus.cs:10-11` — "나중에 아이템창·상점처럼 글자를 받는 화면이 늘어도
> 여기에 한 줄 얹으면 끝이다."

`GameplayInputLock` 등으로 바꾸는 것은 언제든 할 수 있는 3파일짜리 후속 작업으로 남깁니다.
**클래스 주석에 그 사실을 적어 둡니다.**

#### 제단 UI 쪽 규칙

```csharp
// AltarOfferingUIController
private void Open()     { ChatFocus.Begin(this); ... }
private void Close()    { ChatFocus.End(this);   ... }
private void OnDisable(){ ChatFocus.End(this);   }   // 두 번 불려도 안전하다
```

`ownsFocus` 같은 플래그가 필요 없습니다. 집합이 그 역할을 합니다.

#### Esc 를 위해 한 가지 더 — "나 말고 누가 잠갔는가"

제단 UI 가 열려 있으면 **자기 자신이 보유자**이므로 `ChatFocus.Typing` 은 언제나 참입니다.
그 값으로 Esc 를 거르면 제단 UI 는 Esc 로 절대 닫히지 않습니다.

```csharp
// ChatFocus.cs 에 한 줄 더
public static bool HeldByOther(object me)
{
    foreach (object h in holders) if (!ReferenceEquals(h, me)) return true;
    return false;
}
```

```csharp
// AltarOfferingUIController.Update
// 채팅칸이 켜져 있으면 Esc 는 채팅 것이다. 그때는 손대지 않는다.
if (!ChatFocus.HeldByOther(this) && Keyboard.current.escapeKey.wasPressedThisFrame)
    Close();
```

### 6.4.2 UI 가 들고 있는 값은 **스냅샷**입니다 — 2단계 검증

이번 정책에서 가장 오해하기 쉬운 부분입니다.

```text
UI 를 열 때 조회한 995 / 1000 이
[봉헌] 을 누르는 순간까지 그대로라는 보장이 없다.
그 사이 다른 플레이어가 봉헌할 수 있다.
```

그래서 검증을 **두 단계**로 나눕니다. **역할이 다릅니다.**

| 단계 | 누가 | 무엇을 | 목적 |
|---|---|---|---|
| 1단계 | 클라이언트 UI | 스냅샷의 `maxOfferAmount` 로 선택 범위를 제한 | **사용성.** 애초에 못 고르게 해서 실패를 줄인다 |
| 2단계 | 서버 | 봉헌 처리 순간의 최신 DB 값으로 재검증 | **무결성.** 불변식을 지키는 것은 여기뿐이다 |

⚠ **1단계는 무결성을 보장하지 않습니다.** 조작된 클라이언트는 얼마든지 큰 값을 보낼 수
있고, 정상 클라이언트도 스냅샷이 낡았을 수 있습니다. **최종 책임은 언제나 서버입니다.**

⚠ **그렇다고 1단계를 빼면 안 됩니다.** 빼면 "가진 만큼 골랐는데 서버가 거절" 이 일상이
됩니다. 1단계는 그 경험을 드물게 만드는 장치입니다.

#### 실패했을 때 UI 가 하는 일

```text
서버가 OFFERING_AMOUNT_CHANGED 를 돌려준다 (응답에 최신 상태가 들어 있다)
  ↓
보유량 · 전체 진행도 · maxOfferAmount 를 응답값으로 덮어쓴다
  ↓
선택 수량이 새 maxOfferAmount 보다 크면 그 값으로 낮춘다   (5 → 2)
  ↓
"다른 플레이어가 먼저 봉헌했습니다. 섬 회복까지 2개 남았습니다." 를 띄운다
  ↓
봉헌 버튼을 다시 활성화한다
```

⚠ **자동으로 다시 봉헌하지 않습니다.** 사용자가 바뀐 수량을 보고 스스로 `[봉헌]` 을
다시 눌러야 합니다. 자동 재시도는 "내가 5개를 내려 했는데 2개가 나갔다" 와 같아지고,
그건 10.3.2절에서 배제한 부분 수락과 결과가 같습니다.

⚠ 실패 응답이 최신 상태를 싣고 오므로(9.2절) **여기서 `GET /api/altar/state` 를 다시
부르지 않습니다.** 한 번 더 부르면 그 사이 또 바뀔 수 있어 끝이 없습니다.

### 6.5 다른 UI 와 동시에 열리는 문제

로비에서 동시에 뜰 수 있는 화면은 지금 셋입니다.

```text
LobbyTutorial       LobbyTutorial.Started / Finished 이벤트를 쏜다
LobbyChatView       튜토리얼이 도는 동안 스스로 숨는다 (LobbyChatView.cs:100-102)
AltarOfferingUI     ★ 신규
```

`LobbyChatView` 가 이미 확립한 규칙을 그대로 따릅니다.

```csharp
// LobbyChatView.cs:100-111 과 같은 모양
LobbyTutorial.Started  += Hide;
LobbyTutorial.Finished += Show;
if (LobbyTutorial.IsRunning) Hide();
```

즉 **튜토리얼이 도는 동안 제단 상호작용은 아예 막습니다.**
튜토리얼은 몇십 초면 끝나고, 그동안 화면 구석이 복잡해지는 편이 더 나쁩니다.

---

## 7. 봉헌 UX Flow

```text
① 제단 접근 (계단 위, 반경 4m)
   화면 하단:  [ALTAR_INTERACT_KEY] 조각 봉헌

              ↓ 키 입력

② 봉헌 UI 열림 + ChatFocus.Begin(this)   ← 6.4.1
   ⚠ 이 순간 서버에 현재 상태를 한 번 물어본다 (GET /api/altar/state)
      보유량도, 제단의 남은 칸도 그 사이 바뀌었을 수 있다.
      여기서 받은 값은 **스냅샷**이다 (6.4.2).

   [로딩]  불러오는 중...
              ↓

③  ┌────────────────────────────────────┐        보유 5, 남은 칸 1 인 경우
    │  🔷  바다의 심장 조각                │
    │      보유량  5                      │        maxOfferAmount = min(5, 1) = 1
    │                                    │
    │   봉헌 수량                          │        [+] 를 눌러도 1 에서 멈춘다
    │   [ - ]    1    [ + ]  [MAX]        │        [MAX] 도 1 이다
    │                                    │
    │   섬 회복까지 1개만 더 필요합니다.      │        ← maxOfferAmount < 보유량일 때만
    │                                    │
    │        [ 봉헌 ]   [ 취소 ]           │        maxOfferAmount == 0 이면 잠긴다
    └────────────────────────────────────┘

              ↓ [봉헌]

④ 요청 중
   · 봉헌 · 취소 · -/+/MAX 전부 interactable = false
   · "봉헌하는 중..." 표시
   · requestId (Guid) 를 만들어 함께 보낸다 → 10.4
              ↓
        서버 응답 (성공이든 실패든 최신 상태가 함께 온다 — 9.2)
              ↓
   ┌──────────────────────┴──────────────────────┐
   ▼                                             ▼
⑤-성공  (요청한 수량이 그대로)              ⑤-실패  (아무것도 바뀌지 않았다)

 "1개를 봉헌했습니다"                        code 에 따라 문구를 띄운다
                                            · OFFERING_AMOUNT_CHANGED
 보유량   5 → 4     (서버가 준 값)             "다른 플레이어가 먼저 봉헌했습니다.
 전체   999 → 1000  (서버가 준 값)              섬 회복까지 2개 남았습니다."
 섬 회복도 HUD 갱신                          · OFFERING_CLOSED
                                              "섬 회복이 완료되어 더 이상
 ⚠ 부분 수락은 없다. 1개를 냈으면              봉헌할 수 없습니다."
    정확히 1개가 나갔다.                     · NOT_ENOUGH_FRAGMENTS
                                              서버 문구 그대로

                                            보유량 · 전체 · maxOfferAmount 를
                                            응답값으로 덮어쓴다
                                            선택 수량이 새 최대치보다 크면 낮춘다 (5 → 2)
                                            버튼 다시 활성화
                                            ⚠ UI 를 닫지 않는다
                                            ⚠ 자동으로 다시 봉헌하지 않는다.
                                               사용자가 [봉헌] 을 다시 누른다

              ↓ totalOffered == targetOffering 이면

⑥ 돌기둥 각성 연출 활성화
   · 내 화면: 응답의 altarActivated 를 보고 즉시
   · 남의 화면: Fusion RPC 로 "제단 상태가 바뀌었다" 를 받고 다시 조회 → 12.5
```

**원칙 1 — 클라이언트가 빼고 더하지 않습니다.**
⑤에서 `remainingFragments` · `totalOffered` · `maxOfferAmount` 는 **서버 응답값을 그대로
대입**합니다. 로컬 계산이 서버와 1이라도 어긋나면 그 어긋남이 다음 봉헌까지 따라갑니다.

**원칙 2 — 성공은 전부, 실패는 전무입니다.**
요청한 수량이 그대로 나가거나, 한 개도 나가지 않거나 둘 중 하나입니다 (10.3.2절).

**원칙 3 — 실패 응답이 최신 상태를 싣고 옵니다.**
그래서 실패 직후 `GET /api/altar/state` 를 다시 부르지 않습니다.

---

## 8. 서버 권위 봉헌 처리

### 8.1 이 설계에서 가장 어려운 부분

1.2절의 사실을 다시 씁니다.

```text
Fusion Dedicated Server  ─  진짜 권위 서버인데, 플레이어가 DB 의 누구인지 모른다
AraAtti.Api              ─  DB 와 신원을 아는데, 게임 플레이에 관여하지 않는다
```

"서버 권위" 를 어느 쪽에 맡길지에 따라 설계가 완전히 달라집니다. 세 안을 놓고 비교합니다.

### 8.2 A안 — REST API 권위 + Fusion 은 알림만 (권장)

```text
클라이언트                      AraAtti.Api                 Fusion 서버
    │                              │                          │
    │ POST /api/altar/offer        │                          │
    │ Authorization: Bearer <JWT>  │                          │
    │ { amount:50, requestId:... } │                          │
    ├─────────────────────────────▶│                          │
    │                              │ ① JWT sub → users.id     │
    │                              │ ② amount 검증            │
    │                              │ ③ 트랜잭션                │
    │                              │    인벤토리 차감          │
    │                              │    전역 합산 증가         │
    │                              │ ④ 최종 상태 반환          │
    │◀─────────────────────────────┤                          │
    │ { remainingFragments: 77,    │                          │
    │   totalOffered: 730,         │                          │
    │   altarActivated: false }    │                          │
    │                              │                          │
    │ Rpc_NotifyAltarChanged()     │                          │
    ├──────────────────────────────┼─────────────────────────▶│
    │                              │                          │ 같은 채널 모두에게
    │◀─────────────────────────────┼──────────────────────────┤ Rpc_AltarChanged()
    │                              │                          │
    │ 각 클라이언트가 GET /api/altar/state 로 최신값을 받아간다  │
```

**Fusion RPC 는 "숫자" 를 나르지 않습니다. "바뀌었으니 다시 물어봐라" 만 나릅니다.**
그래야 클라이언트가 RPC 인자에 거짓 숫자를 넣어도 아무 의미가 없습니다.

| | |
|---|---|
| 장점 | Fusion 서버에 인증·DB 를 붙이지 않아도 된다. **지금 구조에서 바로 된다** |
| | 신원은 JWT 가 확정한다 — 클라이언트가 `playerId` 를 못 보낸다 (요청서 7장 요구 충족) |
| | 수량 검증·차감·합산이 전부 DB 트랜잭션 안에서 일어난다 (요청서 6장 요구 충족) |
| | 서버 재시작·채널 이동·재로그인에도 값이 남는다 |
| 단점 | 클라이언트가 API 를 직접 부른다. **Fusion 서버는 봉헌이 일어난 줄 모른다** |
| | 브로드캐스트가 "알림 + 재조회" 라 두 번 왕복한다 (실측 부담은 작다 — 로비 인원 100명 기준) |
| 남는 위험 | 클라이언트가 봉헌 API 를 **안 부르고** 인벤토리 숫자만 바꿔 보여줄 수는 있다. 하지만 서버가 그걸 안 믿으므로 **자기 화면만 거짓말**하게 된다. 실질 피해 없음 |

### 8.3 B안 — Fusion 서버 권위 (Fusion 서버가 API 를 호출)

```text
클라이언트  ──Rpc_RequestOffer(amount, requestId)──▶  Fusion 서버
                                                          │ PlayerRef → userId ???
                                                          ▼
                                                    AraAtti.Api  (서버 전용 토큰)
```

**먼저 해결해야 하는 것들:**

1. **`PlayerRef` → `users.id` 매핑.** 현재 없습니다. 만들려면 접속 시 JWT 를
   `StartGameArgs.AuthValues` 또는 connection token 으로 실어 보내고, Fusion 서버가
   그것을 API 에 검증 요청해야 합니다. `FusionLauncher.cs:208-217` 과
   `FusionNetworkService.OnCustomAuthenticationResponse` 를 모두 손대야 합니다.
2. **서버 전용 인증.** Fusion 서버 빌드가 쓸 서비스 계정 토큰을 API 에 추가해야 합니다.
3. **Unity 서버 빌드에서의 HTTP.** `UnityWebRequest` 는 돌지만, 코루틴 기반인
   `HttpJson` 을 `NetworkBehaviour` 안에서 쓰는 모양을 새로 잡아야 합니다.

| | |
|---|---|
| 장점 | 진짜 서버 권위. 클라이언트는 API 주소조차 몰라도 된다 |
| | Fusion 서버가 결과를 직접 브로드캐스트하므로 한 번 왕복 |
| 단점 | **작업량이 A안의 3~4배다.** 인증 파이프라인을 새로 만들어야 한다 |
| | 실패 지점이 늘어난다. Fusion 서버가 API 에 못 붙으면 봉헌이 통째로 막힌다 |

### 8.4 C안 — Fusion 메모리만 (영속 없음)

전역 봉헌량을 Fusion 서버의 `[Networked]` 프로퍼티로만 들고 있는 안입니다.

| | |
|---|---|
| 장점 | 제일 빠르게 만들 수 있다. Late Join 이 Fusion 의 `[Networked]` 로 공짜로 해결된다 |
| 단점 | **서버를 재시작하면 0으로 돌아간다** |
| | **채널마다 값이 다르다.** `lobby-ch1` 은 1000, `lobby-ch2` 는 0 (`ChannelCatalog.cs:56-60`) |
| | 인벤토리 차감을 할 데가 없다 (인벤토리가 DB 에 있어야 하므로) |

`섬 회복도` 가 **모든 플레이어가 함께 쌓는 장기 누적 콘텐츠**라면 C안은 기획과 맞지 않습니다.

### 8.5 권장

**A안을 권합니다.** 이유:

1. 요청서 16장의 보안 요구(`클라이언트가 전체 봉헌량 직접 변경 금지`, `서버가 amount 검증`,
   `서버가 실제 Inventory 조회`, `서버가 차감`, `서버가 최종 상태 반환`)를 **전부 만족**합니다.
2. 현재 코드 구조에서 **새 프레임워크를 하나도 안 추가**하고 됩니다 (요청서 7장 요구).
3. B안의 인증 파이프라인은 그 자체로 별도 PRD 크기의 작업입니다.
4. A안에서 B안으로 나중에 옮길 수 있습니다. **API 스펙은 그대로 두고 호출 주체만 바뀝니다.**

### 8.6 서버가 검증하는 것 (요청서 6장 대응)

`POST /api/altar/offer` 핸들러가 **이 순서대로** 확인합니다.

| # | 검증 | 실패 시 | 근거 코드 패턴 |
|---|---|---|---|
| 1 | 로그인된 플레이어인가 | `401 TOKEN_INVALID` | `.RequireAuthorization()` + `principal.TryGetUserId` (`CharacterEndpoints.cs:70, 90`) |
| 2 | ~~이 계정에 캐릭터가 있는가~~ | — | **필요 없음.** 결정 #6 으로 인벤토리가 `users` 에 매달린다 (15.4절) |
| 3 | `amount` 가 정수인가 | `400 AMOUNT_INVALID` | JSON `int` 로 받으면 파싱 단계에서 걸린다 |
| 4 | `amount > 0` 인가 | `400 AMOUNT_INVALID` | |
| 5 | `amount ≤ 상한` 인가 | `400 AMOUNT_TOO_LARGE` | `int.MaxValue` 오버플로 방어. 상한은 보유량으로 충분 |
| 6 | `requestId` 형식이 맞는가 | `400 REQUEST_ID_INVALID` | Guid 문자열 |
| 7 | 같은 `requestId` 가 이미 처리됐는가 | `200` + **이전 결과 그대로** | 10장 |
| 8 | **남은 칸이 `amount` 이상인가** (`targetOffering - totalOffered >= amount`) | `409 OFFERING_AMOUNT_CHANGED` 또는 `409 OFFERING_CLOSED` | **DB 안에서 원자적으로** — 10.3.1. 아래 8.6.1 |
| 9 | 보유량 ≥ `amount` 인가 | `409 NOT_ENOUGH_FRAGMENTS` | **DB 안에서 원자적으로** — 10.3.1 |

#### 8.6.1 8번과 9번은 **읽어서 판단하지 않습니다**

두 검증 모두 `WHERE` 조건으로 들어가고, **영향받은 행 수 0** 이 곧 실패입니다.
먼저 `SELECT` 해서 판단하고 나중에 `UPDATE` 하면 그 사이에 다른 요청이 끼어들어
**1001/1000 이 만들어집니다** (10.2절 ✗2).

#### 8.6.2 8번 실패를 두 코드로 나눕니다

행이 0 개 바뀐 뒤 최신 상태를 읽어 `remainingToTarget` 을 보고 가릅니다.

```text
remainingToTarget == 0   → OFFERING_CLOSED
                           "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다."

remainingToTarget  > 0   → OFFERING_AMOUNT_CHANGED
   (요청량보다 작음)        "다른 플레이어가 먼저 봉헌했습니다."
                           (화면 문구는 9.2절에서 클라이언트가 조합한다)
```

둘을 가르는 이유는 **사용자에게 다음 행동이 다르기 때문**입니다.
전자는 끝난 것이고, 후자는 수량을 낮춰 다시 누르면 됩니다.

⚠ **어느 실패든 인벤토리는 한 개도 차감되지 않습니다.** 10.3.1 이 제단을 먼저 건드리므로
8번에서 걸리면 9번에 닿지도 않고, 9번에서 걸리면 트랜잭션이 통째로 되돌아갑니다.
**부분 차감은 어떤 경로로도 일어나지 않습니다.**

⚠ 실패 응답은 **반드시 기존 형식**을 따릅니다.

```json
{ "code": "NOT_ENOUGH_FRAGMENTS", "message": "보유한 조각이 모자랍니다. (보유 20 / 요청 30)" }
```

`message` 는 **한국어**이고 화면에 그대로 띄울 수 있어야 합니다
(`CharacterEndpoints.cs:17` "실패는 ErrorResponse(code, message) 로 나가고 message 는 한국어다").
Unity 쪽은 `HttpJson.Interpret` 가 이미 `message` 를 꺼내 줍니다 (`HttpJson.cs:126-131`).

---

## 9. API 또는 Network Message 설계

### 9.1 기존 규약 (반드시 따를 것)

| 항목 | 규약 | 근거 |
|---|---|---|
| 경로 | `/api/<복수명사>` | `/api/auth/...`, `/api/characters` |
| 그룹 | `MapGroup(...).WithTags(...).RequireAuthorization()` | `CharacterEndpoints.cs:67-70` |
| 신원 | **언제나 JWT 의 `sub`.** 본문의 `userId` 는 받지 않는다 | `CharacterEndpoints.cs:15` |
| 오류 | `ErrorResponse(code, message)`, message 는 한국어 | `Contracts/AuthContracts.cs` |
| 등록 | `Program.cs` 에 `app.MapXxxEndpoints();` 한 줄 | `Program.cs:164, 170` |
| Unity 경로 상수 | `HttpApiConfig` 에 모은다 | `Account/HttpApiConfig.cs:43-45` |

### 9.2 신규 엔드포인트 3개

#### `GET /api/inventory`

```json
200 OK
{
  "items": [
    { "itemId": "sea_heart_fragment", "displayName": "바다의 심장 조각", "quantity": 127 }
  ]
}
```

아이템이 하나뿐이어도 **배열**로 돌려줍니다. `CharacterListResponse` 가 같은 이유로
"없으면 빈 배열이다. 404 가 아니다" 라고 적고 있습니다 (`CharacterEndpoints.cs:74`).

#### `GET /api/altar/state`

```json
200 OK
{
  "totalOffered":       730,
  "targetOffering":     1000,
  "remainingToTarget":  270,
  "myFragments":        77,
  "maxOfferAmount":     77,
  "altarActivated":     false,
  "recoveryPercent":    73.0,
  "myOfferedTotal":     50,
  "updatedAt":          "2026-09-20T10:15:00.000000Z"
}
```

- **`remainingToTarget` 과 `maxOfferAmount` 를 서버가 계산해서 내려줍니다.** UI 가
  `min(보유량, 남은 칸)` 을 스스로 계산하면 공식이 두 군데에 생깁니다 (11.1절).

```text
remainingToTarget = max(0, targetOffering - totalOffered)
maxOfferAmount    = min(myFragments, remainingToTarget)
```

- `recoveryPercent` 도 **서버가 계산**합니다 (결정 #3):
  `min(totalOffered / targetOffering, 1) * 100`. 정상 데이터에서는 `totalOffered` 가
  `targetOffering` 을 넘지 않으므로 `min` 은 사실상 방어 코드입니다 (10.7절).
- `myFragments` 를 같이 넣으면 UI 를 열 때 호출이 한 번으로 끝납니다.
- `altarActivated` 도 **서버가 판단**합니다 (요청서 8장 요구).

⚠ **이 응답은 그 순간의 스냅샷입니다.** 6.4.2절의 2단계 검증을 반드시 읽으세요.

#### `POST /api/inventory/clear-reward`

미니게임 클리어 보상입니다. 결정 #1 로 **수량은 언제나 1** 이므로 요청에 수량이 없습니다.

```json
요청
{ "gameId": "mining", "matchKey": "mine-1:mining:9f2c1b4e6a404b3f9a1d9c2f6f3e77aa" }
```

⚠ **요청이 수량을 정하지 않습니다.** 서버가 1 을 더합니다. 클라이언트가 `amount: 999` 를
보낼 여지 자체를 없앱니다.

⚠ `matchKey` 로 멱등성을 겁니다. 한 판에 한 번만 지급됩니다 (STEP 11).

```text
matchKey = {sessionName}:{gameId}:{matchInstanceId}

sessionName      미니게임의 Fusion 세션 이름
                   mine-1      (MineNet.cs:34)
                   shipcoop-1  (ShipCoopNet.cs:43)
                   warriors-1  (WarriorsNet.cs:34)
                 실행 인자 -session 으로 바꿀 수 있다
gameId           MiniGameConfig.fragmentId — "sword" · "mining" · "ship"
matchInstanceId  판 시작 시 StateAuthority 가 만든 Guid.NewGuid().ToString("N") — 정확히 32자
```

⚠ **고유성은 `matchInstanceId` 가 전부 책임집니다.** `sessionName` 은 지금 고정값이라
고유성에 기여하지 않습니다. Tick · Seed 기반 값을 쓰지 않는 이유는 STEP 11 에 있습니다.

**① 지급 성공**

```json
200 OK
{ "success": true, "granted": true, "quantity": 12, "duplicate": false }
```

**② 섬 회복이 완료되어 지급하지 않음** (결정 #8)

```json
200 OK
{ "success": true, "granted": false, "code": "ALTAR_COMPLETED", "quantity": 12, "duplicate": false }
```

⚠ **`200 OK` 입니다. `409` 도 `422` 도 아닙니다.**

```text
미니게임 클리어 자체는 정상이다.  게임 결과는 성공이고 점수도 정상이다.
보상만 지급되지 않은 것이다.
```

클라이언트가 이것을 오류로 다루면 결과 화면이 실패처럼 보입니다. **클리어 여부와
조각 지급 여부는 별개입니다** (18장 원칙, STEP 11).

**③ 같은 `matchKey` 재요청** (응답 유실 후 재시도)

```json
200 OK
{ "success": true, "granted": true, "quantity": 12, "duplicate": true }
```

이미 지급된 판입니다. **다시 +1 하지 않고, 지급되었다는 사실을 그대로 돌려줍니다.**

⚠ **이때 제단 완료 여부를 보지 않습니다.** 첫 요청이 완료 전에 지급을 확정했다면,
그 사이 제단이 완료되었더라도 결과는 `granted: true` 입니다.
확정된 지급을 뒤늦게 취소하지 않습니다 (11.4.6 Case A).

⚠ `quantity` 는 **지금 시점의 보유량**입니다. 10.4절의 멱등 응답 계약과 같습니다 —
"그때의 값" 이 아니라 "지금의 값" 을 돌려줍니다.

**④ 완료 상태에서 같은 `matchKey` 재요청**

```json
200 OK
{ "success": true, "granted": false, "code": "ALTAR_COMPLETED", "quantity": 12, "duplicate": false }
```

`duplicate` 가 `false` 인 이유는 **`reward_claims` 에 행을 만들지 않기 때문**입니다.
매 요청이 제단 상태를 새로 확인해 같은 답을 냅니다 (11.4.7 방식 A).

**⑤ 60초 쿨다운에 걸림** (11.4.15)

```json
429 Too Many Requests
{
  "success": false,
  "code": "REWARD_COOLDOWN",
  "message": "보상을 다시 받을 수 있을 때까지 잠시 기다려 주세요.",
  "retryAfterSeconds": 23
}
```

⚠ **②(`ALTAR_COMPLETED`)와 달리 `200` 이 아닙니다.** 완료는 정상적으로 도달하는
상태지만, 쿨다운은 정상 플레이에서 나오지 않는 이상 신호입니다.

⚠ **상태 코드는 `429` 로 확정입니다. `409` 로 바꾸지 않습니다** (11.4.15).
rate limit 성격의 응답이라 HTTP 의미상 `429 Too Many Requests` 가 가장 직접적입니다.

⚠ 클라이언트는 이것을 결과 화면의 **`NotGrantedBecauseCooldown`** 으로 다룹니다.
`Unknown` 이 아닙니다 (11.4.8).

⚠ **응답 우선순위가 정해져 있습니다** (11.4.15).

```text
③ duplicate          가장 먼저. 쿨다운을 보지 않는다
② ALTAR_COMPLETED    그다음. REWARD_COOLDOWN 보다 먼저다
⑤ REWARD_COOLDOWN    새 지급 요청일 때만
```

#### `POST /api/altar/offer`

```json
요청
{ "amount": 50, "requestId": "0f2c9b1e-6a40-4b3f-9a1d-9c2f6f3e77aa" }
```

**전체 성공** — 요청한 수량이 그대로 반영됩니다.

```json
200 OK
{
  "success":            true,
  "offeredAmount":      50,
  "remainingFragments": 77,
  "totalOffered":       730,
  "targetOffering":     1000,
  "remainingToTarget":  270,
  "maxOfferAmount":     77,
  "altarActivated":     false,
  "recoveryPercent":    73.0,
  "duplicate":          false
}
```

⚠ **`acceptedAmount` · `refundedAmount` 같은 필드는 없습니다.** 부분 수락을 하지 않으므로
(10.3.2절) 성공했다면 반영된 수량은 언제나 요청한 수량입니다. `offeredAmount` 는 확인용이고,
요청의 `amount` 와 다른 값이 오는 경우는 없습니다.

**전체 실패** — 아무것도 바뀌지 않고, 최신 상태가 함께 옵니다.

```json
409 Conflict
{
  "success":            false,
  "code":               "OFFERING_AMOUNT_CHANGED",
  "message":            "다른 플레이어가 먼저 봉헌했습니다.",
  "remainingFragments": 5,
  "totalOffered":       998,
  "targetOffering":     1000,
  "remainingToTarget":  2,
  "maxOfferAmount":     2,
  "altarActivated":     false,
  "recoveryPercent":    99.8
}
```

```json
409 Conflict
{ "success": false, "code": "OFFERING_CLOSED",
  "message": "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다.",
  "totalOffered": 1000, "targetOffering": 1000,
  "remainingToTarget": 0, "maxOfferAmount": 0, "altarActivated": true, "recoveryPercent": 100.0 }
```

```json
409 Conflict
{ "success": false, "code": "NOT_ENOUGH_FRAGMENTS",
  "message": "보유한 조각이 모자랍니다. (보유 20 / 요청 30)",
  "remainingFragments": 20, "totalOffered": 730, "targetOffering": 1000,
  "remainingToTarget": 270, "maxOfferAmount": 20, "altarActivated": false, "recoveryPercent": 73.0 }
```

⚠ **실패 응답에도 최신 상태를 전부 싣습니다.** 이것이 이번 정책의 핵심입니다.
클라이언트가 실패 직후 `GET /api/altar/state` 를 한 번 더 부르지 않아도
UI 를 바로 맞출 수 있습니다 (§7 UX 흐름 ⑤-실패).

⚠ **응답에 `playerId` 가 없습니다.** 요청에도 없습니다. 서버가 JWT 로 압니다.

⚠ `duplicate: true` 는 "같은 `(user_id, requestId)` 를 다시 받아서 **아무것도 하지 않고**
지금 상태를 돌려줬다" 는 뜻입니다. 클라이언트는 이걸 성공으로 다뤄야 합니다 (10.4절).

#### 화면 문구는 클라이언트가 조합합니다

기존 서버 오류 규약은 `message` 를 **그대로 화면에 띄울 수 있는 한국어**로 보냅니다
(`CharacterEndpoints.cs:17`). 그런데 이번 문구에는 **수량이 들어갑니다.**

```text
"다른 플레이어가 먼저 봉헌했습니다. 섬 회복까지 2개 남았습니다."
                                    ↑ remainingToTarget
```

서버 `message` 에 숫자를 박아 넣으면 그 값이 응답의 `remainingToTarget` 과 어긋날 수
있는 자리가 하나 더 생깁니다. 그래서 **서버는 앞 문장까지만 보내고, 클라이언트가
`remainingToTarget` 으로 뒷 문장을 붙입니다.**

```csharp
// AltarOfferingUIController
string text = error.Code == "OFFERING_AMOUNT_CHANGED"
    ? $"{error.Message} 섬 회복까지 {state.RemainingToTarget}개 남았습니다."
    : error.Message;     // 다른 오류는 서버 문구를 그대로 쓴다 (기존 규약)
```

최종 사용자에게 보이는 문구는 11.3절의 세 가지로 통일합니다.

### 9.3 Fusion RPC — 알림 하나

```csharp
// Assets/Game/Scripts/Lobby/AltarOfferingRelay.cs  (신규)
// 플레이어 프리팹에 붙는다. LobbyChatRelay 와 같은 자리, 같은 모양.

[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void Rpc_NotifyOffered(RpcInfo info = default)
{
    if (info.Source != Object.InputAuthority) return;   // LobbyChatRelay.cs:118 과 같은 방어
    // 도배 방지: 같은 플레이어가 0.5초 안에 두 번 보내면 무시 (LobbyChatRelay.cs:124 패턴)
    Rpc_AltarChanged();
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void Rpc_AltarChanged()
{
    AltarState.RequestRefresh();   // 받은 쪽이 GET /api/altar/state 를 다시 부른다
}
```

**인자가 없다는 것이 핵심입니다.** 숫자를 실어 나르면 그 숫자를 믿게 되고, 믿는 순간
클라이언트가 조작할 수 있습니다. "바뀌었다" 는 사실만 나르면 조작할 게 없습니다.

⚠ `LobbyChatRelay.cs:124-137` 의 **레이트 리밋**을 반드시 베끼세요.
누가 봉헌 버튼을 연타하면 모든 클라이언트가 API 를 연타하게 됩니다.
서버에서 `Time.time` 으로 재면 클라이언트가 못 속입니다.

⚠ `Rpc_AltarChanged` 를 받은 클라이언트는 **바로 조회하지 말고 0~1초 랜덤 지연 후** 조회합니다.
100명이 동시에 같은 API 를 때리면 그 순간이 가장 위험합니다.

### 9.4 Unity 클라이언트 쪽 배선

```text
Assets/Game/Scripts/Inventory/
  ItemIds.cs                상수
  PlayerInventory.cs        static 캐시 + event Changed        (RewardService 와 같은 모양)
  IInventoryService.cs      인터페이스                          (IAuthService 와 같은 모양)
  HttpInventoryService.cs   UnityWebRequest 구현
  FakeInventoryService.cs   서버 없이 화면만 볼 때              (FakeAuthService 와 같은 모양)

Assets/Game/Scripts/Lobby/
  AltarState.cs             static 캐시 + event Changed
  IAltarService.cs
  HttpAltarService.cs
  FakeAltarService.cs
```

**`HttpJson` 을 그대로 재사용합니다.**

```csharp
// HttpJson 은 internal 이지만, Fishing 을 제외한 모든 스크립트가 같은
// Assembly-CSharp 에 있으므로 접근된다. (asmdef 는 Fishing 에만 있다 — 1.1절)
yield return HttpJson.Send(url, "POST", json, token, result => { ... });
```

토큰은 `AccountServiceLocator.Auth` 가 들고 있습니다.
`HttpCharacterService.cs` 가 이미 같은 일을 하고 있으니 그 파일을 본보기로 삼으면 됩니다.

**등록 지점도 기존과 같습니다.**

```csharp
// AccountServiceBootstrap.cs:60-70 에 두 줄을 더한다.
host.AddComponent<HttpInventoryService>();
host.AddComponent<HttpAltarService>();
```

`Active = Implementation.Fake` 한 줄로 서버 없이도 화면을 볼 수 있게 유지됩니다
(`AccountServiceBootstrap.cs:47`).

---

## 10. 동시성 및 중복 요청 방지

### 10.1 지켜야 할 불변식

```text
0 <= total_offered <= target_offering        ← 어떤 동시 요청 조합에서도 깨지면 안 된다
```

`targetOffering` 은 **"이 값을 넘으면 켜지는 임계값" 이 아니라 "제단이 받을 수 있는
최대 누적 봉헌량"** 입니다. 1001/1000 은 정상 상태가 아니라 **버그**입니다.

문제 상황은 두 가지입니다.

```text
① 합계가 어긋난다
   현재 500,  A: 3개  B: 5개  동시에   →  반드시 508. 하나가 사라지면 안 된다

② 마지막 칸을 두고 부딪힌다              ← 이번 정책에서 새로 생긴 문제
   현재 999 / 1000,  A: 1개  B: 1개  동시에
   →  한 명만 성공해 1000 / 1000
   →  다른 한 명은 전체 실패, 인벤토리 차감 없음
   →  1001 이 되면 안 된다
```

①만 풀면 되던 때는 `UPDATE x = x + n` 하나로 충분했습니다.
②가 생기면서 **"남은 칸을 확인하는 일과 더하는 일이 쪼개지지 않아야" 합니다.**

### 10.2 하면 안 되는 것

```csharp
// ✗ 1 — 읽어서 계산하고 절대값을 쓴다
var state = await db.AltarStates.FirstAsync();
state.TotalOffered += amount;          // UPDATE ... SET total_offered = 503 WHERE id = 1
await db.SaveChangesAsync();           // 늦게 쓴 쪽이 이긴다. 요청이 사라진다
```

```csharp
// ✗ 2 — 남은 칸을 먼저 읽어서 판단하고, 그다음에 더한다
var state = await db.AltarStates.AsNoTracking().FirstAsync();
int remaining = state.TargetOffering - state.TotalOffered;   // 여기서 1 을 읽었는데
if (amount > remaining) return Fail();                       // 검사와 갱신 사이에
await db.AltarStates.Where(a => a.Id == 1)                   // 다른 요청이 끼어든다
    .ExecuteUpdateAsync(s => s.SetProperty(a => a.TotalOffered, a => a.TotalOffered + amount));
// → A 와 B 가 둘 다 remaining = 1 을 읽고 둘 다 통과한다. 1001 이 된다.
```

✗ 2 가 이번 정책에서 가장 빠지기 쉬운 함정입니다. **읽고 → 판단하고 → 쓰는 사이가
열려 있으면(check-then-act) 불변식이 깨집니다.**

### 10.3 핵심 원칙

> **`altar_state` 의 남은 칸을 확인하는 일과 `total_offered` 를 늘리는 일 사이에
> 다른 봉헌 요청이 끼어들 수 없어야 한다.**

이 원칙만 지키면 구현 수단은 무엇이든 됩니다. 후보는 넷입니다.

| 후보 | 방식 | 이 프로젝트에서 |
|---|---|---|
| **조건부 원자적 UPDATE** | 판단을 `WHERE` 안에 넣어 검사와 갱신을 한 문장으로 만든다 | **권장.** 아래 10.3.1 |
| `SELECT ... FOR UPDATE` | 행을 잠그고 읽은 뒤 계산해서 쓴다 | 가능. 읽은 값을 그대로 쓸 수 있어 코드가 직관적이다 |
| 트랜잭션 + 행 잠금 | 위와 같은 계열 | 가능 |
| 낙관적 동시성 + 재시도 | `version` 컬럼을 두고 충돌하면 다시 시도 | 가능하나 재시도 루프가 늘어난다 |

**어느 것을 써도 불변식은 지켜집니다.** 실제 구현 단계에서 팀이 고르면 됩니다.

#### 10.3.1 권장 — 조건부 원자적 UPDATE

이 프로젝트에 이것을 권하는 이유는 성능이 아니라 **일관성**입니다.
인벤토리 차감이 이미 같은 모양(`ExecuteUpdateAsync` + `WHERE` 조건)이라,
두 연산이 같은 방식으로 읽힙니다. 별도의 잠금 관리 코드도 없습니다.

```csharp
// ── 트랜잭션 밖에서 먼저 거르는 값싼 검사 ──────────────────
if (amount <= 0) return AmountInvalid();

await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);

// ① 제단의 남은 칸을 확인하면서 동시에 더한다.
//    "남은 칸이 amount 이상일 때만" 을 WHERE 가 판단하므로
//    검사와 갱신 사이가 열리지 않는다.
int accepted = await db.AltarStates
    .Where(a => a.Id == 1
             && a.TargetOffering - a.TotalOffered >= amount)     // ← 불변식을 지키는 곳
    .ExecuteUpdateAsync(s => s
        .SetProperty(a => a.TotalOffered, a => a.TotalOffered + amount)
        .SetProperty(a => a.UpdatedAt,    _ => DateTime.UtcNow), ct);

if (accepted == 0)
{
    // 남은 칸이 모자라다. 두 경우가 여기로 온다.
    //   · 이미 목표에 도달했다            → 회복 완료
    //   · 그 사이 다른 사람이 먼저 넣었다  → 봉헌 가능량이 줄었다
    // ⚠ 인벤토리는 아직 건드리지 않았다. 차감 없이 그대로 빠져나간다.
    await tx.RollbackAsync(ct);
    return await OfferingAmountChangedAsync(db, userId, ct);     // 최신 상태를 담아 반환
}

// ② 인벤토리 차감 — 같은 모양이다. 보유량이 모자라면 0행이 바뀐다.
int decremented = await db.PlayerInventories
    .Where(i => i.UserId   == userId                 // ← JWT sub. 요청 본문에서 오지 않는다
             && i.ItemId   == ItemIds.SeaHeartFragment
             && i.Quantity >= amount)
    .ExecuteUpdateAsync(s => s.SetProperty(i => i.Quantity, i => i.Quantity - amount), ct);

if (decremented == 0)
{
    await tx.RollbackAsync(ct);          // ① 의 증가도 함께 되돌아간다
    return NotEnoughFragments(...);
}

// ③ 이력 기록 (중복 방지의 핵심 — 10.4)
db.AltarContributions.Add(new AltarContribution { UserId = userId, RequestId = requestId, ... });
await db.SaveChangesAsync(ct);

// ④ 최종 상태를 읽어 응답에 담는다 — 여기서 읽은 값이 진짜다
await tx.CommitAsync(ct);
```

**왜 제단을 먼저 건드리는가.** 8.6절의 검증 순서와 같습니다.
"봉헌 가능량이 줄었다" 가 "보유량이 모자라다" 보다 먼저 판정되어야, 사용자에게
`OFFERING_AMOUNT_CHANGED` 라는 정확한 이유를 줄 수 있습니다.
어느 쪽이 실패하든 **트랜잭션이 통째로 되돌아가므로 인벤토리가 일부만 깎이는 일은 없습니다.**

#### 10.3.2 부분 수락은 하지 않습니다

```text
✗ 5개를 요청했는데 남은 칸이 2개라서 2개만 받는다
✗ 응답에 acceptedAmount / refundedAmount 를 나눠 담는다
✓ 5개를 요청했고 남은 칸이 2개면 → 요청 전체 실패, 인벤토리 차감 0
```

이유는 두 가지입니다.

1. **사용자가 낸 적 없는 수량이 소비되는 일이 없습니다.** "5개 내려고 눌렀는데 2개만
   나갔다" 는 되돌릴 수 없는 결과를 사용자 확인 없이 만드는 것입니다.
2. **응답 계약이 단순해집니다.** 성공이면 요청한 수량 그대로, 실패면 아무 일도 없음.
   UI 가 "얼마가 나갔는지" 를 다시 계산할 필요가 없습니다.

실패한 뒤에는 **최신 상태로 UI 를 맞추고, 사용자가 다시 `[봉헌]` 을 누릅니다.**
자동 재시도하지 않습니다 (12장 §7 UX 흐름).

⚠ **`altar_state` 는 행이 하나뿐이라 모든 봉헌이 그 한 행에서 직렬화됩니다.**
로비 인원(채널당 100명) 규모에서는 문제없습니다. 이번 정책에서는 오히려 그 직렬화가
불변식을 지켜 주는 장치입니다.

### 10.4 중복 요청 방지 — `requestId`

**클라이언트가 요청마다 Guid 를 만들고, 재전송할 때는 같은 값을 씁니다.**

```csharp
// 봉헌 버튼을 누른 순간 한 번만 만든다. 재시도해도 이 값을 바꾸지 않는다.
_pendingRequestId = System.Guid.NewGuid().ToString();
```

서버는 **`(user_id, request_id)` 에 UNIQUE 인덱스**를 겁니다.

```text
uk_contributions_user_request   UNIQUE (user_id, request_id)
```

⚠ **`request_id` 단독 UNIQUE 로 하지 않습니다.** 다른 사용자가 (실수로든 고의로든) 같은
`requestId` 를 보내면 그 요청이 **남의 멱등성 레코드에 걸려** 조용히 무시됩니다.
봉헌한 줄 알았는데 아무 일도 안 일어나고, 서버 로그에는 정상 중복으로 보입니다.
`user_id` 를 같이 묶으면 요청의 소유권이 명시되어 이 경로가 사라집니다.

#### 재수신했을 때 무엇을 돌려주는가 — 계약 수정

초안에는 **"그때 저장해 둔 결과를 그대로 돌려준다"** 고 적었습니다. **이것은 구현할 수
없습니다.** `altar_contributions` 에는 `amount` 만 있고 그 시점의 `remainingFragments` ·
`totalOffered` 같은 응답값을 저장하지 않기 때문입니다.

응답 전체를 저장하는 대신 **계약을 바꿉니다.**

```text
같은 (user_id, requestId) 재수신
  → 봉헌 작업을 다시 하지 않는다          ← 이것이 멱등성의 본질이다
  → duplicate = true
  → 지금 시점의 권위 상태를 다시 조회해서 돌려준다
     (remainingFragments · totalOffered · altarActivated · recoveryPercent)
```

**"그때의 값" 이 아니라 "지금의 값" 을 돌려줍니다.** 그 편이 오히려 정확합니다 —
재시도 사이에 다른 사람이 봉헌했을 수 있고, 클라이언트가 화면에 그려야 하는 것은
과거가 아니라 현재입니다. 응답 스냅샷을 DB 에 저장할 이유가 사라집니다.

처리 순서:

```text
① (user_id, request_id) 로 기존 이력을 찾는다
   있으면 → 작업을 건너뛰고 현재 상태를 조회해 duplicate: true 로 돌려준다
   없으면 → ②

② 위 10.3 의 트랜잭션을 돈다
   INSERT 가 UNIQUE 위반으로 터지면 → 그 사이 같은 요청이 먼저 처리된 것이다
                                       롤백하고 ① 로 돌아간다
```

①만으로는 부족합니다. 두 요청이 **정말 동시에** 오면 둘 다 "없음" 을 보고 ②로 갑니다.
그래서 **UNIQUE 인덱스가 최후의 방어선**이고, 그 예외를 잡아 처리하는 것이 핵심입니다.

이 방식의 이름은 **멱등성 키(idempotency key)** 이고, 결제 API 들이 쓰는 표준 방식입니다.

### 10.5 클라이언트 쪽 중복 방지 (보조)

서버가 막아 주지만, 클라이언트도 막습니다. **서버 방어가 있어도 UI 는 응답해야 합니다.**

```text
봉헌 버튼 누름 → 즉시 interactable = false
                 requestId 생성 (이후 재시도해도 유지)
                 "봉헌하는 중..." 표시

응답 도착     → 버튼 다시 활성화
타임아웃      → HttpJson.TimeoutSeconds 는 10초 (HttpJson.cs:56)
                "서버가 응답하지 않습니다. 다시 시도해 주세요." + 버튼 활성화
                ⚠ 이때 requestId 를 새로 만들지 않는다.
                  타임아웃은 "서버가 처리했는데 응답만 유실" 일 수 있다.
                  같은 requestId 로 재시도하면 서버가 duplicate 로 안전하게 처리한다
```

### 10.6 마지막 칸을 두고 동시에 부딪히는 경우

```text
999 / 1000 에서
  A: 1개 요청 ┐ 거의 동시에
  B: 1개 요청 ┘

DB 가 두 UPDATE 를 직렬화한다
  먼저 도착한 쪽   WHERE (1000 - 999) >= 1  → 참  → 1행 갱신 → 1000 / 1000  성공
  나중에 도착한 쪽 WHERE (1000 - 1000) >= 1 → 거짓 → 0행 갱신 → 전체 실패

순서는 DB 가 정한다. A 가 이기든 B 가 이기든 상관없다.
확실한 것은 둘 중 하나만 성공하고, 결과가 언제나 1000 / 1000 이라는 것이다.
```

진 쪽에게 일어나는 일:

```text
인벤토리 차감 없음        ← ① 에서 걸렸으므로 ② 에 닿지도 않았다
contribution 기록 없음
altar total 변화 없음
응답 OFFERING_AMOUNT_CHANGED + 최신 상태
```

### 10.7 `altarActivated` 는 계산값으로 둡니다

세 가지를 구분해서 씁니다. **의도적으로 부등호가 다릅니다.**

```text
정상 데이터 불변식     total_offered <= target_offering      ← DB 와 서버가 지킨다
회복 완료 정상 상태    total_offered == target_offering      ← 도달하면 봉헌이 닫힌다
방어적 활성화 판정     total_offered >= target_offering      ← 화면을 켜는 조건
```

```csharp
bool altarActivated = state.TotalOffered >= state.TargetOffering;
```

**불변식은 `<=` 인데 판정은 왜 `>=` 인가.** 정상 경로에서는 `==` 에서만 참이 됩니다.
그런데 누군가 DB 값을 손으로 고쳤거나(시연 중 `target_offering` 을 낮추는 경우가 실제로
있습니다 — 4.2.2절) 과거 데이터가 남아 `total > target` 이 되면, `==` 판정은 **거짓이 되어
이미 회복된 섬의 빛이 꺼집니다.** `>=` 는 그 경우에도 켜 둡니다.

> `target_offering` 을 1000 에서 20 으로 낮추면 그 순간 `total(730) > target(20)` 이 됩니다.
> 시연 준비 중에 충분히 일어납니다. 그때 제단이 어두워지면 안 됩니다.

컬럼으로 두고 싶다면 `activated_at` (nullable, 처음 달성한 시각)만 기록하고,
**읽을 때는 언제나 위 계산값을 씁니다.**

---

## 11. 전체 봉헌량 1000개 처리

### 11.1 `targetOffering` 은 임계값이 아니라 **상한**입니다 — 확정 (2026-09-20)

> **[확정] 제단 최대 봉헌량 정책**
>
> - `totalOffered` 는 `targetOffering` 을 **초과하지 않는다.**
> - UI 에서 `remainingToTarget` 까지만 선택할 수 있다.
> - 다른 플레이어의 선행 봉헌으로 선택 수량이 유효하지 않게 되면 **요청 전체를 실패**시킨다.
> - **부분 수락하지 않는다.**
> - 실패 후 최신 상태를 반영하고, 사용자가 다시 봉헌한다.

```text
0 <= totalOffered <= targetOffering            기본 목표가 1000 이면  0 ~ 1000
```

핵심 파생값 두 개를 문서 전체에서 같은 이름으로 씁니다.

```text
remainingToTarget = targetOffering - totalOffered            0 이상
maxOfferAmount    = min(playerOwnedAmount, remainingToTarget)
```

**세 부등호를 의도적으로 다르게 씁니다.** (10.7절)

```text
정상 데이터 불변식     total_offered <= target_offering
회복 완료 정상 상태    total_offered == target_offering
방어적 활성화 판정     total_offered >= target_offering
```

```csharp
bool altarActivated = totalOffered >= targetOffering;   // 방어적. 이유는 10.7절
```

### 11.2 1000 은 하드코딩하지 않습니다

`targetOffering` 을 **DB 컬럼**으로 둡니다.

```text
altar_state.total_offered     BIGINT UNSIGNED   기본값 0
altar_state.target_offering   INT    UNSIGNED   기본값 1000
```

⚠ **불변식을 스키마와 서버 양쪽에 겁니다.** MySQL 8.4 는 `CHECK` 를 실제로 강제합니다
(8.0.16 이후). EF Core 에서는 `entity.ToTable(t => t.HasCheckConstraint(...))` 입니다.

```sql
CHECK (target_offering > 0)                    -- 0 이면 recoveryPercent 가 Infinity/NaN
CHECK (total_offered  >= 0)                    -- UNSIGNED 가 이미 막지만 의도를 남긴다
CHECK (total_offered  <= target_offering)      -- 11.1 의 불변식
```

⚠ **세 번째 제약은 운영 중 `target_offering` 을 낮출 때 걸립니다.** 시연 준비로
1000 → 20 으로 낮추려는데 이미 `total_offered = 730` 이면 `UPDATE` 가 거부됩니다.
그때는 둘을 **한 문장으로** 바꿉니다.

```sql
UPDATE altar_state SET target_offering = 20, total_offered = 0 WHERE id = 1;
```

> 이 불편이 싫다면 세 번째 `CHECK` 를 빼도 됩니다. **불변식을 지키는 주체는
> 10.3.1 의 조건부 UPDATE 이고, `CHECK` 는 이중 안전장치입니다.**
> 다만 그 경우에도 `total > target` 상태가 만들어질 수 있으므로
> 10.7절의 방어적 `>=` 판정이 반드시 필요합니다.

⚠ **애플리케이션 검증을 `CHECK` 하나로 대체하지 않습니다.** `CHECK` 는 위반을 예외로
터뜨릴 뿐, 사용자에게 `OFFERING_AMOUNT_CHANGED` 같은 정확한 이유를 돌려주지 못합니다.
서버 계산 코드에서도 `target <= 0` 이면 `recoveryPercent = 0` 으로 떨어뜨립니다.

이유:
- 기획이 1000 → 1500 으로 바뀔 때 배포 없이 바꿀 수 있습니다.
- 클라이언트가 `recoveryPercent` 를 계산할 필요가 없어집니다 (서버가 줍니다).
- 시연 때 "1000개를 정말 모을 수 없으니 50으로 낮추자" 가 가능합니다. **실무적으로 이게 큽니다.**

### 11.3 회복 완료 상태

```text
totalOffered == targetOffering      예: 1000 / 1000
→ remainingToTarget = 0
→ maxOfferAmount    = 0
→ 더 이상 봉헌할 수 없다
```

#### 서버 동작

```csharp
// GET /api/altar/state 와 POST /api/altar/offer 가 공유하는 계산
int   remainingToTarget = Math.Max(0, state.TargetOffering - state.TotalOffered);
bool  altarActivated    = state.TotalOffered >= state.TargetOffering;
float recoveryPercent   = state.TargetOffering <= 0
                        ? 0f
                        : Math.Min(state.TotalOffered / (float)state.TargetOffering, 1f) * 100f;
```

봉헌이 닫혔는지는 **별도 플래그 없이** `remainingToTarget <= 0` 으로 판정합니다.
10.3.1 의 조건부 UPDATE 가 이것을 자동으로 처리합니다 —
`TargetOffering - TotalOffered >= amount` 는 남은 칸이 0 이면 어떤 `amount` 에도 거짓입니다.

```text
남은 칸이 0 인데 요청이 왔다   → 0행 갱신 → 전체 실패
그 뒤 최신 상태를 읽어 응답을 만든다 (remainingToTarget = 0, maxOfferAmount = 0)
```

⚠ **`altar_state.is_open` 같은 플래그를 두지 않습니다.** 플래그와 실제 수치가 어긋나는
상태가 생기고, 어긋났을 때 무엇이 옳은지 정할 방법이 없습니다.

#### 사용자에게 보이는 문구

```text
회복 완료 후 봉헌을 시도했을 때
  "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다."

남은 칸보다 많이 고르려 할 때 (UI 가 미리 막는다)
  "섬 회복까지 N개만 더 필요합니다."

다른 플레이어가 먼저 봉헌해서 요청이 실패했을 때
  "다른 플레이어가 먼저 봉헌했습니다. 섬 회복까지 N개 남았습니다."
```

문구는 문서 전체에서 이 세 가지로 통일합니다. 9.2절·6.1절·15장이 같은 문장을 씁니다.

#### UI 동작

```text
altarActivated == true 인 동안
  · 제단에 다가가도 [E] 조각 봉헌 안내를 띄우지 않는다
    (또는 "섬이 모두 회복되었습니다" 로 문구를 바꾼다 — 연출 선택)
  · 섬 회복도 HUD 는 100% 고정
  · 달성 연출(AltarBeam_Awakened)은 켜진 상태 유지
  · 혹시 UI 가 열려 있었다면 maxOfferAmount = 0 이므로 봉헌 버튼이 잠긴다
```

⚠ **클라이언트가 `altarActivated` 를 보고 UI 를 막아도 서버 검증은 그대로 둡니다.**
UI 를 안 거치고 API 를 직접 부를 수 있습니다 (6.4.2절의 2단계 검증 원칙).

### 11.4 회복 완료 이후 — 조각 지급을 중단합니다 (결정 #8 확정)

> **[결정 #8 — 섬 회복 완료 후 조각 지급 중단]** (2026-09-20 확정)
>
> - 섬 회복 **완료 전**에는 미니게임 성공 시 조각을 지급한다.
> - 섬 회복 **완료 후**에는 새로운 조각을 지급하지 않는다.
> - **기존에 보유하고 있던 잔여 조각은 삭제하지 않는다.**
> - 완료 상태는 **서버의 `altar_state`** 를 기준으로 판단한다. 클라이언트 캐시로 판단하지 않는다.
> - `ALTAR_COMPLETED` 는 게임 실패가 아니므로 `clear-reward` 는 **`200 OK` + `granted:false`** 를 반환한다.
> - 결과 화면은 **즉시 표시**하되 보상 여부는 **서버 응답으로 확정**한다.
> - 통신 실패 시 **동일 `matchKey`** 로 재시도한다.
> - 응답을 끝내 확인하지 못하면 **보상 지급 여부를 임의로 단정하지 않는다.**
> - `clear-reward` 와 마지막 봉헌이 동시에 발생하면 **DB 에서 먼저 확정된 작업의 순서**를 따른다.
> - **마지막 봉헌 완료 Commit 이후에는 새 조각이 지급되어서는 안 된다.**

#### 11.4.1 무엇을 풀고 무엇을 안 푸는가

11.4 초안에서 문제를 둘로 나눴습니다. 결정 #8 은 **B 만** 풉니다.

```text
문제 A  완료 순간의 낙오 조각        일회성 · 규모 작음   →  그대로 둔다 (11.4.2)
문제 B  완료 이후의 가짜 보상        지속적 · 무한 누적   →  지급을 중단한다  ← 결정 #8
```

문제 B 는 이런 모습이었습니다. STEP 11 의 지급 경로가 제단 상태를 보지 않아,
회복이 끝난 뒤에도 미니게임을 깰 때마다 계속 일어납니다.

```text
결과 화면      "바다의 심장 조각 +1"      ← 쓸 곳이 없는 물건을 받았다고 알린다
DB             player_inventories +1      ← 영원히 늘어난다
               reward_claims 에 행 추가    ← 영원히 쌓인다
```

**아무것도 안 주는 것보다 나쁩니다.** 보상이라고 말해 놓고 쓸 데가 없기 때문입니다.

채택하지 않은 것들 — **이번 범위에서 전부 제외합니다.**

```text
✗ 완료 후에도 계속 조각 지급
✗ 다음 목표 자동 개방 / targetOffering 자동 증가     ← 11.4.16 참고
✗ 남은 조각 자동 삭제
✗ 남은 조각 자동 봉헌
✗ 다른 재화로 자동 변환
✗ 상점 · 치장 등 신규 소비처 추가
```

**섬 회복은 하나의 명확한 종착 상태입니다.**

#### 11.4.2 잔여 조각은 손대지 않습니다

```text
999 / 1000 에서 A 보유 5
A 가 1개 봉헌  →  1000 / 1000, 회복 완료
A 에게 남은 4개는?
```

```text
삭제하지 않는다.
자동으로 봉헌하지 않는다.
다른 아이템으로 변환하지 않는다.
그대로 인벤토리에 남긴다.
```

정책은 두 줄입니다.

```text
회복 완료 전 이미 획득한 조각   →  그대로 보존
회복 완료 후 새로운 조각         →  지급하지 않음
```

⚠ 문서 어디에도 **잔여 조각을 자동 정리하는 설계를 두지 않습니다.**
"정리해 주는" 동작은 사용자가 요청하지 않은 소멸이고, 되돌릴 수 없습니다.

#### 11.4.3 서버 처리 흐름 — `POST /api/inventory/clear-reward`

```text
① 같은 (user_id, matchKey) 의 reward_claim 이 이미 있는가?   ← 트랜잭션 밖
     있으면 → 아무것도 하지 않고 granted:true, duplicate:true 로 끝낸다
              ⚠ 제단 상태를 보지 않는다. 이유는 11.4.4
              ⚠ altar_state 를 잠그지 않는다. 잠글 이유가 없다 (11.4.14)

BEGIN   ← reward_claim 이 없을 때만 트랜잭션을 연다

② altar_state 를 동시성 안전하게 읽는다 (잠금 읽기 — 11.4.5)
     total_offered >= target_offering 이면
       → player_inventories 변경 없음
       → reward_claims 기록 없음
       → ⚠ 트랜잭션을 먼저 종료해 row lock 을 푼다 (11.4.14)
       → granted:false, code: "ALTAR_COMPLETED"
       → 200 OK

③ 서버 보상 검증 (성공 클리어인지)

④ 60초 쿨다운 확인 — 11.4.15
     막히면 → 트랜잭션 종료 후 429 REWARD_COOLDOWN
     ⚠ 일일 상한은 쓰지 않는다

⑤ sea_heart_fragment 를 UPSERT 로 +1 한다     ← 11.4.12
     행이 없으면 → INSERT quantity = 1
     행이 있으면 → quantity = quantity + 1

⑥ INSERT reward_claims (user_id, game_id, match_key, claimed_at)
     UNIQUE 위반이면 → 11.4.13 의 재시도 절차로 간다

COMMIT → granted:true
```

⚠ **⑤와 ⑥의 순서는 바꿔도 됩니다.** 같은 트랜잭션 안이라 어느 쪽이 먼저든 함께
커밋되거나 함께 되돌아갑니다. 다만 **⑥을 먼저 하면** `reward_claims` 의 UNIQUE 충돌을
UPSERT 전에 잡아 헛일을 줄일 수 있습니다. 구현에서 판단합니다.

⚠ **회복 완료는 클라이언트 캐시로 판단하지 않습니다.** 반드시 서버의 최신 authoritative
`altar_state` 를 기준으로 합니다 (11.4.9).

#### 11.4.4 ⚠ ①을 ②보다 먼저 하는 이유 — 순서를 바꾸면 버그가 납니다

`matchKey` 중복 확인(①)과 제단 완료 확인(②)의 **순서가 결과를 바꿉니다.**

```text
A 가 클리어 → clear-reward → 서버가 +1 을 Commit → HTTP 응답만 유실
그 사이 B 가 마지막 1개를 봉헌 → 1000 / 1000 완료
A 가 같은 matchKey 로 재시도
```

```text
② 를 먼저 보면   제단이 완료됐으니 granted:false ALTAR_COMPLETED
                 → 결과 화면은 보상 줄을 숨긴다
                 → ⚠ 그런데 DB 에는 A 의 +1 이 실제로 들어가 있다. 화면과 DB 가 어긋난다

① 을 먼저 보면   reward_claim 이 있으니 granted:true, duplicate:true
                 → 결과 화면이 "+1" 을 보여준다
                 → ✓ DB 와 일치한다
```

**이미 확정된 지급을 뒤늦게 취소하지 않습니다.** 그래서 중복 확인이 먼저입니다.

#### 11.4.5 마지막 봉헌과 `clear-reward` 의 경쟁 — 가장 중요한 부분

```text
999 / 1000

A  미니게임 클리어  →  clear-reward 요청   ┐ 거의 동시에
B  마지막 1개 봉헌  →  altar/offer 요청    ┘
```

두 요청은 **같은 `altar_state` 행의 완료 상태를 두고 경쟁**합니다.

**기준: DB 에서 먼저 확정(Commit)된 작업을 따릅니다.**

**Case A — `clear-reward` 가 먼저 확정**

```text
A clear-reward
  → 아직 미완료(999 < 1000) 확인
  → A 에게 +1, reward_claim 저장
  → Commit

그 후 B 봉헌  →  999 → 1000  →  회복 완료
```

```text
결과: A 의 +1 은 정상 지급이다. A 가 조각 1개를 갖고 있어도 정상이다.
      "회복 완료 전에 서버에서 먼저 확정된 보상" 이므로 회수하지 않는다.
```

**Case B — 마지막 봉헌이 먼저 확정**

```text
B 봉헌  →  999 → 1000  →  Commit

그 후 A clear-reward
  → 이미 완료 확인
  → granted:false, ALTAR_COMPLETED
  → inventory 변화 없음, reward_claims 변화 없음
```

**금지 상태 — 이것이 나오면 설계 실패입니다**

```text
✗ 봉헌이 1000 / 1000 으로 Commit 된 뒤에 들어온 clear-reward 가
  오래된 스냅샷 999 / 1000 을 읽고 inventory +1 · reward_claims 추가
```

```text
마지막 봉헌의 Commit 이후에는 새 clear-reward 가 절대로 granted:true 가 되면 안 된다.
```

**왜 그냥 `SELECT` 로는 부족한가.** MySQL InnoDB 의 기본 격리수준은 `REPEATABLE READ`
이고, 평범한 `SELECT` 는 **스냅샷 읽기**입니다. 잠금을 걸지 않고, 트랜잭션이 시작된
시점의 값을 봅니다. 그래서 봉헌이 방금 커밋한 1000 을 못 보고 999 를 읽을 수 있습니다.
**그 상태로 지급하면 위 금지 상태가 됩니다.**

**핵심 원칙**

> `altar/offer` 의 "남은 칸 확인 → 인벤토리 차감 → total 증가" 와
> `clear-reward` 의 "완료 여부 확인 → 보상 지급" 이
> **같은 `altar_state` 완료 상태를 기준으로 서로 일관된 순서**를 가져야 한다.

구현 후보 — 원칙만 지키면 무엇이든 됩니다.

| 후보 | 방식 | 판단 |
|---|---|---|
| **`SELECT ... FOR UPDATE`** | `clear-reward` 가 `altar_state` 행을 잠그고 현재 값을 읽는다 | **권장.** 아래 |
| 명시적 트랜잭션 + 행 잠금 | 같은 계열 | 가능 |
| 조건부 Atomic UPDATE | 지급을 제단 조건이 붙은 한 문장으로 만든다 | 가능하나 EF Core 로 표현하기 번거롭다 |
| 격리수준 상향 | `SERIALIZABLE` | 가능하나 범위가 넓어 다른 요청까지 느려진다 |

**`SELECT ... FOR UPDATE` 를 권하는 이유**

```sql
SELECT total_offered, target_offering FROM altar_state WHERE id = 1 FOR UPDATE;
```

- 잠금 읽기라 **스냅샷이 아니라 커밋된 최신 값**을 봅니다. 금지 상태가 원천 차단됩니다.
- 봉헌의 조건부 UPDATE(10.3.1)는 **그대로 둡니다.** 그쪽은 이미 같은 행에 배타 잠금을
  잡으므로, 두 트랜잭션이 자연히 줄을 섭니다. **결정 #7 의 설계를 고치지 않습니다.**
- ⚠ **교착(deadlock) 가능성을 낮춥니다.** 두 트랜잭션 모두 `altar_state` 를 **가장 먼저**
  잠그고, 그다음에 각자의 행(`player_inventories` · `reward_claims` · `altar_contributions`)
  으로 갑니다. 잠금 순서를 같게 유지하면 **이 두 경로 사이의** 순환이 잘 만들어지지 않습니다.
  ⚠ **가능성이 0 이라는 뜻은 아닙니다.** MySQL InnoDB 에서는 UNIQUE 인덱스 · 외래 키 ·
  동시 INSERT · 이 설계 밖의 다른 트랜잭션 때문에도 교착이 납니다.
  **교착을 전제로 한 재시도 정책이 필요합니다 — 11.4.13.**
- `clear-reward` 는 판당 한 번이라 빈도가 낮습니다. 이 잠금이 병목이 되지 않습니다.

> 더 높은 동시성이 필요하면 `FOR SHARE`(공유 잠금)로 낮출 수 있습니다.
> `clear-reward` 는 `altar_state` 를 읽기만 하므로 이론상 충분합니다.
> 다만 `FOR UPDATE` 쪽이 읽기 쉽고, 지금 규모에서는 차이가 없습니다.

#### 11.4.6 `reward_claims` 기록 규칙

```text
실제 지급 성공                          회복 완료로 미지급
  granted = true                          granted = false
  player_inventories  → +1                player_inventories  → 변경 없음
  reward_claims       → 행 추가            reward_claims       → 기록하지 않음
```

⚠ **`ALTAR_COMPLETED` 는 "보상을 지급한 기록" 이 아니므로 `reward_claims` 에 넣지 않습니다.**
멱등성 키는 *지급된 판*을 가리키는 것이고, 지급하지 않은 판까지 넣으면 나중에
"이 판은 지급됐는가" 를 그 표로 답할 수 없게 됩니다.

#### 11.4.7 멱등성과 `ALTAR_COMPLETED` — 방식 A 를 씁니다

`ALTAR_COMPLETED` 는 `reward_claims` 에 행을 남기지 않습니다. 그러면 같은 `matchKey` 로
재시도가 들어왔을 때 서버는 무엇을 기억해야 할까요?

| 방식 | 내용 | 판단 |
|---|---|---|
| **A** | 매 재시도마다 제단 완료 상태를 다시 확인하고 그때마다 `granted:false` | **채택** |
| B | 미지급 결과까지 별도 멱등성 레코드로 저장 | 표가 하나 더 늘어난다. 불채택 |

**방식 A 로 충분한 이유 — 완료는 되돌아오지 않는 상태이기 때문입니다.**

```text
total_offered 는 줄어들지 않는다 (봉헌 취소가 없다)
target_offering 은 올라가지 않는다 (결정 #8 이 "다음 목표 개방" 을 배제했다)
→ 한 번 완료되면 계속 완료다
→ 같은 matchKey 를 몇 번 재시도해도 언제나 같은 granted:false 가 나온다
```

⚠ **이 성질은 결정 #8 이 3안(목표 상향)을 배제했기 때문에 성립합니다.** 나중에
`target_offering` 을 올리는 기능을 넣는다면 `granted:false` 가 `granted:true` 로
뒤집힐 수 있으므로 그때 이 절을 다시 봐야 합니다.

`CLAUDE.md` §2 — 지금 필요 없는 표를 만들지 않습니다.

#### 11.4.8 결과 화면 — "응답 없음" 을 "보상 없음" 으로 읽지 않습니다

초안의 다음 정책을 **폐기합니다.**

```text
✗ 응답이 안 오면(네트워크 실패) → 보상 줄을 숨긴다
```

**폐기 이유**

```text
서버에서 inventory +1 과 reward_claims 저장은 성공했는데
HTTP 응답만 클라이언트에 도착하지 않을 수 있다.

이 경우 응답 실패를 곧바로 "보상을 받지 못했다" 로 해석하면
DB 실제 상태와 결과 화면이 달라진다.
```

**유지하는 원칙** — 결과 화면은 응답을 기다리지 않고 **즉시** 표시합니다.
보상 영역만 나중에 채웁니다.

**보상 영역의 상태 모델**

| 상태 | 화면 | 언제 |
|---|---|---|
| `Pending` | "보상 확인 중..." | 결과 화면이 뜬 직후. 응답을 기다린다 |
| `Granted` | "바다의 심장 조각 +1" | `granted: true` (`duplicate:true` 도 포함) |
| `NotGrantedBecauseAltarCompleted` | **보상 줄을 숨긴다** | `granted: false`, `ALTAR_COMPLETED` |
| `NotGrantedBecauseCooldown` | "이번 판의 보상이 지급되지 않았습니다." | `429 REWARD_COOLDOWN` (11.4.15) |
| `Unknown` | "보상을 확인할 수 없습니다." | 재시도까지 실패해 **최종 상태를 모른다** |

> 실제로 `enum` 을 새로 만들지는 구현 단계에서 판단합니다. 지금은 **UI 상태 정의**만
> 명확히 해 둡니다.

##### 서버 응답 → 결과 상태 매핑

| 서버 결과 | 결과 상태 | 화면 |
|---|---|---|
| `granted:true` | `Granted` | "바다의 심장 조각 +1" |
| `granted:true` + `duplicate:true` | `Granted` | "바다의 심장 조각 +1" (추가 지급 없음) |
| `granted:false` + `ALTAR_COMPLETED` | `NotGrantedBecauseAltarCompleted` | 보상 줄 숨김 |
| `429` + `REWARD_COOLDOWN` | `NotGrantedBecauseCooldown` | "이번 판의 보상이 지급되지 않았습니다." |
| HTTP 결과를 끝내 확인 못 함 | `Unknown` | "보상을 확인할 수 없습니다." |

##### ⚠ `Unknown` 의 의미를 좁힙니다

```text
Unknown  =  서버 결과를 확인할 수 없음        ← 오직 이것만
```

**다음은 `Unknown` 이 아닙니다.** 서버가 결과를 **확정해서** 돌려준 응답이기 때문입니다.

```text
duplicate           → Granted
ALTAR_COMPLETED     → NotGrantedBecauseAltarCompleted
REWARD_COOLDOWN     → NotGrantedBecauseCooldown
```

`Unknown` 은 **응답을 못 받았을 때만** 씁니다 — HTTP 응답 없음 · 네트워크 오류 ·
재시도 1~2회 후에도 결과 확인 실패.

##### 상태별 문구 규칙

⚠ `NotGrantedBecauseAltarCompleted` 에서 **"보상 없음" · "조각 지급 실패" 같은 부정적
문구를 쓰지 않습니다.** 섬 회복이 끝나 더 이상 조각이 필요하지 않은 **정상 상태**입니다.
줄을 조용히 숨깁니다.

⚠ `NotGrantedBecauseCooldown` 에서 **사용자에게 잘못을 돌리지 않습니다.**

```text
✗ "너무 빠르게 플레이했습니다."
✗ "요청을 너무 많이 보냈습니다."
✗ "잠시 후 다시 시도하세요."
✗ "부정한 요청이 감지되었습니다."

◎ "이번 판의 보상이 지급되지 않았습니다."
```

정상 플레이에서는 나오지 않는 **서버 방어 정책**이므로, 화면에는 중립적으로만 적습니다.
진단에 필요한 `REWARD_COOLDOWN` · `retryAfterSeconds` 는 **Console 로그**에 남깁니다.

⚠ `Unknown` 에서 **"+1" 이라고 낙관적으로 표시해서도, "보상 없음" 이라고 확정적으로
표시해서도 안 됩니다.** 서버 상태를 확인하지 못했기 때문입니다.

⚠ **`NotGrantedBecauseCooldown` 과 `Unknown` 을 같은 문구로 묶지 않습니다.**
앞은 "서버가 안 줬다고 확정" 이고 뒤는 "줬는지 모른다" 입니다. 뭉뚱그리면
실제로 이상이 생겼을 때 로그만 보고는 구분할 수 없습니다.

##### `ALTAR_COMPLETED` 와 `REWARD_COOLDOWN` 의 표시가 다른 이유

```text
ALTAR_COMPLETED     보상 자체가 더 이상 존재하지 않는 정상 세계 상태
                    → 보상 줄을 숨긴다

REWARD_COOLDOWN     이번 보상이 서버 정책 때문에 지급되지 않은 상태
                    → 보상 줄을 숨기지 않는다
                    → "이번 판의 보상이 지급되지 않았습니다."
```

숨기면 두 상태가 화면에서 똑같아 보이고, 그러면 **실제로 이상이 생겨도 아무도
눈치채지 못합니다.**

**네트워크 실패 시 재시도**

```text
첫 요청 실패
  ↓
같은 matchKey 로 재시도          ⚠ 새 matchKey 를 만들면 절대 안 된다
  ↓
최대 1~2회까지만
  ↓
그래도 실패하면 Unknown
```

⚠ **`REWARD_COOLDOWN` 은 자동 재시도 대상이 아닙니다.** 서버가 정상적으로 확정한
응답이라 같은 요청을 다시 보내도 같은 답이 옵니다. 자동 재시도는 **응답 없음 ·
네트워크 오류 · 일시적 통신 실패**에만 씁니다 (11.4.13 · 11.4.14).

`matchKey` 는 서버가 정한 판 고유값에서 나옵니다 (원천은 STEP 11 의 "matchKey 의 원천" 절).
**결과가 나온 시점에 한 번 붙잡아 두고 재시도 내내 그 값을 씁니다.** 미니게임 씬을
벗어나면 그 값을 다시 만들 수 없습니다.

`HttpJson` 에는 재시도 정책이 없습니다(`HttpJson.cs` — 타임아웃 10초, 재시도 없음).
그래서 설계 단계에서는 **최대 1~2회**로 둡니다.

**재시도가 안전한 이유**

```text
첫 요청     서버에서 +1 성공, reward_claim 저장, Commit
            → 응답만 유실

동일 matchKey 재시도
            → 서버가 11.4.3 ① 에서 기존 reward_claim 을 발견
            → 추가 지급하지 않음
            → granted:true, duplicate:true 로 기존 결과를 알려줌
```

`reward_claims` 의 `UNIQUE (user_id, match_key)` 가 이것을 보장합니다(STEP 1).
**재시도를 넣어도 보상이 두 번 지급되지 않습니다.**

#### 11.4.9 `AltarState` 캐시로 미리 판단하지 않습니다

```csharp
// ✗ 이렇게 하지 않는다
if (AltarState.Activated) { 보상 줄을 숨긴다; }
```

이유:

```text
· 미니게임 씬에서는 로비의 주기 폴링(12.5절)이 돌지 않는다
· 캐시가 오래됐을 수 있다
· 다른 채널에서 마지막 봉헌이 일어났을 수 있다
· 클라이언트가 "아직 회복 중" 이라고 생각해도 서버는 이미 완료됐을 수 있다
```

```text
보상 지급 가능 여부 = clear-reward API 서버 응답이 최종 권위
```

#### 11.4.10 클리어 여부와 보상 여부는 **별개**입니다

문서 전체에서 이 둘을 섞지 않습니다.

```text
게임 클리어 여부   ≠   조각 지급 여부
```

```text
게임 클리어 = true,  altar completed = true 인 경우

→ 게임 결과는 성공이다
→ 승리 화면을 정상적으로 보여준다
→ 점수 · 기록 · 클리어 표시 전부 정상이다
→ 단지 sea_heart_fragment 만 지급하지 않는다
```

**보상이 없다고 게임 결과를 실패로 바꾸지 않습니다.** `clear-reward` 가 `200 OK` 인
이유가 이것입니다(9.2절).

#### 11.4.11 시연 운영 정책

기획값은 그대로 `targetOffering = 1000` 입니다. 시연에서는 DB 의 `target_offering` 을
낮출 수 있습니다(11.2절).

⚠ **목표는 "작은 숫자로 만드는 것" 이 아니라 "완료 연출이 시연 흐름의 마지막 구간에
나오도록 조절하는 것" 입니다.**

```text
권장 시연 시나리오
  1. 미니게임 플레이
  2. 조각 획득
  3. 제단 봉헌
  4. 섬 회복도 상승
  5. 시연 마지막 구간에서 목표 달성
  6. 돌기둥 각성 연출
  7. 섬 회복 완료
```

회복 완료를 **시연 초반에** 만들면 남은 시간 대부분 동안 조각 보상이 중단된 채로
흘러갑니다. 결정 #8 은 그 상태를 정직하게 만들어 줄 뿐, 재미있게 만들어 주지는 않습니다.

```text
목표 20, 4인 파티가 한 판 클리어 → 4개
다섯 판이면 20 / 20  →  시연 시작 10~20분 만에 완료   ← 이렇게 잡으면 안 된다
```

`target_offering` 은 DB 컬럼이라 **리허설하면서 조정할 수 있습니다.**

#### 11.4.12 첫 지급은 행이 없습니다 — UPSERT 로 처리합니다

초안의 지급 문장에는 **신규 사용자가 빠져 있었습니다.**

```csharp
// ✗ 기존 행이 있다고 가정한다
UPDATE player_inventories SET quantity = quantity + 1
 WHERE user_id = @u AND item_id = 'sea_heart_fragment';
// → 한 번도 조각을 받은 적 없는 사용자는 행이 없다. 0행이 바뀌고 아무 일도 안 일어난다.
//   granted:true 를 돌려줬는데 인벤토리는 그대로인 상태가 된다.
```

**처음 조각을 받는 사람이 영영 못 받습니다.** 미니게임을 처음 깬 신규 계정 전부입니다.

```text
(user_id, 'sea_heart_fragment') 행이 없음   →  INSERT quantity = 1
행이 이미 있음                               →  quantity = quantity + 1
```

⚠ **`SELECT` 로 확인하고 `INSERT` 또는 `UPDATE` 하는 두 단계로 만들지 않습니다.**
그 사이에 다른 요청이 같은 행을 먼저 INSERT 하면 UNIQUE 위반으로 터지거나,
제약이 없으면 **같은 아이템 행이 두 개 생깁니다.** 10.2절 `✗2` 와 같은 함정입니다.

**원자적 UPSERT 를 씁니다. 방식은 하나로 확정합니다.**

```sql
INSERT INTO player_inventories (user_id, item_id, quantity, updated_at)
VALUES (@userId, 'sea_heart_fragment', 1, @now)
ON DUPLICATE KEY UPDATE quantity = quantity + 1, updated_at = @now;
```

이 방식을 쓰는 이유:

```text
· 첫 지급   행이 없으면 quantity = 1
· 이후 지급 기존 quantity + 1
· SELECT 후 INSERT/UPDATE 의 race condition 이 없다
· 한 문장이라 의미가 분명하다
```

##### ✗ `INSERT IGNORE` + `UPDATE` 는 쓰지 않습니다

후보로 적어 두었던 방식인데, **신규 사용자의 첫 지급이 틀립니다.**

```sql
-- ✗ 이렇게 하면 안 된다
INSERT IGNORE INTO player_inventories (user_id, item_id, quantity, ...)
VALUES (@userId, 'sea_heart_fragment', 1, ...);          -- 행이 없으면 1 을 넣는다
UPDATE player_inventories SET quantity = quantity + 1
 WHERE user_id = @userId AND item_id = 'sea_heart_fragment';   -- 그리고 또 +1 한다
```

```text
신규 사용자  INSERT 로 1 이 들어가고  →  UPDATE 로 2 가 된다
             한 판을 깼는데 조각이 2개 들어간다
```

`INSERT` 가 실제로 행을 넣었는지에 따라 뒤의 `UPDATE` 를 건너뛰어야 하는데, 그러려면
영향 행 수를 보고 분기해야 합니다. **두 문장으로 나눈 이점이 사라집니다.**

EF Core 확장 패키지(`Upsert` 계열)도 쓰지 않습니다. 패키지 하나를 위해 의존성을
늘릴 이유가 없습니다 (`CLAUDE.md` §2).

⚠ **이 구문은 `UNIQUE (user_id, item_id)` 가 있어야 동작합니다.**
STEP 1 의 `uk_inventory_user_item` 이 그것입니다. 제약이 없으면
`ON DUPLICATE KEY` 가 걸릴 키가 없어 **행이 계속 새로 생깁니다.**

⚠ EF Core 의 `ExecuteUpdateAsync` 로는 이 구문을 표현할 수 없습니다.
**SQL 자체는 확정이고, 그것을 실행할 API 만 구현 단계에서 고릅니다.**
현재 프로젝트의 코드 스타일을 확인한 뒤 `ExecuteSqlInterpolatedAsync` 등 적절한
API 로 위 `INSERT ... ON DUPLICATE KEY UPDATE` 를 그대로 실행합니다.
**다른 UPSERT 방식으로 바꾸지 않습니다.**

> **봉헌의 차감(10.3.1)은 UPSERT 가 아닙니다.** 그쪽은 `WHERE quantity >= amount` 조건이
> 붙은 `UPDATE` 이고, 행이 없으면 0행이 바뀌어 `NOT_ENOUGH_FRAGMENTS` 가 나오는 것이
> **올바른 동작**입니다. 없는 조각을 낼 수는 없습니다. 지급만 UPSERT 입니다.

#### 11.4.13 교착과 UNIQUE 충돌 — 새 트랜잭션으로 다시 시작합니다

11.4.5 의 잠금 순서 통일은 **가능성을 낮출 뿐 0 으로 만들지 못합니다.**
MySQL InnoDB 는 UNIQUE 인덱스 · 외래 키 · 동시 INSERT 때문에도 교착을 냅니다.

**원칙**

```text
실패한 트랜잭션을 그대로 이어서 사용하지 않는다.
최신 DB 상태를 다시 확인한다.
```

**교착으로 롤백된 경우**

```text
MySQL 이 deadlock 을 감지해 트랜잭션을 rollback 함
  ↓
작업 전체를 새 트랜잭션으로 제한적으로 재시도한다 (1~2회)
  ↓
새 트랜잭션 시작 → 최신 상태 다시 읽기 → 11.4.3 의 검증을 처음부터 다시 수행
```

⚠ **기존 트랜잭션 객체를 이어서 쓰지 않습니다.** 롤백된 트랜잭션에서 읽은 값은
더 이상 유효하지 않습니다. 제단이 그 사이 완료됐을 수도 있습니다.

**`reward_claims` UNIQUE 충돌**

동시에 들어온 재시도 두 건이 둘 다 11.4.3 ①을 통과하면, ⑤의 INSERT 에서 한쪽이
`UNIQUE (user_id, match_key)` 위반으로 터집니다. **정상적인 경쟁 결과입니다.**

```text
UNIQUE 충돌
  ↓
현재 트랜잭션 rollback
  ↓
⚠ 현재 DbContext / ChangeTracker 상태를 그대로 믿고 계속하지 않는다
  ↓
필요하면 새 트랜잭션(또는 새 DbContext scope)에서 다시 시작
  ↓
reward_claims 를 다시 조회
  ↓
이미 존재하면 → granted:true, duplicate:true 로 반환
```

⚠ EF Core 에서 예외가 난 `DbContext` 는 `ChangeTracker` 에 실패한 엔터티가 남아
있습니다. 그대로 `SaveChanges` 를 다시 부르면 같은 INSERT 를 또 시도합니다.
**기존 `DbContext` 를 정리하고 쓸지 새 scope 를 만들지는 현재 서버 구조를 보고
구현 단계에서 정합니다.** 문서가 요구하는 것은 위 두 줄의 원칙입니다.

⚠ **재시도 횟수를 무제한으로 두지 않습니다.** 1~2회로 제한하고, 그래도 실패하면
`500` 으로 올려 보냅니다. 클라이언트는 11.4.8 의 재시도(같은 `matchKey`)로 다시 옵니다.

⚠ **`duplicate` · `ALTAR_COMPLETED` · `REWARD_COOLDOWN` 은 재시도 대상이 아닙니다.**
그것들은 실패가 아니라 정상 업무 결과입니다. 구분은 11.4.14 에 있습니다.


#### 11.4.14 조기 반환에서 트랜잭션을 반드시 끝냅니다

`clear-reward` 는 `altar_state` 에 **잠금 읽기**(`SELECT ... FOR UPDATE`, 11.4.5)를 겁니다.
그래서 **중간에 빠져나가는 경로마다 트랜잭션을 어떻게 끝낼지**를 정해 두어야 합니다.

**원칙 — 두 갈래로 나눠서 봅니다**

```text
duplicate
  → reward_claims 사전 조회에서 확인한다
  → 트랜잭션을 시작하기 전에 반환한다
  → altar_state row lock 을 아예 잡지 않는다

트랜잭션이 시작된 뒤의 모든 조기 반환
  (ALTAR_COMPLETED · REWARD_COOLDOWN · 그 밖의 트랜잭션 내부 검증 실패)
  → 트랜잭션을 정상적으로 종료한 뒤 HTTP 응답을 반환한다
    (읽기만 했으면 Commit·Rollback 어느 쪽이든 무방하다)
```

⚠ 초안에는 셋을 한 줄로 묶어 두어 **`duplicate` 도 트랜잭션 안에 있는 것처럼**
읽혔습니다. `duplicate` 는 트랜잭션 밖입니다.

⚠ **`FOR UPDATE` 로 잡은 row lock 을 HTTP 응답 이후까지 들고 있으면 안 됩니다.**
`altar_state` 는 행이 하나뿐이라 **모든 봉헌과 모든 보상 지급이 그 한 행을 지납니다**
(10.3.2절). 잠금을 늦게 풀면 로비 전체의 봉헌이 그만큼 멈춥니다.

##### duplicate — `altar_state` 를 아예 잠그지 않습니다

중복 확인은 `reward_claims` 만 보면 끝납니다. 제단 상태와 아무 상관이 없습니다
(11.4.4 — 이미 확정된 지급을 뒤늦게 취소하지 않는다).

```text
1. reward_claims 에서 (user_id, matchKey) 조회      ← 트랜잭션 · 잠금 불필요

   있으면
     → granted:true, duplicate:true
     → altar_state 를 잠그지 않고 바로 반환

   없으면
     → 2. 트랜잭션 시작
     → 3. altar_state 잠금 읽기
     → 4. 보상 처리
```

**가장 흔한 재시도 경로가 제단 잠금을 건드리지 않게 됩니다.** 11.4.4 의 순서 결정
(중복 확인이 먼저)이 성능 면에서도 맞는 이유입니다.

##### ALTAR_COMPLETED — 잠갔으면 풀고 나서 응답합니다

```text
reward_claim 없음
   ↓
트랜잭션 시작
   ↓
altar_state FOR UPDATE
   ↓
이미 완료
   ↓
inventory 변경 없음 · reward_claim 생성 없음
   ↓
⚠ 트랜잭션 종료 (읽기만 했으므로 Commit 이든 Rollback 이든 무방하다)
   ↓
200 OK  granted:false  code: ALTAR_COMPLETED
```

```text
ALTAR_COMPLETED 응답을 만들기 위해 altar_state row lock 을 계속 들고 있을 이유가 없다.
```

응답 직렬화 · 로깅 · 네트워크 전송이 잠금 안에서 일어나면, 그 시간만큼 다른 봉헌이
대기합니다. **DB 작업이 끝나는 즉시 트랜잭션을 닫고 그 뒤에 응답을 만듭니다.**

##### 조기 반환은 재시도 대상이 아닙니다

11.4.13 의 재시도 정책과 헷갈리면 안 됩니다. **둘은 성격이 다릅니다.**

| 구분 | 예 | 처리 |
|---|---|---|
| **정상 업무 결과** | `duplicate` · `ALTAR_COMPLETED` · `REWARD_COOLDOWN` | 그대로 응답한다. **재시도하지 않는다** |
| **일시적 DB 실패** | deadlock victim · 트랜잭션 충돌 | 새 트랜잭션으로 1~2회 재시도 (11.4.13) |

⚠ `ALTAR_COMPLETED` 를 재시도하면 같은 답이 계속 나옵니다. 제단 완료는 되돌아오지 않는
상태이기 때문입니다 (11.4.7 방식 A). **재시도가 결과를 바꾸지 못하는 응답은 재시도하지
않습니다.**

#### 11.4.15 보상 abuse 완화 — **60초 쿨다운만** 씁니다 (확정)

> **[확정] 보상 abuse 완화 정책** (2026-09-20)
>
> - 동일 사용자 기준, **성공적인 지급 후 60초 쿨다운**을 적용한다.
> - **일일 지급 상한은 이번 구현에서 쓰지 않는다.**

##### 왜 일일 상한을 빼는가

```text
· 지금 세 미니게임 중 60초 안에 정상적으로 두 판을 연속 클리어할 수 있는 게임이 없다.
  → 60초 쿨다운이 정상 플레이를 막지 않는다.
· UNIQUE (user_id, match_key) 가 같은 판의 중복 지급을 이미 막는다.
· 60초 쿨다운은 "가짜 matchKey 를 빠르게 만들어 연속 요청" 하는 abuse 를 완화한다.
· 일일 상한은 정상 반복 플레이와 시연 · 테스트를 불필요하게 막는다.
· 일일 상한을 넣으려면 값 N · 날짜 기준 · 초기화 시각 · 시연 계정 예외까지
  정해야 한다. 지금 범위에 비해 복잡도가 크다.
· 무엇보다 일일 상한도 "서버가 클리어를 증명한다" 는 근본 해결이 아니다.
  근본 해결은 8.3절 B안(Fusion 서버에 신원 부여)이다.
```

> 나중에 실제 서비스 경제 시스템으로 확장할 경우 일일 지급 상한을 별도 abuse
> 정책으로 검토할 수 있습니다. **이번 구현 범위에는 넣지 않습니다.**

##### 방어 계층 — 역할이 서로 다릅니다

| 장치 | 막는 것 |
|---|---|
| `UNIQUE (user_id, match_key)` | 같은 판의 API 재시도 · 중복 지급 |
| `MatchInstanceId` | 서로 다른 정상 판을 구분 |
| **60초 쿨다운** | **짧은 시간에 가짜 `matchKey` 를 연속 제출하는 abuse** |
| ~~일일 상한~~ | **이번 구현에서는 쓰지 않음** |

##### 쿨다운의 기준은 "실제로 지급한 시각" 입니다

```text
granted:true 로 지급한 순간
  →  그 사용자의 다음 지급 가능 시각 = +60초
```

**다음은 쿨다운을 새로 시작시키지 않습니다.**

```text
duplicate:true            이미 지급된 판의 재시도다. 새 지급이 아니다
ALTAR_COMPLETED           지급하지 않았다
보상 검증 실패             지급하지 않았다
HTTP 재시도                위와 같다
```

⚠ **이 규칙은 저절로 지켜집니다.** 쿨다운을 `reward_claims.claimed_at` 의 최댓값으로
재기 때문입니다. `reward_claims` 에는 **실제로 지급한 판만** 행이 들어갑니다(11.4.6).

```sql
SELECT MAX(claimed_at) FROM reward_claims WHERE user_id = @userId;
-- idx_claims_user_time (user_id, claimed_at) 이 이 조회를 받는다 (STEP 1)
```

별도의 "마지막 지급 시각" 컬럼을 만들지 않습니다. 컬럼을 따로 두면
`duplicate` 경로에서 실수로 갱신할 여지가 생깁니다.

##### 60초 안에 새 판 요청이 들어오면

현재 게임 설계상 정상적으로는 일어나지 않지만, 서버 규칙은 분명히 둡니다.

```text
이전 성공 지급 후 60초 이내  +  새 matchKey  +  clear-reward 요청
  →  지급 거부
```

```json
429 Too Many Requests
{
  "success": false,
  "code": "REWARD_COOLDOWN",
  "message": "보상을 다시 받을 수 있을 때까지 잠시 기다려 주세요.",
  "retryAfterSeconds": 23
}
```

⚠ **`ALTAR_COMPLETED` 와 달리 `200` 이 아닙니다.** 완료는 정상적으로 도달하는
상태지만, 쿨다운은 **정상 플레이에서 나오지 않는 이상 신호**입니다.
클라이언트가 둘을 다르게 다뤄야 합니다.

⚠ **상태 코드는 `429` 로 확정합니다.**

```text
REWARD_COOLDOWN 은 동일 사용자의 짧은 시간 내 반복 지급 요청을 제한하는
rate limit 성격의 응답이다. HTTP 의미상 429 Too Many Requests 가 가장 직접적이다.
409 Conflict 대안은 사용하지 않는다.
```

이 API 에서 처음 쓰는 상태 코드입니다(기존은 `400` · `401` · `409` · `500` · `503`).
그래도 의미가 맞는 코드를 쓰는 편이 낫습니다.
표준 `Retry-After` 헤더를 같이 싣는 것은 **선택 구현**으로 남깁니다.
**본문 JSON 은 위 형태로 확정입니다.**

⚠ `retryAfterSeconds` 는 `ErrorResponse(Code, Message)` 에 없는 필드입니다
(`Contracts/AuthContracts.cs`). 제단 실패 응답이 이미 같은 방식으로 필드를 더하고
있으므로(9.2절) 같은 선례를 따릅니다. 표준 `Retry-After` 헤더를 같이 실어도 됩니다.

##### 검증 순서 — 쿨다운은 **맨 뒤**입니다

```text
① reward_claim 존재 확인   →  duplicate:true          (트랜잭션 시작 전)
② altar_state 완료 확인     →  ALTAR_COMPLETED
③ 서버 보상 검증
④ 쿨다운 확인               →  REWARD_COOLDOWN
⑤ UPSERT +1
⑥ reward_claims INSERT
```

**순서가 정책입니다.** 두 가지를 보장합니다.

```text
· 이미 지급이 확정된 판의 재시도(Test 5b)가 쿨다운에 막혀서는 안 된다
  → ①이 가장 먼저라 쿨다운까지 가지 않는다

· 섬이 이미 회복됐으면 REWARD_COOLDOWN 이 아니라 ALTAR_COMPLETED 가 나와야 한다
  → ②가 ④보다 먼저다
```

⚠ **쿨다운 때문에 `duplicate` 가 오류로 바뀌면 안 됩니다.** 그러면 이미 조각을
받은 플레이어에게 "잠시 기다려 주세요" 가 뜨고, 결과 화면이 보상을 못 보여 줍니다.

##### 결과 화면은 어떻게 보여 주는가

`REWARD_COOLDOWN` 은 11.4.8 의 **`NotGrantedBecauseCooldown`** 으로 다룹니다.

```text
"이번 판의 보상이 지급되지 않았습니다."  +  Console 경고(REWARD_COOLDOWN · retryAfterSeconds)
```

⚠ **`Unknown` 으로 다루지 않습니다.** 둘은 의미가 다릅니다.

```text
Unknown            서버 결과를 확인할 수 없음
REWARD_COOLDOWN    서버가 보상 미지급을 명확히 확정함
```

⚠ **`ALTAR_COMPLETED` 처럼 보상 줄을 숨기지도 않습니다.** 숨기면 "정상적으로 보상이
필요 없는 상태" 와 구분되지 않아, 실제로 이상이 생겼을 때 아무도 눈치채지 못합니다.

⚠ 화면 문구에서 **사용자에게 잘못을 돌리지 않습니다.** 금지 문구 목록은 11.4.8 에 있습니다.

#### 11.4.16 채택하지 않은 3안 (참고용 기록)

`targetOffering` 을 완료 후 늘려 다음 목표를 여는 방식은 **이번 설계에서 채택하지
않습니다.** 분석만 남겨 둡니다.

`altarActivated` 는 계산값이므로(10.7절) 목표를 올리면 연출이 꺼집니다.

```text
1000 / 1000  →  activated = true     빛이 켜져 있다
target 을 2000 으로 올린다
1000 / 2000  →  activated = false    ⚠ 빛이 꺼지고 회복도 100% → 50%
```

플레이어 눈에는 **"다 회복했는데 섬이 다시 망가졌다"** 로 보입니다.
3안을 쓰려면 `activated_at` 을 래치로 두어야 합니다.

```csharp
// 3안을 쓸 때만 필요하다. 이번 구현에는 넣지 않는다.
bool altarActivated = state.ActivatedAt != null
                   || state.TotalOffered >= state.TargetOffering;
```

⚠ **이번 구현 범위에 넣지 않습니다.**

```text
✗ activated_at 래치 도입
✗ 다음 시즌 목표
✗ 다단계 회복 목표
```

`activated_at` 은 **이번 구현의 필수 요구사항이 아닙니다.** 10.7절대로 계산식 판정
(`totalOffered >= targetOffering`)을 그대로 씁니다. DB 감사·로그 목적으로 컬럼을
남겨 두는 것은 무방하지만, **완료 조건에는 넣지 않습니다.**

---

## 12. 돌기둥 Blue VFX 동기화

### 12.1 이미 있는 것을 씁니다

3장에서 확인했듯 `P_HeartAltar` 안에 **`AltarBeam`** 오브젝트와
**`M_Altar_Beam_01.mat`** 머티리얼, **`Light_Monolith`** / **`Light_Crystal`** 라이트가
이미 있고 전부 켜져 있습니다.

그래서 **새 VFX 를 만들지 않습니다.** 기존 연출을 "약함 ↔ 강함" 으로 바꾸는 것이 가장 자연스럽습니다.

### 12.2 URP 에서 가능한 방법과 선택

| 방법 | 이 프로젝트에서 | 판단 |
|---|---|---|
| VFX Graph | **패키지가 없다** (`manifest.json` 에 `visualeffectgraph` 없음) | ✗ 패키지를 추가하면 빌드·팀 설정에 영향 |
| Particle System | 모듈 있음. `ARPG Effects` 에셋에 즉시 쓸 프리팹 다수 | ○ |
| Emission Material | `M_Altar_Beam_01` 이 이미 있다 | **◎** |
| Point Light | `Light_Monolith` 가 이미 있다 | **◎** |
| Shader | `Assets/Game/Art/Shaders/` 에 우리 셰이더가 있다 | △ 필요해지면 |

**권장: 기존 `AltarBeam` 은 그대로 두고, 달성 전용 연출 오브젝트를 따로 둡니다.**

```text
AltarBeam          기존. 항상 켜져 있다. 평소의 은은한 빛
AltarBeam_Awakened ★ 신규 자식. 프리팹에 꺼진 채로 저장한다. 1000 달성 시에만 켠다
Light_Monolith     기존. intensity 만 올린다 (2 → 8)
```

⚠ **`AltarBeam` 자체를 껐다 켜는 방식은 권하지 않습니다.** 두 가지 이유입니다.

1. **초기 한 프레임 문제.** 프리팹에서 `AltarBeam` 은 **켜진 채로 저장되어 있습니다**
   (0장에서 확인 — `m_IsActive: 1`). 로비에 들어가면 서버 응답이 오기 전까지 켜져 보이고,
   응답이 도착하는 순간 꺼집니다. **"파란 빛이 번쩍했다가 사라지는"** 화면이 됩니다.
   달성 전용 오브젝트를 **꺼진 채로 저장**해 두면 이 경로가 아예 없습니다.

2. **컨셉 아트와 맞습니다.** `island-restoration-gameplay-v3.png` 를 보면 회복도 68%
   시점에도 제단에서 이미 파란 빛기둥이 올라갑니다. 평소에 아무것도 없다가 1000 에서
   처음 빛나는 그림이 아닙니다. **평소 빛(기존) + 각성 연출(신규)** 이 아트와 일치합니다.

> 만약 "1000 전에는 제단이 완전히 어두워야 한다" 가 기획이라면, 그때는 `AltarBeam` 을
> **프리팹에서 꺼진 상태로 저장**한 뒤 켜는 방식으로 갑니다. 어느 쪽이든 **"프리팹에
> 저장된 상태 = 서버 응답 전에 보여야 할 상태"** 를 맞추는 것이 핵심입니다.
(연출을 더 키우고 싶으면 `ARPG Effects` 의 기둥 계열 프리팹을 `AltarBeam` 자리에 얹습니다.
`ProximityPortal.cs:59-77` 이 이미 그 에셋의 프리팹을 코드로 스폰하는 본보기입니다.)

⚠ `Assets/ARPG Effects` 는 **외부 에셋**입니다. `CLAUDE.local.md` §4 에 따라
원본을 고치지 않고, 우리 프리팹에서 **참조만** 합니다.

### 12.3 네트워크 로직과 연출의 분리 (요청서 9장 요구)

```text
AltarState  (static)              서버가 준 숫자만 안다. Unity 를 모른다
    │  event Changed
    ▼
AltarVfxController  (MonoBehaviour, 제단에 붙는다)
    │  AltarState.Activated 를 읽어
    ▼
AltarBeam.SetActive(on)  /  Light_Monolith.intensity = on ? 8 : 2
```

`AltarVfxController` 는 **`NetworkBehaviour` 가 아닙니다.** 네트워크를 전혀 모릅니다.
각 클라이언트가 자기 `AltarState` 를 보고 자기 화면을 칠합니다.

### 12.4 Late Join / 씬 재진입 / 재로그인

요청서 9장의 네 조건을 어떻게 만족하는지:

| 조건 | 어떻게 |
|---|---|
| 기존 접속 플레이어 모두에게 활성화 | `Rpc_AltarChanged` → 각자 `GET /api/altar/state` (9.3절) |
| 늦게 접속한 플레이어에게도 복원 | **접속 시 무조건 한 번 조회한다.** `LocalPlayer.Registered` 에서 |
| Scene 재진입 후에도 복원 | 같음. 미니게임 다녀와도 로비 재진입 시 다시 조회 |
| 호스트가 아니라 서버 상태 기준 | 값의 출처가 MySQL 하나뿐이다. Fusion 서버도 안 들고 있다 |

```csharp
// AltarVfxController 또는 AltarStateInstaller
private void OnEnable()
{
    AltarState.Changed += Apply;
    AltarState.RequestRefresh();   // ← 늦게 들어왔든 돌아왔든, 켜지는 순간 한 번 묻는다
    Apply();                       // ← 캐시가 있으면 즉시 반영 (한 프레임 깜빡임 방지)
}
```

**이 한 줄(`RequestRefresh`)이 Late Join 문제 전체를 해결합니다.**
Fusion 의 `[Networked]` 를 쓰지 않아도 되는 이유이기도 합니다 — 상태가 DB 에 있으니까요.

⚠ 조회가 실패하면(서버 꺼짐 등) **연출을 끄지 말고 마지막 상태를 유지**합니다.
꺼 버리면 잠깐 네트워크가 끊긴 것 때문에 "1000개를 모았는데 빛이 사라지는" 화면이 됩니다.

### 12.5 `AltarState` 동기화 규칙 — RPC 만으로는 부족합니다

RPC 알림(9.3절)에는 **구멍이 세 개** 있습니다. 전부 `AltarState` 한 곳에서 막습니다.

#### 구멍 1 — RPC 는 같은 채널에만 갑니다

```text
altar_state 는 DB 하나다          → lobby-ch1 과 lobby-ch2 가 같은 값을 공유한다 (결정 #5)
Rpc_AltarChanged 는 세션 단위다   → ch1 에서 봉헌해도 ch2 는 모른다
```

**ch2 화면은 DB 가 730 인데 729 를 계속 보여줍니다.** 문서가 "전 서버 공통 목표" 라고
정의한 것과 화면이 어긋납니다. RPC 는 **같은 채널을 빠르게 맞추는 보조 장치**일 뿐입니다.

→ **저빈도 주기 동기화를 둡니다.**

```text
간격 30초 (권장)
```

> 5~10초가 아니라 30초인 이유: 결정 #1 로 조각은 클리어 1회당 1개입니다.
> 전역 합계가 **분 단위로도 거의 안 움직입니다.** 100명 채널에서 5초 폴링이면
> 초당 20건이 아무것도 바뀌지 않은 상태를 계속 확인하게 됩니다.
> 같은 채널은 RPC 가 즉시 맞추므로, 폴링은 "다른 채널 · RPC 유실" 만 따라잡으면 됩니다.
> 인스펙터에서 바꿀 수 있게 두고 시연 환경에서 조정하세요.

#### 구멍 2 — `RequestRefresh()` 가 여러 곳에서 동시에 불립니다

부르는 곳이 이미 다섯입니다 — `AltarVfxController.OnEnable`, `IslandRecoveryView.OnEnable`,
`LocalPlayer.Registered`(Late Join), `Rpc_AltarChanged` 수신, 주기 폴링.
로비에 들어가는 순간 이 중 셋이 거의 같은 프레임에 겹칩니다.

→ **진행 중인 요청이 있으면 합칩니다 (coalescing).**

```csharp
private static bool inFlight;

public static void RequestRefresh()
{
    if (inFlight) return;          // 이미 묻고 있다. 같은 답을 두 번 받을 이유가 없다
    inFlight = true;
    // ... 응답이 오거나 실패하면 inFlight = false
}
```

#### 구멍 3 — 늦게 온 옛 응답이 새 값을 덮습니다

```text
GET A 출발 (730)  ────────────────────────▶ 늦게 도착
     POST offer 응답 도착 (731) → 화면 731
GET A 도착 (730)                          → 화면이 731 → 730 으로 되돌아간다
```

구멍 2 를 막으면 GET 끼리는 겹치지 않지만, **`POST /api/altar/offer` 의 응답도 상태를
싣고 옵니다.** 그래서 GET 과 POST 는 여전히 겹칠 수 있습니다.

→ **적용 순번(sequence)으로 막습니다.**

```csharp
private static int issuedSeq;    // 요청을 낼 때마다 +1
private static int appliedSeq;   // 마지막으로 반영한 번호

// 응답을 반영하기 직전
if (responseSeq < appliedSeq) return;   // 이미 더 새 것을 반영했다. 버린다
appliedSeq = responseSeq;
```

> 서버의 `updatedAt` 을 쓰는 방법도 있지만 **권하지 않습니다.**
> `updatedAt` 은 `altar_state`(전역)의 시각이라 `remainingFragments`(내 보유량)의
> 신선도를 말해 주지 못합니다. 전역은 그대로인데 내 보유량만 바뀐 경우를 거르지 못합니다.
> 클라이언트가 세는 순번은 두 값을 한 덩어리로 다루므로 그 함정이 없습니다.
> `updatedAt` 은 로그·디버그용으로만 씁니다.

#### 정리 — 이 셋은 전부 `AltarState` 안에 들어갑니다

```text
AltarState
  RequestRefresh()      coalescing + debounce
  주기 폴링 (30초)       다른 채널 · RPC 유실 복구
  순번 가드              늦게 온 응답 무시
  Changed 이벤트         화면은 이것만 듣는다
```

**화면(`AltarVfxController` · `IslandRecoveryView` · `AltarOfferingUIController`)은
이 규칙을 하나도 모릅니다.** `RequestRefresh()` 를 아무 때나 불러도 안전합니다.

---

## 13. 섬 회복도 HUD 연동

### 13.1 현재 상태: HUD 가 없습니다

0장에서 확인했습니다. `섬 회복도` · `회복도` · `Recovery` 로 검색한 결과
로비 HUD 에 해당하는 코드는 없습니다. `Recovery` 히트는 전부 낚시 텐션 회복
(`FishingV2FightModel.cs`) 과 광산 블록 복구(`MineGame.cs`)입니다.

### 13.2 그런데 이름이 겹치는 값이 하나 있습니다

```csharp
// RewardService.cs:36-41
/// <summary>보유 조각 수가 바뀔 때마다. 로비의 회복 게이지가 듣는다.</summary>
public static event Action Changed;

/// 바다의 심장이 얼마나 돌아왔는가 (0~1).
/// 조각 종류 수를 기준으로 센다. 지금은 세 미니게임 = 세 조각.
public static float RecoveryRatio => OwnedFragments.Count / (float)TotalFragmentCount;
```

주석에 **"로비의 회복 게이지가 듣는다"** 라고 적혀 있지만, 실제로 듣는 코드는 없습니다.
`RewardService` 를 참조하는 곳은 6군데뿐이고 전부 미니게임 결과 화면 쪽입니다.

**즉 회복도 게이지는 "만들다 만 것" 이 아니라 "아직 안 만든 것" 입니다.**

### 13.3 두 값의 관계 — 반드시 정해야 합니다

```text
RewardService.RecoveryRatio     = 가진 조각 종류 / 3       (0, 0.33, 0.67, 1.0)
AltarState.RecoveryPercent      = 전체 봉헌량 / 1000 × 100  (0 ~ 100, 연속)
```

두 값은 **같은 이름으로 완전히 다른 것**을 가리킵니다. 그대로 두면 팀원이 혼동합니다.

> **결정 #4 — (a) 봉헌량 기준으로 확정** (2026-09-20)

```text
섬 회복도 = min(totalOffered / targetOffering, 1) * 100      ← 서버가 계산한다
```

미니게임 클리어는 회복도에 **직접** 기여하지 않습니다. 조각을 통해 기여합니다.

```text
미니게임 클리어 → 조각 +1 → 제단에 봉헌 → 회복도 상승
```

따라오는 정리 두 가지:

1. **`RewardService.RecoveryRatio` 는 섬 회복도가 아닙니다.** 결정 #1 로 `RewardService`
   자체가 보상 경로에서 빠지므로(4.2.3절) 이 속성은 아무 데서도 안 쓰이게 됩니다.
   지우지는 않되, **`:29` 주석의 "로비의 회복 게이지가 듣는다" 한 줄은 지웁니다.**
   그 줄이 지금 혼동의 유일한 원인입니다.

2. **클라이언트는 공식을 갖지 않습니다.** `AltarState.RecoveryPercent` 는 서버 응답을
   그대로 담기만 합니다. 기획이 바뀌어도 고칠 곳이 서버 한 군데입니다.

### 13.4 HUD 구현

화면 상단 중앙. 봉헌 UI 와 같은 방식으로 설치합니다 (6.2절).

```text
Assets/Game/Resources/IslandRecoveryHud.prefab    ★ 신규
Assets/Game/Scripts/Lobby/IslandRecoveryView.cs   ★ 신규
```

```csharp
private void OnEnable()
{
    AltarState.Changed += Apply;
    AltarState.RequestRefresh();
}

private void Apply()
{
    bar.fillAmount  = AltarState.RecoveryPercent / 100f;
    label.text      = $"{AltarState.RecoveryPercent:0}%";
}
```

⚠ 봉헌 직후에는 **숫자가 튀지 않게 보간**하는 편이 좋습니다.
68% → 73% 가 한 프레임에 점프하면 "올랐다" 는 느낌이 안 납니다.
`island-restoration-gameplay-v3.png` 의 "[시스템] 섬 회복도가 3% 상승했습니다" 문구와 짝을 이룹니다.

⚠ `LobbyChatView` 와 같은 자리를 쓰지 않는지 확인하세요. 채팅은 **왼쪽 아래**,
회복도 HUD 는 **위쪽 중앙** 이므로 현재는 안 겹칩니다.
튜토리얼(`LobbyTutorial`)도 왼쪽 아래라 채팅과 겹치고, 그건 이미 서로 양보하도록 되어 있습니다.

---

## 14. 저장 방식

### 14.1 현재 프로젝트는 어디까지 영구 저장하는가

| 데이터 | 저장 위치 | 영속성 | 근거 |
|---|---|---|---|
| 계정 (이메일/비밀번호) | MySQL `users` | **영구** | `AraAttiDbContext.cs:40-73` |
| 캐릭터 (이름/피부색) | MySQL `characters` | **영구** | `:75-137` |
| 캐릭터 외형 파츠 | MySQL `character_parts` | **영구** | `:139-174` |
| JWT 토큰 | 메모리 | 세션 (120분) | `appsettings.json` `ExpiresMinutes: 120` |
| 닉네임/외형 캐시 | `PlayerPrefs` | **캐시일 뿐** | `CharacterSessionCache.cs:14-19` |
| 튜토리얼 본 적 있음 | `PlayerPrefs` | 로컬 | `LobbyTutorial.MarkDone` |
| **보상 조각** | **정적 메모리** | **플레이 모드 종료 시 소멸** | `RewardService.cs:24` |
| 미니게임 결과 | 저장 안 함 | — | `INetworkService.ReportMiniGameResult` 는 Fake 만 있다 |

**패턴이 분명합니다: "계정에 딸린 것은 MySQL, 화면 편의는 PlayerPrefs, 게임 진행은 아직 없음."**

`CharacterSessionCache.cs:14-19` 가 이 원칙을 명시합니다 —
**"서버(MySQL)가 원본이다. PlayerPrefs 는 그 응답의 캐시일 뿐이다."**

### 14.2 A. 서버 실행 중에만 유지한다면

```text
전체 봉헌량을 Fusion 서버의 [Networked] 프로퍼티로 둔다
→ 서버 재시작하면 0
→ 채널마다 값이 다르다 (lobby-ch1 = 1000, lobby-ch2 = 0)
→ 인벤토리를 차감할 곳이 없다 (인벤토리는 DB 에 있어야 하므로)
```

### 14.3 B. DB 에 영구 저장한다면

```text
player_inventories + altar_state (+ altar_contributions) 테이블 신설
→ 서버 재시작해도 유지
→ 채널이 달라도 같은 값 (전 서버 공통 목표)
→ 재로그인·다른 PC 에서도 같은 값
```

### 14.4 현재 프로젝트 권장안

**B (DB 영구 저장) 로 확정되었습니다.** (결정 #5)

선택 이유:

1. **`섬 회복도` 는 장기 누적 콘텐츠입니다.** 1000개를 한 세션에 모을 수 없습니다.
   서버를 재시작할 때마다 0으로 돌아가면 콘텐츠가 성립하지 않습니다.
2. **채널이 둘입니다** (`ChannelCatalog.cs:56-60`). 채널마다 회복도가 다르면
   "우리 섬" 이라는 콘셉트가 깨집니다. DB 하나에 두면 자동으로 공유됩니다.
3. **인벤토리는 선택의 여지가 없습니다.** 조각은 미니게임에서 얻고 로비에서 씁니다.
   그 사이에 씬 전환(=Fusion Runner 종료)이 들어갑니다. 메모리에 두면 사라집니다.
4. **이미 그 구조가 있습니다.** MySQL · EF Core · 마이그레이션 · JWT 가 다 돌고 있습니다.
   테이블 2~3개와 엔드포인트 3개를 더하는 일입니다. 새 인프라가 없습니다.

> **결정 #5 — B (DB 영구 저장) 로 확정** (2026-09-20)

확정에 따른 전제:

```text
인벤토리    MySQL player_inventories     ← 클라이언트 메모리에 두지 않는다
전역 봉헌량 MySQL altar_state            ← Fusion [Networked] 에 두지 않는다
Fusion      아무 상태도 보관하지 않는다   ← "바뀌었다" 알림만 나른다 (9.3절)
```

⚠ 이 결정으로 **Fusion 쪽에 저장 상태가 하나도 없습니다.** 그래서 Late Join 복원이
`[Networked]` 가 아니라 `AltarState.RequestRefresh()` 한 번으로 해결됩니다 (12.4절).
Fusion 프리팹·리베이크를 건드릴 일이 그만큼 줄어듭니다.

---

## 15. 예외 처리

### 15.1 수량 관련

| 입력 | 클라이언트 | 서버 |
|---|---|---|
| `0` | `-` 버튼이 1 아래로 안 내려간다. 봉헌 버튼 비활성 | `400 AMOUNT_INVALID` |
| 음수 | UI 상 불가능 | `400 AMOUNT_INVALID` |
| 문자 | InputField 를 안 쓰면 불가능. 쓴다면 `contentType = IntegerNumber` | JSON 파싱 실패 → `400` |
| 소수 | 위와 같음 | `int` 로 받으므로 파싱 실패 → `400` |
| 보유량 초과 | `MAX` · `+` 가 `maxOfferAmount` 에서 멈춘다 | `409 NOT_ENOUGH_FRAGMENTS` (10.3.1 의 `WHERE quantity >= amount`) |
| **남은 칸 초과** | `MAX` · `+` 가 `maxOfferAmount` 에서 멈춘다. "섬 회복까지 N개만 더 필요합니다." | `409 OFFERING_AMOUNT_CHANGED` — **전체 실패, 차감 0** (8.6.2) |
| 보유량 0 | UI 를 열되 "보유한 조각이 없습니다" + 봉헌 버튼 비활성 | `409 NOT_ENOUGH_FRAGMENTS` |
| **남은 칸 0 (회복 완료)** | 봉헌 버튼 비활성 + "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다." | `409 OFFERING_CLOSED` |
| 빈 입력 | 기본값 1 (또는 마지막 값, `maxOfferAmount` 로 자름) | — |
| 지나치게 큰 숫자 | `+` 가 `maxOfferAmount` 에서 멈춘다 | `int` 범위 초과 시 `400`. 남은 칸·보유량 검증이 또 걸러낸다 |

⚠ **클라이언트 검증은 편의일 뿐입니다.** 전부 서버가 다시 봅니다.
`Unity Inspector 에 저장된 로컬 값만으로 보유량을 판단하는 구조는 사용하지 않는다` (요청서 16장).

### 15.2 네트워크 관련

| 상황 | 처리 |
|---|---|
| 봉헌 버튼 연속 클릭 | 첫 클릭에 `interactable = false`. 응답 전까지 잠금 (10.5) |
| 같은 요청 중복 전송 | `requestId` UNIQUE → `duplicate: true` 로 **이전 결과 반환.** 두 번 안 깎인다 (10.4) |
| 응답 전에 UI 재클릭 | 위와 같음 |
| 네트워크 지연 | `HttpJson.TimeoutSeconds = 10` (`HttpJson.cs:56`). "봉헌하는 중..." 유지 |
| 서버 오류 (5xx) | `HttpJson` 이 `message` 를 꺼내거나 "요청을 처리하지 못했습니다. (HTTP 500)" (`HttpJson.cs:133-135`) |
| 서버 연결 끊김 | `ConnectionError` → "서버에 연결할 수 없습니다..." (`HttpJson.cs:118-123`). **보유량을 건드리지 않는다** |
| 응답 유실 (봉헌) | 같은 `requestId` 로 재시도 → 서버가 `duplicate` 로 안전 처리 (10.4) |
| **응답 유실 (미니게임 보상)** | 같은 `matchKey` 로 1~2회 재시도. **응답 없음을 "보상 없음" 으로 읽지 않는다** (11.4.8) |
| JWT 만료 (401) | 로그인 화면으로 보내지 말고 "로그인이 필요합니다" 표시 + UI 닫기. 재로그인은 별도 흐름 |

⚠ **실패했을 때 로컬 캐시를 지우지 않습니다.** `CharacterSessionCache.cs:21-24` 가
같은 판단을 이미 적어 뒀습니다 — "실패했을 때 캐시를 지우면, 잠깐 서버가 죽은 것 때문에
멀쩡한 로컬 값을 잃는다."

### 15.3 멀티플레이 관련

| 상황 | 처리 |
|---|---|
| A · B 동시 봉헌 (칸이 넉넉) | 조건부 원자적 UPDATE 가 직렬화한다. 둘 다 성공 (10.3.1) |
| **A · B 가 마지막 칸을 두고 경쟁** | **한 명만 성공.** 진 쪽은 전체 실패 + 차감 0 + `OFFERING_AMOUNT_CHANGED` (10.6) |
| **UI 를 연 뒤 남은 칸이 줄었다** | 서버가 최신 값으로 재검증해 전체 실패. UI 가 응답값으로 스스로를 맞춘다 (6.4.2) |
| `total > target` 인 비정상 데이터 | 방어적 `>=` 판정으로 연출은 켜 둔다 (10.7) |
| **마지막 봉헌과 미니게임 보상이 동시** | 같은 `altar_state` 행을 잠금 기준으로 삼아 Commit 순서를 따른다. 완료 Commit 이후 지급 없음 (11.4.5) |
| 늦게 들어온 플레이어 | `LocalPlayer.Registered` → `AltarState.RequestRefresh()` (12.4) |
| Scene 재접속 | 같음 |
| 플레이어 재로그인 | JWT 가 새로 발급되고, 인벤토리는 `users.id` 에 매달려 있으므로 그대로 |
| 다른 채널로 이동 | DB 가 하나라 값이 같다 (14.4) |

### 15.4 인벤토리를 `users` 에 달 것인가 `characters` 에 달 것인가

> **결정 #6 — `users` 로 확정** (2026-09-20)
>
> 그래서 `player_inventories.user_id` 입니다. `character_id` 가 아닙니다.
> `POST /api/altar/offer` 는 **어떤 id 도 본문으로 받지 않습니다.** JWT `sub` 하나로 끝납니다.
> 캐릭터가 몇 개가 되든 이 규칙은 바뀌지 않습니다.
>
> 부수 효과: 8.6절 검증 2번(`CHARACTER_REQUIRED`)이 **필요 없어졌습니다.**
> 인벤토리가 캐릭터에 매달려 있지 않으므로, 캐릭터가 없어도 조각을 들 수 있습니다.

```text
users 에 달면     계정당 하나. 다중 캐릭터가 생겨도 조각을 공유한다
characters 에 달면 캐릭터당 하나. 다중 캐릭터 시 각자 따로 모은다
```

현재 정책은 **계정당 캐릭터 1개**입니다 (`CharacterEndpoints.cs:32` `MaxCharactersPerUser = 1`).
그래서 지금은 차이가 없습니다. 다만 그 파일의 주석이 **다중 캐릭터 확장을 명시적으로 준비**해
두고 있습니다 (`CharacterEndpoints.cs:26-30`, `CharacterSessionCache.cs:52-65`).

**`users` 에 매다는 쪽을 권합니다.** (초안에서는 `characters` 를 권했으나, 아래 ② 을
다시 보고 바꿨습니다.)

① `character_parts` 는 **외형**이라 본질적으로 캐릭터마다 다른 값입니다.
   조각은 그렇지 않습니다. "섬을 함께 복구한다" 는 계정 단위 목표에 더 가깝습니다.

② **결정적인 이유** — 다중 캐릭터가 열렸을 때 `POST /api/altar/offer` 가 모호해집니다.
   인벤토리가 캐릭터에 매달려 있으면 "어느 캐릭터가 봉헌하는가" 를 요청이 말해야 하고,
   그러면 본문에 `characterId` 를 받아야 합니다. 지금 서버의 규칙
   (`CharacterEndpoints.cs:15` — "요청 본문의 userId 같은 것은 받지 않는다")이 깨집니다.
   `users` 에 매달면 JWT `sub` 하나로 끝나고, 캐릭터가 몇 개가 되든 영원히 모호하지 않습니다.

③ 캐릭터를 지우고 다시 만들어도 모은 조각이 사라지지 않습니다.

대신 포기하는 것: `character_parts` 와 모양이 달라집니다. 나중에 "캐릭터마다 따로 모은다" 로
바꾸려면 FK 를 옮기는 마이그레이션이 필요합니다. 다만 그 방향의 기획은 현재 없습니다.

### 15.5 UI 관련

| 상황 | 처리 |
|---|---|
| 인벤토리 열린 상태에서 이동 입력 | `ChatFocus.Begin(this)` 로 막는다. ⚠ 지금의 단순 bool 로는 채팅과 겹칠 때 풀린다 — **6.4.1절을 먼저 고친다** |
| ESC | `ChatFocus.Typing` 이 false 일 때만 제단 UI 가 읽는다 (6.4) |
| 제단 범위를 벗어남 | 서버 응답 대기 중이 아니면 닫는다. 히스테리시스 적용 (5.5) |
| 다른 UI 와 동시에 열림 | 튜토리얼이 돌면 제단 상호작용 자체를 막는다 (6.5) |
| 채팅창과 InputField 충돌 | `- / + / MAX` 방식이면 InputField 가 없어 충돌 자체가 없다 (6.3) |
| 마우스 커서 Lock | **로비는 커서를 잠그지 않는다.** `Cursor.lockState` 를 건드리는 곳은 광산뿐 (`MineCamera.cs:299`). 로비 UI 는 그대로 클릭된다 |
| EventSystem 이 없다 | `LobbyChatInstaller.WarnIfNoEventSystem` 처럼 **경고만 하고 만들지 않는다.** 직접 만들었다가 로비가 통째로 멈춘 적이 있다 (`LobbyChatInstaller.cs:96-105`) |

---

## 16. 기존 수정 파일 후보

> **실제로 같은 역할을 하는 파일이 있으면 신규 생성을 제안하지 않았습니다.**
> 아래는 전부 "이미 있고, 손대야 하는" 파일입니다.

### 서버

| 파일 | 현재 역할 | 왜 수정이 필요한가 | 예상 수정 범위 |
|---|---|---|---|
| `server/AraAtti.Api/Data/AraAttiDbContext.cs` | 기존 테이블 3개(`users` · `characters` · `character_parts`)의 스키마를 한 곳에 모아 정의 | 새 테이블 **4개**의 `DbSet` 과 `OnModelCreating` 블록을 같은 자리에 추가해야 한다. 이 파일의 문서화된 원칙이 "테이블 구조는 파일 하나만 보면 된다" (`:9-11`) | `DbSet` 4줄 + `entity` 블록 4개 (약 +130줄) |
| `server/AraAtti.Api/Program.cs` | 엔드포인트 그룹 등록 | `app.MapInventoryEndpoints();` `app.MapAltarEndpoints();` 두 줄 | +2줄 + 주석 |
| `server/README.md` | 팀원용 서버 실행·검증 절차 | 6장·7장이 엔드포인트별 PowerShell 검증 절차를 담고 있다. 8장(인벤토리)·9장(제단)을 같은 형식으로 추가해야 팀원이 확인할 수 있다 | +80~120줄 |

### Unity 클라이언트

| 파일 | 현재 역할 | 왜 수정이 필요한가 | 예상 수정 범위 |
|---|---|---|---|
| `Assets/Game/Scripts/Account/HttpApiConfig.cs` | 서버 주소·경로 상수를 한 곳에 모음 | `:6-7` 이 "경로 문자열을 서비스 코드 안에 흩어 놓지 않는다" 고 명시. 새 경로 3개를 여기 넣어야 한다 | +3줄 |
| `Assets/Game/Scripts/Account/AccountServiceBootstrap.cs` | Fake↔Http 전환 지점 (유일) | 인벤토리·제단 서비스도 같은 스위치를 타야 한다. `:8` "여기가 갈아끼우는 유일한 지점" | +4줄 (Fake/Http 각 2줄) |
| `Assets/Game/Prefabs/HeartAltar/P_HeartAltar.prefab` | 제단 3D 모델 | `AltarInteraction` 자식 오브젝트 + 스크립트 2개를 붙여야 한다 | **⚠ 사용자가 Unity Editor 에서 직접** (`CLAUDE.local.md` §1) |
| `Assets/Game/Scripts/MiniGames/Common/RewardService.cs` | 미니게임 클리어 도장 | `:29` 주석 "로비의 회복 게이지가 듣는다" 가 사실과 다르고, 13.3절의 혼동 원인이다 | 주석 1~2줄 수정만 |
| `Assets/Game/Scripts/UI/ChatFocus.cs` | 이동 입력 차단 플래그 | **단순 bool 이라 화면이 둘 이상 잠그면 깨진다.** 보유자 집합으로 바꿔야 한다 | 구현 교체 + `HeldByOther` 추가 (약 20줄) — **6.4.1절** |
| `Assets/Game/Scripts/UI/LobbyChatView.cs` | 로비 채팅창 | `ChatFocus` 시그니처가 바뀌므로 호출 4곳에 `this` 를 넘긴다 | `:94, :95, :144, :262` 각 1줄 |
| 플레이어 프리팹 (`Assets/Game/Prefabs/Characters/` 하위) | Fusion 네트워크 캐릭터 | `AltarOfferingRelay` (NetworkBehaviour) 를 붙여야 한다. `LobbyChatRelay` 가 붙어 있는 바로 그 오브젝트 | **⚠ 사용자가 Unity Editor 에서 직접** |

### 결정 #1(갈래 1)에 따라 함께 고칠 파일 — STEP 11

| 파일 | 왜 | 범위 |
|---|---|---|
| `Assets/Game/Scripts/MiniGames/Common/MatchFlowController.cs` | `:175` 의 `RewardService.Grant(rewardId)` 가 로컬 `HashSet` 에 조각을 넣는다. 서버 지급(+1)으로 바꿔야 한다 | `CompleteMiniGame` 내부 (약 15줄) |
| `Assets/Game/Scripts/MiniGames/Common/UI/ResultPanelPresenter.cs` | `:119` 의 "이미 보유 중" 분기가 의미를 잃는다. 그리고 결정 #8 로 보상 줄이 **다섯 가지 상태**(Pending / Granted / 완료로 숨김 / 쿨다운 / Unknown)를 가져야 한다 | `ShowReward` 내부 (약 30줄) — **11.4.8절** |

**이 둘은 고치지 않습니다** (4.2.3절):

```text
MiniGames/Common/RewardService.cs      클래스를 지우지 않는다. 주석 한 줄만 고친다
MiniGames/Common/MiniGameConfig.cs     fragmentId 를 지우지 않는다.
                                       지급량 필드(fragmentRewardAmount)도 만들지 않는다 —
                                       지금은 상수 1 이다 (CLAUDE.md §2)
```

### 수정하지 않는 파일 (확인함)

```text
Assets/Game/Scripts/Lobby/MiniGamePortal.cs      키가 E 로 확정되어 손대지 않는다 (2.6절)
Assets/Game/Scripts/Network/PlayerInputProvider.cs  ChatFocus 를 이미 보므로 그대로 둔다
Assets/Game/Scripts/UI/ChatFocus.cs              그대로 쓴다. :10-11 이 "아이템창이 늘어도 여기에
                                                 한 줄 얹으면 끝" 이라고 적어 뒀지만, 실제로는
                                                 한 줄도 안 얹어도 된다 — Begin/End 를 부르기만 하면 된다
Assets/Game/Scenes/Main/CoreGames/Lobby.unity    UI 를 씬에 올리지 않으므로 손대지 않는다 (6.2)
Assets/Game/Scripts/Network/LocalPlayer.cs       그대로 쓴다
```

---

## 17. 신규 파일 후보

### 서버 (`server/AraAtti.Api/`)

| 파일 | 경로 | 책임 | 왜 필요한가 |
|---|---|---|---|
| `PlayerInventoryItem.cs` | `Entities/` | `user_id` / `item_id` / `quantity` | 인벤토리 테이블이 없다 (결정 #6) |
| `AltarState.cs` | `Entities/` | `total_offered` / `target_offering` / `activated_at` / `updated_at` | 전역 상태 테이블이 없다. `activated_at` 은 감사용이고 활성화 판정은 계산식이다 (10.7 · 11.4.16) |
| `AltarContribution.cs` | `Entities/` | `request_id` / `user_id` / `amount` / `created_at` | 봉헌 중복 방지 + 기여 이력 (17.1) |
| `RewardClaim.cs` | `Entities/` | `user_id` / `game_id` / `match_key` / `claimed_at` | **미니게임 보상 중복 방지 + 60초 쿨다운.** 초안에 빠져 있었다 (STEP 1, STEP 11) |
| `InventoryContracts.cs` | `Contracts/` | 요청·응답 DTO | `AuthContracts` · `CharacterContracts` 와 같은 자리 |
| `AltarContracts.cs` | `Contracts/` | 요청·응답 DTO | 같음 |
| `InventoryEndpoints.cs` | `Endpoints/` | `GET /api/inventory` | `CharacterEndpoints` 와 같은 모양 |
| `AltarEndpoints.cs` | `Endpoints/` | `GET /api/altar/state`, `POST /api/altar/offer` | 봉헌 검증·트랜잭션이 전부 여기 |
| 마이그레이션 | `Migrations/` | `dotnet ef migrations add AddInventoryAndAltar` | 자동 생성. `README.md` 3장 절차 |

### Unity (`Assets/Game/Scripts/`)

| 파일 | 경로 | 책임 | 왜 필요한가 |
|---|---|---|---|
| `ItemIds.cs` | `Inventory/` | 아이템 id 상수 | 문자열 리터럴이 흩어지는 것을 막는다 |
| `PlayerInventory.cs` | `Inventory/` | 보유 수량 **캐시** + `Changed` | `RewardService` 와 같은 모양. **원본은 서버** |
| `IInventoryService.cs` | `Inventory/` | 조회 인터페이스 | `IAuthService` 와 같은 경계 |
| `HttpInventoryService.cs` | `Inventory/` | `UnityWebRequest` 구현 | `HttpCharacterService` 를 본보기로 |
| `FakeInventoryService.cs` | `Inventory/` | 서버 없이 화면 확인 | `FakeCharacterService` 와 같은 이유 |
| `AltarState.cs` | `Lobby/` | 전역 봉헌 상태 캐시 + `Changed` + `RequestRefresh()` | 12.4 의 Late Join 해결 지점 |
| `IAltarService.cs` | `Lobby/` | 조회·봉헌 인터페이스 | |
| `HttpAltarService.cs` | `Lobby/` | HTTP 구현 | |
| `FakeAltarService.cs` | `Lobby/` | 가짜 구현 | |
| `AltarInteraction.cs` | `Lobby/` | 거리 판정 + 키 입력 + 안내 토글 | `MiniGamePortal` 패턴. **재사용 가능한 기존 클래스 없음** (5.1) |
| `AltarOfferingUIController.cs` | `Lobby/` | 봉헌 UI 흐름 (수량 · 버튼 · 오류) | |
| `AltarOfferingInstaller.cs` | `Lobby/` | UI 프리팹 설치 | `LobbyChatInstaller` 패턴 (6.2) |
| `AltarOfferingRelay.cs` | `Lobby/` | Fusion RPC 알림 (`NetworkBehaviour`) | `LobbyChatRelay` 패턴 (9.3) |
| `AltarVfxController.cs` | `Lobby/` | `AltarBeam` / 라이트 토글 | 네트워크와 연출 분리 (12.3) |
| `IslandRecoveryView.cs` | `Lobby/` | 상단 회복도 HUD | 13.4 |
| `IslandRecoveryInstaller.cs` | `Lobby/` | HUD 프리팹 설치 | |

### Unity 에셋 (사용자가 Editor 에서 생성)

```text
Assets/Game/Resources/AltarOfferingUI.prefab       봉헌 UI (HUD 이미지 전달 후)
Assets/Game/Resources/IslandRecoveryHud.prefab     상단 회복도 HUD
Assets/Game/Art/UI/Altar/                          HUD 이미지 (사용자 제공 예정)
```

### 17.1 `altar_contributions` 테이블이 정말 필요한가

요청서 15장에서 물어보신 부분입니다. **필요합니다.** 하지만 이유가 "통계" 가 아닙니다.

| 쓸모 | 필요도 |
|---|---|
| **중복 요청 방지 (`request_id` UNIQUE)** | **필수.** 이것 없이는 10.4 의 멱등성을 구현할 방법이 없다 |
| 유저별 기여도 (`myOfferedTotal`) | 선택. 하지만 "내가 얼마나 기여했나" 는 협동 콘텐츠의 핵심 재미다 |
| **지급 중복 방지에도 같은 표를 쓴다** | 미니게임 보상도 같은 멱등성 구조가 필요하다 (STEP 11) |
| 운영 로그 · 문제 추적 | 선택. "조각이 사라졌다" 는 문의가 오면 이게 없으면 답할 수 없다 |
| 통계 | 선택 |

단점(구조 증가 · 저장량)은 실질적으로 무시할 수준입니다.
봉헌 1건당 한 행이고, 이 게임 규모에서 하루 수천 행을 넘지 않습니다.

⚠ 오래된 행을 지우고 싶다면, **`request_id` 를 지우는 순간 멱등성이 깨집니다.**
지운다면 "24시간 지난 것만" 처럼 재시도 윈도우보다 긴 기준으로 하세요.

---

## 18. 구현 순서

각 STEP 은 **혼자 끝낼 수 있고, 끝난 것을 눈으로 확인할 수 있는** 단위로 잘랐습니다.

> ⚠ 전제: `CLAUDE.local.md` §3 — 씬·프리팹·Fusion 동기화·IoT 입력은 자동 테스트가 불가능합니다.
> 검증 기준은 **Unity 정상 실행 + Console 에 새 Error 없음** 입니다.
> Fusion 이 얽힌 STEP 은 **서버 빌드 1개 + 클라이언트 2개**를 띄워야 확인됩니다
> (`GAME_STRUCTURE.md` 147행 "플레이 모드 테스트는 Dedicated Server 에 붙습니다").

---

### STEP 0. 결정 사항 확정 (코드 없음)

**목표** — 21장의 결정 #0~#8 을 확인한다.

```text
#0  키 = Key.E                          #1  갈래 1, 클리어 1회 = 조각 1개
#2  1000 달성 후 추가 봉헌 금지           #3  회복도 100% 고정
#4  회복도 = 봉헌량 기준                  #5  DB 영구 저장
#6  인벤토리는 users 에 매단다
```

**완료 — 2026-09-20 기준 결정 #0~#8 이 모두 확정되었습니다. STEP 1 로 바로 갑니다.**

---

### STEP 1. 서버 — 스키마와 마이그레이션

> 실제 구현 결과, Migration 검증, Pomelo `char(36)` 매핑 이슈는
> `lobby_altar_inventory_system_implementation_log.md` 의 STEP 1 참고.

**목표** 테이블 **4개**가 MySQL 에 생긴다.
**확인할 파일** `Data/AraAttiDbContext.cs`, `Migrations/20260908075724_InitialCreate.cs`, `server/README.md` 3장
**수정할 파일** `Data/AraAttiDbContext.cs`
**신규 파일** (4개)

```text
Entities/PlayerInventoryItem.cs
Entities/AltarState.cs
Entities/AltarContribution.cs
Entities/RewardClaim.cs          ← STEP 11 의 보상 멱등성이 쓴다. 빠뜨리기 쉽다
```

**구현 내용**

```text
player_inventories
  id          bigint unsigned  PK
  user_id     bigint unsigned  FK → users.id  ON DELETE CASCADE     ← 결정 #6
  item_id     varchar(64)
  quantity    int unsigned     기본 0
  updated_at  datetime(6)
  UNIQUE uk_inventory_user_item (user_id, item_id)

altar_state
  id               int  PK  (행 하나. 값은 1)
  total_offered    bigint unsigned  기본 0
  target_offering  int unsigned     기본 1000
  activated_at     datetime(6)  NULL     ← 감사·로그용. 이번 구현은 읽지 않는다 (11.4.16)
  updated_at       datetime(6)
  CHECK (target_offering > 0)
  CHECK (total_offered >= 0)
  CHECK (total_offered <= target_offering)     ← 불변식 (11.1). 11.2 의 주의사항 참고

altar_contributions
  id          bigint unsigned  PK
  request_id  char(36)
  user_id     bigint unsigned  FK → users.id                        ← 결정 #6
  amount      int unsigned
  created_at  datetime(6)
  UNIQUE uk_contributions_user_request (user_id, request_id)        ← 10.4절
  INDEX  idx_contributions_user (user_id)

reward_claims                                                        ← STEP 11 용
  id          bigint unsigned  PK
  user_id     bigint unsigned  FK → users.id  ON DELETE CASCADE
  game_id     varchar(32)      "sword" / "mining" / "ship"
  match_key   varchar(128)     서버가 정한 판 고유값 (STEP 11)
  claimed_at  datetime(6)
  UNIQUE uk_claims_user_match (user_id, match_key)                  ← ⚠ 아래 경고
  INDEX  idx_claims_user_time (user_id, claimed_at)                 ← 60초 쿨다운 (11.4.15)
```

⚠ **`match_key` 단독 UNIQUE 로 만들면 안 됩니다. 이건 실제 버그가 됩니다.**

```text
4인 파티가 한 판을 클리어한다
matchKey 는 서버의 [Networked] 판 식별자에서 나오므로 네 명이 전부 같다 (STEP 11)
→ match_key 단독 UNIQUE 면 가장 먼저 도착한 한 명만 조각을 받고
  나머지 세 명은 duplicate 로 거절된다
```

지급은 **플레이어별**이므로 키도 `(user_id, match_key)` 여야 합니다.

⚠ `claimed_at` 인덱스가 없으면 STEP 11 의 60초 쿨다운을 계산할 방법이 없습니다
(`SELECT MAX(claimed_at) WHERE user_id = ?` — 11.4.15).
"이력은 선택" 이라고 적은 17.1절과 달리, **이 테이블은 기능의 일부입니다.**

⚠ `altar_state` 의 초기 행(id=1)은 **마이그레이션에서 `InsertData` 로 넣습니다.**
런타임에 "없으면 만든다" 로 하면 두 요청이 동시에 만들려다 부딪힙니다.

**완료 조건**

```text
[ ] dotnet ef migrations add AddInventoryAndAltar 로 마이그레이션 생성
[ ] dotnet ef database update --project AraAtti.Api 성공
[ ] SHOW TABLES 에 신규 4개 확인
      player_inventories · altar_state · altar_contributions · reward_claims
[ ] UNIQUE 제약 확인
      uk_inventory_user_item        (user_id, item_id)      ← UPSERT 가 이것을 쓴다 (11.4.12)
      uk_contributions_user_request (user_id, request_id)   ← 봉헌 멱등성 (10.4)
      uk_claims_user_match          (user_id, match_key)    ← 보상 멱등성 (11.4.7)
[ ] INDEX 확인
      idx_contributions_user        (user_id)
      idx_claims_user_time          (user_id, claimed_at)   ← 60초 쿨다운
[ ] CHECK 확인
      ck_altar_target_positive      target_offering > 0
      ck_altar_total_nonneg         total_offered >= 0
      ck_altar_total_le_target      total_offered <= target_offering   (11.2 의 주의 참고)
[ ] altar_state 에 id=1 행이 InsertData 로 들어가 있다 (target_offering = 1000)
```
**테스트** MySQL 에 직접 붙어 `SELECT * FROM altar_state;` → 1행, `target_offering = 1000`.

---

### STEP 2. 서버 — 조회 엔드포인트 2개

**목표** 로그인한 사용자가 자기 인벤토리와 제단 상태를 볼 수 있다.
**확인할 파일** `Endpoints/CharacterEndpoints.cs` (패턴), `Auth/ClaimsPrincipalExtensions.cs`
**수정할 파일** `Program.cs`
**신규 파일** `Contracts/InventoryContracts.cs`, `Contracts/AltarContracts.cs`, `Endpoints/InventoryEndpoints.cs`, `Endpoints/AltarEndpoints.cs`

**구현 내용** `GET /api/inventory`, `GET /api/altar/state`. 둘 다 `.RequireAuthorization()`.
`recoveryPercent` 와 `altarActivated` 를 **서버가 계산**해서 넣는다.

**완료 조건** Swagger 에서 토큰 없이 401, 토큰으로 200.
**테스트** `server/README.md` 6장의 PowerShell 절차를 그대로 따라 한다.
인벤토리가 비어 있으면 `items: []` 가 나와야 한다 (404 아님).

---

### STEP 3. 서버 — 봉헌 엔드포인트 (이 작업의 심장)

**목표** 동시 요청·중복 요청에도 수량이 정확하다.
**확인할 파일** STEP 1~2 의 결과물
**수정할 파일** 없음 (STEP 2 에서 만든 `AltarEndpoints.cs` 에 추가)

**구현 내용** `POST /api/altar/offer`. 8.6절 검증 9단계 + 10.3 트랜잭션 + 10.4 멱등성.

**완료 조건**
- 보유 100 → 30 봉헌 → 보유 70, 전체 +30
- 보유 20 → 30 봉헌 → `409 NOT_ENOUGH_FRAGMENTS`, **수량 변화 없음**
- 같은 `(user_id, requestId)` 두 번 → 한 번만 반영, 두 번째는 `duplicate: true`
- **`requestedAmount > remainingToTarget` 인 요청은 인벤토리를 전혀 차감하지 않고
  `409 OFFERING_AMOUNT_CHANGED` 로 실패한다** — 부분 수락 없음
- **남은 칸이 0 이면 `409 OFFERING_CLOSED`**
- **동시 요청을 아무리 보내도 `total_offered` 가 `target_offering` 을 넘지 않는다**
  → `SELECT * FROM altar_state WHERE total_offered > target_offering;` 이 언제나 0행
- 실패 응답에도 `totalOffered` · `remainingToTarget` · `maxOfferAmount` 가 실려 온다

**테스트 (중요)** — 동시성은 반드시 **실제로 부딪혀 봐야** 합니다. 두 번 돌립니다.

```powershell
# ① 합계 정확성 — 남은 칸을 넉넉히 두고 (예: 전체 500 / 1000, 보유 50)
1..20 | ForEach-Object -Parallel {
  Invoke-RestMethod -Method Post -Uri "http://localhost:5080/api/altar/offer" `
    -Headers @{ Authorization = "Bearer $using:token" } `
    -ContentType "application/json" `
    -Body (@{ amount = 1; requestId = [guid]::NewGuid().ToString() } | ConvertTo-Json)
} -ThrottleLimit 20

# total_offered 가 정확히 +20 이어야 한다. 하나라도 사라지면 10.3.1 이 틀린 것이다
```

```powershell
# ② 불변식 — 남은 칸보다 요청이 많을 때 (테스트 G)
#    ⚠ 계정 20개로 돌린다. 한 계정으로 돌리면 보유량에서 먼저 걸려 칸 경쟁을 못 본다
#    사전: 전체 990 / 1000, 계정 20개에 각각 보유 5, 각자 5개씩 요청

# 기대: 성공한 요청들의 amount 합 == 10, total_offered == 1000
#       실패한 계정은 보유 5 그대로
SELECT * FROM altar_state WHERE total_offered > target_offering;   -- 반드시 0행
```

---

### STEP 4. Unity — 서비스 계층 (UI 없음)

**목표** C# 에서 서버 값을 읽고 쓸 수 있다. 화면은 아직 없다.
**확인할 파일** `Account/HttpCharacterService.cs`, `Account/HttpJson.cs`, `Account/AccountServiceLocator.cs`
**수정할 파일** `Account/HttpApiConfig.cs` (+3줄), `Account/AccountServiceBootstrap.cs` (+4줄)
**신규 파일** 17장 Unity 목록의 서비스 9개

**완료 조건** 임시 디버그 버튼(또는 `[ContextMenu]`)으로 조회·봉헌이 되고
Console 에 서버 값이 찍힌다. **컴파일 에러 0, 새 Console Error 0.**
**테스트** `AccountServiceBootstrap.Active` 를 `Fake` 로 바꿔도 컴파일되고 돌아가는지 확인.

---

### STEP 5. Unity — 제단 상호작용 + 안내 HUD

**목표** 제단에 다가가면 안내가 뜨고, 키를 누르면 (빈) 패널이 열린다.
**확인할 파일** `Lobby/MiniGamePortal.cs`, `Network/LocalPlayer.cs`, `UI/LobbyChatInstaller.cs`
**신규 파일** `Lobby/AltarInteraction.cs`, `Lobby/AltarOfferingInstaller.cs`
**⚠ 사용자 작업** `P_HeartAltar` 프리팹에 `AltarInteraction` 자식 추가 + 위치 조정

**⚠ 이 STEP 에서 `ALTAR_INTERACT_KEY` 를 처음 씁니다. 값은 `Key.E` 입니다** (2.6절).

**완료 조건**
- 계단 아래에서는 안내가 안 뜬다 (높이 무시 + 위치가 `Pedestal` 근처여야 함)
- 계단 위 4m 안에서 안내가 뜬다
- **클라이언트 2개를 띄웠을 때, 남이 제단에 가도 내 화면에 안 뜬다** ← `LocalPlayer` 판별 확인
- Dedicated Server 콘솔에 UI 관련 로그·에러가 없다

**테스트** 서버 1 + 클라이언트 2 를 띄우고 한 명만 제단에 접근.

---

### STEP 6. Unity — 봉헌 UI

**목표** 수량을 고르고 봉헌할 수 있다.
**⚠ 선행 조건** 사용자가 HUD 이미지를 전달해 주셔야 합니다.
**확인할 파일** `UI/LobbyChatView.cs` (ChatFocus 연결 패턴)
**신규 파일** `Lobby/AltarOfferingUIController.cs`, `Assets/Game/Resources/AltarOfferingUI.prefab`

**완료 조건**
- `maxOfferAmount = min(playerOwnedAmount, remainingToTarget)` 를 넘는 선택이 **불가능하다**
  - 보유 5 · 남은 칸 1 → `MAX` 가 1, `[+]` 가 1 에서 멈춘다
  - 안내 "섬 회복까지 1개만 더 필요합니다."
- `maxOfferAmount == 0` 이면 봉헌 버튼이 잠긴다 (보유량 0 이거나 회복 완료)
- 실패 응답을 받으면 보유량 · 전체 · `maxOfferAmount` 를 응답값으로 덮어쓰고,
  선택 수량이 새 최대치보다 크면 그 값으로 낮춘다 (5 → 2)
- 실패 후 **자동으로 다시 봉헌하지 않는다**
- 봉헌 중 버튼이 잠긴다
- **UI 를 열면 못 걷고, 닫으면 다시 걷는다** ← `ChatFocus` 확인
- Esc 로 닫힌다
- **UI 를 열어 둔 채 씬을 바꿔도 못 움직이는 상태가 되지 않는다** ← `OnDisable` 의 `ChatFocus.End(this)`
- **제단 UI 를 연 채 채팅을 켰다 끄면 여전히 못 움직인다** ← 6.4.1 이 제대로 들어갔는지 보는 시험
- **제단 UI 를 연 채 Esc 를 누르면 닫힌다** ← `HeldByOther(this)` 를 썼는지 보는 시험

---

### STEP 7. Unity — 봉헌 실행 + 결과 반영

**목표** 봉헌하면 내 화면의 보유량·전체량이 서버 값으로 갱신된다.
**완료 조건** 7장 UX 흐름 ⑤ 까지가 그대로 동작. **로컬 가감산 코드가 한 줄도 없다.**
**테스트** 봉헌 후 게임을 껐다 켜도 보유량이 유지된다 (DB 영속 확인).

---

### STEP 8. Unity — 다른 플레이어에게 전파

**목표** A 가 봉헌하면 B 의 화면도 바뀐다.
**확인할 파일** `Network/LobbyChatRelay.cs` (RPC 패턴 + 레이트 리밋)
**신규 파일** `Lobby/AltarOfferingRelay.cs`
**⚠ 사용자 작업** 플레이어 프리팹에 `AltarOfferingRelay` 추가 (`LobbyChatRelay` 옆)

**완료 조건** 클라이언트 2개 중 A 가 봉헌 → B 의 전체 봉헌량이 1~2초 안에 갱신.
**테스트** A 가 봉헌 버튼을 10번 연타 → B 가 API 를 10번 때리지 않는다 (레이트 리밋 확인).

---

### STEP 9. Unity — 섬 회복도 HUD

**목표** 화면 상단에 회복도가 뜨고 봉헌에 반응한다.
**신규 파일** `Lobby/IslandRecoveryView.cs`, `Lobby/IslandRecoveryInstaller.cs`, HUD 프리팹
**완료 조건** 로비 진입 즉시 현재 값이 뜬다. 채팅·튜토리얼과 겹치지 않는다.

---

### STEP 10. Unity — 1000 달성 VFX

**목표** `totalOffered == targetOffering` 이 되면 달성 연출이 켜진다. 늦게 들어와도 켜져 있다.
**확인할 파일** `P_HeartAltar.prefab` 의 `AltarBeam` / `Light_Monolith`
**신규 파일** `Lobby/AltarVfxController.cs`
**⚠ 사용자 작업** 프리팹에 컴포넌트 추가 + 슬롯 연결

**완료 조건**
- `target_offering` 을 DB 에서 5 로 낮추고 5개 봉헌 → 5 / 5 에서 켜진다
- **`total_offered` 가 `target_offering` 을 넘는 상태를 손으로 만들어도 켜져 있다**
  ← 10.7 의 방어적 `>=` 확인. `target_offering` 을 낮췄을 때 실제로 생기는 상태다
- 켜진 뒤 새 클라이언트 접속 → **처음부터 켜져 있다** ← Late Join
- 서버를 껐다 켜도 켜져 있다 ← DB 영속
- 켜진 뒤 제단에 다가가면 봉헌 안내가 뜨지 않거나, UI 를 열어도 봉헌 버튼이 잠겨 있다

---

### STEP 10.5. 세 미니게임 공통 `MatchInstanceId` 배선

> **STEP 11 의 선행 작업입니다.** 이것이 없으면 `matchKey` 를 만들 수 없습니다.
> 규격은 STEP 11 의 "matchKey 의 원천" 절에서 확정했습니다.

**목표** 광산 · 배 · 무쌍 세 게임이 같은 규격의 서버 권위 판 식별자를 갖는다.

**확인할 파일**

```text
MiniGames/Mine/Net/MineMatchState.cs              BoardSeed · ResultTick 이 있는 곳
MiniGames/Warriors/Net/WarriorsMatchState.cs      RunSeed · ResultTick
MiniGames/ShipCoop/Net/ShipCoopStateSync.cs       ResultTick (판 식별자는 없다)
Network/NetworkPlayerAppearance.cs:54             NetworkString<_32> 사용 본보기
MiniGames/Common/MiniGameResult.cs                판 식별자를 담을 자리가 없다
MiniGames/Common/MiniGameResultGateway.cs         결과가 지나가는 길
```

**수정할 파일** 위 세 `MatchState` + `MiniGameResult` (전달 경로)

**구현 내용**

```csharp
// 세 게임에 같은 모양으로 넣는다
[Networked] public NetworkString<_32> MatchInstanceId { get; private set; }

// 판 시작 시 StateAuthority 가 한 번만
if (HasStateAuthority && string.IsNullOrEmpty(MatchInstanceId.Value))
    MatchInstanceId = System.Guid.NewGuid().ToString("N");   // 하이픈 없는 32자
```

각 게임이 판을 되돌릴 때(`MineMatchState.cs:867` 의 `BoardSeed = 0` 같은 자리)
`MatchInstanceId` 도 함께 비워, **다음 판에서 새 값이 만들어지게** 합니다.

결과를 넘길 때 값을 함께 싣습니다.

```text
MineMatchState.cs:984  ·  WarriorsMatchState.cs:849  ·  ShipCoopStateSync.cs:635
   MiniGameResultGateway.SubmitAuthoritative(new MiniGameResult(... , matchInstanceId))
```

⚠ **`"N"` 포맷을 반드시 씁니다.** `NetworkString<_32>` 는 32글자이고 `ToString("D")` 는
36자라 **조용히 잘립니다** (`NetworkPlayerAppearance.cs:374` 가 같은 경고를 적어 뒀습니다).
잘리면 서로 다른 판이 같은 값이 되어 멱등성이 깨집니다.

⚠ **클라이언트가 만들지 않습니다.** `HasStateAuthority` 가드를 반드시 둡니다.

⚠ `BoardSeed` · `RunSeed` · `ResultTick` 은 **건드리지 않습니다.** 지금 하던 일을
그대로 합니다 (STEP 11 의 역할 구분 표).

**완료 조건**

```text
[ ] 세 게임 모두 [Networked] MatchInstanceId 를 갖는다
[ ] 판을 시작할 때마다 값이 바뀐다
[ ] 같은 판에서는 모든 참가자가 같은 값을 본다 (클라이언트 2개로 확인)
[ ] 판이 끝나고 다시 시작하면 새 값이 된다
[ ] 결과 데이터(MiniGameResult)까지 값이 전달된다
[ ] Fusion 프리팹을 고쳤다면 리베이크가 필요할 수 있다
```

**테스트** 클라이언트 2개를 띄우고 한 판을 돌린 뒤 로그로 값을 비교합니다.
두 화면에 같은 값이 찍혀야 하고, 다시 한 판을 돌리면 달라져야 합니다.

---

### STEP 11. 미니게임 클리어 → 조각 1개 (결정 #1 · #8)

**목표** 섬 회복 진행 중 미니게임을 성공하고 서버가 보상을 승인하면 DB 인벤토리가 +1 된다.

```text
· 같은 미니게임을 이미 클리어했더라도 다시 성공하면 +1 가능
· 단, 섬 회복 완료 이후에는 granted:false (결정 #8)
· 실제 +1 여부는 서버의 clear-reward 응답이 최종 권위
```
**수정할 파일** `MiniGames/Common/MatchFlowController.cs`, `MiniGames/Common/UI/ResultPanelPresenter.cs`
**신규 서버 작업** `POST /api/inventory/clear-reward`

#### 먼저 — 조사해 보니 판정은 이미 서버가 하고 있습니다

초안에서 저는 "미니게임을 깼다는 걸 클라이언트가 말한다" 고 적었습니다. **절반만 맞았습니다.**
실제 코드를 따라가 보면 세 미니게임 모두 **Fusion 미니게임 서버가 승패를 판정**하고,
그 결과를 `[Networked]` 로 복제합니다.

```text
MineMatchState.cs:151     [Networked] public NetworkBool ResultSuccess { get; private set; }
MineMatchState.cs:154     [Networked] public int        ResultScore   { get; private set; }
MineMatchState.cs:913     ResultTick = Runner.Tick        ← StateAuthority 만 쓴다
MineMatchState.cs:939-951 각 클라이언트가 ResultTick 변화를 감지해 결과 화면을 띄운다
MineMatchState.cs:984     MiniGameResultGateway.SubmitAuthoritative(...)

WarriorsMatchState.cs:159, 209, 849   같은 구조 (Phase == Cleared, Score)
ShipCoopStateSync.cs:635              같은 구조
```

**즉 클라이언트는 승패를 정하지 못합니다.** 복제된 값을 읽을 뿐입니다.
게이트웨이 이름도 `SubmitAuthoritative` 이고, 주석이 "서버가 검증·보상 판정까지 끝낸
확정 결과" 라고 적고 있습니다 (`MiniGameResultGateway.cs:40`).

#### 그런데도 위조 가능한 지점이 하나 남습니다

```text
판정   Fusion 미니게임 서버   ✅ 클라이언트가 못 바꾼다
전달   클라이언트 → REST API  ⚠ 여기가 뚫린다
```

Fusion 미니게임 서버도 **플레이어의 DB 신원을 모릅니다** (로비와 같은 제약, 1.2절).
그래서 서버가 직접 MySQL 에 쓸 수 없고, 클라이언트가 날라야 합니다.
치터는 게임을 조작할 필요 없이 `POST /api/inventory/clear-reward` 를 반복 호출하면 됩니다.

#### 완화책 (완전한 해결이 아님을 밝히고 씁니다)

```text
1. 한 판당 한 번 — 멱등성
   reward_claims 테이블에 UNIQUE (user_id, match_key) 를 건다.   ← STEP 1 에서 만든다
   ⚠ match_key 단독 UNIQUE 로 걸면 4인 파티에서 한 명만 받는다. 네 명이 같은 키를 쓴다.
   matchKey 의 원천은 아래 "matchKey 의 원천" 절에서 정한다.
   ⚠ 클라이언트가 읽을 수 있는 값이라 위조를 막지는 못한다.
     막는 것은 "같은 판 결과가 두 번 들어오는 것" 이다. 그게 실제로 더 흔한 사고다.

2. 60초 쿨다운                                              ← 11.4.15 에서 확정
   한 판은 아무리 빨라도 몇 분이다. 같은 계정에 60초 이내 재지급을 거절한다.
   치터의 초당 수백 회를 분당 1회로 낮춘다.
   기준은 reward_claims.claimed_at 의 최댓값이다 (실제로 지급한 판만 행이 있다).

   ⚠ 일일 지급 상한은 **쓰지 않는다.** 이유는 11.4.15 에 있다.
```

둘을 넣으면 **"게임을 안 하고 조각을 무한히 얻는" 것은 막지 못하지만
"의미 있는 속도로 얻는" 것은 막습니다.** 팀 프로젝트 시연 범위에서는 충분합니다.

> **근본 해결은 Fusion 미니게임 서버에 신원을 주는 것**이고, 그건 8.3절 B안과 같은 작업입니다.
> 로비 봉헌과 미니게임 보상이 **같은 인증 파이프라인 하나**로 동시에 해결됩니다.
> 나중에 그 작업을 하게 되면 이 STEP 의 완화책은 그대로 두고 호출 주체만 바뀝니다.

#### ⚠ 선행 조건 — STEP 10.5 가 먼저 끝나 있어야 합니다

```text
Mine · ShipCoop · Warriors 세 게임에
동일 규격의 서버 권위 MatchInstanceId 가 존재해야 한다.
없으면 matchKey 를 만들 수 없고, 멱등성도 성립하지 않는다.
```

배선은 **STEP 10.5** 에서 합니다 (바로 앞 STEP). 규격은 아래 절에서 확정합니다.

#### matchKey 의 원천 — 실제 저장소 조사 결과 (2026-09-20)

`matchKey` 는 **한 판을 가리키는 서버 권위 값**이어야 합니다. 다섯 조건을 모두 만족해야 합니다.

```text
① 매 판마다 값이 달라진다
② 서버(StateAuthority)가 생성하거나 확정한다
③ 모든 참가자가 같은 값을 본다
④ 클라이언트가 임의로 바꿀 수 없다
⑤ 같은 판 안에서는 재요청해도 값이 유지된다
```

세 미니게임을 직접 조사했습니다.

| 게임 | 후보 | 파일 · 위치 | 생성 시점 | 동기화 |
|---|---|---|---|---|
| 광산 | `BoardSeed` | `Mine/Net/MineMatchState.cs:132` (`[Networked] public int`) | 판 시작 `EnsureBoardOpen` (`:586-590`) | Fusion `[Networked]` |
| 무쌍 | `RunSeed` | `Warriors/Net/WarriorsMatchState.cs:206` (`[Networked] public int`) | 판 시작 (`:1112`) | Fusion `[Networked]` |
| **배** | **없음** | — | — | — |
| 세 게임 공통 | `ResultTick` | `MineMatchState.cs:177` · `WarriorsMatchState.cs:268` · `ShipCoopStateSync.cs:70` (`[Networked] private int`) | 결과 확정 순간 | Fusion `[Networked]` |

##### 배(ShipCoop) — 전용 판 식별자가 **없습니다**

다음을 전부 검색했습니다 — `MatchId` · `RunId` · `RoundId` · `Seed` · `SessionId` ·
`MatchInstanceId` · `[Networked]` 전수 조사.

```text
ShipCoopStateSync 의 [Networked] 목록
  Crew · Countdown · Sailed · ResultTick · ResultClear · ResultScore · ResultPlayTime
  Phase · Elapsed · PhaseIndex · Score · Hp · Invincible · Progress01 · Flood01
  Leaks · SailPower01
→ 판을 구분하는 고유값이 하나도 없다
```

`VoyageEvent.Seed`(`Events/VoyageEvent.cs:201`)가 있지만 **판이 아니라 개별 항해 이벤트
단위**이고, `UnityEngine.Random`(`:346`)으로 만들며 `[Networked]` 도 아닙니다
(`ShipCoopEventSync` 가 이벤트 슬롯별로 따로 나릅니다). **판 식별자로 쓸 수 없습니다.**

##### 조사 중 발견했지만 **채택하지 않은** 것 — `ResultTick`

**세 게임 모두** 결과 확정 순간에 서버가 찍는 `[Networked] int ResultTick` 을 이미
갖고 있습니다. 한때 임시 대안으로 검토했으나 **최종안으로 쓰지 않습니다** (아래 약점).

```csharp
// ShipCoop/Net/ShipCoopStateSync.cs:651-667  (WriteResultWhenFinished)
if (game == null || ResultTick != 0) return;   // 한 판에 한 번만 찍는다
...
ResultTick = Runner.Tick;

// Mine/Net/MineMatchState.cs:913        BoardSeed 와 별개로 존재
// Warriors/Net/WarriorsMatchState.cs:791
```

다섯 조건에 대입하면 **①에서 걸립니다.**

```text
① 매 판마다 다른가     같은 세션 안에서는 예 (Tick 이 증가한다)
                       ⚠ 세션이 재시작하면 Tick 이 0 부터 다시 센다  ← 탈락 사유
② 서버가 확정하는가     예. StateAuthority 경로에서만 대입한다
③ 모두 같은 값인가      예. [Networked] 다
④ 클라이언트가 못 바꾸나 예. private set + [Networked]
⑤ 판 안에서 유지되는가  예. ResultTick != 0 가드가 덮어쓰기를 막는다
```

그리고 `ResultTick` 은 **결과 확정 순간**에 찍힙니다. 판 식별자는 **판 시작**에 정해져야
결과 화면·재시도까지 같은 값을 쓸 수 있습니다. 시점도 맞지 않습니다.

##### ⚠ `Runner.Tick` 기반 값의 약점 — 세션 재시작 시 반복됩니다

`BoardSeed`(광산)와 `ResultTick`(세 게임)은 모두 `Runner.Tick` 에서 나옵니다.
**Tick 은 세션이 시작될 때 0 부터 다시 셉니다.**

```text
미니게임 세션 이름은 지금 고정이다
  MiniGamePortal.cs:140-142 — "세션은 넘기지 않는다. 비워 두면 미니게임의 기본 세션으로 간다.
                               진짜 매칭이 붙기 전까지는 고정 세션"

그래서 matchKey = {세션이름}:{gameId}:{Tick} 는
서버를 껐다 켠 뒤 같은 Tick 에서 결과가 나오면 예전 판과 같은 값이 된다
  → UNIQUE (user_id, match_key) 가 정상 보상을 중복으로 보고 거절한다
  → 플레이어는 이유 없이 조각을 못 받는다
```

무쌍의 `RunSeed` 만 예외입니다 — `System.Environment.TickCount ^ (Runner.Tick * 397)`
(`WarriorsMatchState.cs:1112`)이라 서버 가동 시각이 섞여 재시작 후에도 잘 안 겹칩니다.

##### 결론 — 세 게임 공통 `MatchInstanceId` 를 신규 설계합니다 (확정)

```text
배(ShipCoop)   전용 판 식별자 없음        조사 결과. 추측이 아니다
광산 · 무쌍     값은 있지만 용도가 다르다   아래 역할 구분 표
```

**`Runner.Tick` · `Environment.TickCount` 기반 값은 최종안으로 쓰지 않습니다.**
서버 재시작 이후 과거 판과 값이 겹칠 가능성을 **완전히 없애지 못하기 때문**입니다.
`matchKey` 가 겹치면 `UNIQUE (user_id, match_key)` 가 정상 보상을 중복으로 보고
거절하고, 플레이어는 이유 없이 조각을 못 받습니다.

그래서 **세 게임에 같은 규격의 판 식별자를 새로 둡니다.**

```text
이름     MatchInstanceId
값       GUID 문자열 32자   Guid.NewGuid().ToString("N")
타입     [Networked] public NetworkString<_32> MatchInstanceId { get; private set; }
생성     판 시작 시 StateAuthority 가 한 번만
```

⚠ **`NetworkString<_32>` 는 이 프로젝트에서 이미 쓰고 있는 타입입니다.** 추측이 아닙니다.

```text
Assets/Game/Scripts/Network/NetworkPlayerAppearance.cs:54
    private NetworkArray<NetworkString<_32>> PartKeys { get; }
Assets/Game/Scripts/Network/NetworkPlayerIdentity.cs:36
    public NetworkString<_16> Nickname { get; private set; }
    (:32 주석 — "NetworkString<_16> 은 16 글자까지 담는다")
```

`_32` = 32글자, `ToString("N")` 의 GUID = 하이픈 없는 32자. **정확히 들어맞습니다.**

⚠ **여유가 0 입니다.** `NetworkString<_32>` 는 넘치면 **조용히 자릅니다**
(`NetworkPlayerAppearance.cs:374` 이 같은 경고를 적어 두었습니다).
`ToString("D")`(36자, 하이픈 포함)로 바꾸면 **뒤 4자가 잘려 서로 다른 판이 같은 값이
됩니다.** 반드시 `"N"` 을 씁니다. 포맷을 바꾸려면 타입도 같이 키워야 합니다.

##### 수명주기 — 판 시작에 한 번, 그 뒤로는 고정

```text
판 시작            StateAuthority 가 MatchInstanceId 를 생성한다
                   Guid.NewGuid().ToString("N")
      ↓
플레이 중          같은 값을 유지한다. 모든 참가자가 [Networked] 로 같은 값을 본다
      ↓
결과 화면          같은 MatchInstanceId 를 쓴다
      ↓
clear-reward 최초  같은 MatchInstanceId 로 만든 matchKey 를 보낸다
      ↓
응답 유실 후 재시도  동일 matchKey 를 그대로 재사용한다 (11.4.8)
      ↓
다음 판 시작       새 MatchInstanceId 를 생성한다
```

**금지 사항**

```text
✗ 결과 화면이 뜬 뒤에 새 ID 를 만든다
✗ clear-reward 를 부를 때마다 새 GUID 를 만든다
✗ 클라이언트가 로컬에서 GUID 를 만든다
```

**판이 시작되는 순간 서버 권위로 한 번만** 만듭니다. 그 뒤에 만드는 모든 경로는
멱등성을 깨뜨립니다 — 재시도가 새 판으로 보여 보상이 두 번 나갑니다.

##### 기존 값들과의 역할 구분 — 섞어 쓰지 않습니다

조사 기록은 지우지 않습니다. 다만 **용도가 다릅니다.**

| 값 | 역할 | `matchKey` 에 쓰는가 |
|---|---|---|
| `BoardSeed` (`MineMatchState.cs:132`) | 광산 게임판을 까는 seed | **아니오** |
| `RunSeed` (`WarriorsMatchState.cs:206`) | 무쌍 내부 run seed | **아니오** |
| `ResultTick` (세 게임) | 결과가 확정된 Fusion Tick. 연출 시각의 기준 | **아니오** |
| **`MatchInstanceId`** | **`clear-reward` 판 멱등성 전용 고유 판 ID** | **예. 이것만** |

```text
ResultTick  ≠  MatchInstanceId
BoardSeed   ≠  MatchInstanceId
RunSeed     ≠  MatchInstanceId
```

세 값은 **게임 내부 로직에서 지금 하던 일을 그대로 합니다.** 건드리지 않습니다.
다만 `matchKey` 의 원천으로는 쓰지 않습니다.

##### `matchKey` 구성

```text
matchKey = {sessionName}:{gameId}:{matchInstanceId}
예         "mine-1:mining:9f2c1b4e6a404b3f9a1d9c2f6f3e77aa"
           (세션 이름은 MineNet.cs:34 의 DefaultSession = "mine-1")
```

`gameId` 는 `MiniGameConfig.fragmentId` 의 값을 그대로 씁니다 —
`"sword"` · `"mining"` · `"ship"` (4.2.3절).

`sessionName` 은 지금 고정값이라 고유성에 기여하지 않습니다
(`MiniGamePortal.cs:140-142`). **고유성은 `matchInstanceId` 가 전부 책임집니다.**
그래도 넣어 두는 이유는 운영 로그에서 어느 세션의 판인지 읽히게 하기 위해서입니다.

##### 결과 데이터까지 전달하는 경로

```text
MatchState                 [Networked] MatchInstanceId
      ↓
서버 판정 결과              ResultSuccess · ResultScore …
      ↓
MiniGameResult             ⚠ 지금 이 값을 담을 자리가 없다
      ↓
결과 화면 / 보상 요청
      ↓
clear-reward 의 matchKey
```

⚠ **`MiniGameResult` 에 판 식별자를 담을 필드가 없습니다.**

```text
MiniGames/Common/MiniGameResult.cs 의 현재 필드
  GameId · IsClear · Score · PlayTime · ExtraStatLabel · ExtraStatValue
  RewardId · FragmentObtained · PlayerCount
```

**`MatchInstanceId`(또는 완성된 `matchKey`)를 결과 데이터에 추가해야 합니다.**
세 `MatchState` 가 `MiniGameResultGateway.SubmitAuthoritative(...)` 를 부를 때
그 값을 함께 실어 보내는 것이 자연스럽습니다
(`MineMatchState.cs:984` · `WarriorsMatchState.cs:849` · `ShipCoopStateSync.cs:635`).

⚠ **이번 단계(설계)에서는 코드를 만들지 않습니다.** 배선은 STEP 10.5 에서 합니다.

#### 구현 내용 — 서버

`POST /api/inventory/clear-reward` 는 11.4.3 의 순서를 그대로 따릅니다.

```text
1. 요청 수신 (gameId, matchKey). ⚠ 요청은 수량을 담지 않는다.

2. 같은 (user_id, matchKey) 의 reward_claim 조회          ← 트랜잭션 밖
     있으면 → granted:true, duplicate:true 로 끝낸다
              제단 상태를 보지 않는다 (11.4.4)
              altar_state 를 잠그지 않는다 (11.4.14)
     없으면 → 트랜잭션 시작

3. altar_state 를 잠금 읽기로 확인          SELECT ... WHERE id=1 FOR UPDATE
     완료됐으면 → player_inventories 변경 없음
                  reward_claims 기록 없음
                  ⚠ 트랜잭션을 먼저 종료해 row lock 을 푼다 (11.4.14)
                  granted:false, code "ALTAR_COMPLETED", 200 OK
     진행 중이면 → 계속

4. 서버 보상 검증 (성공 클리어인지)

5. 60초 쿨다운 확인                          — 11.4.15
     SELECT MAX(claimed_at) FROM reward_claims WHERE user_id = ?
     60초가 안 지났으면 → 트랜잭션 종료 → 429 REWARD_COOLDOWN
     ⚠ 일일 상한은 쓰지 않는다

6. player_inventories UPSERT                — 11.4.12
     INSERT ... ON DUPLICATE KEY UPDATE quantity = quantity + 1

7. INSERT reward_claims

8. Commit → granted:true

⚠ reward_claims UNIQUE 충돌 → 현재 트랜잭션 rollback
   → 새 트랜잭션에서 최신 상태로 다시 확인 → 이미 있으면 duplicate:true (11.4.13)
⚠ deadlock → rollback → 새 트랜잭션으로 1~2회 재시도 (11.4.13)
⚠ 조기 반환(duplicate · ALTAR_COMPLETED · REWARD_COOLDOWN)은 재시도 대상이 아니다 (11.4.14)
⚠ duplicate 는 트랜잭션 밖에서 반환한다. 그 외 조기 반환은 트랜잭션을 닫고 응답한다 (11.4.14)
```

⚠ **2번을 3번보다 먼저 하는 이유는 11.4.4 에 있습니다.** 순서를 바꾸면
"DB 에는 지급됐는데 화면은 안 줬다고 하는" 상태가 만들어집니다.

⚠ **3번은 평범한 `SELECT` 로는 안 됩니다.** 스냅샷 읽기라 방금 커밋된 마지막 봉헌을
못 볼 수 있습니다. 근거와 대안은 11.4.5 에 있습니다.

#### 구현 내용 — Unity

```csharp
// MatchFlowController.CompleteMiniGame — 현재 (:173-176)
string rewardId = ...;
bool obtained = result.IsClear && RewardService.Grant(rewardId);   // ← HashSet. 평생 3개
MiniGameResult settled = result.WithReward(rewardId, obtained);

// 바뀐 뒤 — 결과 화면을 막지 않는다. 보상 줄만 나중에 채운다.
if (result.IsClear)
    InventoryServiceLocator.Current.RequestClearReward(config.GameId, matchKey);
MiniGameResult settled = result.WithReward(ItemIds.SeaHeartFragment, result.IsClear);
```

```text
1. 결과 화면을 즉시 표시한다                   ← 응답을 기다리지 않는다
2. 보상 줄 = Pending                          "보상 확인 중..."
3. clear-reward 를 호출한다
4. granted:true (duplicate 포함)  → Granted                          "바다의 심장 조각 +1"
5. granted:false + ALTAR_COMPLETED → NotGrantedBecauseAltarCompleted  보상 줄을 숨긴다
6. 429 + REWARD_COOLDOWN          → NotGrantedBecauseCooldown
                                     "이번 판의 보상이 지급되지 않았습니다."
                                     ⚠ 자동 재시도하지 않는다. 서버가 확정한 응답이다
7. 네트워크 응답 없음              → 동일 matchKey 로 1~2회 재시도
8. 그래도 확인 실패                → Unknown                          "보상을 확인할 수 없습니다."
```

`ResultPanelPresenter.ShowReward` (`:117-127`) 의 "이미 보유 중" 분기를 없애고
위 **다섯 상태**로 바꿉니다. 상태 정의와 매핑 표는 11.4.8 에 있습니다.

⚠ **`NotGrantedBecauseCooldown` 과 `Unknown` 을 같은 문구로 묶지 마세요.**
앞은 "서버가 안 줬다고 확정", 뒤는 "줬는지 모른다" 입니다.

⚠ **`AltarState` 캐시를 읽어 미리 판단하지 않습니다** (11.4.9).
⚠ **재시도할 때 `matchKey` 를 새로 만들지 않습니다** (11.4.8).
⚠ **보상이 없다고 게임 결과를 실패로 바꾸지 않습니다** (11.4.10).

**완료 조건**

```text
회복 진행 중 성공 클리어
  → player_inventories +1 (행이 없으면 새로 생기고 quantity = 1 — 11.4.12)
  → reward_claims +1건
  → 결과 화면 "바다의 심장 조각 +1"

같은 게임을 다시 클리어 (새 판, 회복 진행 중)
  → 또 +1. "이미 클리어한 게임" 이라는 이유로 거절하지 않는다
  → matchKey 가 판마다 달라야 한다

회복 완료 후 성공 클리어
  → inventory 변화 없음
  → reward_claims 변화 없음
  → granted:false, ALTAR_COMPLETED, HTTP 200
  → 결과 상태 NotGrantedBecauseAltarCompleted → 보상 줄이 숨겨진다
  → ⚠ 게임 결과는 여전히 "성공" 으로 보인다

60초 쿨다운 중 새 판 클리어
  → HTTP 429, REWARD_COOLDOWN
  → inventory · reward_claims 변화 없음
  → 결과 상태 NotGrantedBecauseCooldown
  → "이번 판의 보상이 지급되지 않았습니다."  ⚠ Unknown 문구가 나오면 실패다

서버에서 지급 성공 후 HTTP 응답 유실
  → 동일 matchKey 재시도
  → 중복 지급 없음 (quantity 가 +1 에서 멈춘다)
  → 최종적으로 granted:true 를 확인할 수 있다

999/1000 에서 clear-reward 와 마지막 봉헌이 동시에 발생
  → 둘 중 DB 에서 먼저 확정된 작업을 기준으로 결과가 결정된다
  → 어떤 순서에서도 "완료 Commit 이후 새 조각 지급" 이 발생하지 않는다
```

**테스트**

```text
STEP 11 핵심 검증
  Test 1 · 2       완료 전/후 지급
  Test 5 · 5b · 6  duplicate 재시도
  Test 8           첫 지급 UPSERT
  Test 9 · 9b      반복 클리어
  Test 13~16       60초 쿨다운            ← 11.4.15
  Test 17          Unknown ≠ Cooldown     ← 11.4.8
  Test 3 · 4 · 7   마지막 봉헌과의 동시성

STEP 10.5 가 끝나야 돌릴 수 있는 것
  Test 10 · 11 · 12   MatchInstanceId · 재시작 충돌 · 잠금 해제

전체 회귀 검증
  Test 1~17 + 변형 Test 5b · 9b
```

`target_offering` 을 낮춰 두면 완료 상태를 빨리 만들 수 있습니다.

---

### STEP 12. 멀티 클라이언트 통합 테스트

19장의 시나리오 전부를 서버 1 + 클라이언트 2~3 으로 돌립니다.

---

## 19. 테스트 시나리오

### 19.1 단일 플레이어

```text
사전: 인벤토리에 sea_heart_fragment 100개를 DB 에 직접 넣는다
      전체 봉헌량 0

30개 봉헌
→ 응답 remainingFragments = 70, totalOffered = 30
→ UI 보유량 70
→ DB player_inventories.quantity = 70
→ DB altar_state.total_offered = 30
→ 섬 회복도 3%
```

### 19.2 보유량 초과

```text
사전: 보유 20, 전체 500 / 1000   (남은 칸 500 — 병목은 보유량이다)

30개 봉헌 시도
→ 409 NOT_ENOUGH_FRAGMENTS
→ DB 보유 20 그대로, 전체 500 그대로   ← ⚠ 이게 핵심
→ UI 가 닫히지 않는다
```

⚠ UI 에서 `maxOfferAmount` 가 20 이라 30 을 보낼 수 없습니다.
**API 를 직접 호출해서 확인합니다** (PowerShell / Swagger).

### 19.3 동시 봉헌 — 칸이 넉넉할 때 (합계 정확성)

```text
전체 500 / 1000
A: 7개  ┐ 동시에
B: 10개 ┘

→ 전체 517 (정확히). 둘 다 성공한다
→ 각자 보유량이 정확히 7 / 10 만큼 줄었다
→ altar_contributions 에 행 2개
```

STEP 3 의 PowerShell 병렬 스크립트로 20건까지 부하를 올려 확인합니다.

### 19.4 최대 봉헌량 정책 — A ~ G

> 이 일곱 개가 이번 정책의 **핵심 테스트**입니다.
> `target_offering` 을 10 정도로 낮추면 999 대신 9 로 훨씬 빨리 돌릴 수 있습니다.
> 아래는 목표 1000 기준으로 적었습니다.

#### 테스트 A — UI 최대값 제한

```text
사전  전체 999 / 1000      플레이어 보유 5

기대  maxOfferAmount = min(5, 1) = 1
      [+] 를 눌러도 값이 1 에서 증가하지 않는다
      [MAX] → 1
      안내  "섬 회복까지 1개만 더 필요합니다."
```

#### 테스트 B — 정확히 목표 달성

```text
사전  전체 999 / 1000      A 보유 5

A 가 1개 봉헌
→ 전체 1000 / 1000
→ A 보유 4
→ altarActivated = true,  recoveryPercent = 100.0
→ 달성 연출 ON
```

#### 테스트 C — 마지막 한 칸을 두고 동시 요청

```text
사전  전체 999 / 1000      A 보유 5,  B 보유 5

A 가 1개, B 가 1개를 거의 동시에 요청

→ 정확히 한 명만 성공
→ 전체 1000 / 1000        ⚠ 1001 이 나오면 실패다
→ 진 쪽: 409 OFFERING_AMOUNT_CHANGED
         보유량 5 그대로 (차감 0)
         altar_contributions 에 그 사람 행 없음
→ altar_contributions 에 행 1개
```

#### 테스트 D — UI 를 연 뒤 상태가 바뀜 (이번 정책의 대표 시나리오)

```text
A 가 UI 를 연다        전체 995 / 1000  →  A 화면 maxOfferAmount = 5
A 가 5 를 고른다       (아직 누르지 않았다)
그 사이 B 가 3개 봉헌   전체 998 / 1000
A 가 [봉헌] 을 누른다   amount = 5

기대
  A 요청 전체 실패          ⚠ 2개만 받으면 실패다
  A 인벤토리 변화 없음
  altar total 998 유지
  응답  code = OFFERING_AMOUNT_CHANGED
        totalOffered = 998, targetOffering = 1000
        remainingToTarget = 2, maxOfferAmount = 2

  A 화면
    전체 진행도   995 → 998
    선택 수량      5 → 2
    최대 선택량    5 → 2
    메시지 "다른 플레이어가 먼저 봉헌했습니다. 섬 회복까지 2개 남았습니다."

  ⚠ 자동으로 다시 봉헌되지 않는다. A 가 [봉헌] 을 다시 눌러야 한다.
```

#### 테스트 E — 목표 초과 요청 (조작된 클라이언트 / 낡은 UI)

```text
사전  전체 998 / 1000      보유 10

API 를 직접 호출해 amount = 5 요청

→ 409 OFFERING_AMOUNT_CHANGED
→ 인벤토리 차감 없음 (보유 10 유지)
→ total_offered = 998 유지
```

#### 테스트 F — 회복 완료 이후 요청

```text
사전  전체 1000 / 1000      보유 10

봉헌 요청 (UI 는 버튼이 잠겨 있으므로 API 직접 호출)

→ 409 OFFERING_CLOSED
→ 메시지 "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다."
→ 인벤토리 변화 없음, total_offered = 1000 유지
```

#### 테스트 G — 불변식 (부하 테스트)

```text
사전  전체 990 / 1000
      계정 20개에 각각 보유 5

20개 계정이 동시에 5개씩 요청 (합 100, 남은 칸 10)

기대
  total_offered 가 정확히 1000 에서 멈춘다     ⚠ 1001 이상이면 실패
  성공한 요청들의 amount 합 == 10
  실패한 요청은 인벤토리가 한 개도 안 줄었다
  성공/실패 어느 쪽도 부분 차감이 없다
```

```powershell
# 확인 쿼리 — 어떤 조합에서도 이 값은 0행이어야 한다
# SELECT * FROM altar_state WHERE total_offered > target_offering;
```

⚠ **G 는 반드시 여러 계정으로 돌립니다.** 한 계정으로 20건을 보내면 보유량에서 먼저
걸려서 남은 칸 경쟁을 시험하지 못합니다.

### 19.5 Late Join

```text
이미 전체 1000 / 1000 (VFX ON)
새 클라이언트 접속
→ 로비 로드 직후 VFX 가 이미 켜져 있다
→ "꺼졌다가 1초 뒤 켜지는" 것도 실패다 (12.4 의 Apply() 즉시 호출)
→ 섬 회복도도 처음부터 100%
```

### 19.6 중복 요청

```text
같은 requestId 로 2번 전송
→ 첫 번째: 정상 처리, duplicate = false
→ 두 번째: 아무것도 안 하고 같은 값, duplicate = true
→ DB 보유량은 한 번만 깎였다
→ altar_contributions 에 행 1개
```

네트워크 끊김을 흉내내려면: 봉헌 직후 서버를 잠깐 껐다 켜고 같은 `requestId` 로 재전송.

### 19.7 UI

```text
maxOfferAmount 가 0 이면 봉헌 버튼이 잠겨 있다
봉헌 UI 를 연 채 WASD → 캐릭터가 안 움직인다
UI 를 닫고 WASD       → 움직인다
봉헌 중 버튼 연타      → 요청이 1번만 나간다 (네트워크 로그로 확인)
Esc                   → 닫힌다
채팅을 켠 채 Esc      → 채팅이 먼저 닫힌다. 제단 UI 는 안 닫힌다
UI 를 연 채 미니게임 진입 → 돌아왔을 때 움직일 수 있다  ← ⚠ ChatFocus 누수 확인
튜토리얼 도는 중 제단 접근 → 안내가 안 뜬다
```

### 19.8 멀티 채널 (12.5절)

```text
클라이언트 A 를 lobby-ch1 에, 클라이언트 B 를 lobby-ch2 에 접속시킨다
(서버 exe 를 -session lobby-ch1 / lobby-ch2 로 두 개 띄운다)

A 가 봉헌한다
→ A 화면: 즉시 갱신 (자기 응답)
→ B 화면: RPC 가 안 가므로 즉시 갱신되지 않는다.  ⚠ 이게 정상이다
→ B 화면: 30초 안에 갱신된다                     ← 폴링이 도는지 확인하는 테스트
```

### 19.9 4인 파티 보상 (STEP 11)

```text
4명이 같은 판을 클리어한다
→ 네 명 전부 quantity 가 +1 된다      ⚠ 한 명만 오르면 match_key UNIQUE 를 잘못 건 것이다
→ reward_claims 에 행이 4개 (user_id 가 서로 다르고 match_key 는 같다)

같은 판 결과를 한 명이 두 번 제출한다
→ 그 사람만 +1 에서 멈춘다 (duplicate)
```

### 19.10 입력 잠금 (6.4.1절)

```text
제단 UI 를 연다                    → WASD 로 안 움직인다
그 상태에서 채팅칸을 클릭한다        → 여전히 안 움직인다
채팅 바깥을 클릭해 채팅을 끈다       → ⚠ 여전히 안 움직여야 한다
                                     (고치기 전 코드에서는 여기서 움직인다)
제단 UI 를 닫는다                   → 움직인다

제단 UI 를 연 채 Esc               → 제단 UI 가 닫힌다
채팅칸에 포커스가 있을 때 Esc        → 채팅만 꺼지고 제단 UI 는 남는다
```

### 19.11 서버 장애

```text
API 를 끈 상태로 제단 상호작용
→ "서버에 연결할 수 없습니다" 표시
→ 마지막으로 알던 VFX 상태가 유지된다 (꺼지지 않는다)
→ Unity 가 멈추거나 예외를 뱉지 않는다
```

---

### 19.12 회복 완료와 미니게임 보상 (결정 #8)

> **Test 1~17 + 변형 Test 5b · 9b** — 전부 STEP 11 의 완료 조건입니다.
> (Test 10~12 는 STEP 10.5 가 끝나야 돌릴 수 있습니다.)
> (Test 13~16 은 60초 쿨다운 — 11.4.15)
> `target_offering` 을 낮춰 두면 완료 상태를 빨리 만들 수 있습니다.

#### Test 1 — 완료 전 보상

```text
사전  altar 999 / 1000,  A 보유 0
A 가 미니게임 성공 (그 사이 다른 봉헌 없음)

→ granted:true
→ A player_inventories +1
→ reward_claims 1건
→ 결과 화면 "바다의 심장 조각 +1"
```

#### Test 2 — 완료 후 보상 요청

```text
사전  altar 1000 / 1000,  A 보유 4
A 가 미니게임 성공

→ HTTP 200,  granted:false,  code "ALTAR_COMPLETED"
→ player_inventories 변화 없음 (보유 4 그대로)
→ reward_claims 변화 없음
→ 결과 화면에서 조각 보상 줄이 숨겨진다
→ ⚠ 게임 결과는 "성공" 으로 정상 표시된다 (11.4.10)
→ ⚠ 잔여 조각 4개는 그대로 남아 있다 (11.4.2)
```

#### Test 3 — 마지막 봉헌이 먼저 확정

```text
사전  altar 999 / 1000

B 가 1개 봉헌  →  1000 / 1000 Commit
그 직후 A 의 clear-reward 도착

→ A granted:false, ALTAR_COMPLETED
→ A inventory 변화 없음
→ ⚠ 이것이 금지 상태의 반대다. 여기서 A 가 +1 을 받으면 설계 실패다 (11.4.5)
```

#### Test 4 — 보상이 먼저 확정

```text
사전  altar 999 / 1000

A clear-reward  →  +1 Commit
그 직후 B 가 마지막 1개 봉헌

→ A 의 +1 은 유지된다 (회수하지 않는다)
→ B 봉헌으로 1000 / 1000
→ 둘 다 정상이다 (11.4.5 Case A)
```

#### Test 5 — 지급 성공 후 응답 유실

```text
A clear-reward
서버  → inventory +1 → reward_claim 저장 → Commit
HTTP 응답 유실

클라이언트 → 동일 matchKey 로 재시도

→ 중복 지급 없음. quantity 가 +1 에서 멈춘다
→ granted:true, duplicate:true
→ 결과 화면 "바다의 심장 조각 +1"
→ ⚠ 새 matchKey 를 만들면 여기서 +2 가 된다. 그것이 실패 조건이다
```

**변형 5b — 재시도 사이에 제단이 완료된 경우 (11.4.4 검증)**

```text
A 의 +1 이 Commit, 응답 유실
그 사이 B 가 마지막 봉헌으로 1000 / 1000 완료
A 가 동일 matchKey 로 재시도

→ granted:true   ⚠ ALTAR_COMPLETED 가 나오면 실패다
   (reward_claim 을 먼저 보므로 제단 상태를 보지 않는다)
→ DB 와 화면이 일치한다
```

#### Test 6 — 완료 상태 응답 유실

```text
사전  altar 1000 / 1000

A clear-reward  →  granted:false ALTAR_COMPLETED
응답 유실
동일 matchKey 로 재시도

→ 다시 granted:false, ALTAR_COMPLETED
→ DB 변화 없음 (reward_claims 에 행이 생기지 않는다)
→ 방식 A 가 성립하는지 보는 시험이다 (11.4.7)
```

#### Test 7 — 불변식 (부하)

```text
사전  altar 999 / 1000
      계정 10개가 각각 미니게임 클리어 (clear-reward 10건)
      계정 1개가 마지막 1개 봉헌 (offer 1건)
      11건을 동시에 던진다

기대
  altar 는 정확히 1000 / 1000
  봉헌 Commit 시각보다 뒤에 확정된 clear-reward 는 전부 granted:false
  봉헌 Commit 시각보다 앞에 확정된 clear-reward 는 granted:true
  ⚠ "완료 Commit 이후 새 조각 지급" 이 한 건도 없어야 한다

확인
  SELECT COUNT(*) FROM reward_claims
   WHERE claimed_at > (SELECT updated_at FROM altar_state WHERE id = 1);
  → 0 이어야 한다
```

#### Test 8 — 신규 사용자의 첫 지급 (11.4.12 UPSERT)

```text
사전  altar 500 / 1000
      A 는 player_inventories 에 sea_heart_fragment 행이 아예 없다
      (회원가입 후 미니게임을 한 번도 안 깬 계정)

A 가 미니게임 성공

→ granted:true
→ player_inventories 에 새 행이 생긴다
→ quantity = 1
→ ⚠ UPDATE 만으로 구현하면 0행이 바뀌고 quantity 가 안 생긴다. 그것이 실패 조건이다
→ ⚠ 첫 지급이 2 가 되어도 실패다. INSERT IGNORE + UPDATE 로 만들면 그렇게 된다 (11.4.12)

이어서 A 가 다시 성공 (다른 판)

→ quantity = 2
```

확인 쿼리

```sql
SELECT quantity FROM player_inventories
 WHERE user_id = @a AND item_id = 'sea_heart_fragment';
-- 첫 클리어 후 1, 두 번째 클리어 후 2
```

#### Test 9 — 같은 게임을 반복 클리어 (결정 #1)

```text
사전  altar 500 / 1000   (회복 진행 중)
      A 보유 0

A 가 광산 성공        →  +1   MatchInstanceId = GUID-A
A 가 광산 다시 성공    →  +1   MatchInstanceId = GUID-B
A 가 광산 세 번째 성공 →  +1   MatchInstanceId = GUID-C

→ A 보유 3
→ reward_claims 3건
→ ⚠ 세 GUID 가 서로 달라야 한다. 같으면 STEP 10.5 배선이 잘못된 것이다
→ ⚠ "이미 클리어한 게임" 이라는 이유로 거절되면 실패다
```

**Test 9b — 같은 게임 반복 클리어, 단 회복 완료 후**

```text
사전  altar 1000 / 1000   (회복 완료)

A 가 광산 성공  →  granted:false, ALTAR_COMPLETED
A 가 광산 다시 성공 →  granted:false, ALTAR_COMPLETED

→ 보유량 변화 없음
→ reward_claims 변화 없음
→ 결정 #1(반복 지급)과 결정 #8(완료 후 중단)이 함께 지켜지는지 보는 시험
```

> **Test 9 와 Test 5 는 다른 것을 봅니다.** 헷갈리면 안 됩니다.
>
> ```text
> 새로운 정상 플레이   → 새 matchKey → 회복 진행 중이면 다시 +1  (Test 9)
> 같은 플레이의 재시도 → 같은 matchKey → 중복 지급 금지          (Test 5)
> ```

#### Test 10 — 배(ShipCoop) 판 식별

```text
ShipCoop 판 A 성공  →  MatchInstanceId = GUID-A  →  matchKey A  →  +1
ShipCoop 판 B 성공  →  MatchInstanceId = GUID-B  →  matchKey B  →  +1
   ⚠ GUID-A ≠ GUID-B 여야 한다

matchKey A 로 재요청
→ duplicate:true
→ 추가 지급 없음
```

⚠ **이 테스트가 성립하지 않으면 STEP 10.5 가 끝나지 않은 것입니다.**
배에는 원래 전용 판 식별자가 없었습니다 (STEP 11 "matchKey 의 원천" 조사 결과).

⚠ 광산 · 무쌍으로도 같은 시험을 한 번씩 돌립니다. 세 게임이 **같은 규격**을 쓰는지
확인하는 것이 목적입니다.

#### Test 11 — 서버 재시작 후 matchKey 충돌 방지

```text
서버 실행 #1
  판을 하나 돌린다  →  MatchInstanceId = GUID-A
  A 가 클리어       →  reward_claims 에 matchKey(GUID-A) 저장

서버 종료 / 재시작

서버 실행 #2
  판을 하나 돌린다  →  MatchInstanceId = GUID-B
  A 가 클리어

기대
  GUID-A ≠ GUID-B
  두 번째 클리어가 정상 지급된다 (granted:true, duplicate:false)
  ⚠ duplicate:true 가 나오면 판 식별자가 재시작 후 겹친 것이다 — 실패
```

⚠ **`Runner.Tick` 기반 값(`ResultTick` · `BoardSeed`)으로 구현하면 이 테스트가
깨질 수 있습니다.** 재시작 후 Tick 이 0 부터 다시 세기 때문입니다.
GUID 를 쓰는 이유가 이 테스트 하나입니다.

#### Test 12 — `ALTAR_COMPLETED` 조기 반환에서 잠금이 풀리는가 (11.4.14)

```text
사전  altar 1000 / 1000

A 가 clear-reward 를 보낸다
  → altar_state 를 FOR UPDATE 로 읽고 완료를 확인
  → inventory 변경 없음, reward_claim 없음
  → granted:false, ALTAR_COMPLETED

기대
  응답을 만들기 전에 트랜잭션이 끝나 있다
  응답 이후에 altar_state row lock 이 남아 있지 않다
```

확인 방법 — 응답 직후 다른 연결에서 곧바로 잠금을 잡아 봅니다.

```sql
-- 막히지 않고 즉시 성공해야 한다
BEGIN; SELECT * FROM altar_state WHERE id = 1 FOR UPDATE; ROLLBACK;

-- 또는 대기 중인 잠금이 있는지 본다 (0행이어야 한다)
SELECT * FROM performance_schema.data_lock_waits;
```

⚠ `duplicate` 경로는 애초에 `altar_state` 를 잠그지 않습니다 (11.4.14).
그쪽도 같은 방법으로 확인하면 잠금 대기가 0 이어야 합니다.

#### Test 13 — 쿨다운: 정상 지급 후 60초 이내 새 판

```text
사전  altar 500 / 1000

판 A 성공  →  granted:true  →  +1

30초 뒤, 새로운 정상 matchKey(판 B)로 clear-reward

→ HTTP 429, code "REWARD_COOLDOWN"
→ inventory 증가 없음
→ reward_claims 에 행이 생기지 않음
→ retryAfterSeconds 가 30 근처로 온다
→ 결과 상태 NotGrantedBecauseCooldown
→ 화면 "이번 판의 보상이 지급되지 않았습니다."
   ⚠ "보상을 확인할 수 없습니다." 가 나오면 실패다 (Unknown 과 섞였다는 뜻)
   ⚠ 보상 줄이 숨겨져도 실패다 (ALTAR_COMPLETED 와 섞였다는 뜻)
```

⚠ 실제 게임에서는 60초 안에 두 판을 클리어할 수 없으므로 정상 플레이에서는 나오지
않습니다. **서버 방어를 확인하는 시험**입니다. API 를 직접 호출해서 돌립니다.

#### Test 14 — 쿨다운: 60초 이후

```text
판 A 성공  →  +1
60초 이상 경과
판 B 성공  →  +1

→ 보유 2
→ reward_claims 2건
```

#### Test 15 — 쿨다운이 `duplicate` 를 막지 않는다 (가장 중요)

```text
판 A 성공  →  +1  →  HTTP 응답 유실

10초 뒤 (쿨다운 한가운데) 동일 matchKey 로 재요청

→ granted:true, duplicate:true       ⚠ REWARD_COOLDOWN 이 나오면 실패다
→ 결과 상태 Granted → 화면 "바다의 심장 조각 +1"
→ 추가 +1 없음 (보유량이 1 에서 멈춘다)
→ reward_claims 여전히 1건
→ 쿨다운 시각이 연장되지 않는다
```

⚠ **이 테스트가 11.4.15 의 검증 순서(①이 ④보다 먼저)를 직접 확인합니다.**
쿨다운을 먼저 보면 이미 조각을 받은 플레이어에게 "잠시 기다려 주세요" 가 뜨고
결과 화면이 보상을 못 보여 줍니다.

#### Test 16 — `ALTAR_COMPLETED` 가 쿨다운보다 먼저다

```text
사전  A 가 방금 판 A 로 지급을 받았다 (쿨다운 한가운데)
      그 사이 다른 플레이어가 마지막 봉헌을 해서 altar 1000 / 1000

A 가 새 판 B 를 클리어하고 clear-reward

→ 200 OK, granted:false, code "ALTAR_COMPLETED"
→ ⚠ 429 REWARD_COOLDOWN 이 나오면 실패다 (②가 ④보다 먼저여야 한다)
→ 결과 상태 NotGrantedBecauseAltarCompleted → 보상 줄 숨김
→ inventory · reward_claims 변화 없음
```

#### Test 17 — `Unknown` 은 응답을 못 받았을 때만 (`Cooldown` 과 구분)

```text
사전  altar 500 / 1000, 쿨다운 없음

A 가 클리어 → clear-reward 요청
서버로 가는 길이 끊긴다 (API 를 끄거나 방화벽으로 막는다)

클라이언트
  → 동일 matchKey 로 1~2회 재시도
  → 전부 응답 없음

기대
  결과 상태 Unknown
  화면 "보상을 확인할 수 없습니다."
  ⚠ "이번 판의 보상이 지급되지 않았습니다." 가 나오면 실패다
  ⚠ 보상 줄이 숨겨져도 실패다
  ⚠ "바다의 심장 조각 +1" 이 나와도 실패다 (받았는지 모르는데 받았다고 말한 것)
```

> **Test 13 과 Test 17 을 나란히 돌립니다.** 둘이 같은 화면을 보여 주면
> `Cooldown` 과 `Unknown` 이 섞인 것입니다.
>
> ```text
> Cooldown  서버가 "안 줬다" 고 확정했다        → "이번 판의 보상이 지급되지 않았습니다."
> Unknown   줬는지 못 줬는지 알 수 없다         → "보상을 확인할 수 없습니다."
> ```

## 20. STEP 별 코딩 에이전트 프롬프트

그대로 복사해 붙여 넣을 수 있게 썼습니다.
**각 프롬프트는 이 문서(`docs/prd/lobby_altar_inventory_system_design.md`)를 함께 읽게 합니다.**

---

### STEP 1 — 서버 스키마

```text
아라아띠 프로젝트에 인벤토리·제단 테이블을 추가해줘.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  17장, 18장 STEP 1
  server/AraAtti.Api/Data/AraAttiDbContext.cs
  server/AraAtti.Api/Entities/CharacterPart.cs     (FK + Cascade 본보기)
  server/AraAtti.Api/Migrations/20260908075724_InitialCreate.cs

할 일
  1. Entities/ 에 PlayerInventoryItem, AltarState, AltarContribution, RewardClaim 을 만든다.
     ⚠ 테이블은 넷이다. reward_claims 를 빠뜨리지 마라 (STEP 11 이 쓴다).
     ⚠ 유니크 키를 정확히 걸어라. 셋 다 단일 컬럼이 아니다:
         player_inventories   UNIQUE (user_id, item_id)     ← 보상 UPSERT 가 이 키를 쓴다
         altar_contributions  UNIQUE (user_id, request_id)
         reward_claims        UNIQUE (user_id, match_key)
       match_key 단독 UNIQUE 로 걸면 4인 파티에서 한 명만 보상을 받는다.
       player_inventories 의 UNIQUE 가 없으면 INSERT ... ON DUPLICATE KEY UPDATE 가
       걸릴 키가 없어 같은 아이템 행이 계속 새로 생긴다 (11.4.12).
     ⚠ 인벤토리와 기여 이력은 user_id (FK → users.id) 에 매단다. character_id 가 아니다.
       결정 #6 이고 이유는 설계 문서 15.4절에 있다.
     ⚠ altar_state.target_offering 기본값은 1000 이고 CHECK (target_offering > 0) 을 건다.
       코드 어디에도 1000 을 적지 않는다. 시연 때는 DB 에서 UPDATE 로 낮춘다 (4.2.2절).
  2. AraAttiDbContext 에 DbSet 3개와 OnModelCreating 블록 3개를 추가한다.
     ⚠ 컬럼 이름·타입·인덱스는 엔티티 Attribute 가 아니라 반드시 DbContext 에 적는다.
        이 파일 9-11행이 그렇게 하라고 적혀 있다.
  3. altar_state 의 초기 행(id=1, target_offering=1000)을 마이그레이션의
     InsertData 로 넣는다. 런타임에 "없으면 만든다" 로 하지 않는다.
  4. dotnet ef migrations add AddInventoryAndAltar --project AraAtti.Api

지킬 것
  · 테이블·컬럼은 snake_case. 기존 users / characters / character_parts 와 같은 모양.
  · FK 이름은 fk_<table>_<target>, 유니크는 uk_..., 인덱스는 idx_... 형식을 따른다.
  · 주석은 한국어. 기존 파일과 같은 밀도로.

하지 말 것
  · 기존 세 테이블의 스키마를 고치지 않는다.
  · dotnet ef database update 는 실행하지 않는다. 사용자가 직접 한다.

완료 조건
  마이그레이션 파일이 생성되고 빌드가 통과한다.
  설계 문서 STEP 1 의 완료 조건 체크리스트 항목이 전부 표현되어 있다
  (신규 테이블 4개 · UNIQUE 3개 · INDEX 2개 · CHECK 3개 · altar_state 초기 행).
```

---

### STEP 2 — 조회 엔드포인트

```text
아라아띠 서버에 인벤토리·제단 조회 엔드포인트를 추가해줘.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  9.1, 9.2절
  server/AraAtti.Api/Endpoints/CharacterEndpoints.cs   ← 이 파일의 구조를 그대로 따른다
  server/AraAtti.Api/Contracts/CharacterContracts.cs
  server/AraAtti.Api/Auth/ClaimsPrincipalExtensions.cs
  server/AraAtti.Api/Program.cs

만들 것
  GET /api/inventory        내 인벤토리 (없으면 빈 배열. 404 아님)
  GET /api/altar/state      전체 봉헌 상태

지킬 것
  · MapGroup(...).WithTags(...).RequireAuthorization() 형식.
  · 신원은 언제나 principal.TryGetUserId. 요청에서 userId 를 받지 않는다.
  · remainingToTarget, maxOfferAmount, recoveryPercent, altarActivated 를 전부
    서버가 계산해서 응답에 넣는다. 클라이언트가 계산하게 두지 않는다.
      remainingToTarget = max(0, targetOffering - totalOffered)
      maxOfferAmount    = min(myFragments, remainingToTarget)
      recoveryPercent   = min(totalOffered / targetOffering, 1) * 100   (target <= 0 이면 0)
  · altarActivated = totalOffered >= targetOffering 로 둔다.
    불변식은 <= 인데 판정만 >= 인 이유는 설계 문서 10.7절에 있다. == 을 쓰지 않는다.
  · 실패는 ErrorResponse(code, message), message 는 한국어.
  · Program.cs 에 MapXxxEndpoints() 한 줄씩 추가.
  · Swagger 요약(WithSummary/WithDescription)을 기존과 같은 밀도로 단다.

완료 조건
  빌드 통과. Swagger 에 두 엔드포인트가 보이고 토큰 없이 부르면 401.
```

---

### STEP 3 — 봉헌 엔드포인트 (가장 중요)

```text
아라아띠 서버에 제단 봉헌 엔드포인트를 추가해줘. 동시성이 이 작업의 핵심이다.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  8.6, 9.2, 10장 전체
  STEP 1~2 에서 만든 파일들

만들 것
  POST /api/altar/offer   { "amount": int, "requestId": "guid" }

반드시 이렇게 할 것
  1. 멱등성
     · UNIQUE 는 (user_id, request_id) 다. request_id 단독이 아니다. 이유는 10.4절.
     · 같은 (user_id, requestId) 가 이미 있으면 봉헌을 다시 하지 않는다.
     · ⚠ "그때 저장해 둔 응답" 을 돌려주지 않는다. 그런 컬럼은 없다.
       **지금 시점의 권위 상태를 다시 조회해서** duplicate:true 와 함께 돌려준다.
     · INSERT 가 UNIQUE 위반으로 터지면 롤백하고 같은 경로로 돌아간다.

  2. 불변식 — 이번 작업의 핵심이다
     total_offered 는 target_offering 을 절대 초과하면 안 된다.
         0 <= total_offered <= target_offering

     ⚠ 부분 수락하지 마라.
       다른 플레이어가 먼저 봉헌해서 requestedAmount 가 현재 remainingToTarget 을
       초과하면 요청 전체를 실패시켜라.
       Inventory 를 일부라도 차감하면 안 된다.
       acceptedAmount / refundedAmount 같은 필드를 만들지 마라.

     ⚠ 동시 요청에서도 total_offered <= target_offering 를 지켜라.

  3. 원자적 처리 — 읽고 계산해서 쓰지 않는다
     · 제단:  Where(id == 1 && TargetOffering - TotalOffered >= amount)
                .ExecuteUpdateAsync(t => t + amount)
              영향 행이 0이면 남은 칸이 모자란 것이다. 롤백하고 409.
              → 최신 상태를 읽어 remainingToTarget 이 0 이면 OFFERING_CLOSED,
                0 보다 크면 OFFERING_AMOUNT_CHANGED 로 가른다 (8.6.2절).
     · 차감:  Where(quantity >= amount).ExecuteUpdateAsync(q => q - amount)
              영향 행이 0이면 보유량 부족이다. 롤백하고 409.
     · 제단을 먼저, 인벤토리를 나중에. 둘을 하나의 BeginTransactionAsync 안에 넣는다.
     ⚠ var s = await db...First(); s.Total += amount; SaveChanges(); 형태를 절대 쓰지 마라.
     ⚠ remaining 을 먼저 SELECT 해서 if 로 판단하고 나중에 UPDATE 하지도 마라.
       그 사이에 다른 요청이 끼어들어 1001/1000 이 만들어진다 (설계 문서 10.2 ✗2).
     · FOR UPDATE 나 낙관적 동시성으로 구현해도 된다. 원칙만 지키면 된다 (10.3).

  3. 검증 순서는 설계 문서 8.6절 표를 그대로 따른다.

  4. 에러 코드
     AMOUNT_INVALID / AMOUNT_TOO_LARGE / REQUEST_ID_INVALID /
     NOT_ENOUGH_FRAGMENTS / OFFERING_AMOUNT_CHANGED / OFFERING_CLOSED
     message 는 전부 한국어.
       OFFERING_AMOUNT_CHANGED  "다른 플레이어가 먼저 봉헌했습니다."
                                 ⚠ 뒤에 붙는 "섬 회복까지 N개 남았습니다." 는
                                   클라이언트가 remainingToTarget 으로 조합한다 (9.2절).
                                   서버 message 에 숫자를 박지 마라.
       OFFERING_CLOSED          "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다."
       NOT_ENOUGH_FRAGMENTS     실제 수치를 넣는다.

  5. 성공이든 실패든 응답에 최신 상태를 전부 싣는다.
     totalOffered / targetOffering / remainingToTarget / maxOfferAmount /
     remainingFragments / altarActivated / recoveryPercent
     클라이언트가 실패 직후 state 를 다시 조회하지 않아도 UI 를 맞출 수 있어야 한다.

하지 말 것
  · 요청 본문에서 playerId / characterId / totalOffered 를 받지 않는다.
  · Serializable 격리수준이나 lock 문을 쓰지 않는다. 설계 문서 10.3 의 이유를 읽어라.

완료 조건
  빌드 통과. 설계 문서 19.1~19.3, 19.4(테스트 A~G), 19.6 시나리오가 통과한다.
  특히 테스트 G: 계정 20개가 남은 칸 10 을 두고 동시에 요청해도
    SELECT * FROM altar_state WHERE total_offered > target_offering;  가 0행이다.
```

---

### STEP 4 — Unity 서비스 계층

```text
아라아띠 Unity 클라이언트에 인벤토리·제단 서비스를 추가해줘. UI 는 아직 만들지 마.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  9.4, 17장
  unity/UnderTheSea/Assets/Game/Scripts/Account/HttpCharacterService.cs  ← 본보기
  unity/UnderTheSea/Assets/Game/Scripts/Account/HttpJson.cs
  unity/UnderTheSea/Assets/Game/Scripts/Account/HttpApiConfig.cs
  unity/UnderTheSea/Assets/Game/Scripts/Account/AccountServiceBootstrap.cs
  unity/UnderTheSea/Assets/Game/Scripts/MiniGames/Common/RewardService.cs  ← 캐시 모양
  unity/UnderTheSea/CONVENTION.md  5장

만들 것 (Assets/Game/Scripts/Inventory/ 와 Assets/Game/Scripts/Lobby/)
  ItemIds, PlayerInventory, IInventoryService, HttpInventoryService, FakeInventoryService
  AltarState, IAltarService, HttpAltarService, FakeAltarService

고칠 것 (아주 조금)
  HttpApiConfig       경로 상수 3개 추가
  AccountServiceBootstrap  Fake/Http 분기에 각각 2줄 추가

지킬 것
  · HttpJson 과 HttpApiConfig 를 그대로 재사용한다. UnityWebRequest 를 직접 쓰지 않는다.
  · PlayerInventory / AltarState 는 static 캐시 + event Changed. RewardService 와 같은 모양.
    ⚠ 이들은 캐시일 뿐이다. 값을 로컬에서 더하거나 빼는 메서드를 만들지 마라.
      서버 응답을 통째로 대입하는 메서드만 둔다.
  · AltarState 에 RequestRefresh() 를 둔다. Late Join 복원의 핵심이다.
  · 봉헌 요청 시 requestId(Guid)는 호출한 쪽이 한 번만 만들고, 재시도해도 유지한다.
  · 주석은 한국어. 기존 Account 폴더와 같은 밀도와 어투로.

하지 말 것
  · 씬이나 프리팹을 고치지 않는다.
  · asmdef 를 만들지 않는다. (Fishing 외에는 전부 Assembly-CSharp 다)

완료 조건
  Unity 가 컴파일되고 Console 에 새 Error 가 없다.
  ⚠ 컴파일 확인은 사용자가 한다. 나는 코드만 쓰고 확인을 요청한다.
```

---

### STEP 5 — 제단 상호작용

```text
아라아띠 로비의 심장 제단에 상호작용을 붙여줘.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  5장 전체, 6.2절
  unity/UnderTheSea/Assets/Game/Scripts/Lobby/MiniGamePortal.cs   ← 이 구조를 그대로 따른다
  unity/UnderTheSea/Assets/Game/Scripts/Network/LocalPlayer.cs
  unity/UnderTheSea/Assets/Game/Scripts/UI/LobbyChatInstaller.cs  ← 설치 패턴

상호작용 키는 Key.E 다. (2026-09-20 확정. 설계 문서 2.6절)
⚠ MiniGamePortal 은 F 를 쓴다. E 를 골랐으므로 그 파일은 고치지 않는다.

만들 것
  Assets/Game/Scripts/Lobby/AltarInteraction.cs
  Assets/Game/Scripts/Lobby/AltarOfferingInstaller.cs

지킬 것
  · Trigger Collider 를 쓰지 않는다. LocalPlayer.Transform 과의 거리로 판정한다.
    이유는 설계 문서 5.2절에 있다.
  · ignoreHeight = true (제단이 계단 위에 있다).
  · 키는 [SerializeField] private Key interactKey 로 인스펙터에서 바꿀 수 있게 한다.
  · 여는 거리보다 닫는 거리를 크게 잡는다 (히스테리시스).
  · 설치기는 LobbyChatInstaller 를 본보기로:
      - graphicsDeviceType == Null 이면 아무것도 안 만든다 (Dedicated Server)
      - LocalPlayer.Registered 콜백 안을 try/catch 로 감싼다
        (여기서 예외가 새면 카메라가 안 붙어 로비가 멈춘다. 실제로 겪은 사고다)
      - 로비가 아니면 숨긴다

하지 말 것
  · Lobby.unity 를 고치지 않는다.
  · .prefab / .meta 파일을 직접 고치지 않는다.
    P_HeartAltar 에 컴포넌트를 붙이는 일은 사용자가 Unity Editor 에서 한다.
    무엇을 어디에 붙여야 하는지만 정확히 알려준다.

완료 조건
  코드가 컴파일된다. 사용자에게 다음을 요청한다:
    "P_HeartAltar 프리팹에 AltarInteraction 이라는 빈 자식을 만들고,
     위치를 Pedestal 근처로 옮긴 뒤 AltarInteraction.cs 를 붙여 주세요."
```

---

### STEP 6~7 — 봉헌 UI 와 실행

```text
아라아띠 로비의 제단 봉헌 UI 를 만들어줘.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  6장, 7장, 15장
  unity/UnderTheSea/Assets/Game/Scripts/UI/LobbyChatView.cs   ← ChatFocus 연결 방식
  unity/UnderTheSea/Assets/Game/Scripts/UI/ChatFocus.cs
  STEP 4 에서 만든 PlayerInventory / AltarState / IAltarService

만들 것
  Assets/Game/Scripts/Lobby/AltarOfferingUIController.cs

UI 요소는 설계 문서 6.1절 표를 따른다.
수량 입력은 - / + / MAX 버튼 + 읽기 전용 표시로 만든다 (6.3절의 이유).
  · 선택 범위는 1 ~ maxOfferAmount 다.
    maxOfferAmount = min(playerOwnedAmount, remainingToTarget)  ← 서버가 계산해서 준다
    ⚠ MAX 는 보유량이 아니라 maxOfferAmount 다. + 도 거기서 멈춘다.
  · maxOfferAmount < 보유량이면 "섬 회복까지 {remainingToTarget}개만 더 필요합니다." 를 띄운다
  · maxOfferAmount == 0 이면 봉헌 버튼을 잠근다
  · + 를 길게 누르면 가속한다 (0.4초 후 초당 10 → 50)

반드시 지킬 것
  · ⚠ ChatFocus 를 먼저 고쳐야 한다. 설계 문서 6.4.1절을 반드시 읽어라.
    지금의 단순 bool 로는 "제단 UI 를 연 채 채팅을 껐다 켜면 이동이 풀리는" 버그가 난다.
    ChatFocus 를 보유자 HashSet 으로 바꾸고 Begin(this)/End(this) 로 부른다.
    LobbyChatView 의 호출 4곳(:94 :95 :144 :262)에도 this 를 넘긴다.
    PlayerInputProvider 는 고치지 않는다.
  · Esc 판정은 ChatFocus.Typing 이 아니라 ChatFocus.HeldByOther(this) 로 한다.
    제단 UI 자신이 보유자라서 Typing 은 언제나 참이다.
  · 봉헌 버튼을 누르면 즉시 모든 버튼을 interactable = false.
  · requestId 는 버튼을 누른 순간 한 번 만들고, 재시도해도 바꾸지 않는다.
  · 서버 응답의 remainingFragments / totalOffered 를 그대로 대입한다.
    ⚠ 로컬에서 빼거나 더하지 않는다. 한 줄도.
  · 실패 응답의 message 를 오류 영역에 띄운다. 새로 문구를 만들지 않는다.
    ⚠ 예외가 하나 있다. code == "OFFERING_AMOUNT_CHANGED" 일 때만
      뒤에 "섬 회복까지 {remainingToTarget}개 남았습니다." 를 붙인다 (9.2절).
      최종 문구: "다른 플레이어가 먼저 봉헌했습니다. 섬 회복까지 2개 남았습니다."
  · 실패해도 UI 를 닫지 않는다.
  · 실패 응답에 최신 상태가 들어 있다. 그것으로 보유량 · 전체 · maxOfferAmount 를 덮어쓰고,
    선택 수량이 새 maxOfferAmount 보다 크면 그 값으로 낮춘다 (5 → 2).
    ⚠ 여기서 GET /api/altar/state 를 다시 부르지 마라. 응답에 이미 다 있다.
  · ⚠ 실패 후 자동으로 다시 봉헌하지 마라. 사용자가 [봉헌] 을 다시 눌러야 한다.
    자동 재시도는 부분 수락과 결과가 같아진다 (10.3.2절).
  · Esc 는 ChatFocus.Typing 이 false 일 때만 읽는다 (채팅이 우선이다).

하지 말 것
  · UI 프리팹의 최종 디자인을 만들지 않는다. 사용자가 HUD 이미지를 준다.
    코드가 기대하는 슬롯(필드) 목록만 정확히 알려준다.
  · 씬을 고치지 않는다.

완료 조건
  컴파일 통과. 사용자에게 프리팹에 연결해야 할 슬롯 목록을 표로 알려준다.
```

---

### STEP 8 — 다른 플레이어에게 전파

```text
아라아띠 로비에서 누가 봉헌하면 다른 플레이어 화면도 갱신되게 해줘.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  9.3, 12.4절
  unity/UnderTheSea/Assets/Game/Scripts/Network/LobbyChatRelay.cs  ← 이 구조를 그대로 따른다
  unity/UnderTheSea/Assets/Game/Scripts/Network/NetworkPlayerIdentity.cs

만들 것
  Assets/Game/Scripts/Lobby/AltarOfferingRelay.cs   (NetworkBehaviour)

반드시 지킬 것
  · RPC 에 숫자를 싣지 않는다. "바뀌었다" 는 사실만 보낸다.
    받은 쪽이 GET /api/altar/state 로 스스로 확인한다.
    이유: 숫자를 실으면 클라이언트가 조작할 수 있다.
  · Rpc_Send 는 [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    Rpc_Receive 는 [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    LobbyChatRelay 와 같은 모양이다.
  · info.Source != Object.InputAuthority 방어를 넣는다 (LobbyChatRelay.cs:118).
  · 서버에서 Time.time 으로 레이트 리밋을 건다 (LobbyChatRelay.cs:124 패턴).
    연타하면 모두가 API 를 연타하게 된다.
  · 받은 쪽은 0~1초 랜덤 지연 후에 조회한다.
    100명이 동시에 같은 API 를 때리지 않게 한다.
  · ⚠ RPC 는 같은 채널에만 간다. lobby-ch2 는 이 알림을 못 받는다.
    그래서 AltarState 에 30초 주기 폴링 + coalescing + 순번 가드를 함께 넣는다.
    설계 문서 12.5절이 그 규칙 전부를 적어 두었다. RPC 만 만들고 끝내지 마라.

하지 말 것
  · 프리팹을 직접 고치지 않는다.
    플레이어 프리팹에 컴포넌트를 붙이는 일은 사용자가 Editor 에서 한다.

완료 조건
  컴파일 통과. 사용자에게 "LobbyChatRelay 가 붙어 있는 플레이어 프리팹에
  AltarOfferingRelay 도 붙여 주세요" 라고 요청한다.
  ⚠ Fusion 프리팹을 고쳤으면 Fusion 리베이크가 필요할 수 있다고 함께 알린다.
```

---

### STEP 9~10 — 회복도 HUD 와 VFX

```text
아라아띠 로비에 섬 회복도 HUD 와 제단 달성 연출을 붙여줘.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  11장, 12장, 13장
  unity/UnderTheSea/Assets/Game/Prefabs/HeartAltar/P_HeartAltar.prefab
    (AltarBeam, Light_Monolith, Light_Crystal, HeartCrystal 이 이미 있다)
  unity/UnderTheSea/Assets/Game/Scripts/UI/LobbyChatInstaller.cs

만들 것
  Assets/Game/Scripts/Lobby/AltarVfxController.cs
  Assets/Game/Scripts/Lobby/IslandRecoveryView.cs
  Assets/Game/Scripts/Lobby/IslandRecoveryInstaller.cs

반드시 지킬 것
  · AltarVfxController 는 NetworkBehaviour 가 아니다. 네트워크를 전혀 모른다.
    AltarState.Changed 만 듣고 자기 화면을 칠한다.
  · 기존 AltarBeam 은 그대로 두고, 달성 전용 자식 오브젝트를 새로 둔다 (12.2절).
    AltarBeam 은 프리팹에 켜진 채 저장되어 있어서, 그것을 껐다 켜면 로비 진입 직후
    "파란 빛이 번쩍했다 사라지는" 한 프레임이 생긴다.
    달성 전용 오브젝트는 프리팹에 꺼진 채로 저장한다.
    VFX Graph 패키지는 이 프로젝트에 없다. 추가하지 마라.
  · OnEnable 에서 AltarState.RequestRefresh() 를 부르고, 캐시가 있으면 즉시 Apply().
    "꺼졌다가 1초 뒤 켜지는" 것도 실패다.
  · 조회가 실패하면 연출을 끄지 말고 마지막 상태를 유지한다.
  · 켜짐 판정은 반드시 totalOffered >= targetOffering.  == 을 쓰지 않는다.
  · 회복도 숫자는 보간해서 올린다 (68 → 73 이 한 프레임에 점프하지 않게).
  · HUD 는 위쪽 중앙. 채팅(왼쪽 아래)·튜토리얼(왼쪽 아래)과 겹치지 않게 한다.

하지 말 것
  · Assets/ARPG Effects 등 외부 에셋의 원본을 고치지 않는다 (CLAUDE.local.md §4).
    참조만 한다.
  · 프리팹을 직접 고치지 않는다.

완료 조건
  컴파일 통과. 사용자에게 프리팹 배선 목록을 알려주고,
  테스트를 쉽게 하려면 DB 의 altar_state.target_offering 을 잠깐 10 정도로
  낮추라고 안내한다.
```

---

### STEP 11 — 미니게임 클리어 보상 (결정 #1 · #8)

```text
아라아띠에 미니게임 클리어 보상을 붙여줘. 섬 회복이 끝나면 지급을 멈춰야 한다.

먼저 읽을 것
  docs/prd/lobby_altar_inventory_system_design.md  11.4절 전체, 9.2절 clear-reward, STEP 11
  server/AraAtti.Api/Endpoints/CharacterEndpoints.cs        (엔드포인트 패턴)
  unity/UnderTheSea/Assets/Game/Scripts/MiniGames/Common/MatchFlowController.cs
  unity/UnderTheSea/Assets/Game/Scripts/MiniGames/Common/UI/ResultPanelPresenter.cs

서버 — POST /api/inventory/clear-reward
  처리 순서는 설계 문서 11.4.3 을 그대로 따른다.

  ⚠ 순서를 바꾸지 마라. 중복 확인이 제단 확인보다 먼저다.
    바꾸면 "DB 에는 지급됐는데 화면은 안 줬다고 하는" 상태가 생긴다 (11.4.4).

  ⚠ 제단 완료 확인은 평범한 SELECT 로 하지 마라.
    REPEATABLE READ 의 스냅샷 읽기라 방금 커밋된 마지막 봉헌을 못 본다.
    SELECT ... WHERE id=1 FOR UPDATE 같은 잠금 읽기를 써라 (11.4.5).
    FOR UPDATE 가 아니어도 되지만, "봉헌 Commit 이후 새 지급이 절대 없다" 를
    보장하는 방법이어야 한다.

  ⚠ altar/offer 쪽(10.3.1 조건부 UPDATE)은 고치지 마라. 이미 같은 행을 잠근다.

  ⚠ ALTAR_COMPLETED 는 오류가 아니다. 200 OK + granted:false 로 응답해라.
    409 · 422 · 500 을 쓰지 마라. 미니게임 클리어 자체는 성공이다.

  ⚠ ALTAR_COMPLETED 일 때 reward_claims 에 행을 만들지 마라 (11.4.6).
    재시도는 매번 제단 상태를 다시 확인해 같은 답을 낸다 (11.4.7 방식 A).

  ⚠ 잔여 조각을 지우거나 자동 봉헌하거나 변환하는 코드를 만들지 마라 (11.4.2).

Unity
  1. 결과 화면을 즉시 표시한다. 응답을 기다리지 않는다.
  2. 보상 줄은 Pending("보상 확인 중...") 으로 시작한다.
  3. clear-reward 를 호출한다.
  4. granted:true                  → "바다의 심장 조각 +1"
  5. granted:false ALTAR_COMPLETED → 보상 줄을 숨긴다
     ⚠ "보상 없음" "조각 지급 실패" 같은 부정적 문구를 쓰지 마라. 정상 상태다.
  6. 429 REWARD_COOLDOWN           → NotGrantedBecauseCooldown
                                      "이번 판의 보상이 지급되지 않았습니다."
     ⚠ 보상 줄을 숨기지 마라. ALTAR_COMPLETED 와 화면에서 구분되어야 한다.
     ⚠ 자동 재시도하지 마라. 서버가 확정한 응답이다.
  7. 네트워크 응답 없음            → 동일 matchKey 로 최대 1~2회 재시도
     ⚠ 새 matchKey 를 만들면 절대 안 된다. 보상이 두 번 지급된다.
  8. 끝내 확인 실패                → Unknown  "보상을 확인할 수 없습니다."
     ⚠ "+1" 로 낙관하지도, "보상 없음" 으로 단정하지도 마라.

  결과 화면 상태는 다섯 개다 (11.4.8)
    Pending / Granted / NotGrantedBecauseAltarCompleted /
    NotGrantedBecauseCooldown / Unknown
  ⚠ Unknown 은 응답을 못 받았을 때만 쓴다. duplicate · ALTAR_COMPLETED ·
    REWARD_COOLDOWN 은 서버가 결과를 확정한 것이라 Unknown 이 아니다.
  ⚠ NotGrantedBecauseCooldown 과 Unknown 을 같은 문구로 묶지 마라.
  ⚠ 쿨다운 문구에서 사용자 잘못으로 읽히는 표현을 쓰지 마라
    ("너무 빠르게 플레이했습니다" · "잠시 후 다시 시도하세요" 등).

  ⚠ AltarState 캐시를 읽어 보상 여부를 미리 판단하지 마라 (11.4.9).
    미니게임 씬에는 폴링이 돌지 않아 캐시가 낡는다. 서버 응답이 최종 권위다.

  ⚠ 보상이 없다고 게임 결과를 실패로 바꾸지 마라 (11.4.10).

인벤토리 지급
  · UPSERT 로 처리한다. 신규 유저는 player_inventories 에 행이 없다 (11.4.12).
    UPDATE ... SET quantity = quantity + 1 만 쓰면 첫 지급이 0행으로 조용히 실패한다.
  · SELECT 후 INSERT/UPDATE 두 단계로 만들지 마라. 원자적 UPSERT 를 써라.
    INSERT ... ON DUPLICATE KEY UPDATE 가 UNIQUE (user_id, item_id) 를 쓴다.
  · ⚠ INSERT IGNORE + UPDATE 조합을 쓰지 마라. 신규 유저의 첫 지급이 2 가 된다 (11.4.12).
  · EF Core 확장 패키지를 추가하지 마라. raw SQL 로 호출한다.

반복 플레이와 재시도를 구분해라
  · 같은 게임을 다시 플레이한 새로운 판이면 → 새 matchKey → 회복 진행 중이면 다시 +1
  · 같은 플레이의 네트워크 재시도면      → 같은 matchKey → 중복 지급 금지
  · "이미 클리어한 게임이므로 보상 없음" 같은 구조를 만들지 마라.

matchKey 원천
  · STEP 10.5 가 먼저 끝나 있어야 한다. 세 게임에 [Networked] MatchInstanceId 가 있어야 한다.
  · matchKey = {sessionName}:{gameId}:{matchInstanceId}
    예: "mine-1:mining:9f2c1b4e6a404b3f9a1d9c2f6f3e77aa"
    sessionName 은 MineNet.cs:34 · ShipCoopNet.cs:43 · WarriorsNet.cs:34 의 DefaultSession
    matchInstanceId 는 Guid.NewGuid().ToString("N") — 정확히 32자
  · 같은 판의 재시도는 duplicate:true. 새로운 정상 판은 서로 다른 MatchInstanceId 를 갖는다.
  · ⚠ ResultTick · BoardSeed · RunSeed 를 matchKey 원천으로 쓰지 마라.
    용도가 다르고, Tick 기반 값은 서버 재시작 후 겹친다 (STEP 11 역할 구분 표).

abuse 완화 (11.4.15)
  · 동일 사용자의 성공 지급 후 60초 쿨다운을 적용한다.
    기준은 SELECT MAX(claimed_at) FROM reward_claims WHERE user_id = ? 다.
  · ⚠ 이번 구현에서는 일일 지급 상한을 적용하지 않는다. 만들지 마라.
  · ⚠ duplicate 요청에는 쿨다운을 적용하지 마라. 이미 지급된 판의 재시도다.
  · ⚠ ALTAR_COMPLETED 가 REWARD_COOLDOWN 보다 우선한다.
  · 검증 순서: ① duplicate → ② ALTAR_COMPLETED → ③ 보상 검증 → ④ 쿨다운 → ⑤ UPSERT → ⑥ claim
  · REWARD_COOLDOWN 은 반드시 HTTP 429 Too Many Requests 로 반환한다. 409 로 바꾸지 않는다.
    본문: { success:false, code:"REWARD_COOLDOWN", message, retryAfterSeconds }

동시성
  · deadlock 가능성을 0 으로 가정하지 마라. 잠금 순서를 맞춰 낮출 뿐이다 (11.4.13).
  · deadlock / UNIQUE 충돌로 rollback 되면 **새 트랜잭션**으로 다시 시작해라.
    롤백된 트랜잭션과 DbContext/ChangeTracker 상태를 그대로 이어서 쓰지 마라.
    최신 DB 상태를 다시 읽고 검증을 처음부터 다시 해라.
  · 재시도는 1~2회로 제한한다.
  · ⚠ duplicate · ALTAR_COMPLETED · REWARD_COOLDOWN 은 재시도 대상이 아니다. 정상 업무 결과다.

트랜잭션 수명 (11.4.14)
  · duplicate 는 트랜잭션을 시작하기 전에 반환한다. altar_state 를 잠그지 마라.
  · 트랜잭션이 시작된 뒤의 조기 반환(ALTAR_COMPLETED · REWARD_COOLDOWN · 검증 실패)은
    트랜잭션을 먼저 종료한 뒤 응답을 만들어라.
    FOR UPDATE 로 잡은 row lock 을 HTTP 응답 이후까지 들고 있으면
    로비 전체의 봉헌이 그만큼 멈춘다. altar_state 는 행이 하나뿐이다.

하지 말 것
  · RewardService 클래스를 지우지 않는다. MiniGameConfig.fragmentId 도 지우지 않는다 (4.2.3).
  · 씬과 프리팹을 고치지 않는다.
  · activated_at 래치를 만들지 않는다. 3안은 채택하지 않았다 (11.4.16).

완료 조건
  설계 문서 STEP 11 의 완료 조건과 19.12 의 Test 1~17 (+ 변형 5b · 9b) 이 통과한다.
  ⚠ Test 10~12 는 STEP 10.5 가 끝나야 돌릴 수 있다.
  ⚠ Test 15 (쿨다운이 duplicate 를 막지 않는다) ·
     Test 16 (ALTAR_COMPLETED 가 쿨다운보다 먼저) ·
     Test 17 (Unknown 과 Cooldown 의 화면이 달라야 한다) 를 반드시 포함해라.
```

---

## 21. 결정 목록 (요약)

### 21.1 확정된 것 (2026-09-20)

| # | 결정 | 결과 | 문서 위치 |
|---|---|---|---|
| **0** | `ALTAR_INTERACT_KEY` = **`Key.E`** | `MiniGamePortal` 을 안 고친다. 5.4절 공통 규칙 불필요 | 2.6절 |
| **1** | **갈래 1 — 조각을 재화로 통일.** 클리어 1회 = 1개. 같은 게임을 다시 깨도 매번 1개 (섬 회복 진행 중일 때) | `RewardService` 가 보상 경로에서 빠진다. ⚠ 목표 1000 의 현실성은 4.2.2 | 4.2.1절 |
| **2** | 회복 완료 후 **추가 봉헌 금지** | `remainingToTarget <= 0` 이면 `409 OFFERING_CLOSED`. 결정 #7 이 이것을 포함한다 | 11.3절 |
| **3** | 회복 완료 후 **회복도 100% 고정** | `min(total / target, 1) * 100`. 정상 데이터에서는 `min` 이 방어 코드다 | 11.3절 |
| **4** | 섬 회복도 = **봉헌량 기준** | 미니게임은 조각을 통해서만 기여한다 | 13.3절 |
| **5** | **DB 영구 저장** | Fusion 에 저장 상태가 하나도 없다 | 14.4절 |
| **6** | 인벤토리를 **`users`** 에 매단다 | 요청 본문이 영원히 id 를 안 나른다 | 15.4절 |
| **7** | **`targetOffering` 은 임계값이 아니라 상한.** `totalOffered <= targetOffering` | 초과 봉헌·부분 수락을 모두 배제. 동시 경쟁 시 한 명만 성공 | 11.1절 |
| **8** | **회복 완료 후 조각 지급 중단.** 잔여 조각은 보존 | `clear-reward` 가 제단 상태를 확인. `200 OK` + `granted:false` | 11.4절 |

> **[확정] 보상 abuse 완화** (2026-09-20) — 결정 번호를 붙이지 않은 부속 정책입니다.
> 동일 사용자의 성공 지급 후 **60초 쿨다운**만 적용하고, **일일 지급 상한은 쓰지 않습니다** (11.4.15).

두 결정은 **함께** 읽어야 합니다.

```text
섬 회복 진행 중  AND  미니게임 성공  AND  서버가 clear-reward 를 승인
  →  sea_heart_fragment +1
  →  같은 게임을 여러 번 클리어해도 위 조건을 만족하면 매번 +1

섬 회복 완료 후
  →  같은 게임을 다시 클리어해도 지급 없음 (granted:false)
```


**설계 결정은 전부 닫혔습니다. STEP 1 부터 바로 들어갈 수 있습니다.**

### 21.2 구현 전에 남은 미확정 사항

**설계 수준의 미확정은 없습니다.** 아래는 전부 **구현 단계에서 코드를 보며 정할 세부**입니다.

| # | 항목 | 상태 | 문서 위치 |
|---|---|---|---|
| A | ~~배(ShipCoop)의 서버 권위 판 식별자~~ | **해결.** 기존 판 ID 가 없음을 확인 → 세 게임 공통 `MatchInstanceId` 신규 설계로 확정 | STEP 11 · STEP 10.5 |
| B | `MatchInstanceId` 를 `MiniGameResult` 로 나르는 **구체적 필드 위치** | 값을 넘기기로는 확정. 필드를 더할지 생성자 인자를 늘릴지는 구현에서 | STEP 10.5 |
| C | `INSERT ... ON DUPLICATE KEY UPDATE` 를 **EF Core 에서 호출하는 방식** | SQL 은 확정. `ExecuteSqlInterpolatedAsync` 등 호출 API 는 현재 코드 스타일을 보고 | 11.4.12 |
| D | 교착·충돌 재시도에서 `DbContext` 재사용 vs 새 scope | 원칙("이어 쓰지 않는다")은 확정. 방법은 구현에서 | 11.4.13 |
| E | 쿨다운 조회 SQL 을 EF Core 어디에 둘지 | 정책(60초 · `MAX(claimed_at)` 기준)은 확정 | 11.4.15 |
| F | 서버 시각을 어떤 API 로 가져올지 · `retryAfterSeconds` 계산 | 응답 필드는 확정 | 11.4.15 |

**A 는 조사 결과로 닫혔습니다.** 배에 판 식별자가 없다는 것을 실제 코드에서 확인했고,
추측으로 채우는 대신 **세 게임 공통 규격을 새로 정의**했습니다 (STEP 10.5).
`NetworkString<_32>` 는 이 프로젝트에 이미 쓰이는 타입이므로 그것도 추측이 아닙니다.

### 21.3 결정은 아니지만 사용자가 주셔야 할 것

```text
· 봉헌 인벤토리 HUD 이미지   → STEP 6 선행
· 섬 회복도 HUD 이미지       → STEP 9 선행
· 시연용 target_offering 값   → STEP 1 이후 아무 때나 (DB 에서 UPDATE 한 줄, 4.2.2절)
```

### 21.4 확정 이후 새로 생긴 과제

| 과제 | 상태 | 문서 위치 |
|---|---|---|
| 목표 1000 은 4인 파티가 약 21시간 플레이해야 도달한다 | **해결됨** — `target_offering` 이 DB 컬럼이라 시연 때 낮춘다 | 4.2.2절 |
| 미니게임 보상 지급 경로의 위조 가능성 | **완화만 됨.** 근본 해결은 Fusion 서버에 신원 부여(8.3절 B안) | STEP 11 |
| 회복 완료 후 남는 잔여 조각 (문제 A) | **의도적으로 남겨 둠.** 삭제·자동 봉헌·변환을 전부 배제했다 | 11.4.2절 |
| 회복 완료 시점을 시연 흐름 끝에 맞추는 일 | **운영 과제.** `target_offering` 을 리허설하며 조정 | 11.4.11절 |
| `ChatFocus` 이름이 더 이상 정확하지 않다 | 의도적으로 둠. `GameplayInputLock` 으로 바꾸는 것은 3파일짜리 후속 작업 | 6.4.1절 |

### 21.5 검토로 바로잡은 것 (2026-09-20)

초안을 검토한 결과 아래 항목을 고쳤습니다. **STEP 1 을 시작하기 전에 고쳐야 했던 것들**입니다.

| 문제 | 초안 | 고친 뒤 |
|---|---|---|
| **보상 키를 저장할 테이블이 없었다** | `matchKey` 를 API 에만 적고 테이블을 안 만듦 | `reward_claims` 신설 (STEP 1) |
| **4인 파티에서 한 명만 보상받는 버그** | `UNIQUE (match_key)` 로 읽힐 수 있었음 | `UNIQUE (user_id, match_key)` 로 명시 |
| **멱등 응답을 구현할 수 없었다** | "그때 저장해 둔 결과를 돌려준다" | 지금 상태를 다시 조회해 `duplicate:true` 로 반환 |
| **다른 사용자의 requestId 에 걸릴 수 있었다** | `UNIQUE (request_id)` | `UNIQUE (user_id, request_id)` |
| **다른 채널이 영영 갱신 안 됨** | RPC 알림만 | + 30초 폴링 (12.5절) |
| **동시 `RequestRefresh` 와 응답 역전** | 언급 없음 | coalescing + 순번 가드 (12.5절) |
| **`ChatFocus` 재사용 시 이동 잠금이 풀림** | 그대로 쓰라고 적음 | 보유자 집합으로 교체 (6.4.1절) |
| **제단 UI 의 Esc 가 영영 안 먹힘** | `Typing == false` 조건 | `HeldByOther(this)` |
| **`target_offering = 0` 방어 없음** | 없음 | `CHECK (target_offering > 0)` + 서버 방어 |
| **VFX 진입 시 한 프레임 번쩍임** | 기존 `AltarBeam` 을 껐다 켬 | 달성 전용 오브젝트를 꺼진 채 저장 (12.2절) |
| **신규 유저의 첫 지급이 조용히 실패** | `UPDATE ... quantity + 1` (행이 있다고 가정) | 원자적 UPSERT (11.4.12절) |
| **배의 판 식별자를 추측으로 적음** | `BoardSeed` · `RunSeed` 만 적고 배는 공백 | 실제 조사 → **없음** 확인, 신규 설계 필요 명시 (STEP 11) |
| **교착 가능성을 0 으로 단정** | "교착이 생기지 않습니다" | 가능성을 낮출 뿐. 새 트랜잭션 재시도 정책 추가 (11.4.13절) |
| **반복 클리어 정책이 결정 #8 과 충돌해 보임** | "같은 게임을 다시 깨도 또 1개" | "섬 회복 진행 중이라면" 조건을 붙임 (4.2.1 · 21.1) |
| **신규 테이블 수가 3개로 남아 있었음** | `RewardClaim` 추가 후에도 "테이블 3개" | 4개로 수정, `RewardClaim.cs` 를 신규 파일 목록에 추가 (STEP 1) |
| **판 식별자가 게임마다 달랐음** | 광산 `BoardSeed` · 무쌍 `RunSeed` · 배 없음 | 세 게임 공통 `MatchInstanceId`(GUID) 로 통일 (STEP 10.5) |
| **`Tick` 기반 값이 재시작 후 겹침** | `ResultTick` 임시 사용안 | GUID 로 확정. `Tick` 기반 대안 전부 배제 (STEP 11) |
| **UPSERT 대안이 여럿 남아 있었음** | `ON DUPLICATE KEY` · `INSERT IGNORE`+`UPDATE` · EF 확장 | `ON DUPLICATE KEY UPDATE` 하나로 확정. `INSERT IGNORE` 는 첫 지급이 2가 되어 배제 (11.4.12) |
| **`FOR UPDATE` 후 조기 반환 시 잠금 해제 규칙 없음** | 규정 없음 | 조기 반환마다 트랜잭션을 먼저 종료 (11.4.14) |
| **`matchKey` 예시가 Tick 기반으로 남아 있었음** | `lobby-ch1:mining:184920371` | 실제 세션 이름 + GUID 32자로 교체 (9.2) |
| **`duplicate` 도 트랜잭션 안인 것처럼 읽힘** | 조기 반환 셋을 한 줄로 묶음 | 트랜잭션 전/후로 갈라서 서술 (11.4.14) |
| **abuse 완화가 "쿨다운 + 일일 상한" 이었음** | 상한 값 N 이 미정인 채로 남음 | **60초 쿨다운만** 적용, 일일 상한 제외 확정 (11.4.15) |
| **`REWARD_COOLDOWN` 상태 코드가 미정이었음** | "429 vs 409, 팀 취향" | **429 로 확정.** `409` 대안 삭제 (11.4.15 · 9.2) |
| **`REWARD_COOLDOWN` 을 `Unknown` 으로 처리** | 서버가 확정한 미지급이 "모름" 과 같은 화면 | `NotGrantedBecauseCooldown` 신설, 결과 상태를 **다섯 개**로 (11.4.8) |

추가로 사용자가 주셔야 할 것:

```text
· 봉헌 인벤토리 HUD 이미지        → STEP 6 선행
· 섬 회복도 HUD 이미지            → STEP 9 선행
· Lobby 씬의 나머지 MiniGamePortal 2개의 위치 (F 를 고르실 경우) → 2.4절
```

---

## 부록 A. 확인했지만 쓰지 않기로 한 것들

설계에서 검토했으나 채택하지 않은 선택지입니다. 나중에 같은 검토를 반복하지 않기 위해 남깁니다.

| 검토한 것 | 채택 안 한 이유 |
|---|---|
| `ProximityPortal` 재사용 | 입력을 안 읽는다. 게다가 Lobby 씬에 붙어 있지도 않다 (5.1) |
| ShipCoop `TaskBase` 재사용 | `IPlayerController`(IoT) 에 묶여 있다. 로비 캐릭터에 없는 컴포넌트다 (5.1) |
| Trigger Collider 로 범위 판정 | 남의 캐릭터도 들어온다. 거리 방식이 이 프로젝트의 기존 규약이다 (5.2) |
| `RewardService` 를 인벤토리로 개조 | `HashSet` 이다. 수량도 차감도 없다. 역할이 다르다 (3.3) |
| `ItemData` ScriptableObject | 아이템이 한 종류뿐이다. 배선 부담만 늘어난다 (4.3) |
| Fusion `[Networked]` 로 전역 봉헌량 | 서버 재시작 시 0, 채널마다 다름, 인벤토리 차감 불가 (8.4) |
| Fusion 서버가 API 를 호출 (B안) | 인증 파이프라인을 새로 만들어야 한다. 별도 PRD 크기 (8.3) |
| RPC 에 숫자를 실어 브로드캐스트 | 클라이언트가 조작할 수 있다. "바뀌었다" 만 나른다 (9.3) |
| `SELECT ... FOR UPDATE` / Serializable | **배제한 것이 아니라 후보 중 하나다.** 조건부 원자적 UPDATE 를 권하는 이유는 인벤토리 차감과 모양이 같아서다 (10.3) |
| 부분 수락 (`acceptedAmount` / `refundedAmount`) | 사용자가 내려 한 적 없는 수량이 소비된다. 전부 성공 또는 전부 실패 (10.3.2) |
| `altar_state.is_open` 플래그 | 수치와 어긋나는 상태가 생긴다. `remainingToTarget <= 0` 으로 판정 (11.3) |
| VFX Graph | 패키지가 없다. 추가하면 팀 전체 설정에 영향 (12.2) |
| `TMP_InputField` 로 수량 입력 | `ChatFocus` 관리 지점이 늘어난다. 버튼으로 시작한다 (6.3) |
| UI 를 `Lobby.unity` 에 배치 | 22MB 씬이고 Fusion 이 인수한다. Resources + 설치기가 관례다 (6.2) |

---

## 부록 B. 참고한 파일 목록

이 문서의 모든 주장은 아래 파일에서 확인했습니다.

**Unity — 입력 · 로비**
```text
Assets/Game/Scripts/Network/PlayerInputProvider.cs
Assets/Game/Scripts/Network/NetworkInputData.cs
Assets/Game/Scripts/Network/LocalPlayer.cs
Assets/Game/Scripts/Network/LocalPlayerView.cs
Assets/Game/Scripts/Network/LobbyChatRelay.cs
Assets/Game/Scripts/Network/NetworkPlayerIdentity.cs
Assets/Game/Scripts/Network/FusionLauncher.cs
Assets/Game/Scripts/Network/FusionNetworkService.cs
Assets/Game/Scripts/Network/PlayerSpawner.cs
Assets/Game/Scripts/Network/ChannelCatalog.cs
Assets/Game/Scripts/Network/INetworkService.cs
Assets/Game/Scripts/Lobby/MiniGamePortal.cs
Assets/Game/Scripts/Lobby/ProximityPortal.cs
Assets/Game/Scripts/UI/ChatFocus.cs
Assets/Game/Scripts/UI/LobbyChatView.cs
Assets/Game/Scripts/UI/LobbyChatInstaller.cs
Assets/Game/Scripts/UI/LobbyTutorial.cs
Assets/Game/Scripts/UI/LoginScreenController.cs
Assets/Game/Scripts/UI/StartMenuController.cs
Assets/Game/Scripts/IoT/KeyboardPlayerController.cs
```

**Unity — 보상 · 미니게임 입력**
```text
Assets/Game/Scripts/MiniGames/Common/RewardService.cs
Assets/Game/Scripts/MiniGames/Common/MiniGameConfig.cs
Assets/Game/Scripts/MiniGames/Common/MiniGameResult.cs
Assets/Game/Scripts/MiniGames/Common/MatchFlowController.cs
Assets/Game/Scripts/MiniGames/Common/UI/ResultPanelPresenter.cs
Assets/Game/Scripts/MiniGames/Mine/Net/MineInputProvider.cs
Assets/Game/Scripts/MiniGames/Mine/MineMoveInput.cs
Assets/Game/Scripts/MiniGames/Mine/MineCamera.cs
Assets/Game/Scripts/MiniGames/Warriors/Net/WarriorsInputProvider.cs
Assets/Game/Scripts/MiniGames/Warriors/Net/WarriorsPauseControl.cs
Assets/Game/Scripts/MiniGames/Warriors/UI/WarriorsDevMode.cs
Assets/Game/Scripts/MiniGames/ShipCoop/UI/ShipCoopDevMode.cs
Assets/Game/Scripts/MiniGames/ShipCoop/UI/ShipCoopTutorialView.cs
Assets/Game/Scripts/Fishing/Runtime/Infrastructure/KeyboardFishingInputSource.cs
Assets/Game/Scripts/Fishing/Runtime/Presentation/FishingDebugUI.cs
```

**Unity — 계정 · HTTP**
```text
Assets/Game/Scripts/Account/HttpApiConfig.cs
Assets/Game/Scripts/Account/HttpJson.cs
Assets/Game/Scripts/Account/HttpCharacterService.cs
Assets/Game/Scripts/Account/AccountServiceLocator.cs
Assets/Game/Scripts/Account/AccountServiceBootstrap.cs
Assets/Game/Scripts/Account/CharacterSessionCache.cs
```

**Unity — 씬 · 프리팹 · 설정**
```text
Assets/Game/Scenes/Main/CoreGames/Lobby.unity            (GUID 대조 + 직렬화 값 확인)
Assets/Game/Prefabs/HeartAltar/P_HeartAltar.prefab
Assets/InputSystem_Actions.inputactions(.meta)
ProjectSettings/ProjectSettings.asset                    activeInputHandler
ProjectSettings/ProjectVersion.txt
ProjectSettings/GraphicsSettings.asset
ProjectSettings/InputManager.asset
Packages/manifest.json
```

**서버**
```text
server/AraAtti.Api/Program.cs
server/AraAtti.Api/Data/AraAttiDbContext.cs
server/AraAtti.Api/Endpoints/CharacterEndpoints.cs
server/AraAtti.Api/Auth/ClaimsPrincipalExtensions.cs
server/AraAtti.Api/appsettings.json
server/README.md
```

**문서 · 아트**
```text
unity/UnderTheSea/CONVENTION.md
unity/UnderTheSea/GAME_STRUCTURE.md
unity/UnderTheSea/CLAUDE.local.md
docs/prd/auth-character-roadmap.md
docs/prd/fusion-dedicated-lobby-roadmap.md
art/lobby-ui/island-restoration-hud-v1.png
art/lobby-ui/island-restoration-gameplay-v3.png
```
