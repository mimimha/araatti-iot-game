# IotPlayerController 작업 기록

무선 완드(ESP32-S3)를 게임에 붙이는 브리지를 만들면서 **무엇을 확인했고, 무엇이 막혔고, 왜 그렇게 풀었는지** 적어둔 문서입니다.

코드를 읽으면 "무엇을 하는지"는 알 수 있지만 "왜 이 모양인지"는 알 수 없습니다. 그 부분만 모았습니다.

- 장치 규격(무엇을 구현해야 하는가) → 저장소 최상위 `IOT_INPUT.md`
- 경계 정의(폴더 규칙·누가 함께 정하는가) → `GAME_STRUCTURE.md` 9장
- 이 문서 → 그 규격을 시리얼로 어떻게 채웠는가

| | |
|---|---|
| 브랜치 | `feature/jy-iot-wand-tuning` |
| 담당 | IoT (`jy`) |
| 대상 파일 | `IotPlayerController.cs` (1603줄) |
| | `firmware/dongle_esp32s3/dongle_esp32s3.ino` (334줄) |
| | 로비 · 낚시: `IotLobbyInteract.cs` · `IotFishingBridge.cs` · `WandFishingInputSource.cs` (키 배치는 `KEY_MAPPING.md`) |
| | `firmware/wand_tinys3/wand_tinys3.ino` (756줄) |
| | `firmware/tools/tilt_sign.ps1` (510줄) · `steer_verify.ps1` (285줄) |
| 보드에 굽혀 있는 것 | 7-3 |
| 기준 커밋 | `20a70de9` |

---

## 1. 무엇을 만들었나

```text
입력  완드 → 동글(USB) → 시리얼 → IotPlayerController → IPlayerController → 각 미니게임
진동  완드 ← 동글(USB) ← 시리얼 ← IotPlayerController ← IHandDevice.Vibrate ← 각 미니게임
```

`IotPlayerController`는 `IPlayerController`를 구현한 `MonoBehaviour` 하나입니다. 빈 오브젝트나 플레이어 오브젝트에 붙이면 끝나고, **게임 코드는 한 줄도 고치지 않았습니다.**

`KeyboardPlayerController`와 정확히 같은 자리에 꽂힙니다. 하드웨어가 없으면 그쪽을 켜고, 있으면 이쪽을 켭니다.

---

## 2. 붙이는 법

### 2-1. 어디에 붙이는가

| 상황 | 붙이는 오브젝트 |
|---|---|
| 광산 `MineTest.unity` | `KeyboardPlayerController`가 붙어 있는 **Player** |
| 배 협동 `ShipCoopTest.unity` | 같음 — **Player** |
| 네트워크 배 협동 | **NetworkRunner** 오브젝트 |

**한 오브젝트에 `IPlayerController` 구현체는 하나만** 있어야 합니다. 기존 `KeyboardPlayerController`는 **지우지 말고 체크박스만 끄세요.** 하드웨어 없이 테스트할 때 다시 켭니다.

> ⚠ 네트워크 배 협동에서 NetworkRunner에 붙이는 이유
> `ShipCoopInputProvider`가 `[RequireComponent(typeof(NetworkRunner))]`이고, 자체 주석이 *"캐릭터가 아니라 러너에 붙인다. 캐릭터에 붙이면 화면에 있는 남의 캐릭터 복사본도 내 키보드를 읽는다"*라고 적고 있습니다.
> `IOT_INPUT.md` 4장은 "플레이어 오브젝트"라고만 적혀 있어 이 경우와 어긋납니다. 코드 쪽이 맞습니다.

> ⚠ **먼저 붙어 있어야 합니다.**
> `ShipCoopInputProvider.Awake()`가 `GetComponent<IPlayerController>()`로 찾고, **못 찾으면 `KeyboardPlayerController`를 스스로 붙입니다.** 늦게 초기화되면 키보드에 선점당합니다. 그래서 이 스크립트는 `Awake`에서 손을 미리 만듭니다.

### 2-2. 인스펙터

| 그룹 | 항목 | 기본값 | 설명 |
|---|---|---|---|
| 시리얼 포트 | `portName` | 비어 있음 | **비워두면 COM 포트를 훑어 동글을 스스로 찾는다** (CSV 또는 `#MAC` 이 들리면 동글, 조용하면 `?` 를 보내 묻는다). 적어두면 그 포트만 연다. 이미 씬에 `COM3` 이 적혀 있으면 그대로 쓰이니 지워야 자동이 된다 |
| | `baudRate` | `115200` | 펌웨어와 같아야 함 |
| | `pollIntervalMs` | `5` | 받은 것이 있는지 확인하는 간격 |
| | `dtrEnable` | `true` | 아래 4-2 참고 |
| | `rtsEnable` | `false` | 아래 4-2 참고 |
| 키 배치 | `controlProfile` | `Shared` | 광산·배는 `Shared`, 무쌍은 `Warriors` (아래 2-4) |
| 손 배정 | `leftHandId` | `0` | 왼손으로 쓸 완드 번호 |
| | `rightHandId` | `1` | 오른손으로 쓸 완드 번호 |
| | `handTimeoutSeconds` | `0.5` | 이만큼 무응답이면 끊긴 것으로 봄 |
| 조타 | `mirroredGrip` | `false` | 양손을 마주 보게 쥐는지. 아래 4-11 |
| 달리기 | `sprintToggle` | `true` | 왼손 버튼2를 토글로 |
| 진단 | `logRawLines` | `false` | 받은 줄 그대로 |
| | `logParseFailures` | `false` | 해석 실패한 줄 |
| | `logDongleMessages` | `false` | 동글이 보내는 `#` 줄 |
| | `logConnectionChanges` | `false` | 완드 연결/끊김 |
| | `logMotions` | `false` | 동작이 들어올 때만 종류·세기 |
| | `logDeviceOutput` | `false` | 진동 호출 |

### 2-3. 처음 연결할 때 순서

0. **동글 포트를 쓰는 것을 전부 닫는다** — Arduino 시리얼 모니터 등
1. `logRawLines` 켜고 Play → `수신 — 0,-127,64,...`가 흐르는지
2. 안 나오면 `dtrEnable`부터 뒤집어 본다 (4-2 참고)
3. `logParseFailures` 켜고 자릿수 어긋난 줄이 없는지
4. `logConnectionChanges` 켜고 완드를 껐다 켜며 `완드 0 연결됨/끊김`이 뜨는지
5. 전부 확인되면 로그를 다시 끈다

> ⚠ **시리얼 포트는 배타적입니다.** 0번을 안 하면 포트를 못 열어 경고 한 줄만 남기고 조용히 비활성이 됩니다. 반대로 Play 중에는 시리얼 모니터가 안 열립니다. 둘 중 하나만 씁니다.

> ⚠ 첫 줄에 `ESP-ROM:esp32s3-...` 같은 것이 몇 줄 섞일 수 있습니다. 포트를 열 때 DTR 이 동글을 리셋하면 나오는 부트로더 메시지입니다. `#` 로 시작하지 않아 **해석 실패로 잡히지만** 버려도 되는 줄입니다.

### 2-4. 게임별 키 배치

같은 완드가 게임마다 다른 행동을 냅니다. **그 해석은 완드가 하지 않습니다.** 각 미니게임이 `IPlayerController` 값을 읽어 자기 행동으로 번역합니다. (`IOT_INPUT.md` 3장)

| 장치 부품 | 광산 `MineDigger` | 무쌍 `WarriorsKeyboardInput` | 배 `ShipCoopInput` |
|---|---|---|---|
| 왼손 스틱 | 이동 | 이동 | 걷기 |
| 왼손 버튼 1 | 땅 복구 | — | 도움 요청 |
| 왼손 버튼 2 | 힌트 | **미정** (아래) | **달리기 (토글)** |
| 오른손 스틱 | — (자체 마우스) | 카메라 x·y | 카메라 x |
| 오른손 버튼 1 | — | — | 상호작용 |
| 오른손 버튼 2 | **달리기** | — | 발사 · 망치질 |
| IMU 동작 | `VerticalSwing` = 파기 | 3종 = 가로·세로·찌르기 | `VerticalSwing` = 망치질 |
| 기울기 · 비틀기 | — | — | 조타 · 돛 |

**완드가 게임 이름을 알아야 하는 것은 한 칸뿐입니다 — 왼손 버튼 2 입니다.**

배는 이 버튼을 토글로 잠가야 합니다. 엄지가 스틱을 떠나면 걷기가 멈추기 때문입니다. (4-8)

무쌍은 잠그지 않습니다. 달리기라는 조작 자체가 없고, **그 자리에 무엇이 올지는 아직 안 정했습니다.** `IOT_INPUT.md` 7장이 회피를 키보드 Shift 로 옮기라고 적고 2장이 Shift 를 왼손 버튼 2 에 두지만, **무쌍의 장치 배치를 직접 정한 표는 문서에 없습니다.** 8장도 무쌍의 남는 키를 열어 둔 상태입니다. 정해지기 전에는 누른 그대로 내보내는 쪽이 안전합니다 — 토글로 잠가 두면 그 자리에 단발 조작이 오는 순간 한 번 걸러 먹힙니다.

광산도 잠그지 않습니다. 거기는 왼손 버튼 2 가 **힌트**(단발)고, 달리기는 **오른손 버튼 2** 를 씁니다. 광산은 판을 내려다보며 파는 게임이라 카메라를 계속 돌릴 일이 없어서, 엄지가 스틱을 떠나도 괜찮습니다. 그래서 토글이 아니라 **누르고 있기**입니다.

그 한 칸 때문에 `IotControlProfile { Shared, Warriors, Mine }` 을 뒀습니다. `KeyboardPlayerController` 의 `KeyboardControlProfile` 과 값이 **1:1 이어야 합니다.** 저쪽에 프로필이 늘면 여기도 늘립니다.

```csharp
bool enabled = sprintToggle
               && _hasTwoDevices
               && controlProfile == IotControlProfile.Shared;
```

`Shared` 는 이제 **배 전용**입니다. 광산은 `Mine`, 무쌍은 `Warriors` 를 씁니다.

**정하는 순서는 인스펙터 → 씬입니다.** 기본값은 `Shared` 이고, 무쌍 씬이 `SetControlProfile` 로 덮어씁니다. 광산은 부르는 코드가 없어 **인스펙터로만** 정합니다 — 키보드 쪽도 같습니다(`MineTest.unity` 의 `controlProfile: 2`).

| 부르는 곳 | 언제 |
|---|---|
| `WarriorsSceneBootstrap.Awake` | 프리팹으로 플레이어를 만드는 씬 |
| `WarriorsLocalPlayerController.ResolvePlayerController` | `WarriorsTest` 처럼 플레이어가 씬에 직접 놓인 경우 |

> ⚠ `Look` 에는 프로필을 두지 않았습니다.
> 카메라가 세로를 쓸지는 **받는 쪽**이 이미 갈라 놨습니다. 배는 `ShipCoopCamera` 가 `Look.x` 만 떼어 쓰고(`ShipCoopCamera.cs:496`), 무쌍은 `WarriorsThirdPersonCamera` 가 `x`·`y` 를 다 씁니다. 스틱을 그대로 넘기는 것이 양쪽 다 맞습니다.

> ⚠ **프로필 enum 이 두 벌입니다.** `KeyboardControlProfile` 과 `IotControlProfile` 이 같은 값을 따로 들고 있고, 부르는 쪽은 둘 다 `as` 로 캐스팅해야 합니다. 원래 자리는 `IPlayerController` 지만 거기는 공용 경계라 혼자 못 고칩니다. 세 번째 구현체가 나오기 전에 미니게임 담당자들과 함께 정해야 합니다. (`GAME_STRUCTURE.md` 9장)

### 2-5. 게임별 테스트 절차

**게임마다 붙이는 법이 다릅니다.** 무쌍에서 `WarriorsKeyboardInput` 을 켜서 됐다고 해서 배·광산도 같지 않습니다. 그런 "키보드 input" 컴포넌트는 **무쌍에만 있습니다.**

| | 완드를 찾아가는 방법 | 키보드 구현체와 공존 |
|---|---|---|
| 무쌍 | `WarriorsKeyboardInput` 이 `IPlayerController` 를 번역 | 가능 (`playerControllerSource` 칸) |
| 배 | `TaskWorker` · `DebugPlayerMover` 가 `GetComponent` | **불가 — 키보드 쪽을 지워야 함** |
| 광산 | `MineDigger` 가 `GetComponent` | 가능 (`playerControllerSource` 칸) |

> ⚠ **체크박스를 꺼도 `GetComponent` 는 찾아옵니다.** 컴포넌트를 끄는 것은 `Update` 를
> 멈추는 것이지 없애는 것이 아닙니다.
>
> 한 오브젝트에 구현체가 둘이면 `GetComponent` 는 **붙인 순서대로 첫 번째 것**을 돌려줍니다.
> 무작위가 아니라 정해져 있습니다. 다만 **기대면 안 됩니다** — 나중에 누가 컴포넌트를
> 지웠다 다시 붙이면 순서가 바뀌고, 그 순간 완드가 조용히 무시됩니다. 게임은 키보드로
> 멀쩡히 돌아가서 **고장난 줄도 모릅니다.**
>
> 우회 칸이 있는 쪽은 그 칸을 채우고, 없는 쪽은 **지웁니다.**

#### 배 협동 — `ShipCoopTest.unity`

씬에 **`Player_01` · `Player_02` 두 명**이 있고 각자 `DebugPlayerMover` · `TaskWorker` · `KeyboardPlayerController` 를 답니다.

준비 — **한 명에게만** 합니다. 나머지 한 명은 키보드로 둬야 2인 동작을 볼 수 있습니다.

1. `Player_01` 의 **`KeyboardPlayerController` 를 지운다** (체크박스 아님, 컴포넌트 제거)
2. `IotPlayerController` 를 추가한다
3. `portName` 은 비워 두고(자동 탐색, 2-2), `controlProfile` 은 **`Shared`** 로 둔다

확인 — `IOT_INPUT.md` 2장 표 그대로입니다.

| 장치 | 무엇을 보나 | 기대 |
|---|---|---|
| 왼손 스틱 | 걷기 | 카메라 기준으로 움직인다 |
| 왼손 버튼 1 | 도움 요청 | **기기 2대일 때만** 된다 |
| 왼손 버튼 2 | 달리기 | 한 번 눌러 켜고 다시 눌러 끈다 (**토글**) |
| 오른손 스틱 | 카메라 | 좌우만 돈다. 세로는 `ShipCoopCamera` 가 버린다 |
| 오른손 버튼 1 | 상호작용 | 붙기 · 집기 · 놓기 · 장전 |
| 오른손 버튼 2 | 발사 · 망치질 | 대포에 붙었으면 발사, 수리 지점이면 망치 |
| 양손 기울기 | 조타 | 조타륜에 붙어서 완드를 눕힌다 |
| 세로 내리치기 | 망치질 | 버튼 2 와 **같은 일**. 둘 중 아무거나 |

**달리기 토글이 배에서만 걸리는 것**이 프로필이 맞게 물렸다는 증거입니다. 물건을 들면 달리기가 막히는 것도 정상입니다 (`DebugPlayerMover` 가 막습니다).

⚠ 완드가 **1대**면 도움 요청과 달리기가 스스로 물러납니다. `ShipCoopInput.ConsumeHelpCall` · `Sprint` 가 `HasTwoDevices` 를 보고 빠집니다. 버그가 아닙니다.

#### 광산 — `MineTest.unity`

**✅ 키보드 · 완드 둘 다 실기 확인 완료.** 매핑이 전부 `IPlayerController` 를 거칩니다.

| 항목 | 지금 | 읽는 곳 |
|---|---|---|
| 땅 파기 | ✅ 완드 | `MineDigger` ← `TryConsumeMotion` (VerticalSwing) |
| 땅 복구 | ✅ 완드 | `MineDigger` ← `Left.ConsumeButton1Press` |
| 힌트 | ✅ 완드 | `MineDigger` ← `Left.ConsumeButton2Press` |
| **이동** | ✅ 완드 | `MineMoveInput` ← `Move` |
| **달리기** | ✅ 완드 | `MineMoveInput` ← `Right.Button2` (**2대일 때만**) |
| 카메라 | ❌ 마우스 전용 | `MineCamera` ← 우클릭 드래그. MINE.md 8장 표에 카메라가 없어 스코프 밖 |
| 점프 | 없음 | 씬에서 `jumpButton` 을 비워 뒀다 |

달리기가 **오른손** 버튼인 이유는 왼손이 이미 찼기 때문입니다 — 버튼1 은 복구, 버튼2 는 힌트입니다. 완드 **1대면 `Right` 가 `Left` 와 같은 객체**라 그 버튼이 곧 힌트 버튼이 되므로, 1대에서는 달리기를 뺍니다. 배도 같은 이유로 1대에서 달리기를 뺍니다.

키보드는 `Mine` 프로필이 같은 자리를 그대로 덮습니다. **완드 없이도 광산 전체가 됩니다.**

| 광산 행동 | 완드 | 키보드 (`Mine` 프로필) |
|---|---|---|
| 이동 | 왼손 스틱 | `W A S D` |
| 땅 파기 | 세로 내리치기 | `Space` |
| 땅 복구 | 왼손 버튼 1 | `C` |
| 힌트 | 왼손 버튼 2 | `J` |
| 달리기 | 오른손 버튼 2 | `Shift` |

준비

1. Player 에 `IotPlayerController` 를 추가한다
2. `controlProfile` 은 **`Mine`** (배의 `Shared` 가 아니다 — 그걸 쓰면 왼손 버튼 2 가
   달리기 토글로 잠기는데 광산에서 그 자리는 **힌트**다)
3. `MineDigger` 와 `MineMoveInput` 의 **`Player Controller Source` 칸에 쓸 쪽을 지정한다**

#### ⚠ 지금 `MineTest.unity` 는 3번이 비어 있습니다

실기 확인은 통과했지만 **순서에 기대고 있는 상태**입니다.

```
Player 의 IPlayerController 구현체
  1. IotPlayerController        ← insertIndex 3. GetComponent 가 이걸 돌려준다
  2. KeyboardPlayerController   ← insertIndex -1 (뒤에 붙음). 지금은 아무도 안 본다
```

우연히 완드가 앞에 와서 동작합니다. 두 가지를 알아 두십시오.

- **지금은 완드 전용입니다.** 완드를 뽑으면 모든 값이 0 이라 **키보드로도 안 움직입니다.**
  키보드로 돌아가려면 두 칸에 `KeyboardPlayerController` 를 지정하십시오.
- 컴포넌트를 지웠다 다시 붙이면 순서가 뒤집혀 **완드가 조용히 무시됩니다.**

칸을 채워 두면 둘 다 안 생깁니다. 바꿔 끼우는 것도 칸 하나로 끝납니다.

확인

| 장치 | 기대 |
|---|---|
| 세로 내리치기 | 발밑이 파인다. **내 턴일 때만** (`DiggingAllowed`) |
| 왼손 버튼 1 | 되메우기 요청 |
| 왼손 버튼 2 | 힌트 요청 |
| 왼손 스틱 | 걷는다 (화면 기준) |
| 오른손 버튼 2 | 달린다. **2대일 때만** |

⚠ 파기는 `VerticalSwing` 만 받습니다. 가로로 휘두르면 아무 일도 안 일어나는 것이 맞습니다.

⚠ 되메우기·힌트는 **힌트를 보는 동안 같이 막힙니다.** `MineDigger` 주석이 의도한 동작이라고 적어 뒀습니다.

#### 공통 — 둘 다 확인할 것

1. 콘솔에 `완드 0 연결됨` 이 뜨는가 (`logConnectionChanges`)
2. `logMotions` 로 동작이 들어오는가
3. **완드를 껐을 때** 값이 0 으로 떨어지고 게임이 안 죽는가
4. 다시 켰을 때 묵은 입력이 한꺼번에 안 터지는가

3·4 번은 `Clear()` 와 첫 줄 기준 잡기(4-6)가 도는지 보는 것입니다.

#### 회귀 — 완드 없이

`KeyboardPlayerController` 가 잡히는 상태로 두 씬을 Play 합니다.

배는 그 파일을 건드리지 않았으므로 **예전과 똑같아야** 합니다. 달라지면 이쪽 잘못입니다.

광산은 **한 군데만 달라집니다 — 방향키가 안 됩니다.** 예전 `MineMoveInput` 은 레거시
`Input.GetAxis("Horizontal")` 이라 WASD 와 방향키를 둘 다 받았는데, `Move` 는 왼손 스틱
하나이고 키보드는 거기에 WASD 만 답니다. `IOT_INPUT.md` 1장이 네 게임 공통 이동을
WASD 로 정했으니 방향은 맞지만, **광산 담당자의 테스트 습관이 바뀝니다.** (아래 7-6)

---

## 3. 조사에서 바로잡은 것

작업 시작 전 알고 있던 환경 정보 중 **세 가지가 실제와 달랐습니다.** 처음 세운 계획이 통째로 바뀐 원인입니다.

| 항목 | 알고 있던 것 | 실제 |
|---|---|---|
| Input Handling | New Input System 전용 (`activeInputHandler=1`) | **`2` = Both.** 레거시 `Input.GetAxis`를 쓰는 코드가 아직 살아 있음 (낚시, `LocalPlayerView`, ithappy 에셋) |
| Cinemachine | 3.x 사용 중 | **패키지가 아예 없음.** 카메라는 전부 자체 제작 |
| Api Compatibility | — | **`6` = .NET Standard.** `System.IO.Ports`를 못 씀 (4-1 참고) |

Unity 6000.5.9f1, URP 17.5.0, Input System 1.20.0은 맞았습니다.

### 가상 Gamepad 방식을 접은 이유

처음 계획은 New Input System의 가상 Gamepad 장치를 만들어 값을 주입하는 것이었습니다. **액션 이름을 몰라도 물린다**는 것이 장점이었는데, 조사해 보니 전제가 성립하지 않았습니다.

- 프로젝트 전체에 Unity의 `PlayerInput` 컴포넌트가 **0개**였습니다. `MovePlayerInput`(ithappy 에셋)이 이름만 비슷한 다른 것이고, 그나마 **레거시 Input Manager**를 씁니다.
- `Assets/InputSystem_Actions.inputactions`는 **Unity 기본 템플릿 그대로이고 아무도 참조하지 않습니다.** 래퍼 코드 생성도 꺼져 있습니다.
- `Gamepad.current`를 읽는 곳은 `StartMenuController`(Title 메뉴) **한 군데뿐**이었습니다.

즉 가상 Gamepad를 만들어도 **Title 메뉴 말고는 아무 데도 안 물립니다.**

반면 팀은 이미 `IPlayerController` / `IHandDevice`라는 경계를 만들어 뒀고, `KeyboardPlayerController`에 *"IoT 담당자의 진짜 구현이 나오면 이 컴포넌트만 갈아끼우면 됩니다"*라고 적어 뒀습니다. 그쪽으로 방향을 바꿨습니다.

---

## 4. 막혔던 것과 해결

### 4-1. `System.IO.Ports`가 컴파일되지 않는다

`ProjectSettings.asset`의 `apiCompatibilityLevel`이 `6`(.NET Standard)이었습니다. Unity는 `System.IO.Ports.dll`을 **.NET Framework 프로파일에서만** 제공합니다.

**해결: 됐음.** Player Settings → Other Settings → Api Compatibility Level을 **.NET Framework**로 바꿨습니다. `apiCompatibilityLevel`이 `3`인 것을 확인했습니다.

> ⚠ **`ProjectSettings.asset`은 팀 전체가 받는 파일입니다.** 브리지 코드보다 **먼저, 또는 같은 MR에** 실어 보내야 합니다. 브리지가 먼저 머지되면 팀원들이 `develop`을 받는 순간 `CS0234`를 봅니다. 팀에는 공유했습니다.

### 4-2. 포트를 열어도 값이 안 나온다 — DTR/RTS

ESP32-S3의 네이티브 USB CDC는 보통 **DTR을 올려야** 값이 나옵니다. 그런데 CP2102·CH340 같은 변환 칩을 쓰는 보드는 DTR/RTS가 **리셋 회로에 물려 있어서**, 켜는 순간 완드가 재부팅됩니다.

**해결:** 둘 다 인스펙터 토글로 뺐습니다. 기본은 `dtrEnable = true`, `rtsEnable = false`. 값이 안 나오면 여기부터 뒤집습니다.

### 4-3. `SerialPort.DataReceived`가 안 불린다

Mono 구현에서 이 이벤트는 동작하지 않습니다.

**해결:** 처음부터 쓰지 않고 별도 스레드에서 직접 읽는 구조로 갔습니다.

### 4-4. `SerialPort.ReadLine()`이 동작하지 않는다 ← 실기 확인

Mono의 `ReadLine()`은 Unity에서 값을 돌려주지 않습니다. **실제 장치로 확인했고, `ReadExisting()`으로 바꾸니 값이 들어왔습니다.**

**해결:** `ReadLoop`만 수술했습니다.

`ReadExisting()`은 블로킹하지 않고 **지금 와 있는 만큼만** 돌려주므로 세 가지가 따라옵니다.

1. **줄이 중간에서 잘려 들어옵니다.** `StringBuilder` 잔여 버퍼를 루프 밖에 두고, `'\n'`을 만난 것만 잘라 큐에 넣습니다. 마지막 조각은 다음 회차로 넘깁니다.
2. **`Thread.Sleep`이 필요합니다.** 없으면 코어 하나를 100%로 태웁니다. → `pollIntervalMs`(기본 5ms)
3. **버퍼 상한이 필요합니다.** `'\n'`이 영영 안 오면 무한히 커집니다. → `MaxLineBufferLength = 4096`, 넘으면 통째로 비웁니다.

**큐가 "완성된 줄만 담는다"는 약속을 지킨 것이 핵심입니다.** 줄 조립을 `ReadLoop` 안에 가둔 덕분에 `DrainLines` → `TryParse` → `Wand.Apply` 경로는 한 줄도 바뀌지 않았습니다.

같이 정리한 것:

| 이전 | 이후 | 이유 |
|---|---|---|
| `readTimeoutMs` | `pollIntervalMs` (기본 5) | `ReadTimeout`은 `ReadExisting`에 영향이 없음 |
| `NewLine = "\n"` | 제거 | `ReadLine`/`WriteLine` 전용 |
| `catch (TimeoutException)` | 제거 | `ReadExisting`은 던지지 않음 |
| `ClosePort`: 닫고 → Join | **Join → 닫기** | 읽기가 더는 블로킹하지 않아 폴링 주기마다 스스로 빠져나옴. 억지로 예외를 내지 않아도 됨 |

### 4-5. 완드가 리부팅하면 동작이 수백 번 터진다

`mcount`는 누적 카운터라 **늘어난 만큼**이 동작 횟수입니다. 완드가 다시 켜지면 0부터 다시 시작하는데, 그 낙차를 그대로 받으면 한 번에 수백 번 휘두른 것이 됩니다.

**1차 해결 — `mcount` 절반 규칙.** 0~255를 순환하므로 `(cur - last + 256) % 256`으로 감쌉니다. **감싼 값이 절반(128)을 넘으면 뒤로 간 것**으로 보고 기준만 새로 잡습니다. `255 → 0` 같은 진짜 순환은 감싼 값이 1이라 걸리지 않습니다.

**남은 구멍.** 직전 값이 128보다 컸으면(`200 → 0` → 감싼 값 56) 재부팅을 순환으로 오인합니다.

**2차 해결 — `ms`로 보강.** 같은 완드 안에서 **시간이 되감긴 것**은 리부팅의 확실한 신호입니다. `ms`도 16비트라 약 65초마다 정상 순환하므로 같은 방식으로 감싸고, **뒤로 간 폭이 절반(32768)을 넘을 때만** 리부팅으로 봅니다.

```csharp
int delta   = (packet.MotionCount  - _lastCounter          + CounterWrap)       % CounterWrap;
int elapsed = (packet.Milliseconds - _previousMilliseconds + MillisecondsWrap)  % MillisecondsWrap;

bool rebooted = elapsed > MillisecondsWrap / 2;

if (rebooted || delta == 0 || delta > CounterWrap / 2) { return; }
```

**둘 중 하나라도 걸리면 차단**이고, 기준값 갱신은 차단 판정보다 **먼저** 일어납니다. 그래서 버린 줄도 기준은 새로 잡힙니다.

> ⚠ `ms`의 용도 제한
> **다른 완드의 `ms`와 비교하지 않습니다.** `millis()`는 각 완드가 켜진 시점부터 세기 때문에 원점이 서로 다릅니다. 같은 완드 안에서 되감긴 것만 봅니다. 코드 주석에도 못박아 뒀습니다.

### 4-6. 첫 줄에 쌓인 동작이 한꺼번에 터진다

게임을 켜기 전에도 완드는 카운터를 올리고 있습니다.

**해결:** 첫 수신 줄은 `mcount`·`ms` 기준만 잡고 넘어갑니다(`_hasCounter`). 끊겼다 다시 붙을 때도 `Clear()`가 기준을 비워 같은 처리를 탑니다.

### 4-7. 완드 1대일 때 걸으면 화면이 같이 돈다

`Look => Right.Stick`인데, 1대면 `Right`가 `Left`를 가리켜 `Move`와 `Look`이 같아집니다.

**해결:** `KeyboardPlayerController`와 `SHIPCOOP.md`가 이미 합의한 기준을 따랐습니다.

```csharp
public Vector2 Look => HasTwoDevices ? Right.Stick : Vector2.zero;
```

`SHIPCOOP.md`의 1대 모드 표도 카메라·대포 조준을 **"고정"**으로 적고 있습니다.

> ⚠ `IPlayerController.cs`의 XML 주석은 *"1대만 들면 왼손 스틱을 나눠 쓴다"*라고 되어 있습니다. **이 주석이 뒤처진 것입니다.** 참조 구현과 문서를 따랐습니다.

### 4-8. 달리기가 토글인데 펌웨어는 자기가 어느 손인지 모른다

`IOT_INPUT.md`는 달리기를 **토글**로 정했습니다. 기기에서 엄지는 스틱과 면버튼 중 하나만 잡기 때문에, 누르고 있는 방식으로는 달리면서 걸을 수가 없습니다. 문서는 *"장치가 상태를 들고 있으면 된다"*고 합니다.

문제는 **토글은 왼손 버튼2에만** 걸어야 한다는 것입니다. 오른손 버튼2는 발사·망치질이라 엣지여야 합니다. 그런데 손 배정(`leftHandId`/`rightHandId`)은 **이 스크립트의 인스펙터에만** 있고 펌웨어는 모릅니다.

**해결:** 펌웨어는 버튼 레벨을 그대로 보내고, **브리지가 토글로 바꿉니다.**

- 왼손 완드의 버튼2가 눌린 **순간**에 내부 상태를 뒤집음
- `Button2` 프로퍼티가 그 상태를 반환
- `ConsumeButton2Press()`는 **실제로 누른 순간을 그대로** 반환 → 발사가 안 망가짐
- 완드가 끊기면 `Clear()`가 토글을 내림 (완드를 꺼도 계속 달리면 안 됨)
- `sprintToggle` 토글을 끄면 레벨 그대로 통과

---

### 4-9. `ReadExisting()` 이 읽는 도중에 터진다 ← 실기 확인

```
[IotPlayerController] 읽기가 멈췄습니다. 동글을 다시 꽂고 컴포넌트를 껐다 켜세요.
  — 제공된 사용자 버퍼가 요청된 작업에 적합하지 않습니다.
```

Win32 오류 1784 `ERROR_INVALID_USER_BUFFER` 입니다. `ReadExisting()` 은 내부에서 바이트를 읽고 **문자로 디코딩**까지 한 번에 하는데, Mono 의 Windows 구현에서 그 경로가 이 오류로 터집니다. 4-4 에서 `ReadLine()` 을 버린 것과 같은 계열의 문제입니다.

**한 번 터지면 읽기 스레드가 죽습니다.** 그러면 줄이 더는 안 들어오고, 0.5초 뒤 완드가 타임아웃으로 끊긴 것이 되어 **모든 값이 0 으로 나갑니다.** 조이스틱이 갑자기 안 먹는 증상이 이것입니다.

**해결: 바이트로 직접 읽는다.**

```csharp
int available = port.BytesToRead;
if (available <= 0) { Thread.Sleep(sleepMs); continue; }

read = port.Read(bytes, 0, Mathf.Min(available, bytes.Length));
```

디코딩을 `SerialPort` 에서 떼어내 우리가 합니다. 세 가지가 따라옵니다.

1. **`BytesToRead` 로 먼저 물어봅니다.** 와 있는 만큼만 달라고 하므로 블로킹하지 않습니다.
2. **`Decoder` 를 씁니다.** 동글의 `#` 로그에 한글이 섞여 옵니다. UTF-8 은 한 글자가 여러 바이트라 조각 경계에서 잘릴 수 있는데, `Encoding.UTF8.GetDecoder()` 는 상태를 들고 있어 다음 조각에서 이어 풉니다. `Encoding.UTF8.GetString` 을 조각마다 부르면 경계에서 글자가 깨집니다.
3. **`ReadTimeout` 이 다시 의미를 갖습니다.** 4-4 에서 뺐던 것인데, `Read` 는 영향을 받습니다. 무한 대기로 두면 포트를 닫을 때 스레드가 안 빠져나오므로 100ms 로 두고 `TimeoutException` 은 조용히 넘깁니다.

`ExtractLines` 아래로는 한 줄도 바뀌지 않았습니다. 큐가 "완성된 줄만 담는다"는 약속은 그대로입니다.

> ⚠ **시리얼 포트는 한 프로그램만 엽니다.** Arduino 시리얼 모니터를 열어둔 채 유니티를 Play 하면 포트를 못 열거나 이상하게 동작합니다. 한쪽만 켜세요.

---

### 4-10. `WiFi.macAddress()` 가 `00:00:00:00:00:00` 을 돌려준다 ← 실기 확인

동글 부팅 로그의 `#MAC` 이 전부 0 으로 찍혔습니다. esptool 은 같은 칩에서 `58:E6:C5:6A:92:D8` 을 제대로 읽습니다.

arduino-esp32 3.x 에서 `WiFi.macAddress()` 는 STA netif 를 거치는데, `esp_now_init()` 직후 시점에는 아직 netif 가 안 올라와 있습니다. ESP-NOW 자체는 멀쩡히 동작하므로 **로그만 거짓말을 합니다.**

**해결:** eFuse 에서 직접 읽습니다. Wi-Fi 상태와 무관합니다.

```c
#include <esp_mac.h>
uint8_t mac[6];
esp_read_mac(mac, ESP_MAC_WIFI_STA);
```

> ⚠ 이걸 못 잡았으면 *"동글 부팅 로그의 `#MAC` 을 완드의 `DONGLE[6]` 에 적는다"* 는 절차가 통째로 안 돌아갑니다. 저희는 esptool 출력으로 우회해서 모르고 지나갈 뻔했습니다. 완드의 `#NOW` 에도 같은 자리가 있어 양쪽 다 고쳤습니다.

### 4-11. 양손으로 휠을 돌리면 조타가 0 에 붙는다

조타는 `ShipCoopInput.Steer` 가 **양손 평균**(`(Left.Tilt + Right.Tilt) * 0.5f`)으로 읽습니다. 이 평균은 두 완드의 `tilt` 가 **같은 방향으로** 움직인다는 전제 위에 있습니다.

그런데 조타륜이나 배수 밸브를 잡을 때 손은 보통 **손바닥을 마주 보게** 쥡니다. 그러면 두 완드가 서로 180도 돌아간 상태가 되고, 휠을 한 방향으로 돌려도 각 완드가 중력으로 재는 roll 은 **부호가 반대로** 나옵니다. 평균내는 순간 서로 상쇄됩니다.

합성 데이터로 재현했습니다. 두 손의 `tilt` 가 정확히 반대인 기록을 넣으면:

```
그냥 평균   폭 0.00              ← 조타가 완전히 죽는다
부호 보정   -0.78 ~ +0.78
```

**해결: 브리지가 왼손 완드의 `Tilt` 부호만 뒤집습니다.** (`mirroredGrip`)

펌웨어가 아니라 브리지에 넣은 이유는 **완드가 자기가 어느 손인지 모르기 때문**입니다. 손 배정(`leftHandId`/`rightHandId`)은 이 스크립트의 인스펙터에만 있습니다. 달리기 토글을 브리지에서 거는 것(4-8)과 같은 이유이고, `ShipCoopInput.cs` 는 한 줄도 건드리지 않았습니다.

- 원값(`_tilt`)은 그대로 두고 **내놓을 때만** 뒤집습니다. 손 배정이 바뀌어도 받아둔 값을 다시 계산하지 않습니다
- **1대만 들었을 때는 걸지 않습니다.** 상쇄될 짝이 없고, `Steer` 도 1대면 `Left.Tilt` 를 그대로 씁니다. 여기서 뒤집으면 혼자 들었을 때만 조타가 거꾸로 돕니다
- 잡는 방식이라 상태가 아닙니다. `Clear()` 가 건드리지 않습니다

> ⚠ **`TILT_SIGN`(펌웨어)과 `mirroredGrip`(유니티)은 다른 문제입니다.** `TILT_SIGN` 은 *"오른쪽으로 돌리면 양수인가"* 를 정하고, `mirroredGrip` 은 *"두 손이 서로 반대로 나오는가"* 를 정합니다. 둘 다 눈으로는 알 수 없어서 실기로만 정합니다. → `firmware/tools/tilt_sign.ps1` 이 한 번에 둘 다 판정합니다.

**실측 결과 지금 배치에서는 `mirroredGrip` 이 필요 없었습니다** (두 손이 같은 방향, 7-5). 스위치는 잡는 방식을 바꿀 때를 위해 남겨 둡니다.

#### 재는 도구가 네 번 틀렸습니다

`tilt_sign.ps1` 을 만들면서 **판정이 네 번 연속 틀렸습니다.** 같은 함정에 다시 빠지지 않도록 적어 둡니다.

| 틀린 것 | 어떻게 드러났나 | 고친 방법 |
|---|---|---|
| 잘린 `tilt` 정수를 씀 | 오른손이 세 구간 중 둘에서 `-127` 에 박혀 변화량 0 | `#TILT` 가 같이 찍는 `grav` 원값으로 각도를 다시 계산 |
| 기준 구간을 빼서 봄 | 기준(`-80도`)이 스윙 범위(`-62~+87`) 바깥이라 "왼쪽으로 돌렸는데 기준보다 오른쪽" | **좌우 극값끼리만** 비교. 기준은 안 씀 |
| 결과를 ±180 으로 되감음 | 209도를 돌렸는데 `-151도` 로 둔갑 → **부호가 뒤집힘** | 초읽기 동안에도 계속 읽어 각도를 이어 붙임. 되감기는 표본 사이에서만 |
| 흔들림을 고정 한계로 막음 | 폭 190도에 흔들림 ±25도인 멀쩡한 기록을 버림 | 폭 대비(1/3)로 판정 |

> ⚠ **세 번째가 제일 위험했습니다.** 손이 안 흔들렸으면 아무 경고 없이 **정반대 답**을 내놨을 것입니다. 흔들림 가드가 우연히 먼저 걸려서 드러났습니다. 각도를 다루는 코드는 되감기를 **어디서** 하는지가 전부입니다.

> ⚠ 안내 문구도 고쳤습니다. *"오른쪽으로 끝까지 돌리세요"* 는 측정 중에도 계속 돌리라는 말로 들립니다. 재는 것은 **다 돌린 끝 자세**이므로 *"돌린 다음 멈추세요"* 로 바꿨습니다.

도구는 `-SelfTest` 로 보드 없이 판정부만 돌려볼 수 있습니다. 실측 기록 세 벌이 회귀 케이스로 들어가 있고 10건입니다.

---

## 5. 문서가 바뀌면서 갈아엎은 것

작업 도중 `develop`에서 **인터페이스가 바뀌었습니다.** 만들어 둔 파일이 컴파일되지 않아 절반쯤 다시 썼습니다.

| 커밋 | 내용 |
|---|---|
| `b922d67` (09-15) | `IOT_INPUT.md` 최초 추가 (175줄) |
| `9c2e667` (09-16) | `IOT_INPUT.md` 전면 갱신 (320줄). 네 미니게임 공통 키 배치 문서로 성격 변경 |
| `bf62f99` (09-16) | **쥐기 삭제 · 키 재배치 · 달리기 토글** |
| `dfd9101` (09-16) | 무쌍을 IMU 동작 입력으로 전환 |

### 인터페이스 변경

| 종류 | 내용 |
|---|---|
| 삭제 | `bool Grip { get; }` — 압력센서 |
| 삭제 | `bool ConsumeSwing()` |
| 추가 | `bool TryConsumeMotion(out HandMotion motion)` |
| 추가 | `enum HandMotionType { None, HorizontalSwing, VerticalSwing, Thrust }` |
| 추가 | `readonly struct HandMotion { Type, Strength }` |

**쥐기가 사라진 이유**는 장치가 조이콘 형태(스틱 2 + 면버튼 4)로 정해지면서 압력센서 자리가 없어졌기 때문입니다. 하던 일은 "집기 우선"과 "상호작용 한 번 더"로 나눠 갔습니다.

**동작이 3종으로 늘어난 이유**는 무쌍의 가로베기·세로베기·찌르기가 전부 IMU로 들어오게 됐기 때문입니다. 배는 `VerticalSwing` 하나만 쓰지만 **장치는 네 종류를 구분해 줘야 합니다.**

### CSV 포맷 변천

```text
1차 (계획)   id,x,y,grip,buttons
             → IMU가 없어 광산(스윙)·조타·돛·수리가 전부 죽음

2차 (구현)   id,x,y,grip,buttons,tilt,rot,swing,ms
             → 인터페이스 변경으로 grip 사용처 소멸,
               swing 카운터 하나로는 동작 3종을 구분할 수 없음

3차 (현재)   id,x,y,buttons,tilt,rot,mcount,mtype,strength,ms
```

**현재 포맷**

| 필드 | 범위 | 변환 |
|---|---|---|
| `id` | 0~3 | 완드 고유번호. **손이 아님** |
| `x`, `y` | -127~127 | `/127f` + 원형 클램프 → `Stick` |
| `buttons` | 비트 | bit0=버튼1, bit1=버튼2 |
| `tilt` | -127~127 | `/127f` + 클램프 → `Tilt` (조타) |
| `rot` | -127~127 | `/127f` + 클램프 → `Rotation` (돛) |
| `mcount` | 0~255 순환 | 델타만큼 대기열에 적재 |
| `mtype` | 0~3 | `0=None 1=Horizontal 2=Vertical 3=Thrust` |
| `strength` | 0~255 | `/255f` → `HandMotion.Strength` |
| `ms` | millis() 하위 16비트 | 리부팅 판정 전용 |

`#`으로 시작하는 줄은 동글 로그입니다. 버리되 해석 실패로 세지 않고, `logDongleMessages`를 켜면 버리기 직전에 찍습니다.

**필드가 10개보다 많으면 초과분은 무시합니다.** 펌웨어가 값을 덧붙였을 때 유니티까지 같이 배포해야 하면 곤란하기 때문입니다.

### 역방향 포맷 (진동)

올라오는 쪽만 정해져 있었고 내려가는 쪽은 비어 있었습니다. 진동 때문에 뒤늦게 정했습니다.

```text
V,<완드번호 0~3>,<세기 0~255>,<지속 ms>
V,1,200,120
```

| 필드 | 범위 | 변환 |
|---|---|---|
| 완드번호 | 0~3 | 올라오는 `id`와 같은 기기 번호. **손이 아님** |
| 세기 | 0~255 | `Vibrate(strength)`의 `Clamp01` × 255 |
| 지속 ms | 0~65535 | `Vibrate(seconds)` × 1000. 펌웨어가 16비트로 받아 여기서 자름 |

동글은 이 줄을 `Cmd`(8바이트)로 바꿔 ESP-NOW로 해당 완드에 넘깁니다. 완드는 받자마자 `Echo`(4바이트)를 돌려주고, 동글이 그것을 `#ECHO 완드 n seq n`으로 찍습니다. **왕복이 닫혔는지는 이 줄 하나로 봅니다.**

시리얼 모니터에 `V,0,200,120`을 직접 쳐도 같은 일이 일어납니다. 유니티 없이 역방향을 확인할 수 있습니다.

---

## 6. 구조 요약

### 스레드

```text
[읽기 스레드]                        [메인 스레드 · Update]
ReadExisting()
  → StringBuilder 에 이어 붙임
  → '\n' 만난 것만 잘라냄
  → ConcurrentQueue<string> ──────→ DrainLines()   한 프레임 64줄
       (완성된 줄만, 상한 512)          → '#' 이면 버림
  → Thread.Sleep(pollIntervalMs)       → TryParse() 10필드
                                       → Wand.Apply()
                                     RefreshConnections()  타임아웃 판정
                                     ResolveHands()        손 배정 + 토글
```

큐를 건너는 것은 **문자열 한 종류뿐**입니다. 파싱과 상태 갱신은 전부 메인 스레드라 락이 필요 없습니다.

### 손 배정

| 상황 | Left | Right | HasTwoDevices |
|---|---|---|---|
| 둘 다 연결 | `leftHandId` 완드 | `rightHandId` 완드 | `true` |
| 한 대만 연결 | 살아있는 완드 | Left와 **같은 객체** | `false` |
| 둘 다 끊김 | 값이 전부 0인 완드 | 같음 | `false` |

`handTimeoutSeconds`(0.5s) 기반입니다. 짧게 잡으면 줄 한두 개 유실로도 `HasTwoDevices`가 깜빡이고, 그 순간 카메라가 이동 스틱에 붙어 화면이 튑니다.

왼손이 끊기고 오른손만 남아도 **살아있는 쪽을 왼손 자리에** 놓습니다. 걷기가 왼손 스틱이라, 그러지 않으면 완드를 들고도 한 발짝 못 뗍니다.

### 상수

| 이름 | 값 | 이유 |
|---|---|---|
| `WandCount` | 4 | `id` 범위 |
| `CounterWrap` | 256 | `mcount` 순환 |
| `MillisecondsWrap` | 65536 | `ms` 순환 |
| `MaxPendingMotions` | 4 | 알트탭 복귀 시 동작 폭주 방지 |
| `MaxLinesPerFrame` | 64 | 밀린 줄로 프레임이 멎지 않게 |
| `MaxQueuedLines` | 512 | 큐 무한 증가 방지 |
| `MaxLineBufferLength` | 4096 | 개행이 안 올 때 대비 |

### 안전장치

- 포트를 못 열면 **경고 한 줄만 남기고 조용히 비활성**. 모든 값이 0·false로 나가고 게임은 그대로 돌아감
- `OnDisable` / `OnApplicationQuit`에서 스레드·포트 정리. 에디터에서 플레이를 멈췄다 다시 시작해도 포트가 잠기지 않음
- 읽기 스레드의 예외는 `_threadError`로 넘겨 메인 스레드가 한 번만 로그로 옮김
- 끊기면 값·래치·토글을 전부 비움

### 검증 도구 (`firmware/tools/`)

눈으로 알 수 없는 값을 실기로 정하는 스크립트입니다. 둘 다 유니티와 **같은 계산**을 해서, 유니티를 켜지 않고도 답을 냅니다.

| 도구 | 쓰는 때 | 필요한 것 |
|---|---|---|
| `tilt_sign.ps1` | `TILT_SIGN` · `mirroredGrip` 을 정할 때 | 완드 2대를 USB 직결 (`#TILT` 를 읽는다) |
| `steer_verify.ps1` | 조타를 50Hz 로 길게 볼 때 | 동글 (CSV 를 읽는다) |

```
powershell -ExecutionPolicy Bypass -File firmware\tools\tilt_sign.ps1
powershell -ExecutionPolicy Bypass -File firmware\tools\tilt_sign.ps1 -SelfTest
```

- **`-SelfTest`** 는 보드 없이 판정부만 돌립니다. 실측 기록 세 벌이 회귀 케이스로 들어가 있고 **10건**입니다. 판정 규칙을 고치면 이것부터 돌립니다
- 음성으로 구간을 안내합니다. 양손에 완드를 들면 화면을 못 봅니다. `[Console]::Beep` 은 메인보드 비프로 나가 노트북에서 안 들려서 SAPI 로 바꿨습니다
- **PowerShell 5.1 은 BOM 없는 UTF-8 을 ANSI 로 읽습니다.** 한글이 깨지면서 구문 오류가 납니다. 이 파일들을 고칠 때는 **BOM 을 유지**하세요

> ⚠ 이 도구들이 왜 이렇게 생겼는지는 **4-11** 에 있습니다. 판정이 네 번 틀렸고, 그중 하나는 아무 경고 없이 정반대 답을 낼 뻔했습니다.

---

## 7. 남은 일

| # | 내용 | 막고 있는 것 |
|---|---|---|
| 1 | ~~Api Compatibility Level → .NET Framework~~ | **됐음.** `apiCompatibilityLevel: 3` 확인. 팀에 공유함 (4-1) |
| 2 | ~~펌웨어가 `mtype`을 실제로 판정해 보내기~~ | **됐음.** `wand_tinys3.ino` 의 `detectMotion` 이 3종을 판정한다 (아래 7-1) |
| 2b | **네트워크 무쌍에 동작이 안 닿는다** | 아래 7-2. 설계 판단이 필요함 |
| 3 | ~~진동 역방향 프로토콜~~ | **됐음.** `V,id,세기,ms` 로 정하고 `SendVibrate` 를 채웠다 (아래 "진동에 대해") |
| 3b | **진동 모터가 안 달려 있다** | 부품 문제. 경로는 `#VIB` 로그로만 확인된다 |
| 4 | ~~실기 연결 확인~~ | **됐음.** 완드 **2대** 동시 15초 1485줄, 양쪽 유실 0. 양방향 왕복까지 확인 (아래 7-3) |
| 5 | ~~낚시(`IFishingInputSource`)~~ | **코드 됐음.** 낚시 쪽이 연 외부 입력 입구(`SetInputSource`)에 `IotFishingBridge` 로 꽂았다. 낚시 폴더는 안 고쳤다. **씬에 안 붙였고 실기 확인 전** (아래 7-7) |
| 6 | 무쌍 게임 로직 | 이번 스코프 밖. 프로필 연결(2-4)만 했다 |
| 7 | ~~광산 힌트가 키보드로 안 눌린다~~ | **해결됨.** 키보드에 `Mine` 프로필이 생겼다 (아래) |
| 8 | ~~`TILT_SIGN` 좌우 방향 미정~~ | **정해짐.** `+1` · `mirroredGrip` 끔. 실측 3회 (아래 7-5) |
| 9 | ~~조타 중립이 매번 다르다~~ | **고침.** `HelmTask` 가 붙는 순간을 0 으로 잡는다. 실기 확인 남음 (아래 7-5) |
| 10 | ~~광산 이동이 완드로 안 된다~~ | **됐음.** `MineMoveInput` 을 `IPlayerController` 로 옮겼다 (아래 7-6) |
| 11 | **광산 씬이 컴포넌트 순서에 기대고 있다** | `Player Controller Source` 두 칸이 비어 있다 (2-5) |
| 12 | **배 협동 실기 확인** | 코드는 다 되어 있다. 씬에 붙이기만 하면 된다 (2-5) |
| 13 | **로비 · 낚시가 씬에 안 붙어 있다** | 코드만 있다. `Lobby.unity` 에 `IotPlayerController` · `IotLobbyInteract` · `IotFishingBridge` 가 하나도 없다 (7-7) |
| 14 | **완드가 씬을 넘어가지 못한다** | `IotPlayerController` 가 씬마다 따로 있다. 로비 → 미니게임에서 사라진다 (7-7) |
| 15 | **네트워크 광산 · 무쌍에 완드가 안 닿는다** | `MineInputProvider` · `WarriorsInputProvider` 가 키보드를 직접 읽는다. 담당자와 합의 필요 (7-7) |
| 16 | ~~develop 을 받으면 컴파일이 깨진다~~ | **고침.** `AnyWandConnected` 를 공개 프로퍼티 하나로 합쳤다. batchmode 컴파일 에러 0 (7-7) |
| 17 | **포탈 인원 선택 화면이 마우스 전용이다** | develop 에서 포탈이 `CrewPickerScreen` 을 먼저 띄운다. 완드로 포탈은 열리지만 인원은 마우스로 골라야 한다 (7-7) |

### 광산 힌트 (7번) — 해결됨

**한동안 이 문서가 틀린 말을 하고 있었습니다.** 기록으로 남깁니다.

`Shared` 프로필만 있던 시절에는 왼손 버튼 2 가 `Key.None` 이라 힌트가 키보드로 안 눌렸고, 동작(IMU) 키도 전부 `Key.None` 이라 파기도 안 됐습니다. 그래서 "광산은 키보드로 테스트가 안 된다"고 적어 뒀습니다.

그 사이 `KeyboardPlayerController` 에 **`Mine` 프로필이 추가**됐습니다. (`9cb29308 [fix] 광산 프로필의 오른손 면버튼 2 를 Shift 로`)

| 광산 행동 | 완드 | 키보드 (`Mine` 프로필) |
|---|---|---|
| 이동 | 왼손 스틱 | `W A S D` |
| 땅 파기 | 세로 내리치기 | `Space` (VerticalSwing 흉내) |
| 땅 복구 | 왼손 버튼 1 | `C` |
| 힌트 | 왼손 버튼 2 | `J` |
| 달리기 | 오른손 버튼 2 | `Shift` |

`MineTest.unity` 의 `KeyboardPlayerController` 가 이미 `controlProfile: 2`(= `Mine`) 로 맞춰져 있습니다. **완드 없이도 광산 전체를 키보드로 확인할 수 있습니다.**

> 교훈 — 다른 담당자 파일의 상태를 근거로 쓸 때는 **읽은 시점**을 같이 적어야 합니다.
> 이 문서는 한 세션 안에서도 낡을 수 있습니다.

### 7-1. 동작 3종은 실제로 들어온다

펌웨어가 `mtype` 을 판정합니다. 체인을 끝까지 맞춰 봤습니다.

| 단계 | 어디 | 확인한 것 |
|---|---|---|
| 판정 | `wand_tinys3.ino` `detectMotion` | Y·Z 회전 최대값이 `MOTION_MIN_PEAK`(200dps) 이상이면 스윙, `Z > Y × 1.2` 면 가로(1) 아니면 세로(2). 회전 없이 가속만 `THRUST_ACCEL`(0.8G) 넘으면 찌르기(3) |
| 전송 | 같은 파일 | 20ms(50Hz) 주기, 동작 쿨다운 500ms |
| 중계 | `dongle_esp32s3.ino` | CSV 필드 순서가 브리지 파싱 순서와 같음 |
| 해석 | `IotPlayerController.ToMotionType` | 1→`HorizontalSwing` 2→`VerticalSwing` 3→`Thrust` |
| 소비 | `WarriorsKeyboardInput.TryConsumeAttack` | 세 종류를 각각 가로베기·세로베기·찌르기로 넘김 |

**X축(길이 방향)을 판정에서 뺀 것이 핵심입니다.** 손목 비틀기가 X 400dps 대로 튀어서 가로베기로 잡히던 문제를 펌웨어 쪽에서 막았습니다. 스윙과 찌르기의 감지 창을 하나만 여는 것도 같은 이유입니다 — 따로 열면 스윙 감속이 찌르기로 또 잡혔습니다.

`mcount` 델타가 2 이상이면 같은 종류로 뭉칩니다(줄 하나에 `mtype` 이 하나뿐). 다만 쿨다운 500ms 에 전송이 20ms 라 **줄 25개가 연속으로 유실돼야** 생기는 일이라 두고 봅니다.

### 7-2. 네트워크 무쌍에는 안 닿는다

무쌍에는 IoT 진입점이 **두 개** 있고, 이 브리지는 그중 하나만 채웁니다.

| 씬 | 입력원 | 완드 동작 |
|---|---|---|
| 로컬 · 싱글 (`WarriorsSceneBootstrap`) | `WarriorsKeyboardInput` → `IPlayerController.TryConsumeMotion` | **닿는다** |
| 네트워크 (`WarriorsSceneSetup`) | `WarriorsInputProvider` → `WarriorsIoTInput.AttackRequested` | **안 닿는다** |

`WarriorsSceneSetup.StripLocalOnlyParts` 가 네트워크 플레이어 프리팹에서 `WarriorsKeyboardInput` 을 **떼어냅니다.** 각자 자기 화면에서만 베는 것을 막으려는 것이라 맞는 결정입니다. 대신 `WarriorsInputProvider` 가 키보드를 직접 읽고, 장치는 `WarriorsIoTInput.OnSwing` 으로 들어오게 되어 있습니다. 그쪽 주석도 *"장치가 붙는 쪽에서 `WarriorsIoTInput.OnSwing` 을 부르면 그 순간부터 이 경로로 흘러든다"* 고 적고 있습니다.

**그 호출자가 없습니다.** 시리얼 브리지는 `IPlayerController` 만 채웁니다.

**공격만이 아니라 이동도 안 닿습니다.** `WarriorsInputProvider` 는 `IPlayerController` 를 아예 안 읽습니다.

```csharp
// WarriorsInputProvider.cs
if (keyboard.wKey.isPressed) move.y += 1f;
...
data.Move = move;
```

> ⚠ **"서버 쪽은 잘 된다" 와 충돌하지 않습니다. 서로 다른 구간을 말하는 것입니다.**
>
> | | 확인한 구간 | 결과 |
> |---|---|---|
> | 서버 담당 | `WarriorsIoTInput` → 네트워크 → 서버 | **된다** |
> | 이 문서 | **완드** → `WarriorsIoTInput` | **연결이 없다** |
>
> `SubmitImuSample` 을 실제로 부르는 곳은 `WarriorsFakeImuInput`(가짜 IMU 생성기) **하나뿐**입니다. 그걸로 확인하면 IoT 경로가 끝까지 도는 것이 맞습니다. 다만 그 입구에 진짜 완드가 안 물려 있습니다.
>
> **가르는 방법:** 완드를 들고 **네트워크 씬에서** 휘둘러 봅니다. 로컬 씬(`WarriorsTest`)이나 가짜 IMU 로는 이 차이가 안 드러납니다.

메울 방법이 둘인데 성격이 다릅니다.

| | 하는 일 | 문제 |
|---|---|---|
| `OnSwing` 을 부른다 | 브리지가 판정된 동작을 그대로 넘긴다 | 완드·`WarriorsIoTInput` 둘 다 쿨다운이 걸려 이중이 된다 (250ms + 0.45s) |
| `SubmitImuSample` 을 부른다 | 원시 IMU 를 넘겨 판정을 그쪽에 맡긴다 | 지금 CSV 는 **판정 결과만** 보낸다. 원시 각속도·가속도를 안 실어서 포맷을 늘려야 한다 |

`OnSwing` 쪽이 현실적입니다. 완드가 종류와 세기를 이미 판정해서 보내므로 값이 다 있습니다. 연결 자체는 10줄쯤입니다.

또 `WarriorsIoTInput.minimumStrength` 가 **0.35** 인데, 펌웨어 최소 스윙(350dps)의 세기는 `(350-150) × 255/450 ÷ 255 ≈ 0.44` 입니다. 간신히 넘습니다 — **조금만 약하게 베면 버려집니다.** 임계값을 함께 맞춰야 합니다.

쿨다운 이중과 `minimumStrength` 둘 다 무쌍 담당자와 값을 맞출 일이라 **손대지 않았습니다.**

### 7-3. 실기에서 확인한 것

**지금 보드에 굽혀 있는 것** — 셋 다 같은 소스입니다.

| 보드 | 포트 | STA MAC | 굽는 법 |
|---|---|---|---|
| 동글 ESP32-S3 DevKit | COM3 | `58:E6:C5:6A:92:D8` | `--fqbn esp32:esp32:esp32s3` |
| 완드 0 (TinyS3 #1) | COM7 | `DC:54:75:EB:82:C0` | `--fqbn esp32:esp32:um_tinys3` (기본값) |
| 완드 1 (TinyS3 #4) | COM13 | `DC:54:75:EB:80:B4` | 같은 것 + `--build-property "compiler.cpp.extra_flags=-DWAND_ID=1"` |

`WAND_ID` 와 `HAS_IMU` 는 `#ifndef` 로 감싸 뒀습니다. **IDE 에서는 값을 고치고, arduino-cli 에서는 파일을 안 고치고 덮어씁니다.** 보드마다 파일을 고쳤다 되돌리는 것이 제일 사고가 나는 지점이라 그렇게 했습니다.

> ⚠ **지금 두 완드 다 되돌림 거르기가 꺼진 채로 굽혀 있습니다.** 무쌍 리듬 구간용입니다(7-4). 광산·배 협동을 확인할 때는 `mcount` 가 약 2배로 들어오니, **그 게임들을 볼 때는 주석을 풀고 다시 구워야** 합니다.

완드는 배터리로도 돕니다. 보조 배터리 전원으로 완드 0 을 8초 재 봤을 때 390줄 · 유실 0 으로 USB 와 차이가 없었습니다. 다만 보조 배터리는 소비 전류가 적으면 스스로 꺼지는 것이 있어, 안 들어오면 **전원부터** 봅니다.

> ⚠ **동글 포트는 배타적입니다.** 유니티가 Play 중이면 시리얼 모니터가 안 열리고, 반대도 같습니다. 값이 안 나오면 이것부터 봅니다. (2-3)

> ⚠ **`arduino-cli upload` 는 컴파일을 다시 하지 않습니다.** 소스를 고친 뒤 `upload` 만 부르면 옛 빌드가 올라갑니다. 언제나 `compile --upload` 를 씁니다.

> ⚠ **두 세션이 같은 스케치를 동시에 빌드하면 깨집니다.** `arduino-cli` 가 스케치 경로 해시로 캐시 디렉터리를 잡아서 서로 밟습니다. `Wire.cpp.d: No such file or directory` 나 `cannot specify '-o' with '-c' ... multiple files` 가 나오면 이것이고, `--clean` 으로 복구합니다.

**측정 결과**

| 항목 | 결과 |
|---|---|
| 완드 2대 동시, 15초 | CSV 1485줄. **양쪽 유실 0%** |
| `ms` 간격 | 20/21ms 두 종류뿐. 30ms 넘는 구멍 0개 |
| 16비트 순환 | `-65515` 1회 관측 = 65536 경계. 4-5 의 wrap 처리가 쓰이는 자리 |
| 완드 자동 등록 | `#WAND 0 등록 DC:54:75:EB:82:C0` |
| 진동 왕복 | `V,0,200,120` → `#ECHO 완드 0 seq 1` → 완드에서 `#VIB 시작 세기 200 120 ms` |
| 16비트 지속시간 | `500ms` 가 그대로 도착. `arg3`(상위 바이트)가 실제로 쓰인다 |
| 잘못된 입력 | 완드번호 9 거절, 미등록 완드 거절, `HELLO` 거절 |

`handTimeoutSeconds` 가 0.5초인데 최대 간격이 21ms 라 **24배 여유**입니다. 4-7 이 걱정한 `HasTwoDevices` 깜빡임은 이 조건에서 안 일어납니다.

대역폭은 완드 2대에 2,700 B/s 로 115200baud 의 23% 입니다. **4대까지 가도 47%** 라 동글은 손댈 것이 없습니다.

**2026-09-23 — 조타가 유니티에서 돌았습니다.**

| 항목 | 결과 |
|---|---|
| 배선 | 동글 COM3(유선) · 완드 0 **무선** · 완드 1 유선 |
| 동글 수신 4초 | CSV 388줄 = 완드 0 이 194줄, 완드 1 이 194줄. **50Hz 두 대 정확히 반씩** |
| 유니티 | `ShipCoopTest` 에서 조타가 실제로 움직임 |

> ⚠ **유니티에 잡는 포트는 동글(COM3)입니다.** 완드를 USB 로 직접 꽂아도 CSV 는 안 나옵니다 — 완드의 시리얼은 `#` 로 시작하는 디버그뿐이고, 측정값은 ESP-NOW 로만 나갑니다. 완드 포트(COM7 · COM13)를 잡으면 한 줄도 안 들어옵니다.
>
> 완드를 USB 에 꽂는 것은 전원·굽기·`#TILT` 확인용입니다. 무선으로 써도 동글에는 똑같이 올라옵니다 (위 표에서 완드 0 이 무선인데 194줄로 동일).

### 7-4. 동작 판정을 실측으로 맞춘 것

무쌍에서 **"세게 찌를수록 안 먹힌다"** 는 증상이 있었습니다. 완드 로그를 종류별로 30회씩, 다섯 번 모아서 원인 세 가지를 찾았습니다. 값을 되돌리기 전에 여기를 보세요.

**1. 문턱이 낮아서 찌르기가 베기로 샜다** — `MOTION_MIN_PEAK` 200 → **350**

힘줘 찌르면 손목이 딸려 돌아가 Z 회전이 200~301 까지 나옵니다. 문턱이 200 이라 **진짜 찌르기 10회 중 7회가 스윙 분기로 넘어갔고**, 게임이 "찌르기" 로 받은 것은 손을 빼는 약한 동작이었습니다. 세게 찌를수록 틀리는 구조였습니다.

```
찌르기가 딸고 오는 회전   최대 301
진짜 좌우베기            최소 440      ← 그 사이가 비어 있다
진짜 세로내리치기         최소 431
```

350 으로 올린 뒤 오분류 **7건 → 0건**.

**2. `fabs()` 가 되돌림의 방향을 지웠다** — 부호 있는 최대값을 따로 들고 비교

내려치는 것과 팔을 올리는 것은 **같은 축을 반대로** 도는 것이라, 크기만 보면 똑같습니다. 그래서 10회 휘두른 것이 31회로 잡혔습니다.

**쿨다운으로는 못 막습니다.** 250 → 450 으로 올려 봤지만 이벤트 간격이 `MOTION_MAX + MOTION_COOLDOWN` 에 딱 붙어 포화될 뿐이었습니다. 1회로 만들려면 쿨다운이 제스처 전체 길이를 넘어야 하는데 그건 사람마다 다릅니다.

`sPeakGy` · `sPeakGz` 로 부호를 살려서, **같은 축을 반대로 돌았고 게다가 더 약하면** 되돌림으로 봅니다(`isReturn`). "더 약하면" 을 붙인 덕에 **진짜 연타는 안 걸립니다** — 두 번째도 세게 치기 때문입니다.

비교는 **직전에 인정한 동작의 축**으로 합니다. 현재 동작이 무엇으로 분류됐는지는 상관없습니다. 되돌림은 문턱을 못 넘어 찌르기로 분류되기도 하는데, 그때 현재 축으로 비교하면 기준과 축이 안 맞아 그냥 통과합니다.

**3. 찌르기에 방향 조건이 없었다** — `fabs(peakLax)` 가 Y·Z 보다 커야 찌르기

찌르기는 완드를 **길이 방향(X)으로 미는 것**입니다. 이 조건이 없으니 베기의 되돌림이 전부 찌르기로 샜습니다. 세로베기 뒤에 나온 가짜 찌르기 13개 중 **12개가 Z 지배**였습니다.

넣은 뒤 세로베기 뒤 가짜 찌르기 **15개 → 0개**, 가로베기 뒤 **12개 → 2개**.

**남은 것** — 10회가 14회로 잡힙니다. 다만 **전부 같은 종류**라 "한 번 베면 한두 번 더 벤다" 수준입니다. 되돌림이 직전 공격보다 셀 때가 있어서(사람이 매번 같은 힘으로 안 침) "더 약하면" 조건을 빠져나갑니다.

그 조건을 빼면 10 에 가까워지지만 **좌우 연속 콤보가 막힙니다.** 무쌍은 다수를 베는 게임이라 여분의 같은 공격이 자연스러울 수도 있어, 화면에서 거슬리는지 보고 정하기로 했습니다.

> 게임 쪽은 여러 번 들어오는 것을 막지 않습니다. `WarriorsPlayerCombat.StartAttack` 이 공격 중이면 **버립니다** 가 아니라 **버퍼에 넣었다가 이어서 재생**합니다.

#### ⚠ 지금 되돌림 거르기(2번)는 **꺼져 있습니다**

무쌍 **리듬 구간**에서 박자를 놓쳐서 뺐습니다. 리듬은 박자마다 입력이 들어와야 하는데, 되돌림을 거르면 빠르게 이어 치는 동작이 같이 먹혔습니다. 여분의 입력보다 놓치는 쪽이 치명적이라는 판단입니다. 빼니 잘 된다고 확인했습니다.

`isReturn` 블록만 주석으로 감쌌고, **분류(문턱 350 · 찌르기 X축 조건)는 그대로**입니다. 종류는 여전히 맞게 나갑니다.

**부작용 두 가지가 다른 미니게임에 갑니다.** 완드는 네 게임이 같이 씁니다.

| | |
|---|---|
| `mcount` 가 약 2배 | 한 번 휘두르면 되돌림까지 2회쯤 잡힌다. **광산은 한 번에 두 번 파이고**, 배 협동 수리·망치질도 두 번 먹힌다 |
| 가짜 찌르기가 늘어남 | 위 2번 설명대로 되돌림은 문턱을 못 넘어 찌르기로 분류되기도 한다. 배·광산은 `Thrust` 를 안 써서 조용히 무시되지만, **무쌍에서 세로베기가 찌르기로 한 번 더** 들어간다 |

> ⚠ **주석으로 껐다 켰다 하지 말고 빌드 플래그로 빼는 쪽을 권합니다.** 커밋할 때마다 어느 쪽 상태인지 헷갈립니다. `WAND_ID` · `HAS_IMU` 와 같은 방식입니다.
>
> ```cpp
> #ifndef FILTER_RETURN
> #define FILTER_RETURN 1     // 0 이면 되돌림을 안 거른다 (무쌍 리듬용)
> #endif
> ```
>
> 그러면 리듬용 보드만 `-DFILTER_RETURN=0` 으로 굽고, 나머지는 소스를 고치지 않고 굽습니다.

### 7-5. 배협동 세션으로 넘기는 것

**`TILT_SIGN` = `+1`, `mirroredGrip` = 끔 으로 확정했습니다.** (2026-09-23 실측, 완드 0 · 1)

IMU 가 뒤집혀 붙어 있어 기준 자세에서 `az ≈ -0.97` 입니다. 예전 `atan2(ay, az)` 는 `roll ≈ -169도` 를 내서 **`tilt` 가 -127 에 고정**돼 있었습니다. 지금은 `atan2(-ay, -az)` 로 바꿔 두 완드 다 기준 자세에서 `#TILT` 가 5~15 로 나옵니다.

양손으로 휠을 잡고 좌우 끝까지 돌린 **끝 자세**를 세 번 따로 쟀습니다. 왼쪽 끝에서 오른쪽 끝까지의 각도입니다.

| 측정 | 왼손 | 오른손 |
|---|---|---|
| 1 | +132.5도 | +149.2도 |
| 2 | +209.3도 | +204.2도 |
| 3 | +190.2도 | +160.4도 |

여섯 번 다 **같은 부호**입니다. 두 완드의 roll 이 같은 방향으로 움직이므로 `mirroredGrip` 은 필요 없고, 오른쪽이 양수이므로 `TILT_SIGN` 도 `+1` 그대로입니다. 잡는 자세가 매번 달랐는데도 부호는 일관됐습니다.

판정은 `firmware/tools/tilt_sign.ps1` 이 합니다. 다시 정해야 하면 그것만 돌리면 됩니다.

> ⚠ **잡는 방식을 바꾸면 다시 재야 합니다.** 지금 결론은 *"두 완드를 같은 방향으로 쥔다"* 는 전제 위에 있습니다. 손바닥을 마주 보게 쥐는 배치로 바꾸면 부호가 반대가 되고, 그때 `mirroredGrip` 을 켭니다. (4-11)

> ⚠ **두 완드의 IMU 는 같은 방향으로 달아야 합니다.** 한 번 반대로 달았다가 완드 1 의 `tilt` 만 -127 로 포화된 적이 있습니다. 동작 판정(가로·세로·찌르기)은 영향이 없지만 — `fabs` 로 크기만 보고, 되돌림 필터는 같은 완드 안에서만 부호를 비교하므로 — **조타만 깨집니다.**

> ⚠ **IMU 배선을 고쳤으면 보드를 리셋해야 합니다.** `CTRL1_XL` · `CTRL2_G` 설정은 `setup()` 에서 한 번만 씁니다. 부팅 때 배선이 잘못돼 있었으면 그 뒤에 고쳐도 IMU 는 파워다운인 채로 0 만 내보냅니다. `r` 은 자이로 재보정만 하고 레지스터를 다시 쓰지 않습니다. **조이스틱 중립도 부팅 때만 잽니다.**

조타는 `ShipCoopInput.Steer` 가 **양손 평균**(`(Left.Tilt + Right.Tilt) * 0.5f`)을 씁니다. 한쪽 완드의 `tilt` 가 죽어 있으면 조타가 절반만 먹습니다. 1대일 때(`Left.Tilt`)보다 오히려 나빠지므로, 배협동은 **두 완드가 다 멀쩡한 뒤에** 봐야 합니다.

`mirroredGrip`(2-2)도 같은 문제를 다룹니다. 조타륜을 잡듯 손바닥을 마주 보게 쥐면 두 완드의 roll 부호가 반대로 나와 평균이 상쇄됩니다. 지금 배치에서는 **끔** 입니다.

#### 조타 중립이 매번 다릅니다 — `HelmTask` 에서 잡았습니다

부호는 정해졌지만 **0 이 어디인지는 못 정했습니다.** 같은 사람이 "조타 자세로 가만히" 를 세 번 잡았는데 왼손 기준 각도가 이렇게 나왔습니다.

```
-38.1도   ·   -0.8도   ·   +71.6도
```

`Tilt` 는 **중력 기준 절대 각도**입니다. 잡는 각도가 세션마다 70도씩 달라지면, 손을 가만히 두고도 조타가 한쪽으로 먹습니다. 왼손 기준이 `+71도` 인 날에는 `tilt ≈ +100` 이 되어 **가만히 있어도 배가 오른쪽으로 계속 돕니다.**

좌우 폭 자체는 160~209도로 충분히 넓습니다. 문제는 폭이 아니라 **원점**입니다.

**해결 — 붙는 순간의 값을 빼서 그 자리를 중립으로 삼습니다.** (`HelmTask`, 배 협동 담당 동의를 받고 넣음)

```csharp
protected override void OnWorkerJoined(TaskWorker worker)
{
    _neutralSteer[worker] = ShipCoopInput.Steer(worker.Input);
}

private float SteerOf(TaskWorker worker)
{
    float raw = ShipCoopInput.Steer(worker.Input);
    if (_neutralSteer.TryGetValue(worker, out float neutral)) { raw -= neutral; }
    return Mathf.Clamp(raw, -1f, 1f);
}
```

`Work()` 가 `ShipCoopInput.Steer(...)` 대신 `SteerOf(...)` 를 부릅니다. 그게 전부입니다.

**왜 `HelmTask` 인가.** `ShipCoopInput.Steer` 는 상태가 없는 정적 함수입니다. 중립은 **사람마다 · 붙을 때마다** 달라서 상태가 필요하고, `TaskBase` 에 `OnWorkerJoined` / `OnWorkerLeft` 훅이 이미 있었습니다.

같이 챙긴 것:

| | |
|---|---|
| `Clamp(-1, 1)` | 중립을 뺀 뒤 한쪽으로 1 을 넘을 수 있다. 그대로 두면 `Capacity` 계산이 틀어진다 |
| `OnWorkerLeft` 에서 제거 | 안 지우면 `Dictionary` 가 샌다 |
| `ResetHelm` 에서 **재포착** | 지우기만 하면 다음 판이 중립 없이 시작한다 |

**키보드는 영향이 없습니다.** `KeyboardPlayerController` 는 안 누르면 `Tilt` 가 0 이라 빼는 값도 0 입니다. A/D 조작은 그대로입니다.

> ⚠ **`IHandDevice.Tilt` 의 뜻은 바꾸지 않았습니다.** 여기를 상대 각도로 바꾸면 광산 · 무쌍도 같이 영향을 받습니다. 공용 경계라 미니게임 담당자들과 함께 정할 일입니다. (`GAME_STRUCTURE.md` 9장) 배 협동만의 해석이라 그쪽 파일에서 풀었습니다.

> ⚠ **남은 한계 — 잡는 순간의 자세가 중립이 됩니다.** 팔을 내린 채로 상호작용하면 그 각도가 0 이 되어, 손을 들어올리는 것만으로 배가 돕니다. 지금은 **붙는 순간 한 번만** 잡습니다. 거슬리면 "붙고 한 박자 뒤에 잡기" 나 "버튼으로 다시 잡기" 를 얹어야 하는데, 어느 쪽이 나은지는 쥐어보고 정할 일이라 가장 단순한 것만 넣었습니다.

> ⚠ **`tilt` 는 ±90도에서 잘립니다.** (`roll * 127/90` 을 ±127 로 자름) 실측 반폭이 80~105도라 **끝자락이 잘립니다.** 원점만 제대로 잡으면 "90도만 돌려도 최대 조타" 가 되어 오히려 편할 수 있습니다. 넓혀야 한다고 판단되면 그때 펌웨어를 고칩니다 — 지금 값으로 못 쓸 정도는 아닙니다.

### 7-6. 광산으로 넘기는 것

광산은 **키보드 · 완드 둘 다 실기 확인까지 끝났습니다.** 다만 남의 게임 파일을 고쳤으므로 담당자에게 알려야 할 것이 있습니다.

#### 왜 옮겨야 했나

`IOT_INPUT.md` 3장("광산 — 키보드가 본체이고 장치는 나중입니다")의 **현재 구현과의 차이** 표가 이 작업을 그대로 지시하고 있었습니다.

> | 걷기 | 왼손 스틱 | `MineMoveInput` 이 `Input.GetAxis` 로 직접 읽는다 | **장치를 안 거침** |
> | 달리기 | 오른손 면버튼 2 | `MineMoveInput.runKey` 가 `Input.GetKey` 로 직접 읽는다 | **장치를 안 거침** |
>
> **걷기와 달리기가 장치를 안 거칩니다.** … `IPlayerController.Move` 를 읽도록 고쳐야 합니다.

레거시 `Input` 을 그대로 두면 **완드가 걷기를 채울 길이 원천적으로 없습니다.** 같은 문서 7장이 *"키 이벤트를 만들어 보내기 — 네트워크 경로를 안 탑니다. 인터페이스를 구현하세요"* 로 그 우회를 막아 뒀기 때문입니다. 장치가 값을 올릴 수 있는 자리는 `IPlayerController` 하나뿐입니다.

그래서 광산은 입력 출처가 **둘로 갈려 있었습니다.** `MineDigger` 는 경계를 읽고, `MineMoveInput` 은 키보드를 직접 읽었습니다. 완드를 꽂으면 파기·복구·힌트만 살아나고 걷기는 죽는 반쪽 상태가 됩니다.

`MINE.md` 8장 표도 이동을 `IPlayerController.Move` 로 정해 두고 있어, 문서 두 개가 같은 곳을 가리키는데 코드만 따라오지 않은 상태였습니다.

#### 고친 것 — 파일 하나

`MineMoveInput.cs` (`e9a2e54c [feat] 광산 이동을 IPlayerController 로`)

```csharp
// 이전
Vector2 axis = new Vector2(Input.GetAxis(horizontalAxis), Input.GetAxis(verticalAxis));
bool run = Input.GetKey(runKey);

// 지금
Vector2 axis = _controller.Move;
bool run = _controller.HasTwoDevices && _controller.Right.Button2;
```

고아가 된 `horizontalAxis` · `verticalAxis` · `runKey` 는 지웠습니다. 점프는 그대로 뒀습니다 — 씬에서 `jumpButton` 을 비워 둬서 실행되지 않고, `MineJump` 가 실행 순서 −50 에서 덮어쓰는 구조도 건드리지 않았습니다.

#### ⚠ 회귀 하나 — 방향키

예전에는 레거시 `Input.GetAxis("Horizontal")` 이라 **WASD 와 방향키를 둘 다** 받았습니다. 지금은 `Move` 하나이고 키보드는 거기에 WASD 만 답니다. **방향키로는 안 움직입니다.**

`IOT_INPUT.md` 1장이 네 게임 공통 이동을 `W A S D` 로 정했으니 방향은 맞습니다. 다만 방향키로 테스트하던 습관이 있으면 고장으로 보입니다.

#### 달리기를 오른손 버튼 2 에 둔 이유

왼손 버튼이 이미 다 찼습니다 — 버튼1 은 복구, 버튼2 는 힌트입니다. (MINE.md 8장)

`KeyboardPlayerController` 의 `Mine` 프로필이 오른손 버튼 2 를 **Shift** 에 달아 두고 있어, 그 자리를 그대로 읽습니다. 키보드 조작감은 이전과 같습니다.

> ⚠ 버튼 1 이 아니라 2 입니다. 오른손 버튼 1 은 키보드에서 **Space** 인데 그건 광산에서 땅 파기라, 거기에 달리기를 걸면 팔 때마다 달립니다.

**완드 1대에서는 달리기가 빠집니다.** `Right` 가 `Left` 와 같은 객체라 그 버튼이 곧 힌트 버튼이 되기 때문입니다. MINE.md 가 1대 기준으로 꼽은 필수 입력 넷(이동 · 스윙 · 복구 · 힌트)에 달리기가 없으므로 규격에는 맞습니다.

#### 안 건드린 것

- **카메라** — `MineCamera` 는 이미 우클릭 드래그이고 커서도 안 잠급니다. `IOT_INPUT.md` 7장의 광산 카메라 항목은 **이미 끝나 있었습니다.** MINE.md 8장 IoT 표에도 카메라가 없어 스코프 밖입니다.
- **네트워크 광산** — `MineInputProvider` 가 새 Input System 으로 키를 직접 읽습니다. `IPlayerController` 를 안 봐서 **네트워크에서는 완드가 안 닿습니다.** 무쌍 7-2 와 같은 구조이고, 로컬을 먼저 하기로 해서 미뤘습니다.

### 7-7. 로컬은 끝, 네트워크는 남음 (2026-09-25 기준)

> 이 절은 `origin/develop` `812788cb` 까지 이 브랜치에 병합(`1248a6d2`)하고 적었습니다. 그 뒤로는 다시 확인해야 합니다.

#### 어디까지 됐나

| 게임 | 로컬 씬 (테스트용) | 실제 네트워크 씬 | 완드가 닿는가 |
|---|---|---|---|
| 로비 이동 · 점프 · 달리기 | — | `Lobby` | **코드 됐음, 씬에 안 붙음.** `PlayerInputProvider.OnInput` 안에서 더하므로 Fusion 입력으로 그대로 서버에 간다 |
| 로비 카메라 | — | `Lobby` | 같음. `LocalPlayerView` (카메라는 로컬 전용이라 네트워크 불필요) |
| 로비 상호작용 (낚시 · 포탈 · 제단) | — | `Lobby` | 같음. `IotLobbyInteract` 가 키와 똑같은 함수(`Enter` · `InteractPressed`)를 부른다 |
| 낚시 | — | `Lobby` | 같음. 낚시는 로컬 권한(`LocalFishingAuthority`)이고 연출만 동기화하므로 입력을 네트워크에 실을 필요가 없다 |
| 배 협동 | `ShipCoopTest` 실기 확인 | `ShipCoopBoot` | **안 닿음.** 러너에 `KeyboardPlayerController` 가 붙어 있다. `IotPlayerController` 로 바꾸면 값 전송 · 진동 RPC 는 이미 있다 |
| 광산 | `MineTest` 실기 확인 | `MineNet` | **안 닿음.** `MineInputProvider` 가 키보드를 직접 읽는다 |
| 무쌍 | `WarriorsTest` 실기 확인 | `WarriorsNet` | **안 닿음.** `WarriorsInputProvider` 가 키보드를 직접 읽는다 (7-2) |

`IotPlayerController` 가 들어 있는 씬은 지금 `MineTest` · `WarriorsTest` **둘뿐**입니다. `IotLobbyInteract` · `IotFishingBridge` 는 어느 씬에도 없습니다.

#### 남은 일 — 순서대로

1. ~~develop 을 받고 `AnyWandConnected` 이름 겹침을 푼다.~~ **됐음.** git 은 충돌 없이 합쳐 주지만 합친 파일에 `public bool AnyWandConnected { get; }`(배 협동 HUD 용, `4e9d3f09`)와 `private bool AnyWandConnected()`(키보드 자동 전환용)가 함께 생겨 CS0102 로 깨지는 자리였습니다.
   - **이름과 공개 여부는 HUD 쪽을 따랐습니다.** `ShipCoopHud` 가 `wand.AnyWandConnected` 로 읽습니다.
   - **판정은 완드 전체를 훑는 쪽으로 했습니다.** HUD 원본은 `_left.Connected` 만 봤는데, 손 배정이 다시 돌기 전에는 방금 붙은 완드가 왼손 자리에 없을 수 있습니다. 키보드 자동 전환도 같은 프로퍼티를 보므로 **키캡과 실제 입력이 같은 답을 냅니다.**
   - `EnsureWands` → `EnsureKeyboardFallback` → `RefreshKeyboardFallback` → `AnyWandConnected` → `EnsureWands` 로 돌아오지만, 그때는 `_wands` 가 이미 있어 곧바로 빠집니다. 되부름이 아닙니다.
   - Unity 6000.5.9f1 batchmode 로 전체 컴파일 — 에러 0.
2. **완드를 게임 내내 하나로 둔다.** 씬이 `SceneManager.LoadScene` 으로 통째로 바뀌므로 로비에 둔 컨트롤러는 미니게임에서 사라지고 포트를 다시 찾습니다. `DontDestroyOnLoad` 로 하나만 두고, 씬에 들어갈 때 `SetControlProfile` 을 부르게 합니다. (지금은 무쌍만 부른다)
3. **로비에 붙이고 확인한다.** `IotPlayerController` · `IotLobbyInteract` 하나씩, `PlayerFishingAdapter` 옆에 `IotFishingBridge`. 두 클라이언트를 띄워 **상대 화면에서도** 내 캐릭터가 완드로 움직이는지 봅니다.
4. **배 협동** — `ShipCoopInputProvider.Awake` 가 `GetComponent` 로만 찾는다. 2번의 컨트롤러를 찾는 한 줄이 필요하다. 배 협동 담당에게 알리고 넣는다.
5. **광산 · 무쌍** — 담당자 파일이라 방식부터 합의한다. 광산 파기와 무쌍 공격은 "사건" 이라 감지한 틱에만 켜서 보내야 한다 (서버가 `GetPressed` 로 판정). 무쌍은 7-2 의 쿨다운 이중 · `minimumStrength` 도 같이 정한다.

> ⚠ **포탈 뒤의 인원 선택 화면은 마우스 버튼뿐입니다.** develop 에서 `MiniGamePortal.Enter` 가 바로 떠나지 않고 `MatchScreenFlow.Begin` → `CrewPickerScreen` 을 띄웁니다. `TryEnterFromDevice` 는 키와 같은 `Enter` 를 부르므로 **화면은 완드로 열리지만 인원은 마우스로 고릅니다.** 완드로 끝까지 가려면 그 화면이 `IPlayerController` 를 읽거나 UI 내비게이션을 받아야 합니다 — 매칭 화면 담당과 정할 일입니다.

> ⚠ **장치용 네트워크 경로를 따로 내지 않습니다.** 각 게임의 `OnInput` 안에서 키보드와 같은 자리에 더합니다. 따로 보내면 같은 틱에 입력이 두 번 들어갑니다. (`IOT_INPUT.md` 5장)

### 진동에 대해

`ShipCoopNetworkedController`에 `Rpc_Vibrate`가 있어 서버 → 클라이언트 → `devices.Left/Right.Vibrate()`로 내려옵니다. 즉 **게임 쪽은 이미 부를 준비가 되어 있습니다.**

1차는 빈 구현으로 갔습니다. 시리얼이 완드 → PC 한 방향뿐이라 울릴 수단이 없었고, `IOT_INPUT.md` 5장이 *"장치가 못 하면 빈 함수로 둬도 게임은 돌아갑니다"*라고 허용해 뒀기 때문입니다.

**역방향이 급해진 것은 무쌍입니다.** 몬스터를 때린 느낌을 진동으로 주려는데, 진동은 다른 미니게임도 쓰므로 **판정한 게임이 장치에게 시키는 쪽**이 맞습니다. 완드가 스스로 울릴 수는 없습니다.

ESP-NOW가 양방향으로 도는 것을 실기로 확인하고 프로토콜을 `V,id,세기,ms`로 정했습니다(5장 역방향 포맷). 예고한 대로 `SendVibrate` **하나만** 채웠고, 부르는 쪽(`Wand.Vibrate`, `VibrateBoth`)은 한 줄도 안 건드렸습니다.

**포트 충돌은 락 없이 해결됩니다.** 두 가지 이유입니다.

1. `SerialPort`는 읽는 쪽 한 스레드 · 쓰는 쪽 한 스레드면 서로를 건드리지 않습니다. `ReadExisting`과 `Write`는 같은 fd에 각각 `read()`/`write()`를 할 뿐 공유 버퍼가 없습니다.
2. **`ClosePort`도 `SendVibrate`도 메인 스레드입니다.** 게임 로직 → `Wand.Vibrate` → `SendVibrate`가 전부 메인이고 `OnDisable`/`OnApplicationQuit`도 메인입니다. 닫히는 중에 쓰는 상황 자체가 안 생깁니다.

그래서 지역 변수로 받아 `IsOpen`을 보고 `try/catch`로 감싸는 것으로 끝냈습니다. 포트가 안 열려 있으면 조용히 넘어갑니다 — 하드웨어 없이 켰을 때 타격마다 경고가 쏟아지면 안 됩니다.

> ⚠ **읽기 스레드에서는 부르면 안 됩니다.** 위 2번이 무너집니다. XML 주석에도 못박아 뒀습니다.

> ⚠ **모터가 아직 안 달려 있습니다.** `wand_tinys3.ino`의 DRV2605 구동부는 주석이라 실제로 울리지는 않습니다. 명령이 닿았다는 것은 `#VIB` 로그로만 보입니다. 모터를 달면 그 파일의 주석 네 곳(include · 객체 · `setup`의 `begin`/`selectLibrary` · `setRealtimeValue` 두 줄)을 풉니다.

---

## 8. 지킨 규칙

`IOT_INPUT.md` 6장의 금지 사항입니다. 전부 지켰습니다.

- **키 이벤트를 만들어 보내지 않았습니다.** 네트워크 경로를 타지 못합니다. 인터페이스를 구현했습니다.
- **`Show...` 함수를 부르지 않았습니다.** 서버가 정한 결과를 화면에 옮기는 자리이지 입력이 아닙니다.
- **작업 클래스(`CannonTask` 등)를 직접 부르지 않았습니다.** 규칙은 호스트가 정하고 장치는 값만 올립니다.

그 밖에:

- `IPlayerController.cs`는 **공용 경계**라 건드리지 않았습니다. 고쳐야 하면 미니게임 담당자들과 함께 정합니다. (`GAME_STRUCTURE.md` 9장)
- `KeyboardPlayerController.cs`는 **지우지 않았습니다.** 하드웨어 없이 테스트해야 합니다.
- `ProjectSettings.asset`을 포함해 팀원이 만든 파일은 하나도 수정하지 않았습니다.
