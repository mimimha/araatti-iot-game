# IotPlayerController 작업 기록

무선 완드(ESP32-S3)를 게임에 붙이는 브리지를 만들면서 **무엇을 확인했고, 무엇이 막혔고, 왜 그렇게 풀었는지** 적어둔 문서입니다.

코드를 읽으면 "무엇을 하는지"는 알 수 있지만 "왜 이 모양인지"는 알 수 없습니다. 그 부분만 모았습니다.

- 장치 규격(무엇을 구현해야 하는가) → 저장소 최상위 `IOT_INPUT.md`
- 경계 정의(폴더 규칙·누가 함께 정하는가) → `GAME_STRUCTURE.md` 9장
- 이 문서 → 그 규격을 시리얼로 어떻게 채웠는가

| | |
|---|---|
| 브랜치 | `feature/jy-iot-wand-serial` |
| 담당 | IoT (`jy`) |
| 대상 파일 | `IotPlayerController.cs` (1125줄) |
| | `firmware/dongle_esp32s3/dongle_esp32s3.ino` (278줄) |
| | `firmware/wand_tinys3/wand_tinys3.ino` (547줄) |
| 기준 커밋 | `62a5789` |

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
| 시리얼 포트 | `portName` | `COM3` | 장치 관리자에서 확인 |
| | `baudRate` | `115200` | 펌웨어와 같아야 함 |
| | `pollIntervalMs` | `5` | 받은 것이 있는지 확인하는 간격 |
| | `dtrEnable` | `true` | 아래 4-2 참고 |
| | `rtsEnable` | `false` | 아래 4-2 참고 |
| 키 배치 | `controlProfile` | `Shared` | 광산·배는 `Shared`, 무쌍은 `Warriors` (아래 2-4) |
| 손 배정 | `leftHandId` | `0` | 왼손으로 쓸 완드 번호 |
| | `rightHandId` | `1` | 오른손으로 쓸 완드 번호 |
| | `handTimeoutSeconds` | `0.5` | 이만큼 무응답이면 끊긴 것으로 봄 |
| 달리기 | `sprintToggle` | `true` | 왼손 버튼2를 토글로 |
| 진단 | `logRawLines` | `false` | 받은 줄 그대로 |
| | `logParseFailures` | `false` | 해석 실패한 줄 |
| | `logDongleMessages` | `false` | 동글이 보내는 `#` 줄 |
| | `logConnectionChanges` | `false` | 완드 연결/끊김 |
| | `logDeviceOutput` | `false` | 진동 호출 |

### 2-3. 처음 연결할 때 순서

1. `logRawLines` 켜고 Play → `수신 — 0,-127,64,...`가 흐르는지
2. 안 나오면 `dtrEnable`부터 뒤집어 본다 (4-2 참고)
3. `logParseFailures` 켜고 자릿수 어긋난 줄이 없는지
4. `logConnectionChanges` 켜고 완드를 껐다 켜며 `완드 0 연결됨/끊김`이 뜨는지
5. 전부 확인되면 로그를 다시 끈다

### 2-4. 게임별 키 배치

같은 완드가 게임마다 다른 행동을 냅니다. **그 해석은 완드가 하지 않습니다.** 각 미니게임이 `IPlayerController` 값을 읽어 자기 행동으로 번역합니다. (`IOT_INPUT.md` 3장)

| 장치 부품 | 광산 `MineDigger` | 무쌍 `WarriorsKeyboardInput` | 배 `ShipCoopInput` |
|---|---|---|---|
| 왼손 스틱 | 이동 | 이동 | 걷기 |
| 왼손 버튼 1 | 땅 복구 | — | 도움 요청 |
| 왼손 버튼 2 | 힌트 | **미정** (아래) | **달리기 (토글)** |
| 오른손 스틱 | — (자체 마우스) | 카메라 x·y | 카메라 x |
| 오른손 버튼 1 | — | — | 상호작용 |
| 오른손 버튼 2 | — | — | 발사 · 망치질 |
| IMU 동작 | `VerticalSwing` = 파기 | 3종 = 가로·세로·찌르기 | `VerticalSwing` = 망치질 |
| 기울기 · 비틀기 | — | — | 조타 · 돛 |

**완드가 게임 이름을 알아야 하는 것은 한 칸뿐입니다 — 왼손 버튼 2 입니다.**

배는 이 버튼을 토글로 잠가야 합니다. 엄지가 스틱을 떠나면 걷기가 멈추기 때문입니다. (4-8)

무쌍은 잠그지 않습니다. 달리기라는 조작 자체가 없고, **그 자리에 무엇이 올지는 아직 안 정했습니다.** `IOT_INPUT.md` 7장이 회피를 키보드 Shift 로 옮기라고 적고 2장이 Shift 를 왼손 버튼 2 에 두지만, **무쌍의 장치 배치를 직접 정한 표는 문서에 없습니다.** 8장도 무쌍의 남는 키를 열어 둔 상태입니다. 정해지기 전에는 누른 그대로 내보내는 쪽이 안전합니다 — 토글로 잠가 두면 그 자리에 단발 조작이 오는 순간 한 번 걸러 먹힙니다.

그 한 칸 때문에 `IotControlProfile { Shared, Warriors }` 를 뒀습니다. `KeyboardPlayerController` 의 `KeyboardControlProfile` 과 값이 1:1 입니다. 키보드로 확인한 것이 완드에서 그대로 되어야 합니다.

```csharp
bool enabled = sprintToggle
               && _hasTwoDevices
               && controlProfile == IotControlProfile.Shared;
```

광산도 같은 자리를 힌트로 쓰지만 `Shared` 로 둡니다. 힌트는 `ConsumeButton2Press()` 로만 읽고, 그쪽은 토글과 무관하게 **실제로 누른 순간**을 돌려줍니다. (4-8)

**정하는 순서는 인스펙터 → 씬입니다.** 기본값은 `Shared` 이고, 무쌍 씬이 `SetControlProfile` 로 덮어씁니다. 키보드 쪽이 이미 그렇게 하고 있어서 같은 길로 갔습니다.

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

> ⚠ **체크박스를 꺼도 `GetComponent` 는 찾아옵니다.**
> 한 오브젝트에 `IPlayerController` 구현체가 둘이면 어느 쪽이 잡힐지 정해져 있지 않습니다.
> 우회 칸이 있는 쪽은 그 칸을 쓰고, 없는 쪽은 **지워야** 합니다.

#### 배 협동 — `ShipCoopTest.unity`

씬에 **`Player_01` · `Player_02` 두 명**이 있고 각자 `DebugPlayerMover` · `TaskWorker` · `KeyboardPlayerController` 를 답니다.

준비 — **한 명에게만** 합니다. 나머지 한 명은 키보드로 둬야 2인 동작을 볼 수 있습니다.

1. `Player_01` 의 **`KeyboardPlayerController` 를 지운다** (체크박스 아님, 컴포넌트 제거)
2. `IotPlayerController` 를 추가한다
3. `portName` 을 동글 COM 번호로, `controlProfile` 은 **`Shared`** 로 둔다

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

**여기는 절반만 물려 있습니다.** 준비 전에 이걸 알고 들어가야 합니다.

| 항목 | 지금 | 읽는 곳 |
|---|---|---|
| 땅 파기 | ✅ 완드 | `MineDigger` ← `TryConsumeMotion` (VerticalSwing) |
| 땅 복구 | ✅ 완드 | `MineDigger` ← `Left.ConsumeButton1Press` |
| 힌트 | ✅ 완드 | `MineDigger` ← `Left.ConsumeButton2Press` |
| **이동** | ❌ **키보드 전용** | `MineMoveInput` ← 레거시 `Input.GetAxis("Horizontal"/"Vertical")` |
| **카메라** | ❌ **마우스 전용** | `MineCamera` ← `Input.GetAxis("Mouse X"/"Mouse Y")` |
| 점프 | 키보드 | `MineMoveInput` ← `Input.GetButton("Jump")` |

`IOT_INPUT.md` 1장은 광산 이동을 `W A S D`(= 왼손 스틱)로 정해 뒀지만 **아직 그렇게 안 되어 있습니다.** `MineMoveInput` 이 `IPlayerController` 를 아예 안 봅니다. 광산 담당 몫입니다. (7장)

준비 — `KeyboardPlayerController` 를 **남겨 둡니다.** 이동·카메라가 그쪽에 걸려 있지는 않지만, 지워도 얻을 것이 없고 `MineDigger` 는 우회 칸이 있습니다.

1. Player 에 `IotPlayerController` 를 추가한다
2. `MineDigger` 의 **`Player Controller Source` 칸에 `IotPlayerController` 를 끌어다 놓는다**
3. `controlProfile` 은 **`Shared`**

확인

| 장치 | 기대 |
|---|---|
| 세로 내리치기 | 발밑이 파인다. **내 턴일 때만** (`DiggingAllowed`) |
| 왼손 버튼 1 | 되메우기 요청 |
| 왼손 버튼 2 | 힌트 요청 |
| 왼손 스틱 | **안 움직인다.** 위 표대로 정상 |

⚠ 파기는 `VerticalSwing` 만 받습니다. 가로로 휘두르면 아무 일도 안 일어나는 것이 맞습니다.

⚠ 되메우기·힌트는 **힌트를 보는 동안 같이 막힙니다.** `MineDigger` 주석이 의도한 동작이라고 적어 뒀습니다.

#### 공통 — 둘 다 확인할 것

1. 콘솔에 `완드 0 연결됨` 이 뜨는가 (`logConnectionChanges`)
2. `logMotions` 로 동작이 들어오는가
3. **완드를 껐을 때** 값이 0 으로 떨어지고 게임이 안 죽는가
4. 다시 켰을 때 묵은 입력이 한꺼번에 안 터지는가

3·4 번은 `Clear()` 와 첫 줄 기준 잡기(4-6)가 도는지 보는 것입니다.

#### 회귀 — 완드 없이

`KeyboardPlayerController` 만 있는 상태로 두 씬을 그대로 Play 합니다. 이 브랜치는 그 파일을 건드리지 않았으므로 **예전과 똑같아야** 합니다. 달라지면 이쪽 잘못입니다.

단, 광산 힌트는 원래부터 키보드로 안 눌립니다. (7장 7번)

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

---

## 7. 남은 일

| # | 내용 | 막고 있는 것 |
|---|---|---|
| 1 | ~~Api Compatibility Level → .NET Framework~~ | **됐음.** `apiCompatibilityLevel: 3` 확인. 팀에 공유함 (4-1) |
| 2 | ~~펌웨어가 `mtype`을 실제로 판정해 보내기~~ | **됐음.** `wand_tinys3.ino` 의 `detectMotion` 이 3종을 판정한다 (아래 7-1) |
| 2b | **네트워크 무쌍에 동작이 안 닿는다** | 아래 7-2. 설계 판단이 필요함 |
| 3 | ~~진동 역방향 프로토콜~~ | **됐음.** `V,id,세기,ms` 로 정하고 `SendVibrate` 를 채웠다 (아래 "진동에 대해") |
| 3b | **진동 모터가 안 달려 있다** | 부품 문제. 경로는 `#VIB` 로그로만 확인된다 |
| 4 | ~~실기 연결 확인~~ | **됐음.** 동글·완드 1대로 양방향 왕복까지 확인. 7초 341줄, 유실 0 (아래 7-3) |
| 5 | 낚시(`IFishingInputSource`) | 이번 스코프 밖. 별도 asmdef라 참조 관계부터 손봐야 함 |
| 6 | 무쌍 게임 로직 | 이번 스코프 밖. 프로필 연결(2-4)만 했다 |
| 7 | **광산 힌트가 키보드로 안 눌린다** | 광산 담당. 아래 참고 |

### 광산 힌트 (7번)

`MineDigger` 는 힌트를 `Left.ConsumeButton2Press()` 로 읽는데, `KeyboardPlayerController` 의 `Shared` 프로필은 왼손 버튼 2 가 `Key.None` 입니다. Shift 토글은 `_button2Held` 만 채우고 `_button2Pressed` 는 건드리지 않아서 **`ConsumeButton2Press()` 가 영영 false** 입니다.

`IOT_INPUT.md` 1장은 힌트를 `J` 로 정했지만 `Shared` 에서 `J` 는 기울기 축이 쓰고 있습니다. 어느 키로 옮길지는 광산 담당이 정할 일이라 **고치지 않았습니다.** (7장 표에서 광산 키 재배치가 그쪽 몫입니다)

완드에서는 이 문제가 없습니다. 펌웨어가 버튼 레벨을 그대로 보내므로 눌린 순간이 정상으로 잡힙니다.

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

`WarriorsSceneSetup.StripLocalOnlyParts` 가 네트워크 플레이어 프리팹에서 `WarriorsKeyboardInput` 을 **떼어냅니다.** 각자 자기 화면에서만 베는 것을 막으려는 것이라 맞는 결정입니다. 대신 `WarriorsInputProvider` 가 숫자키를 직접 읽고, 장치는 `WarriorsIoTInput.OnSwing` 으로 들어오게 되어 있습니다. 그쪽 주석도 *"장치가 붙는 쪽에서 `WarriorsIoTInput.OnSwing` 을 부르면 그 순간부터 이 경로로 흘러든다"* 고 적고 있습니다.

**그 호출자가 없습니다.** 시리얼 브리지는 `IPlayerController` 만 채웁니다.

메울 방법이 둘인데 성격이 다릅니다.

| | 하는 일 | 문제 |
|---|---|---|
| `OnSwing` 을 부른다 | 브리지가 판정된 동작을 그대로 넘긴다 | 완드·`WarriorsIoTInput` 둘 다 쿨다운이 걸려 이중이 된다 (500ms + 0.45s) |
| `SubmitImuSample` 을 부른다 | 원시 IMU 를 넘겨 판정을 그쪽에 맡긴다 | 지금 CSV 는 **판정 결과만** 보낸다. 원시 각속도·가속도를 안 실어서 포맷을 늘려야 한다 |

또 `WarriorsIoTInput.minimumStrength` 가 **0.35** 인데, 펌웨어 최소 스윙(200dps)의 세기는 `(200-150) × 255/450 ÷ 255 ≈ 0.11` 입니다. 그 경로로 가면 약한 베기가 전부 버려집니다. 임계값을 함께 맞춰야 합니다.

무쌍 담당자와 정할 일이라 **손대지 않았습니다.**

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
