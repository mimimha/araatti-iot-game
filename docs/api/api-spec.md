# 아라아띠 API 명세

`server/AraAtti.Api` (ASP.NET Core, net8.0) 의 HTTP API 명세입니다.
코드 기준: `server/AraAtti.Api/Endpoints/*.cs` · `Contracts/*.cs` · `Program.cs`

관련 문서

- `server/README.md` — 서버 실행 방법 (MySQL · .env · Jwt 키)
- `docs/prd/auth-character-roadmap.md` — 계정 · 캐릭터 설계
- `docs/prd/lobby_altar_inventory_system_design.md` — 제단 · 인벤토리 설계 (8.6 · 9.2 · 10장)

> 명세와 코드가 다르면 **코드가 맞습니다.** 엔드포인트를 고치면 이 문서도 같이 고쳐 주세요.
> 로컬에서는 Swagger(`http://localhost:5080/swagger`)로 바로 호출해 볼 수 있습니다.

---

## 1. 공통

| 항목 | 값 |
| --- | --- |
| 기본 주소 | `http://localhost:5080` (Unity 는 `HttpApiConfig.DefaultBaseUrl`) |
| 본문 형식 | `application/json; charset=utf-8` |
| 필드 이름 | camelCase (`accessToken`, `myFragments` …) |
| 시간 | UTC, ISO 8601 (`2026-09-25T08:00:00Z`) |
| 인증 | `Authorization: Bearer {accessToken}` |
| 토큰 수명 | 120분 (`Jwt:ExpiresMinutes`). 만료 판정 여유 30초 |

### 1.1 인증

🔒 표시가 붙은 API 는 토큰이 필요합니다. 토큰이 없거나 만료되었거나 위조되었으면 **401** 입니다.

```json
{ "code": "TOKEN_INVALID", "message": "로그인이 필요합니다. 토큰이 없거나 만료되었습니다." }
```

⚠ **요청 본문으로 userId 를 받지 않습니다.** 주인은 언제나 토큰의 `sub` 입니다.
본문으로 받으면 남의 계정에 캐릭터를 만들거나 남의 조각을 쓸 수 있습니다.

### 1.2 오류 응답

모든 오류는 같은 모양입니다. (제단 봉헌 409 만 예외 — 5.2절)

```json
{ "code": "VALIDATION_FAILED", "message": "비밀번호는 8자 이상이어야 합니다." }
```

| 필드 | 뜻 |
| --- | --- |
| `code` | 클라이언트가 분기에 쓰는 값. 대문자 스네이크 |
| `message` | 사용자에게 그대로 보여 줄 수 있는 한국어 문구 |

### 1.3 오류 코드 전체

| code | HTTP | 어디서 |
| --- | --- | --- |
| `TOKEN_INVALID` | 401 | 🔒 API 전부 |
| `VALIDATION_FAILED` | 400 | 회원가입 · 로그인 · 캐릭터 생성 |
| `EMAIL_ALREADY_USED` | 409 | 회원가입 |
| `INVALID_CREDENTIALS` | 401 | 로그인 |
| `CHARACTER_LIMIT_REACHED` | 409 | 캐릭터 생성 |
| `NICKNAME_ALREADY_USED` | 409 | 캐릭터 생성 |
| `AMOUNT_INVALID` | 400 | 봉헌 |
| `AMOUNT_TOO_LARGE` | 400 | 봉헌 |
| `REQUEST_ID_INVALID` | 400 | 봉헌 |
| `OFFERING_CLOSED` | 409 | 봉헌 |
| `OFFERING_AMOUNT_CHANGED` | 409 | 봉헌 |
| `NOT_ENOUGH_FRAGMENTS` | 409 | 봉헌 |

---

## 2. 엔드포인트 목록

| 메서드 | 경로 | 인증 | 설명 |
| --- | --- | --- | --- |
| GET | `/api/health` | | 서버 · DB 상태 (`/health` 도 같다) |
| POST | `/api/auth/signup` | | 회원가입 |
| POST | `/api/auth/login` | | 로그인 → 토큰 발급 |
| GET | `/api/auth/me` | 🔒 | 내 계정 |
| GET | `/api/characters` | 🔒 | 내 캐릭터 목록 |
| POST | `/api/characters` | 🔒 | 캐릭터 생성 |
| GET | `/api/inventory` | 🔒 | 내 인벤토리 |
| GET | `/api/altar/state` | 🔒 | 제단 상태 + 내 조각 수 |
| POST | `/api/altar/offer` | 🔒 | 조각 봉헌 |

---

## 3. 상태 확인

### GET `/api/health`

MySQL 에 붙을 수 있는지 봅니다. 인증이 필요 없습니다.

**200** — 정상

```json
{
  "status": "ok",
  "database": "connected",
  "message": null,
  "serverTimeUtc": "2026-09-25T08:00:00Z"
}
```

**503** — DB 에 붙지 못함. 같은 모양에 `status: "degraded"`, `database: "disconnected"`,
`message` 에 이유가 들어갑니다.

---

## 4. 계정

### POST `/api/auth/signup`

**요청**

```json
{ "email": "user@example.com", "password": "password123" }
```

| 필드 | 규칙 |
| --- | --- |
| `email` | 필수. 앞뒤 공백 제거 · 소문자로 저장. 190자 이하. 이메일 형식 |
| `password` | 필수. 8자 이상 |

**201** — `UserResponse`

```json
{ "id": 1, "email": "user@example.com", "createdAt": "2026-09-25T08:00:00Z" }
```

| HTTP | code | 언제 |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | 규칙 위반. `message` 가 어느 규칙인지 알려 준다 |
| 409 | `EMAIL_ALREADY_USED` | 이미 가입된 이메일 |

### POST `/api/auth/login`

**요청**

```json
{ "email": "user@example.com", "password": "password123" }
```

**200** — `LoginResponse`

```json
{
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 7200,
  "user": { "id": 1, "email": "user@example.com", "createdAt": "2026-09-25T08:00:00Z" }
}
```

| 필드 | 뜻 |
| --- | --- |
| `accessToken` | 이후 요청의 `Authorization: Bearer {accessToken}` |
| `expiresIn` | 만료까지 남은 시간(초) |

| HTTP | code | 언제 |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | 이메일 · 비밀번호 중 하나가 비어 있음 |
| 401 | `INVALID_CREDENTIALS` | 이메일이 없거나 비밀번호가 틀림. **둘을 구분하지 않는다** |

### GET `/api/auth/me` 🔒

**200** — `UserResponse` (회원가입 응답과 같은 모양)

| HTTP | code | 언제 |
| --- | --- | --- |
| 401 | `TOKEN_INVALID` | 토큰 문제, 또는 토큰의 계정이 지워짐 |

---

## 5. 캐릭터

계정당 캐릭터는 **1개**입니다 (`MaxCharactersPerUser = 1`).
그래도 목록은 언제나 **배열**입니다. 다중 캐릭터를 붙일 때 모양을 바꾸지 않으려고요.

### GET `/api/characters` 🔒

**200** — 캐릭터가 없으면 404 가 아니라 **빈 배열**입니다.

```json
{
  "characters": [
    {
      "id": 1,
      "nickname": "바다탐험가",
      "skinColor": "#F2C9A0",
      "slotIndex": 0,
      "parts": [
        { "slot": "Hair", "prefabName": "Hair_01" },
        { "slot": "Top", "prefabName": "Top_03" }
      ],
      "createdAt": "2026-09-25T08:00:00Z",
      "updatedAt": "2026-09-25T08:00:00Z"
    }
  ]
}
```

### POST `/api/characters` 🔒

**요청**

```json
{
  "nickname": "바다탐험가",
  "skinColor": "#F2C9A0",
  "parts": [
    { "slot": "Hair", "prefabName": "Hair_01" },
    { "slot": "Top", "prefabName": "Top_03" }
  ]
}
```

| 필드 | 규칙 |
| --- | --- |
| `nickname` | 필수. 2~10자. 한글 · 영문 · 숫자만 (공백 · 특수문자 불가). 전체에서 중복 불가 |
| `skinColor` | 필수. `#RRGGBB` |
| `parts` | 필수. 입힌 파츠가 없으면 **빈 배열** `[]` 을 보낸다 |
| `parts[].slot` | `Face` · `Hair` · `Shoes` · `Top` · `Bottom` · `Accessory` 중 하나 (대소문자 무시). 같은 slot 두 번 불가 |
| `parts[].prefabName` | 필수. 64자 이하. 서버는 프리팹 목록을 모르고 검사하지 않는다 |

필드 이름은 Unity 의 `CharacterPartSnapshot` 과 같아서, 저장해 둔 스냅샷을 그대로 보낼 수 있습니다.

**201** — `CharacterResponse` (목록의 항목 하나와 같은 모양)

| HTTP | code | 언제 |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | 규칙 위반. `message` 가 어느 규칙인지 알려 준다 |
| 409 | `CHARACTER_LIMIT_REACHED` | 이미 캐릭터가 있음 |
| 409 | `NICKNAME_ALREADY_USED` | 닉네임 중복 |

---

## 6. 인벤토리

### GET `/api/inventory` 🔒

토큰 주인이 가진 아이템입니다. 가진 것이 없으면 404 가 아니라 **빈 배열**입니다.
수량 0 짜리 가짜 항목을 만들어 넣지 않습니다.

**200**

```json
{
  "items": [
    { "itemId": "sea_heart_fragment", "displayName": "바다의 심장 조각", "quantity": 12 }
  ]
}
```

| 필드 | 뜻 |
| --- | --- |
| `itemId` | DB 의 `item_id`. Unity `ItemIds` 상수와 같다 |
| `displayName` | 화면용 한국어 이름. DB 에 없고 서버가 붙인다. 모르는 아이템이면 `itemId` 그대로 |
| `quantity` | 보유 수량. 음수 없음 |

순서는 언제나 `itemId` 오름차순입니다.

| itemId | 이름 | 쓰는 곳 |
| --- | --- | --- |
| `sea_heart_fragment` | 바다의 심장 조각 | 제단 봉헌 · 로비 오른쪽 위 조각 수량 HUD |

---

## 7. 제단

제단은 **서버 전체에 하나**입니다 (`altar_state.id = 1`). 모두의 봉헌이 한 곳에 쌓입니다.

파생값(`remainingToTarget` · `maxOfferAmount` · `altarActivated` · `recoveryPercent`)은
**서버가 계산해서 내려 줍니다.** 클라이언트가 같은 공식을 또 쓰면 기획이 바뀔 때 한쪽만 고쳐집니다.

| 값 | 공식 |
| --- | --- |
| `remainingToTarget` | `max(0, targetOffering - totalOffered)` |
| `maxOfferAmount` | `min(myFragments, remainingToTarget)` |
| `altarActivated` | `totalOffered >= targetOffering` |
| `recoveryPercent` | `min(totalOffered / targetOffering, 1) * 100` — 100 을 넘지 않는다 |

### GET `/api/altar/state` 🔒

**200** — `AltarStateResponse`

```json
{
  "totalOffered": 680,
  "targetOffering": 1000,
  "remainingToTarget": 320,
  "myFragments": 12,
  "maxOfferAmount": 12,
  "altarActivated": false,
  "recoveryPercent": 68.0,
  "myOfferedTotal": 40,
  "updatedAt": "2026-09-25T08:00:00Z"
}
```

| 필드 | 뜻 |
| --- | --- |
| `totalOffered` | 모두가 지금까지 봉헌한 총량 |
| `targetOffering` | 목표량. 코드에 1000 을 박지 않고 DB 값을 쓴다 |
| `myFragments` | 내 조각 보유량. 인벤토리 행이 없으면 0 |
| `myOfferedTotal` | **내가** 지금까지 봉헌한 합계 |
| `updatedAt` | 제단 상태가 마지막으로 바뀐 시각 |

⚠ 그 순간의 스냅샷입니다. 봉헌할 때 서버가 다시 검증합니다.

### POST `/api/altar/offer` 🔒

조각을 제단에 바칩니다. **부분 수락은 없습니다.** 요청한 수량이 전부 들어가거나 아무것도 바뀌지 않습니다.

**요청**

```json
{ "amount": 5, "requestId": "3f2b8c1e-7a4d-4e9b-9c1a-2d5e6f708192" }
```

| 필드 | 규칙 |
| --- | --- |
| `amount` | 필수. 1 이상, `int.MaxValue` 이하 |
| `requestId` | 필수. Guid 문자열. **요청마다 새로 만들고, 재시도할 때는 같은 값을 보낸다** |

#### 검증 순서

순서를 바꾸면 사용자가 받는 실패 이유가 달라집니다. (설계 8.6절)

| # | 검사 | 실패하면 |
| --- | --- | --- |
| 1 | 로그인했는가 | 401 `TOKEN_INVALID` |
| 2 | `amount > 0` | 400 `AMOUNT_INVALID` |
| 3 | `amount <= int.MaxValue` | 400 `AMOUNT_TOO_LARGE` |
| 4 | `requestId` 가 Guid 인가 | 400 `REQUEST_ID_INVALID` |
| 5 | 이미 처리한 `requestId` 인가 | 200 `duplicate: true` (다시 봉헌하지 않는다) |
| 6 | 제단 남은 칸 ≥ `amount` | 409 `OFFERING_CLOSED` 또는 `OFFERING_AMOUNT_CHANGED` |
| 7 | 내 보유량 ≥ `amount` | 409 `NOT_ENOUGH_FRAGMENTS` |

6 · 7 은 먼저 읽고 판단하지 않고 **조건부 UPDATE 한 트랜잭션** 안에서 처리합니다.
동시에 여러 명이 봉헌해도 `1001 / 1000` 같은 초과가 생기지 않습니다.

#### 200 — 성공 (`AltarOfferResponse`)

```json
{
  "success": true,
  "offeredAmount": 5,
  "remainingFragments": 7,
  "totalOffered": 685,
  "targetOffering": 1000,
  "remainingToTarget": 315,
  "maxOfferAmount": 7,
  "altarActivated": false,
  "recoveryPercent": 68.5,
  "duplicate": false
}
```

| 필드 | 뜻 |
| --- | --- |
| `offeredAmount` | 실제로 반영된 수량. 부분 수락이 없으므로 요청한 수량과 같다 |
| `remainingFragments` | 봉헌 뒤 내 보유량. **클라이언트는 빼기 계산 대신 이 값을 그대로 쓴다** |
| `duplicate` | 같은 `requestId` 를 다시 받았으면 `true`. 이때 `offeredAmount` 는 **처음 기록된 수량**이다 |

상태값(`totalOffered` 등)은 요청 당시가 아니라 **응답하는 지금** 값입니다.

#### 409 — 경쟁으로 실패 (`AltarOfferFailureResponse`)

⚠ 1.2절의 공통 오류 모양과 다릅니다. 실패에도 **최신 상태를 전부 싣습니다.**
실패 직후 `GET /api/altar/state` 를 다시 부르지 않아도 화면을 맞출 수 있게 하려는 것입니다.

```json
{
  "success": false,
  "code": "OFFERING_AMOUNT_CHANGED",
  "message": "다른 플레이어가 먼저 등록했습니다.",
  "remainingFragments": 12,
  "totalOffered": 997,
  "targetOffering": 1000,
  "remainingToTarget": 3,
  "maxOfferAmount": 3,
  "altarActivated": false,
  "recoveryPercent": 99.7
}
```

| code | 언제 | 사용자가 할 일 |
| --- | --- | --- |
| `OFFERING_CLOSED` | 제단이 이미 다 찼다 (`remainingToTarget = 0`) | 끝. 더 봉헌할 수 없다 |
| `OFFERING_AMOUNT_CHANGED` | 남은 칸이 요청 수량보다 적다 | 수량을 `maxOfferAmount` 이하로 낮춰 다시 |
| `NOT_ENOUGH_FRAGMENTS` | 보유량이 요청 수량보다 적다 | 수량을 낮춰 다시 |

"N개 남았습니다" 처럼 수량이 들어간 문구는 서버가 만들지 않습니다. 클라이언트가 `remainingToTarget` 으로 조합합니다.

#### 400 — 입력 오류

상태를 읽기 전에 걸러지므로 1.2절의 공통 모양(`code` · `message`)입니다.

---

## 8. Unity 에서 부르는 곳

| API | Unity | 비고 |
| --- | --- | --- |
| `/api/auth/*` | `HttpAuthService` | 로그인 토큰을 들고 있다 |
| `/api/characters` | `HttpCharacterService` | |
| `/api/inventory` | `HttpInventoryService` | 결과는 `PlayerInventory` 캐시로 |
| `/api/altar/*` | `HttpAltarService` | 결과는 `AltarState` · `PlayerInventory` 캐시로 |

- 실제 API 와 가짜 구현(Fake*Service)은 `AccountServiceBootstrap.Active` 한 줄로 바꿉니다.
- 조각 수는 세 응답이 같이 갱신합니다: `GET /api/inventory` 의 `quantity`,
  `GET /api/altar/state` 의 `myFragments`, `POST /api/altar/offer` 의 `remainingFragments` (성공 · 409 둘 다).
- 토큰이 없으면(로그인 전, `-devjoin` 개발 접속) Unity 쪽에서 요청을 보내지 않고 "로그인이 필요합니다" 로 끝냅니다.
