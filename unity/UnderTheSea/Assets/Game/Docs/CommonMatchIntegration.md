# 공통 매칭 · 결과 연동 가이드 (CommonMatchIntegration)

> **매칭 부분(포탈 연결 · PlayerRoster · 시작 조건 · Debug UI)** 은 최신 문서
> 같은 폴더의 `MatchingIntegration.md` 를 먼저 보세요. 이 문서는 결과 · 보상 흐름 위주입니다.

세 미니게임(검 · 광산 · 배)이 같이 쓰는 매칭 → 카운트다운 → 결과 → 보상 흐름입니다.
**연결할 때 알아야 할 함수와 파일만** 적었습니다.

- **드롭인 프리팹: `Assets/Game/Prefabs/MiniGames/Common/CommonMatchCanvas.prefab`**
- 코드: `Assets/Game/Scripts/MiniGames/Common/`
- 테스트 씬: `Assets/Game/Scenes/Develop/SeoYeon/CommonMatchResultTest.unity`
- 설정 에셋: `Assets/Game/ScriptableObjects/MiniGames/Common/MiniGame_{Sword,Mining,Ship}.asset`

씬은 `Tools ▸ 아라아띠 ▸ 공통 매칭·결과 테스트 씬 만들기` 로 언제든 다시 만들 수 있습니다
(`Editor/CommonMatchSceneBuilder.cs`).

---

## 0. 각 미니게임 Scene에 붙이는 방법 (6단계)

1. `CommonMatchCanvas.prefab` 을 씬에 끌어다 놓습니다. UI 와 Systems 가 한 덩어리로 들어갑니다.
2. `Systems/MatchFlow` 의 **Config** 에 그 게임의 `MiniGameConfig` 를 넣습니다.
3. `Systems/MatchFlowController` 의 **Suppress Scene Load** 를 **끕니다**(테스트용 기본값이 켜져 있음).
4. `Systems/SceneTransitionService` 의 **Stub Only** 를 끄고 씬 이동을 연결합니다 (§8).
5. 씬에 빈 오브젝트를 놓고 `PlayerSpawnPoint` 를 붙여 `Index` 를 0부터 매깁니다.
   검 `0~1` · 광산 `0~3` · 배 `0~3`.
6. 게임이 끝나는 곳에서 `controller.CompleteMiniGame(result)` 한 번 부릅니다 (§9).

> UI 를 복사해서 게임마다 고치지 마세요. 프리팹 하나를 세 게임이 같이 씁니다.
> 화면 문구·슬롯 수·시작 조건은 전부 `MiniGameConfig` 값만 보고 바뀝니다.

## 1. Match UI 시작 방법

프리팹 안에 이미 다 들어 있습니다.

| 오브젝트 | 컴포넌트 | 하는 일 |
|---|---|---|
| `Systems/MatchFlow` | `MatchFlow` | 시작 조건 판단 · 카운트다운 |
| `Systems/MatchFlowController` | `MatchFlowController` | **바깥에서 부르는 창구** |
| `Systems/SceneTransitionService` | `SceneTransitionService` | 씬 열기 (지금은 스텁) |
| `Canvas/MatchPanel`, `Canvas/ResultPanel` | `MatchPanelPresenter`, `ResultPanelPresenter` | 그리기만 |

`MatchFlow.config` 에 미니게임 설정을 넣으면 그 게임의 매칭 화면이 됩니다.
포탈에서 게임을 고를 때는 `MatchFlow.Configure(config)`.

## 2. MiniGameConfig 등록 방법

`아라아띠/미니게임 설정` 으로 새 에셋을 만들고 채웁니다. **인원 규칙을 코드에 적지 마세요.**

| 필드 | 검 | 광산 | 배 |
|---|---|---|---|
| `MinPlayers` | 1 | 1 | 4 |
| `MaxPlayers` | 2 | 4 | 4 |
| `RequireFullParty` | false | false | **true** |
| `AutoStartSeconds` | 10 | 10 | (안 씀) |
| `ExtraStatLabel` | 몬스터 처치 | 채굴량 | 항해 점수 |
| `FragmentId` | sword | mining | ship |
| `SceneName` | (비움) | (비움) | (비움) |

> `SceneName` 은 Build Profiles 의 Scene List 에 등록한 뒤 채웁니다.
> 비어 있으면 씬을 열지 않고 로그만 남깁니다 — 없는 이름을 적어 두는 것보다 안전합니다.

## 3~5. Player Join / Leave / Ready

`PlayerRoster.cs` — **static, 씬을 넘어가도 유지**. Photon/Fusion 을 전혀 모릅니다.
네트워크 콜백에서 아래만 부르면 화면·시작 조건이 따라옵니다. **UI 코드는 건드릴 것이 없습니다.**

```csharp
PlayerRoster.RegisterPlayer(playerId, displayName, isLocal, characterPresetId, isReady);
PlayerRoster.UnregisterPlayer(playerId);
PlayerRoster.SetReady(playerId, ready);
PlayerRoster.SetConnectionState(playerId, ConnectionState.Disconnected);
```

`PlayerEntry` 가 가진 것: `PlayerId` `DisplayName` `IsLocal` `IsReady` `CharacterPresetId` `ConnectionState`

## 6. ActivePlayerCount 읽는 방법

```csharp
PlayerRoster.ActivePlayerCount   // 방에 있는 사람
PlayerRoster.ReadyCount          // 준비를 마친 사람 (시작 조건이 보는 값)
PlayerRoster.All                 // 순회용
PlayerRoster.AtSlot(i)           // i 번 슬롯
```

## 7. Countdown 시작 조건

조건식은 `MatchFlow.CanStartMatch()` **한 곳에만** 있습니다. UI 는 이것만 물어봅니다.

- 검 · 광산 — `ReadyCount >= MinPlayers` 이면 시작 가능, 버튼 또는 `AutoStartSeconds` 뒤 자동
- 배 — `ReadyCount == MaxPlayers` 인 순간 **버튼 없이 자동** 카운트다운

중복 시작 방어(이미 되어 있음):
- `Countdown`/`Starting` 상태에서는 `RequestStart()` 가 무시됩니다
- `LaunchRequested` 는 한 판에 **한 번만** 올라옵니다 (`RequestStart` 20회 → 1회 발생 확인)
- 카운트다운 중 인원이 빠져 조건이 깨지면 자동으로 `Matching` 복귀

서버가 시간의 주인이면 `MatchFlow.OverrideCountdown(남은초)` 로 덮어쓰면 됩니다.

## 8. MiniGame Scene 이동 연결 지점

`SceneTransitionService.cs` — **여기 세 함수 안쪽만 채우면 됩니다.**

```csharp
LoadMiniGame(sceneName)   // → SceneFlow 로 미니게임 씬 열기
ReloadMiniGame()          // → SceneFlow.RestartCurrent()  (이미 있음)
LoadLobby()               // → SceneFlow 에 로비로 가는 public 함수가 필요 (현재 없음)
```

- 지금은 `stubOnly = true` 라 로그만 남깁니다. 연결 후 끄세요.
- 로비 씬 이름은 `lobbySceneName` 필드에 있습니다(하드코딩 아님).
- 테스트 씬은 `MatchFlowController.suppressSceneLoad = true` 라 씬을 열지 않고 가짜 한 판을 돌립니다.

> ⚠ 프로젝트 규칙상 씬 전환은 `SceneFlow`(민화) 담당입니다(GAME_STRUCTURE.md 3장).
> 그래서 여기서 `SceneManager.LoadScene` 을 직접 부르지 않았습니다.
> **`SceneFlow` 에 로비로 가는 public 함수 추가가 필요합니다 — 아직 없습니다.**

## 9. Result 데이터 전달 방법

각 미니게임은 **이 함수 하나만** 부르면 됩니다. 결과 화면 계층을 찾을 필요가 없습니다.

```csharp
controller.CompleteMiniGame(new MiniGameResult(
    gameId, isClear, score, playTime,
    extraStatLabel, extraStatValue,
    rewardId, fragmentObtained: false, playerCount));

// 짧은 길 — 라벨·보상id·인원은 설정에서 알아서 채웁니다
controller.CompleteMiniGame(isClear: true, score: 5200, playTime: 82f, extraStatValue: "14");
```

`MiniGameResult` 필드: `GameId` `IsClear` `Score` `PlayTime` `ExtraStatLabel` `ExtraStatValue`
`RewardId` `FragmentObtained` `PlayerCount`

> 결과 UI 의 Text 를 직접 고치지 마세요. UI 는 이 구조체를 **읽기만** 합니다.

## 10. Replay / Lobby 함수

```csharp
controller.ReplayCurrentMiniGame();   // 인원 유지, 같은 게임 다시
controller.ReturnToLobby();           // 파티 비우고 로비로
```

결과 화면의 두 버튼이 이미 여기에 연결돼 있습니다.

## 11. Reward 연결 지점

`RewardService.cs` — 적립은 여기가 합니다. **UI 는 보상 상태를 소유하지 않습니다.**

```csharp
RewardService.Grant(fragmentId);   // 처음이면 true, 이미 있으면 false
RewardService.Has(fragmentId);
RewardService.OwnedCount;
RewardService.RecoveryRatio;       // 0~1, 로비 회복 게이지용
RewardService.LoadFrom(ids);       // ← 로그인 직후 서버 보유 목록으로 덮어쓰기
```

중복 적립은 `MatchFlowController.CompleteMiniGame` 에서 한 번만 부르도록 막혀 있습니다
(같은 게임 두 번 클리어 → 보유 1→1 확인).
지금은 **런타임 메모리에만** 남습니다. 서버 저장은 위 함수 안쪽만 교체하면 됩니다.

## 12. CharacterData 연결 위치

```csharp
PlayerRoster.RegisterPlayer(id, name, isLocal, characterPresetId: "preset_01");
PlayerRoster.SetCharacterPreset(id, presetId);
entry.CharacterPresetId        // 스폰할 때 읽는 값
```

실제 외형 동기화는 아직 구현하지 않았습니다. 데이터 자리만 준비돼 있습니다.

## 미니게임 씬 쪽에서 할 일

1. `MiniGameConfig` 를 `MatchFlow.config` 에 연결
2. 씬에 `PlayerSpawnPoint` 를 놓고 `index` 를 0부터 부여
   - 검 `0~1` · 광산 `0~3` · 배 `0~3`
   - `PlayerSpawnPoint.For(i)` / `AllInScene()` / `Validate(인원수)` 로 읽고 검사
3. 게임이 끝나면 `controller.CompleteMiniGame(result)`
4. 필요하면 `PlayerRoster.ActivePlayerCount` 읽기

## 서버 / Fusion 담당자가 교체할 Stub

| 위치 | 지금 | 해야 할 일 |
|---|---|---|
| `SceneTransitionService.LoadMiniGame/ReloadMiniGame/LoadLobby` | 로그만 | SceneFlow · 네트워크 씬 로드 연결 |
| `SceneFlow` | 로비 함수 없음 | 로비로 가는 public 함수 추가 |
| `RewardService.Grant/Has/LoadFrom` | 메모리 | 서버 저장/조회 |
| `PlayerRoster.RegisterPlayer/UnregisterPlayer/SetReady` | 테스트 버튼이 호출 | Fusion 콜백이 호출 |
| `MatchFlow.joinLocalPlayerOnStart` | true | 네트워크가 넣어 주면 false |
| `MatchFlowController.suppressSceneLoad` | true (테스트) | 실제 씬에서는 false |
| `MatchResultTestRig` | 테스트 조작 줄 | 실제 씬에서는 **올리지 않음** |

## 디버그 UI

테스트 조작 줄(+Player / 게임 3개 / 조각 초기화)은 **프리팹 바깥**, 테스트 씬의 `Debug` 루트에만
있습니다. 미니게임 씬에 프리팹을 놓아도 따라오지 않습니다.

테스트 씬에서는 `MatchResultTestRig.showDebugControls` 로 켜고 끄며 **F1** 로도 여닫습니다.
공통 코드는 이 파일을 참조하지 않으므로 지워도 시스템은 그대로 돕니다.

## 화면 규격 (참고)

- 1920×1080 기준 제작. Canvas Scaler = Scale With Screen Size, match 0.5
- 1600×900 실측: 패널이 0.833배로 통째로 줄고 중앙 정렬 유지, 겹침·화면 밖 없음
- 매칭 패널 1240×840 / 결과 패널 900×830, 요소 간 세로 간격 22px 로 통일
- 카운트다운 **5초**, 자동 시작 대기 **5초**. 화면에는 5 → 4 → 3 → 2 → 1 이 나옵니다.
- 화면은 세 칸 고정 — Header(제목 + StatusArea) / SlotArea / ActionArea.
  상태가 바뀌어도 **파티원 줄과 버튼 칸은 움직이지 않고 StatusArea 내용만 교체**됩니다
  (매칭 = 인원/타이머 한 줄, 카운트다운 = 큰 숫자). 슬롯 Y 동일 실측 확인.
