// 완드 (TinyS3) — IMU · 조이스틱 · 버튼 · 진동
//
//     [완드 TinyS3]  <- 이 코드
//            |  ESP-NOW  (ch 1)
//     [동글 ESP32-S3]
//            |  USB Serial 115200
//     [유니티 IotPlayerController]
//
// 동작 판정은 여기서 끝낸다. 동글도 유니티도 다시 판정하지 않는다.
//
// 시리얼로 나가는 것은 전부 디버그다. 유선으로 꽂았을 때만 보인다.
// 유니티로 가는 값은 시리얼이 아니라 ESP-NOW 로 나간다.
//
// ── 굽기 전에 ─────────────────────────────────────────────
//   DONGLE[6]  동글을 켜면 찍히는 #MAC 을 여기에 적는다
//   WAND_ID    완드마다 0~3 으로 다르게 굽는다

#include <Wire.h>
#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include <esp_mac.h>   // esp_read_mac. WiFi.macAddress() 는 부팅 직후 0 을 준다 (동글에서 실기 확인)

// 진동 드라이버. 모터를 달면 주석을 푼다. (startVibration · stopVibration · setup 도 같이)
// #include <Adafruit_DRV2605.h>
// Adafruit_DRV2605 drv;

#define IMU_ADDR  0x6B

#define CTRL1_XL  0x10   // 가속도 설정
#define CTRL2_G   0x11   // 자이로 설정
#define OUTX_L_G  0x22   // 자이로 X 부터 12바이트 연속

// ── ESP-NOW ────────────────────────────────────────────────

#define CH          1     // 동글과 같아야 한다
#define WAND_ID     0     // 이 완드의 번호. 완드마다 다르게 굽는다

// 동글(ESP32-S3 DevKit)의 STA MAC.
// ⚠ SoftAP 쪽(5A:...)이 아니다. ESP-NOW 를 WIFI_IF_STA 로 보내기 때문이다.
uint8_t DONGLE[6] = { 0x58, 0xE6, 0xC5, 0x6A, 0x92, 0xD8 };

// 완드 보드 STA MAC. 동글이 #WAND n 등록 <MAC> 을 찍으니 어느 보드가 몇 번인지 여기서 맞춘다.
//   TinyS3 #1  DC:54:75:EB:82:C0
//   TinyS3 #2  DC:54:75:EB:84:C8
//   TinyS3 #3  DC:54:75:EB:83:F4
//   TinyS3 #4  DC:54:75:EB:80:B4

#define MAGIC_INPUT 0xA1  // 완드 -> 동글  입력
#define MAGIC_CMD   0xC1  // 동글 -> 완드  명령
#define MAGIC_ECHO  0xC2  // 완드 -> 동글  명령 받았다는 답

#define CMD_VIBRATE 1

// 유니티 CSV 한 줄과 1:1 이다. 동글이 이걸 그대로 풀어서 찍는다.
typedef struct __attribute__((packed)) {
  uint8_t  magic;      // 0xA1
  uint8_t  player_id;  // 0~3
  int8_t   x, y;       // 스틱          -127~127
  uint8_t  buttons;    // bit0=버튼1 bit1=버튼2
  int8_t   tilt;       // IMU roll      -127~127
  int8_t   rot;        // IMU yaw       -127~127
  uint8_t  mcount;     // 동작 누적 카운터. 0~255 순환
  uint8_t  mtype;      // 0=None 1=Horizontal 2=Vertical 3=Thrust
  uint8_t  mstrength;  // 0~255
  uint16_t ms;         // millis() 하위 16비트
} WandInput;

typedef struct __attribute__((packed)) {
  uint8_t  magic;      // 0xC1
  uint8_t  player_id;
  uint8_t  cmd;        // 1 = 진동
  uint8_t  arg1;       // 세기 0~255
  uint8_t  arg2;       // 지속 ms 하위 바이트
  uint8_t  arg3;       // 지속 ms 상위 바이트
  uint16_t seq;
} Cmd;

typedef struct __attribute__((packed)) {
  uint8_t  magic;      // 0xC2
  uint8_t  player_id;
  uint16_t seq;
} Echo;

static_assert(sizeof(WandInput) == 12, "WandInput 12바이트");
static_assert(sizeof(Cmd)       ==  8, "Cmd 8바이트");
static_assert(sizeof(Echo)      ==  4, "Echo 4바이트");

// 콜백에서는 담아만 둔다. Serial 도 I2C 도 거기서 건드리면 안 된다.
volatile bool pendCmd = false;
Cmd  rxCmd;

unsigned long txFail = 0;   // 전송 실패 누적. 동글이 안 듣고 있으면 여기가 올라간다

// 진동이 끝나는 시각. 0 이면 안 울리는 중.
unsigned long vibUntil = 0;

// ── 동작 판정 기준 (고정 후 실측) ─────────────────────────
// X 는 완드 길이 방향(비틀기 축)이라 베기에 안 나타난다. 판정에서 뺀다.
// 세로내리치기  Y 200~309  (Z 28~118)
// 좌우베기      Z 210~297  (Y 40~ 86)
// 찌르기        회전 전 축 175 이하, 순수 가속 0.84~1.44G

const float MOTION_START    = 150.0f;   // dps. 이만큼 빨라지면 동작 시작
const float MOTION_END      = 130.0f;   // dps. 이만큼 느려지면 동작 끝
const float MOTION_MIN_PEAK = 200.0f;   // dps. Y·Z 둘 다 이만큼 안 나오면 버린다
const float AXIS_MARGIN     =   1.2f;   // Z 가 Y 보다 이 배 이상이어야 가로베기

// 창을 닫을 때 보는 가속도(G).
//
// 예전에는 THRUST_ACCEL * 0.5 를 썼는데, 찌르기 문턱을 내리면 이 값도 같이
// 내려가서 창이 늦게 닫혔다. 지연과 직결되는 값이라 따로 뗐다.
const float MOTION_END_ACCEL = 0.4f;

// G. 중력을 뺀 순수 가속. mag 를 그대로 쓰면 미는 방향에 따라 문턱이 달라진다
// (앞으로 0.8G 밀어도 mag 는 1.28 밖에 안 된다. 중력과 직각이라).
// 찌르기로 인정하는 최소 순수 가속(G). 실측 범위가 0.84~1.44G 라
// 0.8 로 두면 약한 찌르기가 문턱에 걸려 통째로 빠졌다.
const float THRUST_ACCEL    =   0.6f;

// 찌르기 세기를 0~255 로 펴는 배수.
//
// 예전 150 은 255 를 채우려면 2.5G 가 필요했다. 실측 최대가 1.44G 라
// 아무리 세게 찔러도 세기가 0.38 을 못 넘었다. 1.45G 언저리에서 꽉 차게 맞춘다.
const float THRUST_SCALE    = 300.0f;

// ⚠ 이 둘이 체감 지연을 정한다. **동작은 창이 닫혀야 보고된다.**
//    휘두르는 중에는 아무것도 안 나가고, 멎은 뒤에야 한 번에 나간다.
const unsigned long MOTION_COOLDOWN = 250;   // ms. 한 번 잡으면 이 시간 쉰다. 내려친 뒤 올라오는 스트로크를 덮는다
const unsigned long MOTION_MAX      = 220;   // ms. 이보다 길면 강제 종료. 검 한 번 휘두름이 150~250ms

// ── 상태 ───────────────────────────────────────────────────

// raw[0..2] = 자이로 XYZ, raw[3..5] = 가속도 XYZ
int16_t raw[6];

// 자이로 바이어스 (원값 단위)
float biasX = 0, biasY = 0, biasZ = 0;

// 중력 벡터 추정. 저역통과로 따라간다. 가속도에서 이걸 빼면 순수 가속만 남는다.
float gravX = 0, gravY = 0, gravZ = 1.0f;
const float GRAV_ALPHA = 0.995f;   // 416Hz 기준 시정수 약 0.5초

bool  inMotion = false;
float peakGx, peakGy, peakGz;
float peakAcc;
unsigned long motionStart = 0;
unsigned long lastMotion  = 0;

// 패킷으로 나갈 값
uint8_t mcount    = 0;
uint8_t mtype     = 0;   // 0=None 1=Horizontal 2=Vertical 3=Thrust
uint8_t mstrength = 0;

unsigned long lastSend = 0;
unsigned long lastLog  = 0;

// ── 조이스틱 · 버튼 · FSR ──────────────────────────────────

#define PIN_JOY_X   1    // ADC1. ESP-NOW 가 Wi-Fi PHY 를 잡아서 ADC2 는 못 쓴다
#define PIN_JOY_Y   2
#define PIN_FSR     3
#define PIN_BTN1    4    // INPUT_PULLUP. 눌리면 LOW
#define PIN_BTN2    5

// 조이스틱 중립값. 부팅할 때 실제로 재서 채운다.
int centerX = 2048;
int centerY = 2048;

// 중립 근처의 흔들림을 죽이는 폭 (ADC 단위)
const int DEADZONE = 200;

// 조이스틱 축 방향. 실물 장착 방향에 맞춘다.
//
// 유니티 기준은 **+y 가 위(앞), +x 가 오른쪽**이다. 밀었는데 게임에서 반대로
// 가면 그 축의 부호를 뒤집는다. ADC 배선을 바꾸는 것보다 여기가 안전하다.
//
// Y 는 뒤집어 뒀다. 위로 밀면 캐릭터가 뒤로 가는 것을 실기에서 확인했다.
const int JOY_X_SIGN = +1;
const int JOY_Y_SIGN = -1;

// ADC 원값을 -127 ~ 127 로 바꾼다. 중립 근처는 0 으로 죽인다.
int toAxis(int raw, int center) {
  int diff = raw - center;

  if (diff > -DEADZONE && diff < DEADZONE) {
    return 0;
  }

  // 데드존 바깥부터 0 에서 시작하도록 빼준다.
  // 안 그러면 스틱을 살짝 밀자마자 값이 훅 튄다.
  if (diff > 0) { diff -= DEADZONE; }
  else          { diff += DEADZONE; }

  int span = 2048 - DEADZONE;
  long scaled = (long)diff * 127 / span;

  if (scaled >  127) { scaled =  127; }
  if (scaled < -127) { scaled = -127; }

  return (int)scaled;
}

// 손을 뗀 상태를 중립으로 잡는다.
// 고정값(2048)을 쓰면 개체 차이 때문에 캐릭터가 혼자 걸어간다.
void calibrateJoystick() {
  long sumX = 0, sumY = 0;

  for (int i = 0; i < 32; i++) {
    sumX += analogRead(PIN_JOY_X);
    sumY += analogRead(PIN_JOY_Y);
    delay(5);
  }

  centerX = sumX / 32;
  centerY = sumY / 32;

  Serial.printf("#BOOT wand=%d centerX=%d centerY=%d\n", WAND_ID, centerX, centerY);
}

// ── IMU ────────────────────────────────────────────────────

void writeReg(uint8_t reg, uint8_t val) {
  Wire.beginTransmission(IMU_ADDR);
  Wire.write(reg);
  Wire.write(val);
  Wire.endTransmission();
}

// 자이로 3축 + 가속도 3축을 한 번에 읽는다. 레지스터가 연속이라 나눌 이유가 없다.
bool readImu() {
  Wire.beginTransmission(IMU_ADDR);
  Wire.write(OUTX_L_G);
  Wire.endTransmission(false);
  Wire.requestFrom(IMU_ADDR, 12);

  if (Wire.available() < 12) {
    return false;
  }

  for (int i = 0; i < 6; i++) {
    uint8_t lo = Wire.read();
    uint8_t hi = Wire.read();
    raw[i] = (int16_t)((hi << 8) | lo);   // 하위 바이트가 먼저 온다
  }

  return true;
}

// 가만히 둔 상태의 자이로 평균을 바이어스로 잡는다.
// 이걸 안 빼면 정지 상태에서도 동작이 잡힌다.
// 같은 정지 구간에서 중력 벡터의 초기값도 같이 잡는다.
void calibrateGyro() {
  Serial.println("#CAL 움직이지 마세요");
  delay(1000);          // 손을 뗀 진동이 가라앉을 시간

  long sx = 0, sy = 0, sz = 0;
  long ax = 0, ay = 0, az = 0;
  const int N = 200;

  for (int i = 0; i < N; i++) {
    if (readImu()) {
      sx += raw[0];
      sy += raw[1];
      sz += raw[2];
      ax += raw[3];
      ay += raw[4];
      az += raw[5];
    }
    delay(5);
  }

  biasX = (float)sx / N;
  biasY = (float)sy / N;
  biasZ = (float)sz / N;

  gravX = (float)ax / N * 0.244f / 1000.0f;
  gravY = (float)ay / N * 0.244f / 1000.0f;
  gravZ = (float)az / N * 0.244f / 1000.0f;

  Serial.printf("#CAL 완료  bias %.1f %.1f %.1f  grav %.2f %.2f %.2f\n",
                biasX, biasY, biasZ, gravX, gravY, gravZ);

  // 읽기가 성공해도 값이 0 이면 IMU 가 파워다운이다. 중력 크기로 잡는다.
  float g = sqrt(gravX * gravX + gravY * gravY + gravZ * gravZ);
  if (g < 0.5f || g > 1.5f) {
    Serial.printf("#ERR IMU 이상 — 중력 %.2fG (1.0 이어야 함). 리셋하세요\n", g);
  }

  inMotion = false;
  lastMotion = millis();
}

// ── 동작 판정 ──────────────────────────────────────────────

// lax/lay/laz 는 중력을 뺀 가속도(G).
// 스윙이든 찌르기든 창을 하나만 연다. 따로 열면 스윙의 감속 가속도가
// 찌르기로 또 잡힌다 (실측: 스윙 직후 Thrust 가 항상 mag 1.8 언저리로 붙어 나왔다).
void detectMotion(float gx, float gy, float gz,
                  float lax, float lay, float laz, unsigned long now) {
  float gyroMag = sqrt(gx * gx + gy * gy + gz * gz);
  float accMag  = sqrt(lax * lax + lay * lay + laz * laz);

  // 쿨다운 중에는 아무것도 안 한다.
  // 없으면 한 번 휘둘렀는데 잔진동으로 서너 번 세진다.
  if (now - lastMotion < MOTION_COOLDOWN) {
    return;
  }

  if (!inMotion) {
    if (gyroMag > MOTION_START || accMag > THRUST_ACCEL) {
      inMotion = true;
      motionStart = now;
      peakGx = fabs(gx);
      peakGy = fabs(gy);
      peakGz = fabs(gz);
      peakAcc = accMag;
    }
    return;
  }

  // 동작 중이면 최대값을 계속 갱신한다
  if (fabs(gx) > peakGx) { peakGx = fabs(gx); }
  if (fabs(gy) > peakGy) { peakGy = fabs(gy); }
  if (fabs(gz) > peakGz) { peakGz = fabs(gz); }
  if (accMag   > peakAcc) { peakAcc = accMag; }

  // 회전이 멎어도 가속이 남아 있으면 아직 동작 중이다.
  // 이게 있어야 스윙의 감속 구간이 같은 창 안에 들어온다.
  bool ended = (gyroMag < MOTION_END && accMag < MOTION_END_ACCEL)
               || (now - motionStart > MOTION_MAX);
  if (!ended) {
    return;
  }

  inMotion = false;
  lastMotion = now;

  // X(길이 방향)는 손목 비틀기 축이라 베기에 안 나타난다. 판정에서 뺀다.
  // 안 빼면 비틀기가 X 400 대로 튀어서 가로베기로 잡힌다.
  float peakAxis = (peakGy > peakGz) ? peakGy : peakGz;

  if (peakAxis >= MOTION_MIN_PEAK) {
    // 회전이 충분하다 → 스윙. Z 가 뚜렷하게 커야 가로베기로 본다.
    float peak;
    if (peakGz > peakGy * AXIS_MARGIN) {
      mtype = 1;
      peak = peakGz;
    } else {
      mtype = 2;
      peak = peakGy;
    }

    mcount++;

    // 150~600dps 를 0~255 에 대응
    mstrength = constrain((int)((peak - 150.0f) * 255.0f / 450.0f), 0, 255);

    Serial.printf("#MOTION %-10s  X%4.0f Y%4.0f Z%4.0f  acc %.2f  str %3d  count %d\n",
                  mtype == 1 ? "Horizontal" : "Vertical",
                  peakGx, peakGy, peakGz, peakAcc, mstrength, mcount);
  }
  else if (peakAcc >= THRUST_ACCEL) {
    // 이렇다 할 회전 없이 직선 가속만 있었다 → 찌르기
    mtype = 3;
    mcount++;

    // 0.6~1.45G 를 0~255 에 대응. 실측 최대(1.44G)에서 꽉 찬다.
    mstrength = constrain((int)((peakAcc - THRUST_ACCEL) * THRUST_SCALE), 0, 255);

    Serial.printf("#MOTION Thrust      X%4.0f Y%4.0f Z%4.0f  acc %.2f  str %3d  count %d\n",
                  peakGx, peakGy, peakGz, peakAcc, mstrength, mcount);
  }
  else {
    // 잔진동. 카운트를 올리지 않는다. 임계값 조정용으로 찍기만 한다.
    Serial.printf("#MOTION drop        X%4.0f Y%4.0f Z%4.0f  acc %.2f\n",
                  peakGx, peakGy, peakGz, peakAcc);
  }
}

// ── 진동 (게임 -> 완드) ────────────────────────────────────
// ⚠ 모터가 아직 안 달려 있어 구동부는 주석이다.
//   지금은 명령이 제때 오는지, 지속시간이 맞게 오는지를 #VIB 로만 확인한다.
//   모터를 달면 setup 의 drv.begin() 과 아래 두 줄의 주석을 푼다.

void startVibration(uint8_t strength, uint16_t durationMs) {
  vibUntil = millis() + durationMs;

  // drv.setMode(DRV2605_MODE_REALTIME);
  // drv.setRealtimeValue(strength);

  Serial.printf("#VIB 시작 세기 %u  %u ms\n", strength, durationMs);
}

void stopVibration() {
  vibUntil = 0;

  // drv.setRealtimeValue(0);

  Serial.println("#VIB 끝");
}

// ── ESP-NOW ────────────────────────────────────────────────

#if ESP_ARDUINO_VERSION >= ESP_ARDUINO_VERSION_VAL(3,0,0)
void onRecv(const esp_now_recv_info_t*, const uint8_t* d, int n)
#else
void onRecv(const uint8_t*, const uint8_t* d, int n)
#endif
{
  if (n != sizeof(Cmd) || d[0] != MAGIC_CMD) { return; }

  Cmd c;
  memcpy(&c, d, sizeof(c));

  if (c.player_id != WAND_ID) { return; }

  rxCmd   = c;
  pendCmd = true;          // 무거운 처리는 loop 에서
}

void initEspNow() {
  WiFi.mode(WIFI_STA);
  WiFi.disconnect();
  WiFi.setSleep(false);    // 안 끄면 동글이 보낸 진동 명령을 씹는다
  esp_wifi_set_channel(CH, WIFI_SECOND_CHAN_NONE);

  if (esp_now_init() != ESP_OK) {
    Serial.println("#ERR esp_now_init 실패");
    return;
  }

  esp_now_register_recv_cb(onRecv);

  esp_now_peer_info_t p = {};
  memcpy(p.peer_addr, DONGLE, 6);
  p.channel = CH;
  p.encrypt = false;
  p.ifidx   = WIFI_IF_STA;

  if (esp_now_add_peer(&p) != ESP_OK) {
    Serial.println("#ERR 동글 peer 등록 실패. DONGLE MAC 을 확인하세요");
    return;
  }

  // ⚠ WiFi.macAddress() 는 부팅 직후 0 을 준다. eFuse 에서 직접 읽는다.
  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  Serial.printf("#NOW wand=%d mac=%02X:%02X:%02X:%02X:%02X:%02X ch=%d "
                "dongle=%02X:%02X:%02X:%02X:%02X:%02X\n",
                WAND_ID, mac[0], mac[1], mac[2], mac[3], mac[4], mac[5], CH,
                DONGLE[0], DONGLE[1], DONGLE[2], DONGLE[3], DONGLE[4], DONGLE[5]);
}

// 동글이 내려보낸 명령을 처리한다. 에코를 먼저 보내 왕복이 닫혔음을 알린다.
void handleCommand() {
  pendCmd = false;
  Cmd c = rxCmd;

  Echo e;
  e.magic     = MAGIC_ECHO;
  e.player_id = WAND_ID;
  e.seq       = c.seq;
  esp_now_send(DONGLE, (uint8_t*)&e, sizeof(e));

  if (c.cmd == CMD_VIBRATE) {
    uint16_t durationMs = (uint16_t)c.arg2 | ((uint16_t)c.arg3 << 8);
    startVibration(c.arg1, durationMs);
    return;
  }

  Serial.printf("#ERR 모르는 명령 %u\n", c.cmd);
}

// ── 메인 ───────────────────────────────────────────────────

void setup() {
  Serial.begin(115200);

#if ARDUINO_USB_CDC_ON_BOOT
  // 완드는 대개 USB 가 안 꽂혀 있다. 기본값이면 디버그 출력 한 번에 최대 100ms 를
  // 기다리다 416Hz 루프가 통째로 밀린다.
  Serial.setTxTimeoutMs(0);
#endif

  delay(1500);

  initEspNow();

  Wire.begin(8, 9);

  writeReg(CTRL1_XL, 0x6C);   // 가속도 416Hz, ±8G
  writeReg(CTRL2_G,  0x6C);   // 자이로 416Hz, ±2000dps
  delay(100);

  // 모터를 달면 주석을 푼다. IMU 와 같은 I2C 버스(8,9)를 쓴다.
  // drv.begin();
  // drv.selectLibrary(1);

  pinMode(PIN_BTN1, INPUT_PULLUP);
  pinMode(PIN_BTN2, INPUT_PULLUP);
  calibrateJoystick();

  calibrateGyro();

  Serial.println("#READY  r 을 보내면 자이로 재보정");
}

void loop() {
  if (Serial.available()) {
    if (Serial.read() == 'r') { calibrateGyro(); }
  }

  if (pendCmd) { handleCommand(); }

  if (!readImu()) {
    Serial.println("#ERR 읽기 실패");
    delay(500);
    return;
  }

  unsigned long now = millis();

  // 진동은 블로킹하지 않는다. 데드라인이 지나면 끈다.
  if (vibUntil != 0 && (long)(now - vibUntil) >= 0) { stopVibration(); }

  // 원값 → 실제 단위. 자이로는 바이어스를 뺀 뒤 환산한다.
  float gx = (raw[0] - biasX) * 70.0f / 1000.0f;   // dps
  float gy = (raw[1] - biasY) * 70.0f / 1000.0f;
  float gz = (raw[2] - biasZ) * 70.0f / 1000.0f;

  float ax = raw[3] * 0.244f / 1000.0f;            // G
  float ay = raw[4] * 0.244f / 1000.0f;
  float az = raw[5] * 0.244f / 1000.0f;

  // 중력은 천천히 따라가고, 나머지를 순수 가속으로 쓴다.
  gravX = GRAV_ALPHA * gravX + (1.0f - GRAV_ALPHA) * ax;
  gravY = GRAV_ALPHA * gravY + (1.0f - GRAV_ALPHA) * ay;
  gravZ = GRAV_ALPHA * gravZ + (1.0f - GRAV_ALPHA) * az;

  float lax = ax - gravX;
  float lay = ay - gravY;
  float laz = az - gravZ;

  detectMotion(gx, gy, gz, lax, lay, laz, now);

  // tilt — 완드를 좌우로 눕히거나 휠처럼 돌린 각도.
  // 실측(고정 후): X 가 회전축이고 중력이 Y↔Z 를 오간다. 기본 자세 +8도,
  // 왼쪽 -82~-90도, 오른쪽 +84~+97도. 그래서 ±90도를 범위로 잡는다.
  float roll = atan2(ay, az) * 57.2958f;

  int tilt = constrain((int)(roll * 127.0f / 90.0f), -127, 127);   // ±90도 → ±127

  // ── 동글로 올려보내기, 50Hz ─────────────────────────────
  // 동글이 이 구조체를 그대로 풀어서 유니티 CSV 한 줄로 찍는다.
  if (now - lastSend >= 20) {
    lastSend = now;

    int x = toAxis(analogRead(PIN_JOY_X), centerX) * JOY_X_SIGN;
    int y = toAxis(analogRead(PIN_JOY_Y), centerY) * JOY_Y_SIGN;

    // 풀업이라 눌리면 LOW. 뒤집어서 읽는다.
    uint8_t buttons = 0;
    if (digitalRead(PIN_BTN1) == LOW) { buttons |= 0x01; }
    if (digitalRead(PIN_BTN2) == LOW) { buttons |= 0x02; }

    WandInput in;
    in.magic     = MAGIC_INPUT;
    in.player_id = WAND_ID;
    in.x         = (int8_t)x;
    in.y         = (int8_t)y;
    in.buttons   = buttons;
    in.tilt      = (int8_t)tilt;
    in.rot       = 0;              // 비틀기는 스코프에서 뺐다. 자리는 유지한다
    in.mcount    = mcount;
    in.mtype     = mtype;
    in.mstrength = mstrength;
    in.ms        = (uint16_t)(now % 65536);

    if (esp_now_send(DONGLE, (uint8_t*)&in, sizeof(in)) != ESP_OK) {
      txFail++;
    }
  }

  // FSR 은 패킷에 안 들어간다. 1 초에 한 번 로그로만 본다.
  // txfail 이 계속 오르면 동글이 안 듣고 있는 것이다. MAC 과 채널부터 본다.
  if (now - lastLog >= 1000) {
    lastLog = now;
    Serial.printf("#FSR %d  txfail %lu\n", analogRead(PIN_FSR), txFail);
  }
}
