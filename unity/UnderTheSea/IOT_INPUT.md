# IoT 입력 규격 — 배 협동 게임(ShipCoop)

IoT 담당자가 장치를 만들 때 보는 문서입니다.
**게임 안에서 키보드를 읽는 곳은 단 한 군데뿐입니다.** 그 한 군데를 장치로 갈아끼우면 끝입니다.

- 경계 정의 · 폴더 규칙은 `GAME_STRUCTURE.md` 의 `IoT/` 절
- 게임 규칙 자체는 `SHIPCOOP.md`

---

## 1. 한 줄 요약

```text
장치  →  IPlayerController  →  ShipCoopInput  →  각 작업(대포 · 조타 · 돛 · 수리 · 운반)
```

`IPlayerController` 하나만 구현하면 됩니다. 그 아래는 건드릴 것이 없습니다.

**키를 흉내 내지 마세요.** 지금 키보드로 되는 것은 `Assets/Game/Scripts/IoT/KeyboardPlayerController.cs`
가 장치인 척하고 있기 때문입니다. 이 파일은 임시이고, 장치가 생기면 대체됩니다.
키 이벤트를 만들어 보내는 방식으로는 네트워크 경로를 타지 못합니다.

---

## 2. 지금 키보드가 하는 일 (개발자 모드 제외)

장치가 대신해야 할 동작의 전부입니다. 개발자 모드(F9 로 켜는 창)의 키는 이 표에 없고,
장치가 흉내 낼 필요도 없습니다. 그쪽은 개발용이라 이 경로를 타지 않습니다.

| 키 | 손 | 눌림 방식 | 게임 안에서 하는 일 |
|---|---|---|---|
| ↑ ↓ ← → | 왼손 스틱 | 계속 | 걷기. 카메라 기준 방향 |
| Shift | 양손 쥐기 | 누르는 동안 | 쥐기. 물건을 들고 있는 상태를 유지한다 |
| C | 왼손 버튼1 | 한 번 | 도움 요청 |
| V | 왼손 버튼2 | 누르는 동안 | 달리기 |
| Q E | 오른손 스틱 X | 계속 | 카메라 좌우 회전 · 대포 조준 (상황에 따라 다름) |
| Space | 오른손 버튼1 | 한 번 | 상호작용 — 붙기 · 집기 · 놓기 (상황에 따라 다름) |
| X | 오른손 버튼2 | 한 번 | 대포 발사 |
| F | 오른손 흔들기 | 한 번 | 망치질 (수리) |
| A D | 양손 기울기 · 비틀기 | 계속 | 조타 · 돛 당기기 (상황에 따라 다름) |

F1 은 디버그 HUD 토글입니다. 게임 조작이 아니라 화면 표시라 장치와 무관합니다.

### 같은 입력이 상황에 따라 달라지는 것

장치는 **구분하지 않아도 됩니다.** 무엇을 할지는 게임이 정합니다. 장치는 값만 보냅니다.

- **오른손 스틱 X** — 대포에 붙어 있으면 포신을 돌리고, 아니면 카메라를 돌립니다.
- **양손 기울기 · 비틀기** — 키에 붙어 있으면 방향을 꺾고, 돛에 붙어 있으면 밧줄을 당깁니다.
- **오른손 버튼1** — 쥔 채로 누르면 집고, 그냥 누르면 가까운 자리에 붙습니다.
  들고 있는 것이 있으면 **버튼이 아니라 쥐기를 놓는 것**이 건네주기 · 버리기입니다.
- **쥐기** — 집기를 열어 주고, 든 것을 유지하고, 자리에 붙는 것을 막습니다. 세 가지를 겸합니다.

---

## 3. 구현할 것

`Assets/Game/Scripts/IoT/IPlayerController.cs` 에 정의돼 있습니다. 그대로 구현합니다.

```csharp
public interface IPlayerController
{
    IHandDevice Left  { get; }
    IHandDevice Right { get; }

    bool    HasTwoDevices { get; }   // 한 손만 있으면 false. 게임이 조작을 줄여 준다
    Vector2 Move { get; }            // 걷기. 길이 0~1
    Vector2 Look { get; }            // 카메라 · 조준

    void VibrateBoth(float strength, float seconds);
}

public interface IHandDevice
{
    Vector2 Stick    { get; }        // −1 ~ 1
    float   Tilt     { get; }        // 기울기. 조타
    float   Rotation { get; }        // 비틀기. 돛

    bool Grip    { get; }            // 쥐는 중
    bool Button1 { get; }
    bool Button2 { get; }

    bool ConsumeButton1Press();      // 아래 주의 참고
    bool ConsumeButton2Press();
    bool ConsumeSwing();             // 휘두름(IMU)

    void Vibrate(float strength, float seconds);
}
```

### 손마다 무엇이 필요한가

```text
왼손   스틱(걷기)  ·  쥐기  ·  버튼1(도움)  ·  버튼2(달리기)  ·  기울기 · 비틀기
오른손 스틱(조준)  ·  쥐기  ·  버튼1(상호작용) · 버튼2(발사) · 휘두름(망치) · 기울기 · 비틀기
```

조타와 돛은 **양손 값의 평균**을 씁니다. 한쪽만 움직여도 절반만큼 동작합니다.

### ⚠ `Consume...` 은 **한 번만 참입니다**

`ConsumeButton1Press` 는 "눌렸는가" 를 묻는 것이 아니라 **"눌린 것을 가져간다"** 는 뜻입니다.
읽는 순간 사라집니다. 한 프레임에 두 곳이 읽으면 한 곳은 못 받습니다.

구현할 때는 **누름의 시작(edge)에서 한 번만 참**이 되게 하고, 읽히면 내려야 합니다.
누르고 있는 동안 계속 참을 돌려주면 발사가 연사되고 집기가 폭주합니다.

`Grip` · `Button1` · `Button2` 는 반대로 **누르는 동안 계속 참**이어야 합니다.

### 아날로그 값의 기대치

- 스틱은 −1 ~ 1. 가만히 둘 때 0 근처로 떨어져야 합니다. 떨림은 장치 쪽에서 죽여 주세요.
- 기울기 · 비틀기도 −1 ~ 1. 갑자기 튀면 배가 홱 돌아갑니다.
  키보드 대역은 초당 4의 속도로 천천히 올라가게 만들어 두었습니다. 장치도 비슷하게 완만하면 좋습니다.

---

## 4. 어디에 붙이는가

플레이어 오브젝트에 `MonoBehaviour` 로 올립니다. **한 오브젝트에 하나만** 있어야 합니다.

찾아가는 쪽은 이미 되어 있습니다.

```text
TaskWorker.Awake              GetComponent<IPlayerController>()
ShipCoopInputProvider.Awake   GetComponent<IPlayerController>()
```

네트워크도 손댈 것이 없습니다. `ShipCoopInputProvider` 가 값을 실어 보내고,
서버 쪽에서 `ShipCoopNetworkedController` 가 같은 `IPlayerController` 로 되살립니다.
누름의 시작은 서버가 직접 계산하므로, 장치는 **로컬에서 자기 값만 채우면** 됩니다.

---

## 5. 진동 (게임 → 장치)

게임이 불러 줍니다. 구현만 해 두면 됩니다.
지금은 전부 `VibrateBoth` 로 **양손을 함께** 울립니다. 손을 가리는 것은 아직 쓰지 않습니다.

| 언제 | 세기 | 시간(초) |
|---|---|---|
| 대포를 쐈을 때 | 0.8 | 0.15 |
| 포탄 없이 방아쇠를 당겼을 때 | 0.3 | 0.1 |
| 물건을 집었을 때 | 0.4 | 0.1 |
| 대포에 포탄을 넣었을 때 | 0.3 | 0.1 |
| 판자 · 물을 건넸을 때 | 0.4 | 0.1 |
| 도움 요청을 받았을 때 | 설정값 | 설정값 |

세기는 0 ~ 1, 시간은 초입니다. 장치가 못 하면 빈 함수로 둬도 게임은 돌아갑니다.

---

## 6. 하지 말아야 할 것

- **키 이벤트를 만들어 보내기** — 네트워크 경로를 안 탑니다. 인터페이스를 구현하세요.
- **`Show...` 로 끝나는 함수 부르기** — `ShowAmmo` · `ShowHeading` · `ShowRepair` ·
  `ShowCarrying` · `ShowDumped` · `ShowOpen` 은 서버가 정한 결과를 화면에 옮기는 자리입니다.
  입력이 아닙니다. 여기를 부르면 클라이언트만 혼자 다른 것을 보게 됩니다.
- **작업 클래스를 직접 부르기** — `CannonTask` 같은 것을 직접 건드리지 마세요.
  규칙은 호스트가 정합니다. 장치는 값만 올립니다.

예외가 하나 있습니다. `ShipCoopHelp.Call()` 은 외부에서 불러도 되게 열어 둔 것입니다.
장치에 "도움" 전용 버튼이 따로 있다면 그쪽을 써도 됩니다.

---

## 7. 참고 파일

| 무엇 | 어디 |
|---|---|
| 경계 정의 | `Assets/Game/Scripts/IoT/IPlayerController.cs` |
| 임시 키보드 구현 (참고용 견본) | `Assets/Game/Scripts/IoT/KeyboardPlayerController.cs` |
| 입력의 게임적 의미 번역 | `Assets/Game/Scripts/MiniGames/ShipCoop/ShipCoopInput.cs` |
| 네트워크로 실어 보내는 곳 | `Assets/Game/Scripts/MiniGames/ShipCoop/Net/ShipCoopInputProvider.cs` |
| 서버 쪽에서 되살리는 곳 | `Assets/Game/Scripts/MiniGames/ShipCoop/Net/ShipCoopNetworkedController.cs` |
