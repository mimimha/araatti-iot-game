# 아라아띠 포팅 매뉴얼 — IoT 완드

C101 팀 · 아라아띠

무선 완드와 동글의 펌웨어를 빌드 · 업로드하고, 게임 PC 에 연결해 실행하는 방법입니다.
[01_포팅매뉴얼.md](01_포팅매뉴얼.md) 의 2-4 절(IoT 완드)을 자세히 풀어 쓴 문서입니다.

| 범위 | 문서 |
|---|---|
| 완드 · 동글 펌웨어, Unity 와의 통신, 하드웨어 연결 순서 | **이 문서** |
| Unity 클라이언트 · 게임 서버 빌드와 배포, 배포 zip(동글 드라이버 포함) | [01_포팅매뉴얼.md](01_포팅매뉴얼.md) |
| 동글 드라이버(CP210x)의 버전 · 받는 곳 · 라이선스 | [02_외부서비스.md](02_외부서비스.md) 5장 |

---

## 1. 시스템 구성

```text
 [완드 TinyS3] × 2  (왼손 · 오른손)
        │   ESP-NOW (2.4GHz 무선, 50Hz)   ↑ 입력   ↓ 진동 명령
 [동글 ESP32-S3 DevKit] × 1
        │   USB 시리얼 115200bps  (CP2102 변환 칩 — PC 에 CP210x 드라이버 필요)
 [게임 PC — Unity 클라이언트 (IotPlayerController)]
```

- 플레이어 한 명이 **완드 2대 + 동글 1대 + 게임 PC 1대**를 씁니다. 이 한 벌을 "자리"라고 부릅니다.
  지금은 자리 A · B 두 벌이 있습니다 (완드 4대, 동글 2대).
- 휘두르기의 종류와 세기는 **완드가 판정**합니다. 동글은 형식만 바꿔 넘기고, Unity 는 받은 값을 게임 행동으로 해석합니다.
- 완드가 하나도 연결되지 않으면 게임은 **키보드 입력으로 자동 전환**됩니다. 완드 없이도 게임은 돌아갑니다.

---

## 2. 개발 환경 및 버전

아래 버전으로 빌드하고 실기에서 확인했습니다. 다른 버전은 확인하지 않았습니다.

| 구분 | 이름 | 버전 | 비고 |
|---|---|---|---|
| OS | Windows 11 | — | |
| IDE | Arduino IDE | 2.3.10 | IDE 와 CLI 중 하나만 있으면 됩니다 |
| CLI | arduino-cli | 1.5.1 | |
| 보드 패키지 | esp32 (Espressif Systems) | **3.3.12** | |
| 업로드 도구 | esptool | 5.3.1 | 보드 패키지에 들어 있습니다 |
| 라이브러리 | Adafruit DRV2605 Library | 1.2.4 | 완드에만 필요합니다 |
| 라이브러리 | Adafruit BusIO | 1.17.4 | 위 라이브러리를 설치할 때 함께 설치됩니다 |
| PC 드라이버 | Silicon Labs CP210x Universal Windows Driver | 11.6.0.420 | 동글용. 저장소에 들어 있습니다 (4-3) |
| 참고 | Unity | 6000.5.9f1 | 클라이언트 빌드는 서버 담당 문서를 봅니다 |

그 밖의 헤더(`WiFi.h`, `esp_now.h`, `Wire.h` 등)는 전부 보드 패키지에 들어 있습니다.

---

## 3. 하드웨어

### 3-1. 부품

**완드 (4대)**

| 부품 | 모델 | 연결 |
|---|---|---|
| 보드 | Unexpected Maker **TinyS3** (ESP32-S3) | — |
| 6축 IMU (가속도 · 자이로) | LSM6DS3 계열 | I2C, 주소 `0x6B` |
| 진동 드라이버 · 모터 | Adafruit **DRV2605L** 보드 + ERM(편심) 모터 | I2C (IMU 와 같은 버스) |
| 조이스틱 | 아날로그 2축, 누르기 스위치 포함 | ADC · GPIO |
| 버튼 | 2개 | GPIO |
| 압력 센서 (FSR) | — | ADC. 로그로만 보고 게임에는 쓰지 않습니다 |
| 전원 | LiPo 배터리 | — |

**동글 (2대)**: ESP32-S3 DevKit + USB 케이블. `UART` 포트에 USB-시리얼 변환 칩 **CP2102** 가 달려 있습니다.

### 3-2. 완드 핀 배선

| 기능 | TinyS3 핀 | 비고 |
|---|---|---|
| 조이스틱 X | GPIO 1 | ADC1 을 씁니다. ESP-NOW 가 Wi-Fi 를 쓰는 동안 ADC2 는 읽을 수 없습니다 |
| 조이스틱 Y | GPIO 2 | |
| FSR | GPIO 3 | |
| 버튼 1 | GPIO 4 | 내부 풀업. 누르면 GND 로 떨어집니다(LOW) |
| 버튼 2 | GPIO 5 | 같음 |
| 조이스틱 누르기 | GPIO 6 | 같음. 누르면 재보정합니다 |
| I2C SDA | GPIO 8 | IMU 와 DRV2605 가 함께 씁니다 |
| I2C SCL | GPIO 9 | |
| 전원 | 3V3 · GND | |

### 3-3. 보드 배정표

**완드 4대에 같은 펌웨어 파일을 그대로 굽습니다.** 완드는 부팅할 때 자기 보드의 MAC 주소를 읽고,
펌웨어 안의 표(`SLOTS`)에서 몇 번 완드인지와 어느 동글로 보낼지를 찾습니다.

| 자리 | 동글 MAC | 무선 채널 | 왼손 (완드 번호 0) | 오른손 (완드 번호 1) |
|---|---|---|---|---|
| A | `58:E6:C5:6A:92:D8` | 1 | TinyS3 #1 `DC:54:75:EB:82:C0` | TinyS3 #4 `DC:54:75:EB:80:B4` |
| B | `10:51:DB:78:F4:58` | 6 | TinyS3 #2 `DC:54:75:EB:84:C8` | TinyS3 #3 `DC:54:75:EB:83:F4` |

- 두 자리를 가까이 두어도 섞이지 않습니다. 완드는 자기 동글의 MAC 으로만 보냅니다.
- 자리마다 채널을 나눈 이유는 유실입니다. 같은 채널을 쓰면 수신률이 91% 였고, 나눈 뒤 99% 가 됐습니다.

---

## 4. 개발 환경 설치

### 4-1. Arduino IDE 로 설치

1. Arduino IDE 2.3.10 을 설치합니다.
2. **파일 → 기본 설정 → 추가 보드 관리자 URL** 에 아래 주소를 넣습니다.
   ```text
   https://espressif.github.io/arduino-esp32/package_esp32_index.json
   ```
3. **보드 매니저**에서 `esp32` (Espressif Systems) **3.3.12** 를 설치합니다.
4. **라이브러리 매니저**에서 `Adafruit DRV2605 Library` **1.2.4** 를 설치합니다.
   의존 라이브러리를 함께 설치할지 물으면 **모두 설치**를 고릅니다.

### 4-2. arduino-cli 로 설치 (IDE 대신 쓸 때)

```bash
arduino-cli config init          # 설정 파일이 없을 때 한 번만
arduino-cli config add board_manager.additional_urls https://espressif.github.io/arduino-esp32/package_esp32_index.json
arduino-cli core update-index
arduino-cli core install esp32:esp32@3.3.12
arduino-cli lib install "Adafruit DRV2605 Library@1.2.4"
```

### 4-3. CP210x 드라이버 (동글을 꽂는 PC)

동글의 `UART` 포트는 CP2102 USB-시리얼 칩을 거칩니다. Windows 에 **Silicon Labs CP210x 드라이버**가 없으면
COM 포트가 생기지 않아서, **동글에 펌웨어를 굽지도 못하고 게임도 동글을 찾지 못합니다.**
완드(TinyS3)는 ESP32-S3 의 USB 에 바로 연결되어 있어 드라이버가 필요 없습니다.

| PC | 할 일 |
|---|---|
| 시연 · 플레이 PC | **따로 할 것이 없습니다.** 배포 zip 의 `게임시작.bat` 이 동글이 꽂혀 있고 드라이버가 없을 때만 관리자 확인 창을 한 번 띄워 설치합니다 (7장 4번, [01_포팅매뉴얼.md](01_포팅매뉴얼.md) 8장) |
| 개발 PC (동글에 펌웨어를 굽는 PC) | 동글을 꽂았는데 COM 포트로 안 보이면, 저장소의 드라이버를 설치합니다 |

```powershell
# 관리자 권한 PowerShell 에서, 저장소 최상위 기준
pnputil /add-driver tools\deploy\drivers\CP210x\silabser.inf /install
```

설치를 확인하려면 동글을 꽂고 **장치 관리자**를 엽니다.

| 장치 관리자에 보이는 것 | 상태 |
|---|---|
| **포트(COM & LPT)** 아래 `Silicon Labs CP210x USB to UART Bridge (COMx)` | 정상 |
| **기타 장치** 아래 느낌표가 붙은 장치 (보통 `CP2102 USB to UART Bridge Controller`) | 드라이버 없음 (오류 코드 28) |

드라이버는 한 번 설치하면 Windows 드라이버 저장소에 남습니다. 다른 USB 포트에 꽂아도 다시 설치할 필요가 없습니다.

---

## 5. 펌웨어 빌드 및 업로드

### 5-1. 보드 설정

|  | 완드 | 동글 |
|---|---|---|
| 스케치 | `firmware/wand_tinys3/wand_tinys3.ino` | `firmware/dongle_esp32s3/dongle_esp32s3.ino` |
| 보드 (IDE) | **UM TinyS3** | **ESP32S3 Dev Module** |
| FQBN (CLI) | `esp32:esp32:um_tinys3` | `esp32:esp32:esp32s3` |
| 보드 옵션 | **전부 기본값** | **전부 기본값** |
| 꽂는 USB 포트 | USB-C (하나뿐) | **`UART` 라고 적힌 포트** |
| 굽는 대수 | 4대 모두 같은 파일 | 2대 모두 같은 파일 |

> ⚠ **동글은 `UART` 포트에 꽂습니다.** 굽을 때도, 게임에 연결할 때도 같은 포트입니다.
> 동글의 보드 기본값은 `USB CDC On Boot = Disabled` 라서 시리얼이 UART 포트로 나옵니다.
> 다른 USB 포트에 꽂으면 게임이 동글을 찾지 못합니다.
> 이 포트는 CP2102 변환 칩을 거치므로 **CP210x 드라이버가 있어야** COM 포트가 생깁니다 (4-3).

> ⚠ **저장소의 `.ino` 파일을 직접 열어서 굽습니다.** 다른 폴더에 있는 옛 사본으로 구우면
> 완드 배정 · 채널 · 휴면 설정이 통째로 옛날 것으로 돌아갑니다.

### 5-2. Arduino IDE 로 굽기

1. 저장소의 `.ino` 파일을 엽니다.
2. **도구 → 보드**에서 5-1 표의 보드를, **도구 → 포트**에서 꽂은 보드의 COM 포트를 고릅니다.
3. **업로드**를 누릅니다.
4. **시리얼 모니터**를 115200 으로 열고 5-4 의 로그가 나오는지 봅니다.
5. 확인이 끝나면 **시리얼 모니터를 닫습니다.** 열려 있으면 게임이 동글 포트를 열지 못합니다.

### 5-3. arduino-cli 로 굽기

저장소 최상위에서 실행합니다. `COMx` 는 꽂은 보드의 포트로 바꿉니다.

```bash
# 완드 — 4대 모두 같은 명령
arduino-cli compile --upload -p COMx --fqbn esp32:esp32:um_tinys3 firmware/wand_tinys3

# 동글 — 2대 모두 같은 명령
arduino-cli compile --upload -p COMx --fqbn esp32:esp32:esp32s3 firmware/dongle_esp32s3
```

- **`upload` 만 쓰지 말고 언제나 `compile --upload` 를 씁니다.** `upload` 는 다시 컴파일하지 않아서 소스를 고쳐도 옛 빌드가 올라갑니다.
- COM 번호는 꽂을 때마다 바뀝니다. `arduino-cli board list` 나 장치 관리자에서 확인합니다.
- 꽂은 보드가 몇 번 완드인지는 MAC 으로 확인합니다. 3-3 표와 맞춰 봅니다.
  ```bash
  "%LOCALAPPDATA%\Arduino15\packages\esp32\tools\esptool_py\5.3.1\esptool.exe" --port COMx read-mac
  ```

### 5-4. 업로드 확인 — 부팅 로그

**완드** — USB 로 꽂고 시리얼 모니터(115200)를 엽니다. 로그가 안 보이면 보드의 RESET 버튼을 누릅니다.

```text
#NOW wand=0 mac=DC:54:75:EB:82:C0 ch=1 dongle=58:E6:C5:6A:92:D8
#BOOT wand=0 centerX=2031 centerY=2064
#CAL 움직이지 마세요
#CAL 완료  bias ...  grav ...
#READY  스틱을 누르거나 r 을 보내면 재보정
#FSR 0  txfail 0          ← 1초마다
#TILT    0  grav ...      ← 1초마다
```

| 볼 것 | 정상 |
|---|---|
| `#NOW` 의 `wand` · `ch` · `dongle` | 3-3 표와 같습니다 |
| `#ERR` 줄 | 없습니다 |
| `txfail` | 동글이 켜져 있으면 늘지 않습니다. 계속 늘면 동글이 안 받고 있는 것입니다 |

**동글** — `UART` 포트로 꽂고 시리얼 모니터(115200, 줄 끝 **New Line**)를 엽니다.

| 할 일 | 나와야 하는 것 |
|---|---|
| 켜기 | `#MAC 58:E6:C5:6A:92:D8 ch=1` · `#READY 완드가 입력을 보내면 자동으로 등록된다` |
| `?` 입력 | `#MAC <동글 MAC> ch=<채널>` — 3-3 표와 같아야 합니다 |
| 완드 켜기 | `#WAND 0 등록 <완드 MAC>` 뒤로 `0,0,0,0,0,0,0,0,0,12345` 같은 줄이 초당 50줄씩 |
| `V,0,200,120` 입력 | `#ECHO 완드 0 seq 1` 이 찍히고 0번 완드가 0.12초 울립니다 |

### 5-5. 보드를 바꾸거나 더할 때

| 상황 | 할 일 |
|---|---|
| **새 완드** | 구우면 `#ERR 배정 표(SLOTS)에 없는 보드 <MAC>` 을 1초마다 찍고 아무것도 보내지 않습니다. 그 MAC 을 `wand_tinys3.ino` 의 `SLOTS` 표에 넣고 **그 완드만** 다시 굽습니다 |
| **새 동글** | 동글을 굽고 `?` 로 MAC 을 읽습니다. 그 값을 `wand_tinys3.ino` 의 `DONGLE_A` 또는 `DONGLE_B` 에 적고, **그 자리의 완드 두 대**를 다시 굽습니다. 자리 B 동글이면 `dongle_esp32s3.ino` 의 `DONGLE_B` 도 고쳐 동글도 다시 굽습니다 |
| **채널 변경** | 두 파일을 같이 고칩니다 — 완드의 `DONGLE_A` · `DONGLE_B` 채널, 동글의 `CH_A` · `CH_B` |
| **IMU 가 없는 보드** | 파일 위쪽의 `HAS_IMU` 를 `0` 으로 굽습니다. 그러지 않으면 IMU 읽기가 계속 실패해 입력을 하나도 안 보냅니다. CLI 에서는 파일을 고치지 않고 `--build-property "compiler.cpp.extra_flags=-DHAS_IMU=0"` 을 붙입니다 |

---

## 6. Unity 와 통신하는 방식

### 6-1. 흐름

```text
입력   완드 → (ESP-NOW) → 동글 → (USB 시리얼) → IotPlayerController → IPlayerController → 각 미니게임
진동   완드 ← (ESP-NOW) ← 동글 ← (USB 시리얼) ← IotPlayerController ← 각 미니게임의 Vibrate 호출
```

`IotPlayerController` 는 키보드 입력(`KeyboardPlayerController`)과 같은 인터페이스(`IPlayerController`)를 구현합니다.
네트워크 게임에서는 완드 값이 키보드 값과 같은 길로 서버에 올라가므로, **서버 쪽에 따로 설정할 것은 없습니다.**

### 6-2. 무선 구간 (완드 ↔ 동글) — ESP-NOW

- 공유기 없이 보드끼리 직접 주고받는 Espressif 의 무선 방식입니다. 암호화는 쓰지 않습니다.
- 완드는 20ms(50Hz)마다 입력 패킷을 보냅니다.
- 동글은 완드 MAC 을 미리 모릅니다. 완드가 **처음 입력을 보낸 순간** 그 MAC 을 완드 번호에 묶습니다(`#WAND n 등록`).
  그래서 진동 명령은 그 완드가 한 번이라도 입력을 보낸 뒤에만 보낼 수 있습니다.

| 패킷 | 방향 | 크기 | 첫 바이트 | 내용 |
|---|---|---|---|---|
| `WandInput` | 완드 → 동글 | 12바이트 | `0xA1` | 아래 CSV 한 줄과 1:1 |
| `Cmd` | 동글 → 완드 | 8바이트 | `0xC1` | 완드 번호, 명령(1 = 진동), 세기, 지속 시간(ms), 순번 |
| `Echo` | 완드 → 동글 | 4바이트 | `0xC2` | 명령을 받았다는 답. 동글이 `#ECHO` 로 찍습니다 |

### 6-3. 유선 구간 (동글 ↔ PC) — USB 시리얼

115200bps, 줄 단위(`\n`)입니다.

**올라가는 줄 (동글 → Unity)** — 완드 패킷 하나가 CSV 한 줄입니다.

```text
id,x,y,buttons,tilt,rot,mcount,mtype,strength,ms
0,-127,64,2,-90,0,7,2,180,56407
```

| 필드 | 범위 | 뜻 |
|---|---|---|
| `id` | 0~3 | 완드 번호 (0 = 왼손, 1 = 오른손) |
| `x`, `y` | -127~127 | 조이스틱. +x 가 오른쪽, +y 가 앞 |
| `buttons` | 비트 | bit0 = 버튼 1, bit1 = 버튼 2 |
| `tilt` | -127~127 | 완드를 좌우로 기울인 각도 (±90도 → ±127). 배의 조타에 씁니다 |
| `rot` | 0 | 자리만 남아 있고 항상 0 입니다 |
| `mcount` | 0~255 순환 | 휘두르기 누적 횟수. 늘어난 만큼이 새 동작 수입니다 |
| `mtype` | 0~3 | 마지막 휘두르기 종류. 0 = 없음, 1 = 가로 베기, 2 = 세로 내리치기, 3 = 찌르기 |
| `strength` | 0~255 | 마지막 휘두르기 세기 |
| `ms` | 0~65535 | 완드가 켜진 뒤 시간(ms)의 하위 16비트. 완드 재부팅을 알아채는 데만 씁니다 |

`#` 으로 시작하는 줄은 동글의 로그입니다. Unity 는 입력으로 세지 않고 버립니다.

**내려가는 줄 (Unity → 동글)**

| 줄 | 뜻 |
|---|---|
| `V,<완드 번호>,<세기 0~255>,<지속 ms>` | 진동. 예: `V,1,200,120` |
| `?` | 동글이 `#MAC <MAC> ch=<채널>` 로 답합니다. Unity 가 동글 포트를 찾을 때 씁니다 |

### 6-4. Unity 쪽 — `IotPlayerController`

파일: `unity/UnderTheSea/Assets/Game/Scripts/IoT/IotPlayerController.cs`

- **씬에 붙이지 않아도 됩니다.** 게임이 시작되면 코드로 하나 만들어지고, 씬이 바뀌어도 끝까지 남아 동글 포트를 잡고 있습니다.
- **동글 포트를 스스로 찾습니다.** 모든 COM 포트를 하나씩 열어 0.3초 동안 듣고, CSV 줄이나 `#MAC` 이 들리면 동글로 봅니다.
  조용하면 `?` 를 보내 물어봅니다. 완드를 PC 에 USB 로 꽂아 두어도 완드 포트를 동글로 착각하지 않습니다.
- **포트는 게임을 시작할 때 한 번만 찾습니다.** 게임을 켠 뒤에 동글을 꽂거나, 도중에 뽑았다 다시 꽂으면 잡지 않습니다. 이때는 게임을 다시 실행합니다.
- **포트를 열면 동글이 한 번 리셋됩니다.** CP2102 의 DTR 신호가 보드의 리셋 회로에 연결되어 있기 때문입니다.
  처음 몇 줄에 `ESP-ROM:esp32s3-...` 같은 부트 메시지가 섞여 들어오는데, 버려도 되는 줄이고 정상입니다.
- **손 배정** — 완드 번호 0 이 왼손, 1 이 오른손입니다. 0.5초 동안 줄이 안 오면 끊긴 것으로 봅니다.
  한 대만 연결되면 그 한 대가 양손을 겸하고, 한 대도 없으면 키보드로 넘어갑니다.
- **빌드 조건** — 시리얼 통신(`System.IO.Ports`)을 쓰려면 Player Settings 의 **Api Compatibility Level 이 .NET Framework** 여야 합니다.
  저장소의 `ProjectSettings.asset` 에 이미 반영되어 있습니다(`apiCompatibilityLevel: 3`).
- **서버 빌드에는 시리얼 코드가 들어가지 않습니다** (`#if !UNITY_SERVER`). 완드와 동글은 **클라이언트 PC 에만** 꽂습니다.

---

## 7. 하드웨어 연결 및 실행 순서

**미리 할 것 (한 번만)**: 5장대로 완드 4대 · 동글 2대에 펌웨어를 굽고, 5-4 로 확인합니다. 완드 배터리를 충전해 둡니다.

**게임 PC 한 대(자리 하나)마다**

1. **게임을 켜기 전에 동글을 게임 PC 에 꽂습니다.** `UART` 포트에 꽂습니다. 한 PC 에는 동글 한 대만 꽂습니다.
2. **동글 포트를 쓰는 프로그램을 모두 닫습니다.** Arduino IDE 시리얼 모니터 등이 열려 있으면 게임이 포트를 열지 못합니다.
3. **완드 두 대의 전원을 넣습니다** (LiPo 배터리 또는 USB-C).
   - 왼손 · 오른손은 3-3 표대로 듭니다. 자리 A 는 #1 왼손 · #4 오른손, 자리 B 는 #2 왼손 · #3 오른손입니다.
   - 켠 뒤 **약 3.5초 동안 완드를 책상에 가만히 두고 스틱을 건드리지 않습니다.** 이 사이에 자이로와 스틱 중립을 잽니다.
   - 완드가 잠들어 있으면 **버튼 1 · 버튼 2 · 스틱 누르기** 중 하나로 깨웁니다. 깨면 다시 부팅하므로 위 3.5초를 똑같이 기다립니다.
4. **배포 zip 의 `게임시작.bat` 으로 게임을 실행합니다.**
   - 이 PC 에 CP210x 드라이버가 없으면 "완드 드라이버를 설치합니다" 와 함께 **관리자 확인 창**이 뜹니다. **[예]** 를 누릅니다. 이 PC 에서 처음 한 번만 뜹니다.
   - [아니요] 를 누르거나 관리자 권한이 없으면 키보드로 시작합니다. 다음 실행 때 다시 묻습니다.
   - 게임이 켜지면서 동글의 COM 포트를 자동으로 찾습니다. COM 번호는 몰라도 됩니다.
5. **확인합니다.** 로비에서 왼손 스틱으로 캐릭터가 걷고, 오른손 스틱으로 카메라가 돌면 연결된 것입니다.
   게임별 버튼 배치는 `KEY_MAPPING.md` 에 있습니다.

**게임 중에**

- 캐릭터가 혼자 걷거나 가만히 있는데 휘두르기가 잡히면: 스틱에서 손을 떼고 **스틱을 한 번 눌렀다 뗍니다.** 약 2초 동안 가만히 두면 다시 잡습니다.
- 버튼 · 스틱을 **30분** 동안 안 건드리면 완드가 잠듭니다. 버튼으로 깨우면 약 3.5초 뒤 게임에 다시 붙습니다. 게임을 다시 켤 필요는 없습니다.
- 완드를 PC 에 USB 로 꽂아도 게임 입력은 USB 로 가지 않습니다. 입력은 언제나 무선으로 동글을 거칩니다. 완드의 USB 는 전원 · 굽기 · 로그 확인용입니다.

---

## 8. 문제 해결

| 증상 | 확인할 것 | 조치 |
|---|---|---|
| 동글을 꽂아도 COM 포트가 안 생긴다 (게임도 Arduino IDE 도 동글을 못 찾는다) | 장치 관리자 **기타 장치**에 느낌표가 붙은 장치 (보통 `CP2102 USB to UART Bridge Controller`) | CP210x 드라이버가 없는 것입니다. 시연 PC 는 `게임시작.bat` 을 다시 실행해 확인 창에서 [예], 개발 PC 는 4-3 대로 설치합니다 |
| 게임에서 완드가 전혀 안 먹는다 | 게임 로그에 `[IotPlayerController] COM 포트 n개를 훑었지만 동글을 찾지 못했습니다` | 동글이 `UART` 포트에 꽂혔는지, 장치 관리자에 COM 포트로 보이는지, 시리얼 모니터가 닫혔는지 보고 **게임을 다시 실행**합니다 |
| 한 완드만 안 먹는다 | 동글 시리얼 모니터에 그 번호의 CSV 가 흐르는지 | ① 완드 전원(배터리) ② 잠들었으면 버튼으로 깨우기 ③ 완드를 USB 로 꽂아 `#NOW` 의 `dongle` · `ch` 가 3-3 표와 같은지 ④ `txfail` 이 계속 늘면 MAC · 채널이 어긋난 것입니다 |
| 완드 로그에 `#ERR 배정 표(SLOTS)에 없는 보드` | — | 5-5 의 "새 완드" |
| 캐릭터가 혼자 걷는다 | 켤 때 스틱을 건드렸는지 | 스틱을 눌렀다 뗍니다 |
| 완드 로그에 `#ERR IMU 이상 — 중력 ...G` | 켤 때 완드를 움직였는지, IMU 배선 | 가만히 두고 RESET. 배선을 고쳤으면 반드시 RESET 합니다 (IMU 설정은 부팅할 때만 씁니다) |
| 진동이 안 온다 | 동글 로그 `#ERR 완드 n 미등록` · `명령 미도달`, 완드 로그 `#ERR 진동 드라이버(DRV2605) 없음` | 미등록이면 그 완드를 깨워 입력을 한 번 보내게 합니다. 드라이버가 없다고 나오면 I2C 배선을 봅니다 |
| Unity 에디터 Play 에서만 완드가 안 붙고 `[IotPlayerController]` 로그가 한 줄도 없다 | 에디터가 Dedicated Server 빌드 프로필에 있는지 | **File → Build Profiles → Windows** 로 Switch Profile 합니다. 서버를 명령줄로 빌드한 뒤 이렇게 남습니다 |
| 잠든 완드에 업로드하면 `Could not open COMx` | — | 버튼으로 깨우고 포트가 목록에 뜬 뒤 1초쯤 기다려 올립니다. 제일 확실한 방법은 **BOOT 를 누른 채 RESET** 을 눌러 다운로드 모드로 굽는 것입니다 |
| 빌드 중 `Wire.cpp.d: No such file or directory` | 같은 스케치를 두 곳에서 동시에 빌드했는지 | `arduino-cli compile --clean ...` 으로 다시 빌드합니다 |
| 동글 두 대를 한 PC 에 꽂았다 | — | 먼저 답한 동글을 잡습니다. 한 PC 에 한 대만 꽂습니다 |

---

## 9. 참고 파일

| 파일 | 내용 |
|---|---|
| [`firmware/wand_tinys3/wand_tinys3.ino`](../firmware/wand_tinys3/wand_tinys3.ino) | 완드 펌웨어 |
| [`firmware/dongle_esp32s3/dongle_esp32s3.ino`](../firmware/dongle_esp32s3/dongle_esp32s3.ino) | 동글 펌웨어 |
| [`firmware/tools/`](../firmware/tools/) | 조타 방향(`TILT_SIGN`)을 실기로 정하는 PowerShell 스크립트 |
| [`tools/deploy/drivers/CP210x/`](../tools/deploy/drivers/CP210x/) | 동글 드라이버 (Silicon Labs CP210x 11.6.0.420, 라이선스 문서 포함) |
| [`tools/deploy/wand-driver-check.ps1`](../tools/deploy/wand-driver-check.ps1) | `게임시작.bat` 이 부르는 드라이버 확인 · 설치 스크립트 |
| [`IotPlayerController.cs`](../unity/UnderTheSea/Assets/Game/Scripts/IoT/IotPlayerController.cs) | Unity 쪽 수신 · 진동 |
| [`IotPlayerController.md`](../unity/UnderTheSea/Assets/Game/Scripts/IoT/IotPlayerController.md) | 작업 기록 — 왜 이렇게 만들었는지, 실측 결과 |
| [`KEY_MAPPING.md`](../unity/UnderTheSea/Assets/Game/Scripts/IoT/KEY_MAPPING.md) | 게임별 완드 버튼 배치 |
| [`IOT_INPUT.md`](../unity/UnderTheSea/IOT_INPUT.md) | 게임과 장치 사이의 입력 규격 |
