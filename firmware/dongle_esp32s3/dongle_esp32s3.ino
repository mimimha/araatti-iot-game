// 동글 (ESP32-S3) — 유니티 <-> 완드(TinyS3) 양방향 중계
//
//     [완드 TinyS3 0~3]   IMU · 버튼 · 진동모터
//            |  ESP-NOW  (자리 A ch 1 · 자리 B ch 6)
//     [동글 ESP32-S3]     <- 이 코드
//            |  USB Serial 115200
//     [유니티 IotPlayerController]
//
// 동글은 값을 만들지 않는다. 형식만 바꿔 양쪽으로 넘긴다.
// 동작 판정은 완드가, 게임 규칙은 유니티가 갖는다.
//
// ── 올려보내는 것 (완드 -> 유니티) ─────────────────────────
//   id,x,y,buttons,tilt,rot,mcount,mtype,strength,ms
//   0,-127,64,2,-90,35,7,2,180,56407
//
//   '#' 으로 시작하는 줄은 동글 로그다. 유니티가 입력으로 세지 않고 버린다.
//
// ── 내려보내는 것 (유니티 -> 완드) ─────────────────────────
//   V,<완드번호 0~3>,<세기 0~255>,<지속 ms>
//   V,1,200,120
//
// ── 완드 MAC 을 적어두지 않는 이유 ─────────────────────────
// 완드가 입력을 보내는 순간 그 MAC 을 완드번호에 묶는다. 완드를 교체하거나
// 번호를 바꿔도 동글을 다시 굽지 않는다.
// 대신 **완드는 동글 MAC 을 알아야 한다.** 부팅 로그의 #MAC 을 완드에 적는다.

#include <WiFi.h>
#include <esp_now.h>
#include <esp_wifi.h>
#include <esp_mac.h>   // esp_read_mac. WiFi.macAddress() 는 부팅 직후 0 을 준다 (printMac 참고)

// 자리마다 채널을 나눈다. 부팅할 때 내 MAC 으로 고르고, 자리 B 가 아니면 전부 자리 A 채널이다.
// ⚠ 완드의 DONGLE_A · DONGLE_B 채널과 같아야 한다.
#define CH_A        1
#define CH_B        6
static const uint8_t DONGLE_B[6] = { 0x10, 0x51, 0xDB, 0x78, 0xF4, 0x58 };
static uint8_t nowCh = CH_A;

#define WAND_COUNT  4

#define MAGIC_INPUT 0xA1  // 완드 -> 동글  입력
#define MAGIC_CMD   0xC1  // 동글 -> 완드  명령
#define MAGIC_ECHO  0xC2  // 완드 -> 동글  명령 받았다는 답

#define CMD_VIBRATE 1

// ── 패킷 ───────────────────────────────────────────────────

// 완드가 주기적으로 올려보내는 한 벌. 유니티 CSV 한 줄과 1:1 이다.
typedef struct __attribute__((packed)) {
  uint8_t  magic;      // 0xA1
  uint8_t  player_id;  // 0~3. 손이 아니라 기기 번호다
  int8_t   x, y;       // 스틱          -127~127
  uint8_t  buttons;    // bit0=버튼1 bit1=버튼2
  int8_t   tilt;       // IMU roll      -127~127
  int8_t   rot;        // IMU yaw       -127~127
  uint8_t  mcount;     // 동작 누적 카운터. 0~255 순환
  uint8_t  mtype;      // 0=None 1=Horizontal 2=Vertical 3=Thrust
  uint8_t  mstrength;  // 0~255
  uint16_t ms;         // 완드 millis() 하위 16비트
} WandInput;

// arg1~arg3 · seq 배치는 이미 확인한 역방향 스케치 그대로다. 완드를 안 고쳐도 붙는다.
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

// ── 수신 링 ────────────────────────────────────────────────
// ESP-NOW 콜백은 WiFi 태스크에서 불린다. 거기서 Serial 을 찍으면
// 호스트가 안 읽어갈 때 태스크가 통째로 막힌다. 담아만 두고 loop 에서 처리한다.

#define RX_SLOTS 16

typedef struct {
  uint8_t mac[6];
  uint8_t len;
  uint8_t data[16];
} RawPacket;

static RawPacket       rxRing[RX_SLOTS];
static volatile uint8_t rxHead = 0;   // 콜백이 쓴다
static volatile uint8_t rxTail = 0;   // loop 가 읽는다

// ── 완드 등록부 ────────────────────────────────────────────

static uint8_t wandMac[WAND_COUNT][6];
static bool    wandKnown[WAND_COUNT] = { false, false, false, false };

static uint16_t cmdSeq = 0;

// 명령 미도달. 송신 콜백이 세우고 loop 가 찍는다.
static volatile bool   sendFailed = false;
static volatile int8_t sendFailId = -1;

// ── ESP-NOW ────────────────────────────────────────────────

#if ESP_ARDUINO_VERSION >= ESP_ARDUINO_VERSION_VAL(3,0,0)
void onRecv(const esp_now_recv_info_t* info, const uint8_t* d, int n) {
  const uint8_t* mac = info->src_addr;
#else
void onRecv(const uint8_t* mac, const uint8_t* d, int n) {
#endif
  if (n <= 0 || n > (int)sizeof(rxRing[0].data)) { return; }

  uint8_t next = (rxHead + 1) % RX_SLOTS;
  if (next == rxTail) { return; }   // 가득 찼다. 유니티가 안 읽어가고 있다

  memcpy(rxRing[rxHead].mac, mac, 6);
  rxRing[rxHead].len = (uint8_t)n;
  memcpy(rxRing[rxHead].data, d, n);

  rxHead = next;
}

// MAC 으로 완드 번호를 되찾는다. 못 찾으면 -1.
static int wandIdOf(const uint8_t* mac) {
  for (int i = 0; i < WAND_COUNT; i++) {
    if (wandKnown[i] && memcmp(wandMac[i], mac, 6) == 0) { return i; }
  }
  return -1;
}

// 명령이 실제로 완드에 닿았는지.
//
// esp_now_send 는 큐에 넣기만 하고 ESP_OK 를 돌려준다. 완드가 꺼져 있어도 마찬가지다.
// 전달 실패는 여기서만 알 수 있다. 동글이 보내는 것은 명령뿐이라 이 콜백이
// 불렸다는 것은 곧 명령 하나가 끝났다는 뜻이다.
//
// ⚠ 성공은 찍지 않는다. 완드가 돌려주는 #ECHO 가 이미 그 역할을 한다.
// ⚠ 수신 콜백과 같이 Serial 을 건드리지 않는다. 여기는 WiFi 태스크다.
#if ESP_ARDUINO_VERSION >= ESP_ARDUINO_VERSION_VAL(3,0,0)
void onSent(const esp_now_send_info_t* info, esp_now_send_status_t status) {
  const uint8_t* mac = info->des_addr;
#else
void onSent(const uint8_t* mac, esp_now_send_status_t status) {
#endif
  if (status == ESP_NOW_SEND_SUCCESS) { return; }

  sendFailId = (int8_t)wandIdOf(mac);
  sendFailed = true;
}

// 완드가 말을 걸어온 MAC 을 그 번호에 묶는다. 바뀌었으면 갈아끼운다.
static void learnWand(uint8_t id, const uint8_t* mac) {
  if (wandKnown[id] && memcmp(wandMac[id], mac, 6) == 0) { return; }

  if (wandKnown[id]) {
    esp_now_del_peer(wandMac[id]);
    wandKnown[id] = false;
  }

  esp_now_peer_info_t p = {};
  memcpy(p.peer_addr, mac, 6);
  p.channel = nowCh;
  p.encrypt = false;
  p.ifidx   = WIFI_IF_STA;

  if (esp_now_add_peer(&p) != ESP_OK) {
    Serial.printf("#ERR 완드 %d peer 등록 실패\n", id);
    return;
  }

  memcpy(wandMac[id], mac, 6);
  wandKnown[id] = true;

  Serial.printf("#WAND %d 등록 %02X:%02X:%02X:%02X:%02X:%02X\n",
                id, mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
}

static void sendVibrate(int id, int strength, int durationMs) {
  if (id < 0 || id >= WAND_COUNT) {
    Serial.printf("#ERR 완드 번호 %d\n", id);
    return;
  }

  // 아직 한 번도 입력을 올려보낸 적 없는 완드다. 보낼 주소가 없다.
  if (!wandKnown[id]) {
    Serial.printf("#ERR 완드 %d 미등록\n", id);
    return;
  }

  strength   = constrain(strength,   0, 255);
  durationMs = constrain(durationMs, 0, 65535);

  Cmd c;
  c.magic     = MAGIC_CMD;
  c.player_id = (uint8_t)id;
  c.cmd       = CMD_VIBRATE;
  c.arg1      = (uint8_t)strength;
  c.arg2      = (uint8_t)(durationMs & 0xFF);
  c.arg3      = (uint8_t)(durationMs >> 8);
  c.seq       = ++cmdSeq;

  esp_err_t r = esp_now_send(wandMac[id], (uint8_t*)&c, sizeof(c));

  if (r != ESP_OK) {
    Serial.printf("#ERR 완드 %d 진동 전송 실패 %d\n", id, (int)r);
  }
}

// 내 MAC 을 밝힌다. 부팅 때 한 번, 그리고 물어보면 언제든.
// 완드는 이 값을 구워야 하고, 유니티는 이 값으로 동글을 알아본다.
static void printMac() {
  // ⚠ WiFi.macAddress() 를 쓰면 안 된다. arduino-esp32 3.x 에서는 STA netif 가
  //    올라오기 전에 부르면 00:00:00:00:00:00 을 돌려준다 (실기 확인).
  //    eFuse 에서 직접 읽으면 Wi-Fi 상태와 무관하게 맞는 값이 나온다.
  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  Serial.printf("#MAC %02X:%02X:%02X:%02X:%02X:%02X ch=%d\n",
                mac[0], mac[1], mac[2], mac[3], mac[4], mac[5], nowCh);
}

// ── 유니티 -> 동글 ─────────────────────────────────────────

static void handleLine(const char* s) {
  int id, strength, ms;

  // 유니티가 어느 COM 포트가 동글인지 찾을 때 쓴다. 완드가 꺼져 있으면
  // CSV 가 한 줄도 안 나오므로, 물어보면 그때 신원을 밝힌다.
  if (s[0] == '?' && s[1] == '\0') { printMac(); return; }

  if (sscanf(s, "V,%d,%d,%d", &id, &strength, &ms) == 3) {
    sendVibrate(id, strength, ms);
    return;
  }

  Serial.printf("#ERR 모르는 명령 %s\n", s);
}

static void readSerial() {
  static char    line[64];
  static uint8_t len = 0;

  while (Serial.available()) {
    char c = (char)Serial.read();

    if (c == '\n' || c == '\r') {
      if (len > 0) {
        line[len] = '\0';
        handleLine(line);
        len = 0;
      }
      continue;
    }

    if (len < sizeof(line) - 1) {
      line[len++] = c;
    } else {
      len = 0;   // 개행이 영영 안 온다. 통째로 버린다
    }
  }
}

// ── 완드 -> 유니티 ─────────────────────────────────────────

static void handlePacket(const RawPacket& p) {
  if (p.len == sizeof(WandInput) && p.data[0] == MAGIC_INPUT) {
    WandInput in;
    memcpy(&in, p.data, sizeof(in));

    if (in.player_id >= WAND_COUNT) { return; }

    learnWand(in.player_id, p.mac);

    Serial.printf("%d,%d,%d,%d,%d,%d,%d,%d,%d,%d\n",
                  in.player_id, in.x, in.y, in.buttons, in.tilt, in.rot,
                  in.mcount, in.mtype, in.mstrength, in.ms);
    return;
  }

  if (p.len == sizeof(Echo) && p.data[0] == MAGIC_ECHO) {
    Echo e;
    memcpy(&e, p.data, sizeof(e));
    Serial.printf("#ECHO 완드 %d seq %d\n", e.player_id, e.seq);
    return;
  }

  Serial.printf("#ERR 모르는 패킷 magic %02X len %d\n", p.data[0], p.len);
}

// ── 메인 ───────────────────────────────────────────────────

void setup() {
  Serial.begin(115200);

#if ARDUINO_USB_CDC_ON_BOOT
  // 유니티가 잠깐 안 읽어가도 펌웨어가 멈추면 안 된다. 기본값은 최대 100ms 를 기다린다.
  Serial.setTxTimeoutMs(0);
#endif

  delay(400);

  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);
  if (memcmp(mac, DONGLE_B, 6) == 0) { nowCh = CH_B; }

  WiFi.mode(WIFI_STA);
  WiFi.disconnect();
  WiFi.setSleep(false);   // 안 끄면 완드가 보낸 것을 씹는다
  esp_wifi_set_channel(nowCh, WIFI_SECOND_CHAN_NONE);

  if (esp_now_init() != ESP_OK) {
    Serial.println("#ERR esp_now_init 실패");
    return;
  }

  esp_now_register_recv_cb(onRecv);
  esp_now_register_send_cb(onSent);

  printMac();
  Serial.println("#READY 완드가 입력을 보내면 자동으로 등록된다");
}

void loop() {
  readSerial();

  if (sendFailed) {
    sendFailed = false;
    Serial.printf("#ERR 완드 %d 명령 미도달 (꺼졌거나 범위 밖)\n", sendFailId);
  }

  while (rxTail != rxHead) {
    RawPacket p = rxRing[rxTail];
    rxTail = (rxTail + 1) % RX_SLOTS;
    handlePacket(p);
  }
}
