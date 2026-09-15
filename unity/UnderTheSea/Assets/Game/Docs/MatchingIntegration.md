# 공통 매칭 연동 가이드 (MatchingIntegration)

검 · 광산 · 배 세 미니게임이 **같이 쓰는** 매칭 화면과 매칭 로직입니다.
UI 프리팹 하나, 코드 한 벌을 세 게임이 재사용하고, 게임마다 다른 것은 `MiniGameConfig` 값뿐입니다.

| 무엇 | 어디 |
|---|---|
| 드롭인 프리팹 | `Assets/Game/Prefabs/MiniGames/Common/CommonMatchCanvas.prefab` |
| 코드 | `Assets/Game/Scripts/MiniGames/Common/` |
| 설정 에셋 | `Assets/Game/ScriptableObjects/MiniGames/Common/MiniGame_{Sword,Mining,Ship}.asset` |
| 테스트 씬 | `Assets/Game/Scenes/Develop/SeoYeon/CommonMatchResultTest.unity` |
| 테스트 씬 배경 | `Assets/Game/Prefabs/Environment/CustomizationBeachBackdrop.prefab` — 캐릭터 커스터마이징 화면의 섬·해변·카메라·조명을 그대로 묶은 것 |
| 씬/프리팹 빌더 | `Editor/CommonMatchSceneBuilder.cs` → 메뉴 `Tools ▸ 아라아띠 ▸ 공통 매칭·결과 테스트 씬 만들기` |

> 결과·보상 화면과 Photon/Fusion 실제 연결은 이 브랜치 범위 밖입니다. 아래는 매칭까지만 다룹니다.
> 결과 화면 · 보상(`CompleteMiniGame`, `RewardService`)은 같은 폴더의 `CommonMatchIntegration.md` 를 보세요.

---

## 1. 미니게임 포탈에서 매칭 화면 열기

프리팹을 씬에 **하나** 놓고, 포탈은 자기 설정만 넘깁니다. 게임마다 UI 를 복사하지 않습니다.

```csharp
using MiniGames.Common;

// 검 포탈
CommonMatchingUI.Current.Show(swordConfig);    // 1~2인, 혼자 시작 가능

// 광산 포탈
CommonMatchingUI.Current.Show(miningConfig);   // 1~4인, 혼자 시작 가능

// 배 포탈
CommonMatchingUI.Current.Show(shipConfig);     // 4인 고정, 다 모이면 자동 시작
```

- `CommonMatchingUI` 는 프리팹 루트에 붙어 있고 `Current` 로 찾거나 인스펙터 참조로 잡습니다.
- `Show(config)` 한 번이면 슬롯 수(2 or 4)·시작 조건·안내 문구가 그 게임 것으로 바뀝니다.
- 포탈에서 열 거라면 `CommonMatchingUI.showOnStart` 를 **끄고** `Hide()` 로 닫습니다.
- 프리팹 안 `Systems/MatchFlow.config` 에 기본 설정을 넣어 두면 `Show` 없이도 그 게임 매칭이 됩니다
  (미니게임 씬 안에 프리팹을 두는 경우).

### 실제 씬에 붙일 때 바꿀 값 (테스트용 기본값이 켜져 있음)

| 위치 | 필드 | 테스트 | 실제 |
|---|---|---|---|
| `Systems/MatchFlowController` | `suppressSceneLoad` | true | **false** |
| `Systems/MatchFlow` | `joinLocalPlayerOnStart` | true | 네트워크가 사람을 넣으면 **false** |
| `Systems/SceneTransitionService` | `stubOnly` | true | 씬 이동 연결 후 **false** |
| `Systems/SceneTransitionService` | `lobbySceneName` | "Lobby" | 실제 로비 씬 이름 |
| 씬의 `Debug/MatchDebugControls` (`MatchResultTestRig`) | — | 있음 | **놓지 않음** |

## 2. MiniGameConfig — 게임별 차이는 여기만

`아라아띠/미니게임 설정` 메뉴로 만드는 ScriptableObject 입니다 (`MiniGameConfig.cs`).
**인원 규칙을 코드에 if 로 적지 마세요.** UI·흐름은 이 값만 읽습니다.

| 필드 | 검 `MiniGame_Sword` | 광산 `MiniGame_Mining` | 배 `MiniGame_Ship` |
|---|---|---|---|
| `GameId` | Sword | Mining | Ship |
| `DisplayName` | 검 미니게임 | 광산 미니게임 | 배 협동 미니게임 |
| `SceneName` | (비움) | (비움) | (비움) |
| `MinPlayers` | 1 | 1 | 4 |
| `MaxPlayers` | 2 | 4 | 4 |
| `RequireFullParty` | false | false | **true** |
| `AutoStartDelay` (`autoStartSeconds`) | 30 | 30 | 30 |

- `SceneName` 은 Build Profiles 의 Scene List 에 등록한 뒤 채웁니다. 비어 있으면 씬을 열지 않고 로그만 남깁니다.
- `AutoStartDelay` 는 시작할 수 있게 된 뒤 자동으로 카운트다운에 들어가기까지의 시간입니다. 화면에 "30초 후 자동 시작" 으로 보입니다.
- 화면 문구도 설정이 만듭니다: `DisplayName` 이 상단 작은 게임 이름, `StartBlockedHint()` 가 배의
  `"4명의 플레이어가 모두 모여야 시작할 수 있습니다."` 한 줄입니다. 인원 숫자는 화면에 적지 않습니다.

## 3. PlayerRoster — 네트워크가 부를 진입점

`PlayerRoster.cs` — **static**, 씬을 넘어가도 유지. Photon/Fusion 을 전혀 모릅니다.
프리팹의 `Systems/PlayerRoster` (`PlayerRosterHost`) 는 같은 함수를 인스턴스로 열어 둔 것이고 인스펙터에 명단을 보여 줍니다.

```csharp
PlayerRoster.RegisterPlayer(playerId, displayName, isLocal, characterPresetId, isReady);  // Player Joined
PlayerRoster.UnregisterPlayer(playerId);                                                  // Player Left
PlayerRoster.SetPlayerReady(playerId, ready);                                             // Ready Sync
PlayerRoster.SetConnectionState(playerId, ConnectionState.Disconnected);                  // 연결 끊김/복구
PlayerRoster.ClearPlayers();                                                              // 방 해산
```

읽기: `ActivePlayerCount` `ReadyCount` `All` `AtSlot(i)` `Local` `ForId(id)`

`PlayerEntry`: `PlayerId` `DisplayName` `IsLocal` `IsReady` `CharacterPresetId` `ConnectionState`

- 같은 `playerId` 로 다시 `RegisterPlayer` 하면 새 사람으로 세지 않고 갱신합니다 (재접속 안전).
- 테스트 씬의 `+ Player / - Player / 준비 토글` 버튼이 **정확히 위 함수들**을 부릅니다.
  Fusion 콜백이 같은 함수를 부르면 UI 는 손댈 것이 없습니다.

## 4. 시작 조건 · 자동 시작 · 카운트다운

조건식은 `MatchFlow.CanStartMatch()` **한 곳**에만 있습니다 (`MiniGameConfig.CanStart(readyCount)` 를 봅니다).
시작은 두 길입니다 — 유저가 [게임 시작] 을 누르거나, 시작할 수 있게 된 뒤 **30초**가 지나면 자동으로.
세 게임 모두 같은 규칙이고, 인원이 바뀌면 30초를 다시 잽니다. 서버는 인원만 넘기면 됩니다.

| 게임 | [게임 시작] 활성 · 자동 시작 시계 | 안내 문구 |
|---|---|---|
| 검 · 광산 | `ReadyCount >= 1` (MinPlayers) 이면 활성, 제목 아래 "30초 후 자동 시작" | — |
| 배 | `ReadyCount == 4` (MaxPlayers) 이면 활성 + 30초 시계 | 4명 미만이면 버튼 위에 "4명의 플레이어가 모두 모여야 시작할 수 있습니다." |

카운트다운은 `MatchFlow.countdownSeconds` = **5초**, 화면에 5 → 4 → 3 → 2 → 1 → "시작!".
제목은 매칭 중 "플레이어를 매칭 중입니다" + 튀는 점 셋, 카운트다운 중 "게임이 곧 시작됩니다" 로 바뀌고,
"30초 후 자동 시작" 줄이 있던 자리에 큰 숫자가 들어오며 그 줄은 사라집니다.

> 자동 시작을 끄려면 `Systems/MatchFlow.autoStart` 를 false 로. 버튼으로만 시작하게 됩니다.

상태: `MatchState` = `Idle` `Matching` `Countdown` `Starting` (`InGame` `Result` 는 결과 흐름용)

중복 방어 (이미 되어 있음):
- `Countdown`/`Starting` 중 `RequestStart()` 무시
- `LaunchRequested` 는 한 판에 한 번
- `MatchFlowController.StartGame()` 은 `HasStarted` 로 한 판에 한 번만 실행
- 카운트다운 중 인원이 빠져 조건이 깨지면 `Matching` 복귀

### 서버 카운트다운으로 바꾸기

지금은 각 클라이언트가 스스로 셉니다. 서버 시간이 들어오면:

```csharp
flow.OverrideCountdown(remainingSeconds);   // 화면 숫자만 서버를 따라간다
controller.StartGame(config);                // 서버가 "지금 시작" 을 내리면 직접 호출 (두 번 불려도 한 번만)
```

## 5. 게임 시작 · 매칭 취소 — 진입 함수

UI 버튼은 `SceneManager.LoadScene` 을 부르지 않습니다. 진입점은 아래 셋뿐입니다.

```csharp
controller.RequestStart();    // [게임 시작] — 조건 맞으면 카운트다운
controller.CancelMatch();     // [매칭 취소] — 파티 비우고, returnToLobbyOnCancel 이면 로비로
controller.StartGame(config); // 카운트다운 끝 → 자동 호출. 씬 이동의 유일한 진입점
```

`StartGame` 안에서 씬을 여는 곳은 `SceneTransitionService.LoadMiniGame(sceneName)` **한 줄**입니다.

### Network Scene Load 로 교체할 위치

`SceneTransitionService.cs` — 세 함수 안쪽만 채우면 됩니다. 지금은 `stubOnly = true` 라 로그만 남깁니다.

```csharp
LoadMiniGame(sceneName)   // → SceneFlow / Fusion NetworkSceneManager 로 미니게임 씬 열기
LoadLobby()               // → 로비로 (lobbySceneName 필드, 하드코딩 아님)
ReloadMiniGame()          // → 다시 하기 (결과 흐름용)
```

> 프로젝트 규칙상 씬 전환은 `SceneFlow` 담당입니다. 그래서 여기서 `SceneManager.LoadScene` 을 직접 부르지 않았습니다.

## 6. CharacterData 연결 위치

```csharp
PlayerRoster.RegisterPlayer(id, name, isLocal, characterPresetId: "preset_01");
PlayerRoster.SetCharacterPreset(id, presetId);
entry.CharacterPresetId   // 스폰할 때 읽는 값
```

슬롯 카드의 초상은 `MatchSlotView.portrait` (Image) 하나입니다. 외형을 반영하려면
`MatchSlotView.ShowMember()` 에서 `entry.CharacterPresetId` 로 스프라이트를 고르는 한 줄만 추가하면 됩니다.
지금은 자리 번호별 고정 초상을 씁니다.

## 7. 화면 구조 (프리팹 안)

```
CommonMatchUI                   CommonMatchingUI  ← Show(config) / Hide()
├── Systems
│   ├── MatchFlow               시작 조건 · 자동 시작 · 카운트다운
│   ├── SceneTransitionService  씬 이동 (스텁)
│   ├── MatchFlowController     RequestStart / CancelMatch / StartGame
│   └── PlayerRoster            PlayerRosterHost — 명단 보기 · 진입 함수
└── Canvas                      Scale With Screen Size 1920×1080, match 0.5
    ├── MatchPanel              MatchPanelPresenter
    │   ├── HeaderArea          GameTitle("광산 미니게임", 작게·금색) + Title("플레이어를 매칭 중입니다")
    │   ├── HeaderArea          Dot1~3 — 제목 뒤에서 튀는 점 (제목 글자는 고정)
    │   ├── StatusArea          MatchStatus(AutoStartText "30초 후 자동 시작") / CountdownStateArea(Number)
    │   ├── PlayerSlotArea      Slot_1 ~ Slot_4 (MatchSlotView, MaxPlayers 만큼만 켜짐)
    │   └── ActionArea          StartHint(배만) + StartButton / CancelButton
    └── ResultPanel             (결과 흐름 — 이 브랜치 범위 밖)
```

- 매칭 ↔ 카운트다운은 **같은 판**입니다. 제목 문구, `StatusArea` 의 숫자, `ActionArea` 표시만 바뀌고
  `PlayerSlotArea` 는 Y · 크기 · 간격이 고정입니다 (컨테이너가 분리돼 있어 밀리지 않음).
- 상태 → 화면 On/Off 는 `MatchPanelPresenter.OnStateChanged()` 한 곳에서만 정합니다.
- 카드 상태 배지는 READY / WAIT 두 단어입니다: 참가+준비 READY, 참가+미준비 WAIT(노란 점), 빈 자리 "매칭 중..." + WAIT. 내 카드는 이름 뒤 "(나)" + 금색.
- 매칭 중 제목 뒤 점 세 개가 차례로 튑니다 (`MatchPanelPresenter.TickWaitingDots`). 점은 별도 오브젝트라 제목 글자는 움직이지 않습니다.

## 8. Debug UI (테스트 씬에만)

`CommonMatchResultTest.unity` 의 `Debug/MatchDebugControls` (`Debug/MatchResultTestRig.cs`).
**프리팹 바깥**에 있어서 미니게임 씬에 프리팹을 놓아도 따라오지 않습니다.
설정 세 개는 씬의 `Systems/MiniGameConfigProvider` 에서 읽습니다.

| 버튼 | 부르는 것 |
|---|---|
| 검 1~2인 / 광산 1~4인 / 배 4인 | `ClearPlayers()` → `CommonMatchingUI.Show(config)` |

씬에는 위 세 버튼만 달려 있습니다. 인원을 넣고 빼는 것은 Play Mode 에서 `PlayerRoster` 함수를 직접 부르거나
(`PlayerRoster.RegisterNextTestPlayer()`, `UnregisterPlayer(id)`, `SetPlayerReady(id, ready)`),
`MatchResultTestRig` 의 `addPlayerButton` / `removePlayerButton` / `toggleReadyButton` / `resetFragmentsButton` 에
버튼을 다시 연결하면 됩니다 — 리그 코드는 그대로 있습니다.

- `showDebugControls` 로 켜고 끕니다. **F1** 로도 여닫습니다. 실제 게임 연결 시 기본값 false.
- 이 버튼들은 설정과 명단 동작을 확인하는 용도입니다. **실제 게임에서 유저는 이 버튼으로 게임을 고르지 않습니다** —
  각 포탈이 자기 설정을 `Show()` 에 넘깁니다.
- 테스트 씬은 씬을 열지 않으므로 "시작!" 뒤 가짜 한 판(1.6초)을 돌리고 결과 화면으로 넘어갑니다.
  결과 화면의 [다시 하기] 로 매칭 화면으로 돌아옵니다.

## 9. 서버 / Fusion 담당자가 연결할 것 — 요약

| 할 일 | 함수 / 위치 |
|---|---|
| Player Join | `PlayerRoster.RegisterPlayer(...)` |
| Player Leave | `PlayerRoster.UnregisterPlayer(id)` |
| Ready Sync | `PlayerRoster.SetPlayerReady(id, ready)` |
| 연결 상태 | `PlayerRoster.SetConnectionState(id, state)` |
| 서버 카운트다운 | `MatchFlow.OverrideCountdown(sec)` / `MatchFlowController.StartGame(config)` (시작 자체는 유저 버튼 → `RequestStart`) |
| Network Scene Load | `SceneTransitionService.LoadMiniGame / LoadLobby` 안쪽, `stubOnly = false` |
| Character 정보 | `PlayerEntry.CharacterPresetId`, `MatchSlotView.ShowMember` |
| 로컬 자동 입장 끄기 | `MatchFlow.joinLocalPlayerOnStart = false` |

UI 코드(`MatchPanelPresenter`, `MatchSlotView`)는 건드릴 것이 없습니다.
