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
//   완드 4대 모두 **같은 파일을 그대로** 굽는다. 번호와 동글은 아래 SLOTS 표가
//   보드 MAC 으로 정한다. 새 보드나 새 동글이 생기면 그 표만 고친다.

#include <Wire.h>
#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include <esp_mac.h>   // esp_read_mac. WiFi.macAddress() 는 부팅 직후 0 을 준다 (동글에서 실기 확인)
#include <esp_sleep.h>
#include <driver/rtc_io.h>

// 진동 드라이버. 모터를 달면 주석을 푼다. (startVibration · stopVibration · setup 도 같이)
// #include <Adafruit_DRV2605.h>
// Adafruit_DRV2605 drv;

#define IMU_ADDR  0x6B

#define CTRL1_XL  0x10   // 가속도 설정
#define CTRL2_G   0x11   // 자이로 설정
#define OUTX_L_G  0x22   // 자이로 X 부터 12바이트 연속

// ── ESP-NOW ────────────────────────────────────────────────

#define CH          1     // 동글과 같아야 한다

// 이 완드에 IMU 가 달려 있는가.
//
// 0 으로 두면 IMU 초기화 · 자이로 보정 · 동작 판정을 통째로 뺀다. 스틱과 버튼만 올라가고
// tilt · mtype · strength 는 0 으로 나간다. 부품이 덜 온 보드로 2대 동시 동작을
// 먼저 확인할 때 쓴다.
//
// ⚠ 0 으로 굽지 않고 IMU 없는 보드에 그냥 구우면 readImu() 가 계속 실패해서
//    **패킷이 하나도 안 나간다.** 동글에는 그 완드가 아예 없는 것처럼 보인다.
#ifndef HAS_IMU
#define HAS_IMU     1
#endif

// ── 완드 배정 ──────────────────────────────────────────────
// 부팅할 때 내 STA MAC 을 읽어 이 표에서 번호와 동글을 찾는다.
// 보드마다 번호를 고쳐 굽고 되돌리는 것을 잊는 사고를 없애려고 표로 뺐다.
//
// 동글 두 대를 가까이 둬도 서로의 완드를 안 받는다. 완드는 제 동글 MAC 으로만
// 보내고(유니캐스트), ESP-NOW 는 자기 MAC 앞으로 온 것만 수신 콜백에 올린다.
//
// ⚠ 동글 MAC 은 STA 쪽이다. SoftAP 쪽(5A:...)이 아니다. ESP-NOW 를 WIFI_IF_STA 로 보낸다.
// 번호는 유니티 기본값(leftHandId 0 · rightHandId 1)에 맞춰 0 = 왼손, 1 = 오른손이다.

const uint8_t DONGLE_A[6] = { 0x58, 0xE6, 0xC5, 0x6A, 0x92, 0xD8 };   // 자리 A
const uint8_t DONGLE_B[6] = { 0x10, 0x51, 0xDB, 0x78, 0xF4, 0x58 };   // 자리 B

struct WandSlot {
  uint8_t        mac[6];   // 이 보드의 STA MAC
  const uint8_t* dongle;
  uint8_t        id;
};

const WandSlot SLOTS[] = {
  { { 0xDC, 0x54, 0x75, 0xEB, 0x82, 0xC0 }, DONGLE_A, 0 },   // TinyS3 #1  자리 A 왼손
  { { 0xDC, 0x54, 0x75, 0xEB, 0x80, 0xB4 }, DONGLE_A, 1 },   // TinyS3 #4  자리 A 오른손
  { { 0xDC, 0x54, 0x75, 0xEB, 0x84, 0xC8 }, DONGLE_B, 0 },   // TinyS3 #2  자리 B 왼손
  { { 0xDC, 0x54, 0x75, 0xEB, 0x83, 0xF4 }, DONGLE_B, 1 },   // TinyS3 #3  자리 B 오른손
};

// 표에서 찾아 채운다 (findSlot)
uint8_t DONGLE[6];
uint8_t wandId = 0;

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
// dps. Y·Z 둘 다 이만큼 안 나오면 스윙으로 안 본다.
//
// 200 이었을 때 **세게 찌른 것이 전부 베기로 샜다** (실측). 힘줘 찌르면 손목이 딸려
// 돌아가 Z 가 200~301 까지 나오는데, 문턱이 200 이라 스윙 분기로 넘어갔다.
// 게임이 "찌르기" 로 받은 것은 손을 빼는 약한 동작이었다.
//
//   찌르기가 딸고 오는 회전   최대 301
//   진짜 좌우베기            최소 440
//   진짜 세로내리치기         최소 431
//
// 그 사이가 비어 있어서 350 으로 올린다. 베기는 다 살고 찌르기는 안 샌다.
const float MOTION_MIN_PEAK = 350.0f;
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
// ms. 한 번 잡으면 이 시간 쉰다. 잔진동으로 두 번 세는 것만 막는다.
//
// ⚠ **되돌림은 이 값으로 못 막는다.** 450 까지 올려 봤지만 이벤트 간격이
//    MOTION_MAX + MOTION_COOLDOWN 에 딱 붙어 포화될 뿐, 10회가 20~24회로 잡혔다.
//    1회로 만들려면 쿨다운이 제스처 전체 길이를 넘어야 하는데 그건 사람마다 다르다.
//    되돌림은 아래 isReturn 이 부호로 거른다.
const unsigned long MOTION_COOLDOWN = 250;

// ms. 직전 동작이 이 시간 안에 있었을 때만 되돌림으로 의심한다.
// 한참 뒤의 딴 동작까지 반대 부호라고 먹어 버리면 안 된다.
const unsigned long RETURN_WINDOW = 800;
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

// 순수가속을 축별로, **부호를 살려서** 들고 있는다.
// 찌르기의 되돌림(손을 빼는 것)은 X 부호가 반대로 나온다.
float peakLax, peakLay, peakLaz;

// 자이로도 부호를 살려서 들고 있는다. peakGy/peakGz 는 fabs 라 방향이 지워진다.
// 내려치는 것과 올리는 것은 **같은 축을 반대로** 도는 것이라, 이게 없으면 구분이 안 된다.
float sPeakGy, sPeakGz;

// 마지막으로 카운트한 동작의 방향. 되돌림 판정에 쓴다.
// 버려진 동작은 여기를 안 건드린다. 기준은 언제나 **진짜 공격**이어야 한다.
int           lastAxis   = 0;   // 0=없음 1=Y회전 2=Z회전 3=X가속(찌르기)
float         lastDir    = 0;   // 그 축의 부호 있는 최대값
unsigned long lastReport = 0;
unsigned long motionStart = 0;
unsigned long lastMotion  = 0;

// 패킷으로 나갈 값
uint8_t mcount    = 0;
uint8_t mtype     = 0;   // 0=None 1=Horizontal 2=Vertical 3=Thrust
uint8_t mstrength = 0;

unsigned long lastSend = 0;
unsigned long lastLog  = 0;

// ── 휴면 ───────────────────────────────────────────────────
// 버튼 · 스틱이 이만큼 안 움직이면 딥슬립에 들어간다. 버튼을 누르면 깬다.
// IMU 는 안 본다. 책상에 놓아도 잔떨림에 값이 바뀌어서 영영 안 잠든다.
const unsigned long IDLE_SLEEP_MS = 30UL * 60 * 1000;   // 30분
unsigned long lastActive = 0;

// ── 조이스틱 · 버튼 · FSR ──────────────────────────────────

#define PIN_JOY_X   1    // ADC1. ESP-NOW 가 Wi-Fi PHY 를 잡아서 ADC2 는 못 쓴다
#define PIN_JOY_Y   2
#define PIN_FSR     3
#define PIN_BTN1    4    // INPUT_PULLUP. 눌리면 LOW
#define PIN_BTN2    5
#define PIN_JOY_SW  6    // 조이스틱 푸시. INPUT_PULLUP. 눌리면 재보정

// 조이스틱 푸시의 직전 상태. 떼는 순간(LOW→HIGH)에만 재보정한다.
int swPrev = HIGH;

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

// tilt(조타 휠) 방향. 완드를 **오른쪽으로 눕히면 +** 여야 한다.
//
// IMU 는 뒤집혀 붙어 있다. 기준 자세에서 az 가 -1G 로 들어오는 것으로 확인했다.
// 그래서 roll 은 -ay / -az 로 계산한다(아래 tilt 계산 참고). 이걸로 **기준 자세가
// 0 도**가 되는 것까지는 확정이다.
//
// 남는 것은 좌우 방향 하나다. 뒤집힌 축이 X(완드 길이 방향)냐 Y 냐에 따라
// 좌우가 반대로 나오는데, az 값만으로는 둘을 구분할 수 없다. 실기로 정한다.
//
//   1. 완드를 굽고 시리얼을 연다
//   2. 기준 자세로 들고 #TILT 가 0 근처인지 본다
//   3. 오른쪽으로 눕혀서 **양수**로 가면 +1 그대로, 음수로 가면 -1 로 바꿔 다시 굽는다
const int TILT_SIGN = +1;

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

  Serial.printf("#BOOT wand=%d centerX=%d centerY=%d\n", wandId, centerX, centerY);
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
      peakLax = lax;
      peakLay = lay;
      peakLaz = laz;
      sPeakGy = gy;
      sPeakGz = gz;
    }
    return;
  }

  // 동작 중이면 최대값을 계속 갱신한다
  if (fabs(gx) > peakGx) { peakGx = fabs(gx); }
  if (fabs(gy) > peakGy) { peakGy = fabs(gy); sPeakGy = gy; }
  if (fabs(gz) > peakGz) { peakGz = fabs(gz); sPeakGz = gz; }
  if (accMag   > peakAcc) { peakAcc = accMag; }

  // 크기가 가장 컸던 순간의 값을 부호째 남긴다
  if (fabs(lax) > fabs(peakLax)) { peakLax = lax; }
  if (fabs(lay) > fabs(peakLay)) { peakLay = lay; }
  if (fabs(laz) > fabs(peakLaz)) { peakLaz = laz; }

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

  // 무엇이었는지와, 그것이 **어느 방향이었는지**를 같이 정한다.
  // 방향은 되돌림을 거르는 데만 쓰고 유니티로는 안 나간다.
  uint8_t type = 0;
  float   peak = 0;     // 세기 계산에 쓸 값
  int     axis = 0;     // 되돌림을 비교할 축
  float   dir  = 0;     // 그 축의 부호 있는 값

  if (peakAxis >= MOTION_MIN_PEAK) {
    // 회전이 충분하다 → 스윙. Z 가 뚜렷하게 커야 가로베기로 본다.
    if (peakGz > peakGy * AXIS_MARGIN) {
      type = 1; peak = peakGz; axis = 2; dir = sPeakGz;
    } else {
      type = 2; peak = peakGy; axis = 1; dir = sPeakGy;
    }
  }
  else if (peakAcc >= THRUST_ACCEL
           && fabs(peakLax) >= fabs(peakLay)
           && fabs(peakLax) >= fabs(peakLaz)) {
    // 이렇다 할 회전 없이 직선 가속만 있었다 → 찌르기.
    // 미는 것과 빼는 것은 완드 길이 방향(X) 가속의 부호가 반대다.
    //
    // ⚠ **X 가 지배적이어야 한다.** 찌르기는 완드를 길이 방향으로 미는 것이다.
    //    이 조건이 없으면 베기의 되돌림이 전부 찌르기로 샌다 — 실측에서 세로베기
    //    뒤에 나온 가짜 찌르기 13개 중 12개가 Z 지배였다.
    type = 3; peak = peakAcc; axis = 3; dir = peakLax;
  }

  if (type == 0) {
    // 잔진동. 카운트를 올리지 않는다. 임계값 조정용으로 찍기만 한다.
    Serial.printf("#MOTION drop        X%4.0f Y%4.0f Z%4.0f  acc %.2f  lin %+.2f %+.2f %+.2f\n",
                  peakGx, peakGy, peakGz, peakAcc,
                  peakLax, peakLay, peakLaz);
    return;
  }

  // 되돌림인가 — 내려친 뒤 팔을 올리는 것은 공격이 아니다.
  //
  // 크기만 보면 되돌림과 본 동작이 똑같이 보인다(fabs 가 방향을 지운다). 그래서
  // **같은 축을 반대로 돌았고, 게다가 더 약하면** 되돌림으로 본다.
  // 진짜 연타는 두 번째도 세게 치므로 "더 약하다" 에 안 걸린다.
  //
  // ⚠ 비교는 **직전에 인정한 동작의 축**으로 한다. 지금 동작이 무엇으로 분류됐는지는
  //    상관없다. 되돌림은 문턱(350dps)을 못 넘어 찌르기로 분류되기도 하는데,
  //    그때 현재 축(X가속)으로 비교하면 기준(Y회전)과 축이 안 맞아 그냥 통과한다.
  //    실측에서 세로베기 되돌림이 이 구멍으로 전부 샜다.
  // ── ⚠ 되돌림 거르기를 꺼 뒀다 (무쌍 리듬 구간) ────────────────────────────
  //
  // 리듬 게임은 **박자마다 입력이 들어와야** 한다. 되돌림을 거르면 빠르게 이어
  // 치는 동작이 같이 먹혀서 박자를 놓친다. 리듬 구간에서는 여분의 입력보다
  // 놓치는 쪽이 훨씬 치명적이라, 거르는 것을 통째로 끈다.
  //
  // 분류(문턱 350 · 찌르기 X축 조건)는 그대로다. 종류는 여전히 맞게 나가고,
  // 대신 한 번 휘두르면 되돌림까지 2회쯤 잡힌다.
  //
  // 되살리려면 아래 주석을 풀기만 하면 된다. 위쪽 설명과 7-4 문서 참고.
  /*
  float back = (lastAxis == 1) ? sPeakGy
             : (lastAxis == 2) ? sPeakGz
             : (lastAxis == 3) ? peakLax
             : 0.0f;

  bool isReturn = lastAxis != 0
                  && back * lastDir < 0
                  && fabs(back) < fabs(lastDir)
                  && now - lastReport < RETURN_WINDOW;

  if (isReturn) {
    Serial.printf("#MOTION return      X%4.0f Y%4.0f Z%4.0f  acc %.2f  lin %+.2f %+.2f %+.2f  축%d %+.2f (직전 %+.2f)\n",
                  peakGx, peakGy, peakGz, peakAcc,
                  peakLax, peakLay, peakLaz, lastAxis, back, lastDir);
    return;
  }
  */

  // 기준은 언제나 마지막으로 **인정한** 동작이다. 버린 것으로 갱신하면
  // 되돌림이 다음 되돌림의 기준이 돼서 판정이 흔들린다.
  lastAxis   = axis;
  lastDir    = dir;
  lastReport = now;

  mtype = type;
  mcount++;

  mstrength = (type == 3)
      // 0.6~1.45G 를 0~255 에 대응. 실측 최대(1.44G)에서 꽉 찬다.
      ? constrain((int)((peak - THRUST_ACCEL) * THRUST_SCALE), 0, 255)
      // 150~600dps 를 0~255 에 대응
      : constrain((int)((peak - 150.0f) * 255.0f / 450.0f), 0, 255);

  Serial.printf("#MOTION %-10s  X%4.0f Y%4.0f Z%4.0f  acc %.2f  lin %+.2f %+.2f %+.2f  str %3d  count %d\n",
                type == 1 ? "Horizontal" : (type == 2 ? "Vertical" : "Thrust"),
                peakGx, peakGy, peakGz, peakAcc,
                peakLax, peakLay, peakLaz, mstrength, mcount);
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

  if (c.player_id != wandId) { return; }

  rxCmd   = c;
  pendCmd = true;          // 무거운 처리는 loop 에서
}

// 내 MAC 으로 SLOTS 에서 번호와 동글을 찾는다.
// 표에 없는 보드면 제 MAC 을 찍으며 멈춘다. 엉뚱한 번호로 남의 자리에 끼어들 바엔 안 보내는 게 낫다.
void findSlot() {
  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  for (const WandSlot& s : SLOTS) {
    if (memcmp(s.mac, mac, 6) == 0) {
      memcpy(DONGLE, s.dongle, 6);
      wandId = s.id;
      return;
    }
  }

  while (true) {
    Serial.printf("#ERR 배정 표(SLOTS)에 없는 보드 %02X:%02X:%02X:%02X:%02X:%02X — 표에 넣고 다시 구우세요\n",
                  mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
    delay(1000);
  }
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
                wandId, mac[0], mac[1], mac[2], mac[3], mac[4], mac[5], CH,
                DONGLE[0], DONGLE[1], DONGLE[2], DONGLE[3], DONGLE[4], DONGLE[5]);
}

// 동글이 내려보낸 명령을 처리한다. 에코를 먼저 보내 왕복이 닫혔음을 알린다.
void handleCommand() {
  pendCmd = false;
  Cmd c = rxCmd;

  Echo e;
  e.magic     = MAGIC_ECHO;
  e.player_id = wandId;
  e.seq       = c.seq;
  esp_now_send(DONGLE, (uint8_t*)&e, sizeof(e));

  if (c.cmd == CMD_VIBRATE) {
    uint16_t durationMs = (uint16_t)c.arg2 | ((uint16_t)c.arg3 << 8);
    startVibration(c.arg1, durationMs);
    return;
  }

  Serial.printf("#ERR 모르는 명령 %u\n", c.cmd);
}

// 딥슬립. 깨어나면 재부팅이라 setup 부터 다시 돈다(보정 포함).
//
// 버튼 1 · 2 · 스틱 누르기로 깬다. 스틱을 **밀어서는 못 깨운다** — 아날로그라
// 깨우는 핀으로 걸 수 없다.
void goToSleep() {
  Serial.printf("#SLEEP 버튼 · 스틱이 %lu분 동안 없었다. 버튼을 누르면 깬다\n", IDLE_SLEEP_MS / 60000);

#if HAS_IMU
  // IMU 는 제 전원으로 계속 돈다. 파워다운으로 내린다. 깨면 setup 이 다시 켠다.
  writeReg(CTRL1_XL, 0x00);
  writeReg(CTRL2_G,  0x00);
#endif

  // 버튼은 눌리면 LOW 다. 잠든 동안에도 풀업이 살아 있어야 뜬 핀이 LOW 로 튀어 저절로 안 깬다.
  const gpio_num_t pins[] = { (gpio_num_t)PIN_BTN1, (gpio_num_t)PIN_BTN2, (gpio_num_t)PIN_JOY_SW };
  uint64_t mask = 0;
  for (gpio_num_t p : pins) {
    rtc_gpio_pullup_en(p);
    rtc_gpio_pulldown_dis(p);
    mask |= 1ULL << p;
  }
  esp_sleep_enable_ext1_wakeup_io(mask, ESP_EXT1_WAKEUP_ANY_LOW);

  delay(50);   // 마지막 로그가 USB 로 나갈 틈
  esp_deep_sleep_start();
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

  findSlot();
  initEspNow();

#if HAS_IMU
  Wire.begin(8, 9);

  writeReg(CTRL1_XL, 0x6C);   // 가속도 416Hz, ±8G
  writeReg(CTRL2_G,  0x6C);   // 자이로 416Hz, ±2000dps
  delay(100);

  // 모터를 달면 주석을 푼다. IMU 와 같은 I2C 버스(8,9)를 쓴다.
  // drv.begin();
  // drv.selectLibrary(1);
#endif

  pinMode(PIN_BTN1, INPUT_PULLUP);
  pinMode(PIN_BTN2, INPUT_PULLUP);
  pinMode(PIN_JOY_SW, INPUT_PULLUP);
  calibrateJoystick();

#if HAS_IMU
  calibrateGyro();
  Serial.println("#READY  스틱을 누르거나 r 을 보내면 재보정");
#else
  Serial.println("#READY  IMU 없음 — 스틱·버튼만 올린다. 스틱을 누르거나 r 을 보내면 재보정");
#endif
}

void loop() {
  if (Serial.available()) {
    if (Serial.read() == 'r') {
#if HAS_IMU
      calibrateGyro();
#else
      calibrateJoystick();
#endif
    }
  }

  // 조이스틱 푸시로 재보정. 누를 때가 아니라 **뗄 때** 잡는다. 누른 채로 재면
  // 엄지에 밀린 스틱 위치가 중립으로 박혀서 캐릭터가 혼자 걷는다.
  // 보정이 2 초 넘게 잡고 있어서 디바운스는 따로 안 둔다.
  int sw = digitalRead(PIN_JOY_SW);
  if (swPrev == LOW && sw == HIGH) {
    calibrateJoystick();
#if HAS_IMU
    calibrateGyro();
#endif
  }
  swPrev = sw;

  if (pendCmd) { handleCommand(); }

#if HAS_IMU
  if (!readImu()) {
    Serial.println("#ERR 읽기 실패");
    delay(500);
    return;
  }
#else
  // IMU 읽기가 빠진 자리. 안 쉬면 코어 하나를 100% 로 태운다.
  delay(2);
#endif

  unsigned long now = millis();

  // 진동은 블로킹하지 않는다. 데드라인이 지나면 끈다.
  if (vibUntil != 0 && (long)(now - vibUntil) >= 0) { stopVibration(); }

  // IMU 가 없으면 0 으로 나간다. 유니티는 Tilt 0 · 동작 없음으로 받는다.
  int tilt = 0;

#if HAS_IMU
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

  // tilt — 완드를 좌우로 눕히거나 휠처럼 돌린 각도. X 가 회전축이고 중력이 Y↔Z 를 오간다.
  //
  // ⚠ IMU 가 **뒤집혀** 붙어 있다. 기준 자세에서 az ≈ -0.97 로 들어온다.
  //    그대로 atan2(ay, az) 를 쓰면 기준 자세가 ±170도 근처로 나와서 tilt 가
  //    -127 에 붙은 채 움직이지 않는다. 좌우 구분이 아예 안 된다.
  //    (무쌍은 tilt 를 안 써서 드러나지 않았고, 조타에서만 터진다.)
  //
  //    두 축의 부호를 되돌려 기준 자세를 0 도로 만든다. 좌우 방향은 TILT_SIGN 으로 정한다.
  float roll = atan2(-ay, -az) * 57.2958f * TILT_SIGN;

  tilt = constrain((int)(roll * 127.0f / 90.0f), -127, 127);   // ±90도 → ±127
#endif

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

    // 휴면 타이머. 버튼 · 스틱 누르기 · 스틱 기울이기만 활동으로 친다.
    if (buttons != 0 || sw == LOW || x != 0 || y != 0) { lastActive = now; }
    if (now - lastActive >= IDLE_SLEEP_MS) { goToSleep(); }

    WandInput in;
    in.magic     = MAGIC_INPUT;
    in.player_id = wandId;
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
#if HAS_IMU
    // TILT_SIGN 을 정할 때 본다. 기준 자세면 0 근처, 오른쪽으로 눕히면 양수여야 한다.
    // grav 는 저역통과한 중력이라 가만히 들고 있으면 가속도 원값과 같다.
    Serial.printf("#TILT %4d  grav %+.2f %+.2f %+.2f\n", tilt, gravX, gravY, gravZ);
#endif
  }
}
