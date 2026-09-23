# 무쌍 게임 (Warriors) 규격

담당: 서연
· 네트워크 씬: `Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity` (부트 `WarriorsBoot.unity`)
· 1인 검증 씬: `Assets/Game/Scenes/Develop/SeoYeon/WarriorsTest.unity`

해변으로 몰려오는 바다 몬스터를 **종류에 맞는 공격**으로 베어 넘기고,
마지막에 크라켄을 쓰러뜨리는 3라운드 전투 게임입니다.

이 문서에는 **다른 사람이 연결할 때 알아야 하는 것만** 적습니다.
전투 규칙과 화면 구성은 코드와 프리팹을 보세요.

---

## 1. 미니게임 공통 규격

`GAME_STRUCTURE.md` 8장이 요구하는 두 가지입니다.

| 항목 | 값 |
| --- | --- |
| 인원 | **1~2명** — 협동은 "없으면 못 깸"이 아니라 "같이 하면 더 빠르다". 상한은 `WarriorsPlayers.Max` 한 곳에서 정합니다 |
| 끝날 때 돌려주는 것 | `WarriorsResult(성공여부, 점수)` — `WarriorsGameFlow.Completed` 이벤트 |

로비 쪽에서는 이 이벤트만 받으면 됩니다.

> ⚠ 예전에는 **4명 고정**이었습니다. 촉수 4개가 상시 떠 있고 협동 마무리가 네 명을
> 전제했기 때문입니다. 그 구조가 패턴 등장 방식으로 바뀌어 전제가 없어졌습니다.
> **로비의 매칭 인원도 같이 조정되어야 합니다.**

---

## 2. 라운드와 공격

| 라운드 | 목표 |
| --- | --- |
| 1 · 해변 방어 | 몬스터 처치. 목표는 **인원에 따라** 늘어납니다 (1인 30 · 2인 50 — `WarriorsRoundGoals` 도구가 씬에 박는 값) |
| 2 · 촉수 절단 | 촉수 성공 14회 (22 는 실측에서 길었음). 베는 대상은 크라켄 머리의 실제 팔 4개 — 9.2절 |
| 3 · 최후의 일격 | 크라켄과 리듬 전투. 한 사람당 14, 즉 2인이면 28 (16 은 실측에서 조금 길었음) |

제한 시간은 **판 전체에 하나**로 240초입니다. 라운드마다 따로 세지 않습니다.
수치는 모두 씬의 `WarriorsMatchState` 에 있습니다.

공격은 3종이고 몬스터마다 통하는 것이 하나로 정해져 있습니다.

| 몬스터 | 공격 | 키보드(검증용) |
| --- | --- | --- |
| 물고기 | 가로베기 | `J` |
| 게 | 세로베기 | `K` |
| 해파리 | 찌르기 | `L` |

> 키보드 `J` `K` `L` 은 IoT 없이 검증하기 위한 임시 입력입니다.
> 실제 입력은 IoT 검에서 들어옵니다 — `WarriorsIoTInput.OnSwing(playerId, ...)`,
> 계약은 `Scripts/MiniGames/Warriors/AI_INPUT_CONTRACT.md`.

나머지 조작은 네 미니게임이 함께 지키는 표를 따릅니다. (`IOT_INPUT.md` 1장)

| | |
| --- | --- |
| `W` `A` `S` `D` | 이동 |
| 마우스 우클릭 드래그 | 카메라 |
| `Shift` | 회피 |

> ⚠ 예전에는 공격이 `1` `2` `3`(과 숫자 키패드 · `F` · `X`), 카메라가 방향키였습니다.
> `IOT_INPUT.md` 7장에 따라 옮겼습니다. **옛 키는 모두 지웠습니다** — 같은 동작에 키가
> 여럿이면 어느 것이 규격인지 알 수 없고, `F` 는 로비에서 포탈 입장이라 뜻이 겹쳤습니다.

---

## 3. 점수는 무엇으로 오르는가

**점수는 팀 하나입니다.** 누가 쳤든 같은 숫자에 더해집니다. 값은 모두 씬의
`WarriorsMatchState` 와 각 Director 인스펙터에 있습니다.

| 언제 | 점수 | 어디서 | 필드 |
| --- | --- | --- | --- |
| 1R 몬스터 처치 | **+100** | `WarriorsMatchState.ReportPhase1Kill` | `killScore` |
| 2R 촉수 절단 | **+200** | `WarriorsMatchState.ReportPhase2Hit` | `tentacleScore` |
| 2R 협동 세트 | **+350** | `WarriorsPhase2Director` | `comboSetScore` |
| 3R 노트 정타 | **+150** | `WarriorsMatchState.ReportPhase3Hit` | `rhythmScore` |
| 3R 콤보 피니시 | **+500 × 인원** | `WarriorsPhase3Director` | `finisherScore` |

**협동 세트**는 두 사람이 정해진 창 안에서 촉수를 이어 자를 때 한 번 들어옵니다.
**콤보 피니시**는 3라운드에서 피니시를 성공한 사람 수만큼 곱해집니다.

시간이나 남은 HP 로는 점수를 주지 않습니다. 실패한 판에서도 그때까지 번 점수는 남습니다.

### 콤보

연속으로 **맞힌 몬스터 수**입니다. 한 번 휘둘러 셋을 베면 3이 오릅니다.

| 끊기는 때 | |
| --- | --- |
| 헛쳤을 때 | 아무것도 못 맞히면 그 자리에서 0 |
| 맞았을 때 | HP 가 줄면 0 |
| 3초 동안 못 맞혔을 때 | `comboResetSeconds` |
| 쓰러졌을 때 | 0 |

> ⚠ **콤보는 지금 점수를 늘리지 않습니다.** 배수(5연타 1.2배 · 10연타 1.5배 · 20연타 2배)가
> `WarriorsComboSystem` 에 있지만 그것은 싱글 씬의 `WarriorsBattleScore` 에만 걸려 있고,
> 네트워크 점수(`WarriorsMatchState.Score`)에는 붙어 있지 않습니다.
> **화면에 숫자만 오르고 보상은 없는 상태입니다.** 붙일지 말지는 밸런스 결정이라 비워 둡니다.

> ⚠ 콤보는 **사람마다 따로** 셉니다. 서버가 세어 `WarriorsNetPlayerCombat.Combo` 로
> 복제하고, HUD 는 자기 캐릭터 것만 읽습니다. 예전에는 서버에서만 계산돼
> **클라이언트 화면의 COMBO 가 늘 0** 이었습니다.

---

## 4. 서버 연동 ⚠ 네트워크 담당자가 볼 부분

**Fusion 데디케이티드 서버로 이미 돌아갑니다.** 네트워크 씬은
`Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity` 이고, 부트 씬은 `WarriorsBoot.unity` 입니다.

판정은 **전부 서버 권위**입니다. 클라이언트는 그리기만 합니다.

| 부품 | 하는 일 |
| --- | --- |
| `WarriorsMatchState` | 판 전체 상태 — 페이즈 · 목표 · 점수 · 제한 시간 · 라운드 전환 |
| `WarriorsEnemyDirector` | 1페이즈 몬스터를 서버가 스폰 |
| `WarriorsPhase2Director` · `WarriorsPhase3Director` | 촉수 패턴 · 리듬 악보 |
| `WarriorsPlayerSpawner` | 자리 배정과 `PlayerIndex` 확정 |
| `WarriorsPlayerLife` | HP · 쓰러짐. **부활 없음** |
| `WarriorsInputProvider` | `INetworkInput` 으로 스윙을 서버에 보냄 |
| `WarriorsTelemetry` | `-telemetry` 로 켜지는 측정 로그 |

난수는 판 단위 시드(`WarriorsRun.BeginRun`)로 묶여 있어 두 사람이 같은 판을 봅니다.

> ⚠ **연출을 서버 전용 경로에서 재생하지 마세요.** `HasStateAuthority` 안에서
> 재생하면 데디케이티드 서버는 화면 요소를 전부 꺼 두므로 **아무도 보지 못합니다.**
> 번호(`[Networked]` serial)만 복제하고 각 화면이 `Render()` 에서 재생합니다 —
> `HitSerial` · `FinishSerial` · `CoopNoteSerial` 이 그 방식입니다.

---

## 5. 지금 알려진 제약

| 항목 | 상태 |
| --- | --- |
| ROUND 3 노트 레인 | `PlayerIndex` 가 같은 레인의 노트만 판정합니다 |
| 사운드 | 효과음 **18칸이 찼습니다** (8장, 전부 CC0 · 출처는 `Assets/Game/Audio/Warriors/CREDITS.md`). 아직 빈 칸은 **배경음악 4곡 · 결과 스팅어 2개 · 루프 2개**(`waveLoop` · `krakenLoop`)와 `comboUp`(1R 에서 너무 자주 나서 일부러 비움) — 그 소리만 안 납니다 |
| IoT 실기 | 장치 없이 키보드로만 검증했습니다. `inputLatencyOffset` 은 아직 0 입니다 |
| 밸런스 | 아래 목표치로 조정 중입니다. 실측은 `-telemetry` 로그로 봅니다 |

### 넣지 말아야 할 것 — 한 번 넣었다가 뺀 규칙들

| 뺀 것 | 이유 |
| --- | --- |
| **부활 · 구조** | HP 가 0 이면 그 사람의 판은 거기서 끝입니다. 남은 사람은 계속하고, 둘 다 쓰러지면 실패입니다 |
| **합동 결정타** | 3라운드 목표를 채운 뒤 "둘이 함께 치는 한 방" 을 더 요구했습니다. 크라켄 체력이 0 인데 판이 끝나지 않고 멈췄습니다 |
| **협동 게이지 · 팀 강화** | 번갈아 맞히면 차는 게이지였습니다 |
| **협동 노트** | 두 레인에 같은 박자의 노트를 하나씩 더 붙이던 규칙입니다 |

3라운드는 **목표 성공 횟수를 채우면 그대로 클리어**입니다. 위 넷을 다시 넣지 마세요.

> 검증용 씬 `Scenes/Develop/SeoYeon/WarriorsTest.unity` 는 혼자 확인하는 용도라
> 목표치를 낮춰 두었습니다. 메인 씬 값은 건드리지 않습니다.

---

## 6. 프리팹 구성

모두 `Assets/Game/Prefabs/MiniGames/Warriors/` 아래에 있습니다.

| 프리팹 | 역할 |
| --- | --- |
| `Core/WarriorsGameRoot` | 진행 · 점수 · 스포너 · 입력 라우터 |
| `Arena/WarriorsBeachArena` | 해변 무대와 스폰 지점 |
| `UI/WarriorsHUD` | HUD 전체 |
| `Boss/WarriorsKrakenBoss` | 크라켄과 촉수 |
| `Enemies/{Fish,Crab,Jellyfish}Enemy` | 몬스터 3종. 겉모습은 `Monsters/*Visual` |
| `Player/WarriorsStandalonePlayer` | 플레이어 |

씬에는 앞의 네 개만 두면 됩니다.
플레이어는 `WarriorsSceneBootstrap` 이, 몬스터는 스포너가 런타임에 만듭니다.

---

## 7. 사용 에셋

| 대상 | 위치 |
| --- | --- |
| 몬스터 · 보스 모델 5종 | `Art/MiniGames/Warriors/Models/` (Meshy 생성) |
| 하단 카드 아이콘 3종 | `Art/MiniGames/Warriors/UI/Icons/` (위 모델을 렌더링한 것) |
| 바 채움 | `Art/MiniGames/Warriors/UI/BarFill.png` |
| 하늘 | `Art/MiniGames/Warriors/Sky/WarriorsSky.mat` (Skybox/Procedural. `Warriors/하늘 밝게 맞추기` 메뉴로 다시 맞춘다) |
| 카드 프레임 | `Art/UI/CharacterCustomization/Frames/RoundedCard.png` 재사용 |
| 플레이어 검 | `Assets/ToonyTinyPeople/` 중 실사용 파일만 (`w_TH_sword`) |
| 뼈를 넣은 크라켄 2종 | `Models/KrakenFinal/KrakenFinal_Rigged.fbx` · `Models/KrakenPhase2/KrakenPhase2_Rigged.fbx` (위 Meshy 모델에 `art/tools/warriors_kraken_rig.py` 로 뼈만 추가. 9장) |

외부 에셋 출처 기록은 `ASSETS.md` 를 따릅니다.

---

## 8. 소리 — 배경음악과 효과음

구조는 `AUDIO.md` 그대로입니다. 소리를 내는 것은 **공용 허브 하나**(`Scripts/Audio/AudioHub.cs`)뿐이고,
무쌍은 "언제 무엇을" 만 정하는 **연출가**를 자기 폴더에 둡니다.

| 파일 | 하는 일 |
| --- | --- |
| `Scripts/MiniGames/Warriors/WarriorsAudio.cs` | 연출가. 판 상태를 보고 허브에 곡·효과음을 부탁한다 |
| `Scripts/MiniGames/Warriors/Editor/WarriorsAudioInstaller.cs` | 씬에 `Audio` 오브젝트를 놓고 클립을 이름으로 채운다 |
| `Assets/Game/Audio/Warriors/` | 클립 파일. 파일 이름 = 연출가의 필드 이름. 출처는 그 폴더의 `CREDITS.md` |

메뉴 `Tools/아라아띠/Warriors 소리 놓고 클립 채우기` 를 돌리면 `WarriorsNet.unity` 에 `Audio` 가 놓이고
파일 이름대로 연결됩니다. **파일이 없는 칸은 비워 둡니다 — 그 소리만 안 납니다.**
1인 검증 씬(`WarriorsTest.unity`)에는 놓지 않습니다. 거기에는 `WarriorsMatchState` 가 없습니다.

### 8.1 어떤 값을 보고 내는가 ⚠

판정은 전부 서버에서 돕니다. **서버 판정 `event Action` 에 소리를 걸면 아무도 듣지 못합니다**(4장과 같은 함정).
그래서 연출가는 **복제되는 값이 바뀐 순간**을 매 프레임 잡습니다.

| 소리 | 보는 값 |
| --- | --- |
| 베기 3종 | `WarriorsInputProvider.LocalSwing` — 내 화면의 로컬 사건이라 왕복 지연이 없다 |
| 몬스터 정타 · 콤보 | 내 `WarriorsNetPlayerCombat.Combo` 가 늘어남 (3의 배수에 닿으면 한 번 더) |
| 몬스터 처치 | `WarriorsMatchState.Phase1Kills` 가 늘어남 (팀 합산) |
| 내 피격 · 쓰러짐 | 내 `WarriorsPlayerLife.Hp` 감소 · `IsDown` |
| 촉수 타격 · 절단 | `WarriorsPhase2Director.Slots[i]` 의 `HitsLeft` · `Result` (내 `Owner` 만) |
| 마무리 창 · 협동 세트 | `FinishWindowOpenFor(내 레인)` · `ComboSetSerial` |
| 노트 정타 · 크라켄 피격 | `WarriorsPhase3Director.HitSerial` + `HitLane` + `HitStrength` |
| 노트 미스 | `Notes[i].State` 가 2 로 바뀜 (내 `Lane` 만) |
| 크라켄 포효 | `FinishSerial`(콤보 피니시, `FinishKind` 2 면 더 크게) · 3라운드 진입 |
| 라운드 전환 · 카운트다운 · 결과 | `Phase` · `Countdown` |

**효과음은 되도록 내가 한 일에만 냅니다.** 베기 · 정타 · 내 피격 · 내 촉수 · 내 노트는 내 것만입니다.
판 전체에 일어나는 일(처치 수 · 크라켄 피격 · 라운드 전환 · 결과)만 공통입니다 —
한 PC 에 클라이언트를 둘 띄우면 그 소리는 두 겹으로 들리는 것이 정상입니다.

### 8.2 곡

```text
대기 곡      bgmWaiting   사람을 기다리는 동안 · 시작 카운트다운
1R           bgmRound1
2R           bgmRound2
3R (긴장)    bgmRound3
결과         스팅어 하나(성공 stingerClear · 실패 stingerFail)만 나고 배경음악은 멈춘다
루프         waveLoop(해변, 1·2R) · krakenLoop(3R)
```

교차 페이드 1.5초로 배 협동과 맞췄습니다. 접속 직후 1.5초는 효과음을 내지 않습니다 —
서버가 쌓아 둔 값이 한꺼번에 도착해 "바뀐 순간"으로 잡히면 들어오자마자 소리가 우르르 납니다.

서버에서는 허브가 `CanHear == false` 라 아무 소스도 만들지 않고, `WarriorsServerCleanup` 이
연출가까지 꺼 둡니다.

---

## 9. 크라켄 다리 — 뼈와 움직임

3라운드 최종 크라켄은 원래 **뼈도 애니메이션도 없는 정지 메시**였습니다 (Meshy 생성).
다리가 한 번도 움직이지 않아, 정타 리액션은 HUD가 겉모습을 통째로 흔드는 것이 전부였습니다.
같은 메시에 **다리 뼈를 넣어** 다시 내보내고, 뼈를 코드로 돌립니다.

| 것 | 어디 |
| --- | --- |
| 뼈 넣는 스크립트 (블렌더 헤드리스) | `art/tools/warriors_kraken_rig.py` |
| 뼈가 든 모델 | `Art/MiniGames/Warriors/Models/KrakenFinal/KrakenFinal_Rigged.fbx` — 다리 8 × 뼈 5 + `Root` = **41개**, 정점 5,227개 전부 웨이트 있음 |
| 프리팹 갈아 끼우는 도구 | `Tools/아라아띠/Warriors 크라켄에 뼈 넣기` (`WarriorsKrakenRigSwap`, 2R·3R 둘 다) |
| 뼈를 돌리는 부품 | `Scripts/MiniGames/Warriors/WarriorsKrakenLegs.cs` |
| 2R 머리 모델 | `Art/MiniGames/Warriors/Models/KrakenPhase2/KrakenPhase2_Rigged.fbx` — 팔 4 × 뼈 5 + `Root` = **21개** |
| 2R 촉수를 머리 팔에 잇는 도구 | `Tools/아라아띠/Warriors 촉수를 크라켄 팔에 잇기` (`WarriorsTentacleRig`) |
| 촉수 ↔ 팔 연결 부품 | `Scripts/MiniGames/Warriors/WarriorsTentacleArmLink.cs` |

```text
잔물결   늘. 다리마다 위상을 어긋내 물속처럼 흐른다 (기본 6도)
움찔     노트 정타. PlayRhythmHit → PlayHit — 모든 다리가 같은 방향으로 한 번 튕긴다
포효     콤보 피니시 · 최종 형태 등장. PlayRhythmFinish · ShowFinalForm → PlayRoar
```

`WarriorsKrakenBoss` 가 이 셋을 부릅니다. **셋 다 각 화면에서 부르는 자리**라(4장 규칙)
서버 전용 경로에 들어가지 않습니다. 서버에서는 `WarriorsServerCleanup` 이 부품을 끕니다.

### 9.1 만들 때 알아낸 것 ⚠

- **다리를 서로 반대로 크게 꺾지 않습니다.** 모델이 한 덩어리라 팔이 서로 닿는 자리가 붙어 있어,
  이웃 팔을 반대로 크게 돌리면 그 자리가 **찢어집니다**(블렌더 렌더로 실측). 이웃끼리는 위상만
  어긋내고 진폭을 10도 안쪽으로 둡니다. 움찔·포효는 **모든 다리가 같은 방향**이라 애초에 안전합니다.
- **자동 웨이트(heat)를 쓰지 않습니다.** 모델이 납작해 머리 뿔이 다리 뼈에 딸려 가 창처럼 늘어났습니다.
  뼈 사슬까지의 직선 거리로 직접 계산하고, 몸통 반지름 안쪽은 `Root` 가 가집니다.
- **크기는 배율이 아니라 월드 크기로 맞춥니다.** 옛 파일은 임포트 배율 0.01 이 메시에 구워져 있고
  새 파일은 1:1 이라, 프리팹의 864 를 그대로 옮기면 86배가 됩니다. 도구가 가장 긴 변을
  **16.41m** 로 맞춥니다 (localScale 8.641).
- **방향도 옮겨 붙지 않습니다.** 블렌더 내보내기는 축 변환(Z-up → Y-up)이 옛 Meshy 파일과 달라,
  프리팹의 옛 회전값을 그대로 쓰면 크라켄이 **누워 버립니다**(화면에서 위에서 본 모습으로 보였습니다).
  도구가 겉모습 안의 **메시 상자 모양**을 옛 모델과 견줘 90도 단위 24가지 중 맞는 것을 고릅니다 —
  이 모델은 가로 1.899 · 깊이 0.718 · 높이 1.197 로 세 변이 모두 달라 짝이 하나로 정해집니다.
  실측: 옛 상자 `(1.000, 0.378, 0.630)` · 새 상자 `(1.000, 0.630, 0.378)` → **X 90도** (오차 0.00000).
  ⚠ 상자는 **가장 긴 변을 1 로 맞춘 뒤** 견줍니다. 두 파일은 단위가 달라(0.01 배) 그냥 빼면
  모양이 아니라 크기 차이를 봅니다 — 그래서 한 번 엉뚱한 회전을 골랐습니다.
- **`WarriorsKrakenBoss.body` 칸은 비어 있습니다**(프리팹 실측 `fileID: 0`). `PlayRhythmHit` ·
  `PlayRhythmFinish` 는 맨 앞에서 그걸 검사하고 되돌아가므로, **다리 호출은 그 검사보다 앞**에 둡니다.
  몸통을 미는 코루틴만 `body` 가 필요합니다.
- **`updateWhenOffscreen` 을 켭니다.** 뼈가 돌면 원래 경계 밖으로 나가, 화면 끝에서 통째로 사라집니다.

### 9.2 2라운드 — 베는 대상은 크라켄의 실제 팔

임시 튜브 메시(`KrakenPlaceholder_CurvedTentacle`)를 걷어내고, **머리 모델의 팔 4개가 베는 대상**입니다.
`WarriorsTentacleArmLink` 가 촉수 자리와 팔을 잇습니다 (도구가 가로 위치 순서로 짝지어 꽂습니다).

```text
표식(↔ ↕ ⊙)   그 팔 위 84% 지점에 얹혀 팔을 따라 흔들린다
판정 상자      옛 튜브 자리에 고정 — 칼 사거리(베기 5.8m · 찌르기 8m) 안이어야 한다
팔 움직임      잔물결만. 맞아도 팔은 움직이지 않는다
맞은 표시      표식이 4배로 커지며 사라지고 분홍 파편 2개가 튄다
```

> ⚠ **판정 상자를 팔 끝에 붙이면 안 됩니다.** 그렇게 했더니 모델의 뒤쪽 팔이 10.7m 밖이라
> 칼이 닿지 않았습니다. 반대로 팔을 상자 쪽으로 겨냥(IK)시켜 봤더니 뿌리 뼈가 꺾여
> **팔 넷이 앞으로 접혀 꼬였습니다.** 그래서 상자는 고정, 표식만 팔을 따라갑니다.

> ⚠ **맞을 때 팔을 움직이지 않습니다.** 네 팔이 한꺼번에 반응해 무엇을 맞혔는지 읽히지 않았습니다.

### 9.3 표식(↔ ↕ ⊙) 규격

```text
표식 전체   markerScale 1.7   (팔이 z 11 로 물러나 원근으로 작아 보이는 것을 통째로 보정)
원판        discSize 0.86
노란 링     ringStartSize 1.4 → 원판까지 조여들며 남은 시간을 보여 준다
터짐        0.45초에 4배로 커지며 옅어짐 + 분홍 파편 2개
```

> ⚠ **원판만 키우면 안 됩니다.** 링은 `ringStartSize` → `discSize` 로 조여들어 시간을 보여 주므로,
> 원판을 키우면 그 여백이 사라져 **조여드는 것이 안 보입니다.** 멀어서 작으면 `markerScale` 로 통째로 키웁니다.

> ⚠ **터짐은 `Time.unscaledDeltaTime` 으로 셉니다.** 일시정지 중에는 `deltaTime` 이 0 이라
> 터지는 중에 멈추면 표식이 **부푼 채 굳습니다** — 화면에서 검은 덩어리로 보였습니다.

> ⚠ **알파는 `burst` 그대로 씁니다**(1 선명 → 0 사라짐). `1 - burst` 로 주면 맞는 순간 사라졌다가
> 잦아들며 **도로 나타납니다.** 그리고 터진 표식은 다음 촉수가 설 때까지 되살리지 않습니다 —
> 그 상태가 남아 세 번째 촉수부터 검은 원판으로 보였습니다.

### 9.4 2라운드 밸런스 (씬 값)

```text
목표 촉수     14회        답할 시간   3.4초 (두 팔 함께면 +0.9)
촉수 사이 틈  0.2초       잘린 뒤     0.15초
```

⚠ 답할 시간은 **슬롯에 복제**합니다(`WarriorsTentacleSlot.Window`). 화면이 제 값으로 계산하면
두 팔 패턴에서 링이 0.9초 먼저 다 조여들어, 다 조여든 뒤에도 반격이 오지 않습니다.

### 9.5 아직 안 한 것

| 대상 | 상태 |
| --- | --- |
| 실제 화면 확인 | 2R·3R 모두 화면에서 확인했습니다. 남은 것은 2인 플레이 확인입니다 |
| 3R 최종 크라켄 피격 반응 | 아직 맞을 때 다리가 움찔합니다(2R만 껐습니다). 잔물결만 남길지는 결정 전 |
