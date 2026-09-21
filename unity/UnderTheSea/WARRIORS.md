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
| 2 · 촉수 절단 | 촉수 성공 22회 |
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
| 사운드 | 프로젝트에 오디오 파일이 **하나도 없습니다.** 에셋이 들어와야 붙일 수 있습니다 |
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

외부 에셋 출처 기록은 `ASSETS.md` 를 따릅니다.
