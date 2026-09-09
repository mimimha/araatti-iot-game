# PRD 00 — 계정 · 캐릭터 저장 도입 로드맵

| 항목 | 내용 |
| --- | --- |
| 문서 성격 | 상위 로드맵 (PRD 01~07 의 부모 문서) |
| 대상 | 아라아띠 Unity 클라이언트 + 신규 `server/` (ASP.NET Core Web API) |
| 작성 시점 브랜치 | `feature/auth-api` |
| 관련 문서 | `unity/UnderTheSea/GAME_STRUCTURE.md` (1 · 4 · 6 · 9 · 11장), `unity/UnderTheSea/CONVENTION.md`, `GIT_CONVENTION.md` |

이 문서는 **구현을 포함하지 않는다.** 현재 코드를 읽어 확인한 사실과, 앞으로 7단계로 나눈
작업 단위 · 완료 조건 · 테스트 방법 · 변경 금지 범위만 담는다.

---

## 0. 확정된 요구사항 (변경 시 이 문서를 먼저 고친다)

1. 백엔드는 **ASP.NET Core Web API + MySQL**.
2. 계정당 캐릭터는 **현재 1개만** 생성 가능.
3. 단, DB 스키마는 **`users : characters = 1:N`** 으로 설계한다.
   1개 제한은 **스키마가 아니라 애플리케이션 계층에서** 건다.
   → 나중에 `CharacterSelect` 씬과 다중 캐릭터를 붙일 때 **마이그레이션이 필요 없어야 한다.**
4. 로그인 성공 후 **캐릭터 0개 → `CharacterCreate`**, **1개 → `ChannelSelect`**.
5. 커스터마이징 항목은 **배열 인덱스가 아니라 프리팹의 안정적인 이름 문자열**로 저장한다.
6. 인증은 **JWT**.
7. Unity 의 서버 통신은 **인터페이스 기반 서비스**로 분리한다.
   현재 Fake 구현 → 실제 HTTP 구현으로 **컴포넌트 교체만으로** 바뀌어야 한다.

---

## 1. 현재 구현 상태와 실제 미구현 부분

### 1-1. 씬 흐름 — 구현되어 있음 (단, 판단 근거가 로컬)

`Assets/Game/Scripts/Core/SceneFlow.cs` 가 전체 화면 순서를 단독으로 관리한다.
씬 이름 상수(`Boot`/`Title`/`Login`/`CharacterCreate`/`ChannelSelect`/`Lobby`)와 전환 메서드가 모두 있고,
실제 씬 파일도 `Assets/Game/Scenes/Main/CoreGames/` 에 6개 모두 존재한다.

분기 지점은 딱 한 줄이다.

```csharp
// SceneFlow.cs
public static void FromLogin() => Load(HasCharacter ? ChannelSelect : CharacterCreate);

public static string Nickname => PlayerPrefs.GetString(NicknameKey, string.Empty);   // "PlayerNickname"
public static bool   HasCharacter => !string.IsNullOrWhiteSpace(Nickname);
```

즉 **분기 구조 자체는 이미 요구사항 4번과 같은 모양**이다.
다만 판단 근거가 "서버에 있는 캐릭터 개수" 가 아니라 **"이 PC 의 PlayerPrefs 에 이름이 있는지"** 다.
→ 바꿔야 하는 것은 흐름이 아니라 **근거 하나**다. 이것이 이 로드맵을 작게 쪼갤 수 있는 이유다.

### 1-2. 로그인 화면 — UI 만 있고 인증이 전혀 없음

`Assets/Game/Scripts/UI/LoginScreenController.cs`

- 로그인/회원가입 **탭 전환은 겉모습만** 바뀐다. (`_isLoginMode` 로 버튼 문구만 교체)
- `Submit()` 은 이메일을 `Debug.Log` 로 찍고 **바로 `SceneFlow.FromLogin()`** 을 호출한다.
- 비밀번호는 `TMP_InputField.ContentType.Password` 로 가려지기만 하고 **아무 곳에도 전달되지 않는다.**
- **빈 입력도 통과한다.** 검증 · 요청 · 실패 표시 · 로딩 표시가 전부 없다.
- 실패 안내를 띄울 라벨 필드조차 없다. (`emailField`, `passwordField`, `submitButton`, `backButton` 만 존재)

미구현: 입력 검증, 서버 요청, 대기 중 버튼 잠금, 실패 메시지 표출, 토큰 보관.

### 1-3. 캐릭터 생성 화면 — 외형이 **저장되지 않는다** (가장 큰 공백)

`Assets/Game/Scripts/Character/CharacterCustomizationController.cs` (877줄, `partial`)
\+ `CharacterCustomizationCatalogue.cs` + `CharacterCustomizationScroll.cs`

저장 코드는 프로젝트 전체에서 아래 두 줄뿐이다.

```csharp
// CharacterCustomizationController.CompleteCustomization()
PlayerPrefs.SetString("PlayerNickname", nickname);
PlayerPrefs.Save();
...
Completed?.Invoke(nickname);     // 씬 전환은 CharacterCreateFlow 가 받아서 처리
```

확인된 사실:

- **닉네임만 저장된다.** `GAME_STRUCTURE.md` 6장이 예고한 `Char.*` 외형 키는 **한 개도 기록되지 않는다.**
  (`Character/` 폴더 내 `PlayerPrefs` 사용처는 위 한 곳뿐)
- 그래서 씬을 다시 열면 매번 `ApplyCuteDefaultCharacter()` 의 **하드코딩된 기본 조합**으로 되돌아간다.
  (`"Male_Emotion_Usual_01"`, `hair[12]`, `shoes[7]`, `"Costume_14_01/02/03"`)
- 현재 선택 상태는 **런타임 메모리에만** 있다.
  - `CharacterCustomizationScroll.cs`: `Dictionary<Category, int> selectedOptions` — **`partPrefabs` 배열의 인덱스**
  - `CharacterCustomizationCatalogue.cs`: `Dictionary<WearSlot, CatalogPart> equippedParts` — **`prefab` 참조를 들고 있음**
- 스킨 색은 `skinColors` (`Color[]`) 의 **인덱스**로만 다룬다. (`ApplySkinColor(int)`, 기본값 `defaultSkinColorIndex = 1`)
- 닉네임 검증은 이미 있다: 2~10자, 한글 · 영문 · 숫자만 (`GetNicknameValidationMessage`).
  **서버 검증 규칙을 이 규칙에 맞추면 된다.**
- 씬 전환 코드가 이 파일에 없다는 규칙(6장)은 잘 지켜져 있다. `CharacterCreateFlow` 가 `Completed` 를 받아 넘긴다.

미구현: 외형 스냅샷 생성, 저장, 복원. 그리고 **인덱스 → 이름 문자열 전환** (요구사항 5).

> ⚠ 인덱스 저장이 왜 위험한가: `selectedOptions` 의 값은 Inspector 의 `partPrefabs` 배열 순서다.
> 서연이 에셋을 정렬하거나 파츠 하나를 중간에 추가하면 **저장된 모든 캐릭터의 외형이 밀린다.**
> 프리팹 이름(`Costume_14_01`, `Female_Emotion_Usual_01`)은 에셋 팩 안에서 고유하고 순서와 무관하다.

### 1-4. 네트워크 경계 — 실시간용으로 이미 확정되어 있음

`Assets/Game/Scripts/Network/INetworkService.cs` / `FakeNetworkService.cs` / `NetworkServiceLocator.cs`

- `INetworkService` 는 **채널 목록 · 로비 접속 · 미니게임 대기열 · 결과 보고** 전용이다.
  `Connect(string nickname, string serverId)` 처럼 **닉네임 문자열만** 받는다.
- `FakeNetworkService` 는 `MonoBehaviour` + `DontDestroyOnLoad`, `Awake()` 에서 `NetworkServiceLocator.Register(this)` 를 호출한다.
  코루틴으로 지연을 흉내내고 `alwaysFailConnect` 로 실패 화면도 테스트할 수 있다.
- `NetworkServiceLocator` 는 `static Current` + `IsReady` 만 가진 얇은 정적 홀더다.
- `ChannelSelectController` 는 `IsReady` 가 false 면 **예시 채널 목록으로 단독 실행**을 지원한다.

**이 교체 패턴(인터페이스 + Fake `MonoBehaviour` + 정적 Locator)이 요구사항 7의 정답 그대로다.
계정 / 캐릭터 서비스는 이 패턴을 복사해서 만든다.**

> ❗ 중요한 판단: **계정 / 캐릭터 API 를 `INetworkService` 안에 넣지 않는다.**
> 이유 3가지 —
> ① `GAME_STRUCTURE.md` 4장이 이 인터페이스의 모양을 **클라이언트↔서버 합의사항으로 고정**했다. 혼자 못 바꾼다.
> ② 성격이 다르다. `INetworkService` 는 Photon Fusion 기반 **실시간 세션**, 계정 / 캐릭터는 **단발성 REST 요청 / 응답**.
> ③ Fake 를 각각 독립적으로 켜고 끌 수 있어야 한다. (서버 인증은 붙었지만 채널은 아직 Fake 인 중간 상태가 실제로 생긴다)
> → `IAuthService`, `ICharacterService` 를 **별도로** 신설한다.

### 1-5. 서버 — 아직 존재하지 않음

- 저장소 루트에 **`server/` 디렉터리가 없다.** (`GIT_CONVENTION.md` 는 `unity/`, `iot/`, `server/` 를 전제한다)
- 현재 브랜치 `feature/auth-api` 는 `develop` 과 **코드 차이가 없다.** (빈 브랜치)
- ASP.NET Core 프로젝트, MySQL 스키마, 마이그레이션, JWT 설정 **전부 미구현.**

### 1-6. 문서와 코드의 불일치 — PRD 02 에서 정정 필요

`GAME_STRUCTURE.md` 11장 "이번 프로젝트에서 하지 않는 것" 이 아래를 **명시적으로 범위 제외**로 적어 두었다.

| 11장의 현재 서술 | 이 로드맵 |
| --- | --- |
| 로그인 / 회원가입 — "화면만 있고 인증은 없습니다. 계정 개념이 없습니다" | **도입한다** (PRD 04) |
| 캐릭터를 서버에 저장 — "없습니다. PlayerPrefs 로 그 PC 에만" | **도입한다** (PRD 05) |

문서가 팀 합의사항이므로 **코드보다 문서를 먼저 고친다.**
11장 표 수정 + 4장에 새 경계(`IAuthService`/`ICharacterService`) 추가 + 9장 폴더 표 갱신을
**PRD 02 의 산출물에 포함**시킨다. (합의 없이 11장을 위반하는 코드를 먼저 넣지 않는다)

### 1-7. 환경 제약 (구현 전 알아둘 것)

| 항목 | 확인된 값 | 영향 |
| --- | --- | --- |
| Unity 버전 | `6000.5.9f1` | `Awaitable` 사용 가능. 단 기존 코드는 코루틴 + `event` 스타일 |
| JSON 라이브러리 | **Newtonsoft 없음** (`manifest.json` 및 `Assets/` 전체 미검출) | `JsonUtility` 사용. `Dictionary` 직렬화 불가 → **DTO 는 배열 필드로 설계해야 한다** |
| HTTP | `com.unity.modules.unitywebrequest` 있음 | `UnityWebRequest` 로 충분. 외부 패키지 추가 불필요 |
| asmdef | `Assets/Game/` 에 없음 (외부 에셋에만 존재) | 새 폴더도 asmdef 없이 `Assembly-CSharp` 에 들어간다. 추가하지 않는다 |

---

## 2. 데이터 모델 초안

MySQL 8.x / `utf8mb4` / `InnoDB` 기준. EF Core 마이그레이션으로 생성한다.

### 2-1. `users`

| 컬럼 | 타입 | 제약 | 비고 |
| --- | --- | --- | --- |
| `id` | `BIGINT UNSIGNED` | PK, AUTO_INCREMENT | |
| `email` | `VARCHAR(190)` | NOT NULL, UNIQUE | 190 = `utf8mb4` 인덱스 길이 한계 대응 |
| `password_hash` | `VARCHAR(255)` | NOT NULL | BCrypt. **평문 · 단순 SHA 금지** |
| `created_at` | `DATETIME(6)` | NOT NULL | UTC 저장 |
| `last_login_at` | `DATETIME(6)` | NULL | |

### 2-2. `characters` — 1:N 의 핵심

| 컬럼 | 타입 | 제약 | 비고 |
| --- | --- | --- | --- |
| `id` | `BIGINT UNSIGNED` | PK, AUTO_INCREMENT | |
| `user_id` | `BIGINT UNSIGNED` | NOT NULL, FK → `users(id)` ON DELETE CASCADE | **1:N 의 N 쪽** |
| `name` | `VARCHAR(10)` | NOT NULL, UNIQUE | Unity 검증(2~10자)과 동일 |
| `skin_color` | `CHAR(7)` | NOT NULL | `"#RRGGBB"`. **인덱스 저장 안 함** |
| `slot_index` | `TINYINT UNSIGNED` | NOT NULL, DEFAULT 0 | 미래 다중 캐릭터의 **표시 순서** |
| `created_at` | `DATETIME(6)` | NOT NULL | |
| `updated_at` | `DATETIME(6)` | NOT NULL | |

인덱스

```sql
INDEX      idx_characters_user_id     (user_id)
UNIQUE KEY uk_characters_name         (name)
UNIQUE KEY uk_characters_user_slot    (user_id, slot_index)
```

> **"계정당 1개" 를 스키마에 넣지 않는 이유** — `user_id` 에 UNIQUE 를 걸면 지금은 편하지만
> 다중 캐릭터를 붙일 때 **인덱스를 드롭하는 마이그레이션**이 필요하다. 요구사항 3번 위반이다.
> 대신 서비스 계층에서 `count >= MaxCharactersPerUser (=1)` 이면 `409` 를 돌려준다.
> 미래에는 **상수 하나를 3으로 바꾸는 것**이 전부다.

### 2-3. `character_parts`

| 컬럼 | 타입 | 제약 | 비고 |
| --- | --- | --- | --- |
| `id` | `BIGINT UNSIGNED` | PK, AUTO_INCREMENT | |
| `character_id` | `BIGINT UNSIGNED` | NOT NULL, FK → `characters(id)` ON DELETE CASCADE | |
| `slot` | `VARCHAR(24)` | NOT NULL | `"Face"`, `"Hair"`, `"Top"`, `"Bottom"`, `"Shoes"`, `"Accessory"` |
| `prefab_name` | `VARCHAR(64)` | NOT NULL | **프리팹 이름 문자열** (`"Costume_14_01"`) |

```sql
UNIQUE KEY uk_parts_character_slot (character_id, slot)
```

`slot` 값은 Unity 캐릭터 생성 화면의 **카테고리 이름**을 그대로 쓴다. PRD 01 구현 기준으로 6개다.

```text
Face  Hair  Shoes  Top  Bottom  Accessory
```

(`CharacterCustomizationPersistence.PersistedCategories` 와 같다.
초안에는 `WearSlot` enum 이름이라고 적혀 있었으나, 실제 구현은 카테고리 이름을 쓴다.)

컬럼이 문자열이라 **카테고리가 늘어나도 DB 는 그대로다.** 서버에서는
`CharacterEndpoints.AllowedSlots` 배열에 한 줄만 추가하면 된다.

`prefab_name` 예시 (실제 에셋에서 확인)

```text
Face      Female_Emotion_Usual_01 / Male_Emotion_Usual_01
Top       Costume_14_01
Accessory Costume_14_02
Bottom    Costume_14_03
Body      Body_01 ... Body_16
```

> **파츠를 `characters` 의 JSON 컬럼으로 하지 않는 이유**
> ① 슬롯별 조회 / 집계가 가능해진다 (어떤 옷이 인기인가). ② `prefab_name` 오타를 UNIQUE · FK 로 잡을 수 있다.
> ③ JSON 컬럼은 `JsonUtility` 와 스키마 계약이 이중으로 생겨 검증 지점이 흐려진다.
> 단점(조인 1회)은 이 규모에서 무의미하다.

### 2-4. 범위 밖 (지금 만들지 않음, 자리만 확인)

- `refresh_tokens` — PRD 04 는 **액세스 토큰 단독**(만료 2시간)으로 간다. 7주 프로젝트에 재발급 회전은 과하다.
- `character_stats`, `inventory` 등 — 미니게임 규격 확정 후 별도 PRD.

---

## 3. API 초안

기본 경로 `/api`. 요청 · 응답 본문은 `application/json`, 프로퍼티는 `camelCase`.

### 3-1. 엔드포인트

| 메서드 | 경로 | 인증 | 용도 |
| --- | --- | --- | --- |
| `GET` | `/api/health` | — | 헬스 체크 (PRD 03) |
| `POST` | `/api/auth/signup` | — | 회원가입 (PRD 04) |
| `POST` | `/api/auth/login` | — | 로그인 (PRD 04) |
| `GET` | `/api/auth/me` | Bearer | 토큰 확인용 내 정보 (PRD 04) |
| `GET` | `/api/characters` | Bearer | **내 캐릭터 목록** (PRD 05) |
| `POST` | `/api/characters` | Bearer | 캐릭터 생성 (PRD 05) |
| `GET` | `/api/characters/{id}` | Bearer | **범위 밖.** 목록으로 충분해 만들지 않았다 |
| `DELETE` | `/api/characters/{id}` | Bearer | **범위 밖.** 다중 캐릭터 때 |

### 3-2. 인증

```jsonc
// 요청 본문은 둘 다 같다
// POST /api/auth/signup  ·  POST /api/auth/login
{ "email": "a@b.com", "password": "secret123" }

// POST /api/auth/signup → 201
// ★ 가입은 토큰을 주지 않는다. 계정만 만든다.
{ "id": 1, "email": "a@b.com", "createdAt": "2026-09-09T02:19:45.881406Z" }

// POST /api/auth/login → 200
{
  "accessToken": "eyJhbGciOi...",
  "expiresIn": 7200,
  "user": { "id": 1, "email": "a@b.com", "createdAt": "2026-09-09T02:19:45.881406Z" }
}
```

> **가입 응답에 토큰을 넣지 않는 이유** — "계정을 만드는 일" 과 "인증하는 일" 을 갈라 두면
> 토큰 발급 경로가 로그인 한 곳뿐이라 흐름이 단순해진다.
> 대신 Unity 는 **회원가입 성공 직후 로그인을 한 번 더 호출**해야 한다. (PRD 06 에서 반영할 것)

- JWT 클레임: `sub` = `users.id`, `email`, `exp`. 서명 `HS256`.
- **응답에 캐릭터 정보를 넣지 않는다.** 0/1/N 분기를 `GET /api/characters` **한 곳에서만** 판단하기 위함이다.
  로그인 응답에 캐릭터를 끼워 넣으면 다중 캐릭터 시 분기 근거가 두 곳으로 갈라진다.

### 3-3. 캐릭터

```jsonc
// GET /api/characters  → 200
// ★ 1개 제한이어도 반드시 배열. CharacterSelect 추가 시 API 변경 0 을 위해서다.
{
  "characters": [
    {
      "id": 10,
      "nickname": "선원김",
      "skinColor": "#F2C9A0",
      "slotIndex": 0,
      "createdAt": "2026-09-09T02:34:59.9918268Z",
      "updatedAt": "2026-09-09T02:34:59.9918268Z",
      "parts": [
        { "slot": "Face",   "prefabName": "Male_Emotion_Usual_01" },
        { "slot": "Top",    "prefabName": "Costume_14_01" },
        { "slot": "Bottom", "prefabName": "Costume_14_03" }
      ]
    }
  ]
}
// 캐릭터가 없으면 → { "characters": [] }   (404 아님)
```

```jsonc
// POST /api/characters  → 201 (응답 본문은 위 character 객체 하나)
{
  "nickname": "선원김",
  "skinColor": "#F2C9A0",
  "parts": [ { "slot": "Face", "prefabName": "Male_Emotion_Usual_01" } ]
}
```

> **`nickname` 인 이유** — DB 컬럼과 엔티티는 `name` 이지만, HTTP 응답/요청은 Unity 의
> 저장 형식(`CharacterAppearanceSnapshot.nickname`)에 맞춘다. 그래야 Unity 가 이름을
> 바꿔 담는 변환 코드 없이 스냅샷을 그대로 주고받는다.
>
> **`userId` 를 본문으로 받지 않는다.** 주인은 언제나 JWT 의 `sub` 다.
> 본문으로 받으면 남의 계정에 캐릭터를 만들 수 있다.

### 3-4. 서버 검증 규칙 (Unity 검증과 일치시킬 것)

| 대상 | 규칙 | 근거 |
| --- | --- | --- |
| `email` | 형식 유효, 190자 이하, 중복 불가 | `users.email` UNIQUE |
| `password` | 8자 이상 | 서버 단독 결정. Unity 안내 문구도 같이 맞춘다 |
| `nickname` | 2~10자, `^[가-힣ㄱ-ㅎㅏ-ㅣA-Za-z0-9]+$`, 전역 중복 불가 | **`GetNicknameValidationMessage` 와 동일 정규식** |
| `skinColor` | `^#[0-9A-Fa-f]{6}$` | |
| `parts[].slot` | `Face` `Hair` `Shoes` `Top` `Bottom` `Accessory` 중 하나 (대소문자 무시) | 2-3 절 |
| `parts` | `slot` 중복 없음. 빈 배열 허용 | `uk_parts_character_slot` |
| `parts[].prefabName` | 빈 값 불가, 64자 이하. **실제 프리팹인지는 검사하지 않는다** | 서버는 Unity 에셋 목록을 모른다 |

### 3-5. 오류 형식

```jsonc
{ "code": "CHARACTER_LIMIT_REACHED", "message": "이미 캐릭터를 보유하고 있습니다." }
```

| 상태 | `code` | 상황 |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | 형식 위반 |
| 401 | `INVALID_CREDENTIALS` | 이메일 / 비밀번호 불일치 |
| 401 | `TOKEN_INVALID` | 토큰 없음 · 만료 |
| 409 | `EMAIL_ALREADY_USED` | 이메일 중복 |
| 409 | `NICKNAME_ALREADY_USED` | 캐릭터 닉네임 중복 |
| 409 | `CHARACTER_LIMIT_REACHED` | 계정당 1개 초과 |

Unity 는 `code` 로 분기하고 화면에는 `message` 를 그대로 띄운다.
→ **문구 수정이 서버 배포만으로 끝난다.**

---

## 4. Unity 씬 전환 및 서비스 인터페이스 연동 방향

### 4-1. 새로 만드는 경계

`Assets/Game/Scripts/Account/` (신설).

`Network/` 아래에 두지 않는다. 저쪽은 Photon Fusion 실시간 세션 폴더이고,
계정 · 캐릭터는 단발성 요청/응답이라 성격이 다르다. 폴더로도 갈라 둔다.

```text
Assets/Game/Scripts/Account/
├── IAuthService.cs             ← 새 합의 경계 (GAME_STRUCTURE 4장에 추가)
├── ICharacterService.cs        ← 새 합의 경계
├── AccountDtos.cs              ← JsonUtility 용 [Serializable] DTO (배열 필드만)
├── AccountServiceLocator.cs    ← NetworkServiceLocator 와 동일한 정적 홀더
├── AccountServiceBootstrap.cs  ← 가짜 ↔ 진짜를 바꾸는 유일한 지점
├── FakeAuthService.cs          ← MonoBehaviour (PRD 02)
├── FakeCharacterService.cs     ← MonoBehaviour (PRD 02)
├── CharacterAppearanceMapping.cs ← 로컬 bodyColorHex ↔ 서비스 skinColor
├── HttpAuthService.cs          ← UnityWebRequest 구현 (PRD 06)
└── HttpCharacterService.cs     ← UnityWebRequest 구현 (PRD 06)
```

인터페이스 모양은 **기존 `INetworkService` 스타일(요청 메서드 + 결과 `event`)을 따른다.**
`async/await` 을 섞으면 이 프로젝트에 두 가지 비동기 관례가 생긴다.

```csharp
public interface IAuthService
{
    void SignUp(string email, string password);   // 서버 경로 /api/auth/signup 과 이름을 맞춘다
    void LogIn(string email, string password);
    void LogOut();

    bool    IsAuthenticated { get; }
    UserDto CurrentUser     { get; }   // 로그인 전이면 null
    string  AccessToken     { get; }

    event Action<bool, string> OnSignUpResult;    // 성공 여부, 실패 이유(사용자 표시용)
    event Action<bool, string> OnLogInResult;
}

public interface ICharacterService
{
    void RequestMyCharacters();
    void CreateCharacter(CharacterCreateRequest request);

    IReadOnlyList<CharacterDto> Characters { get; }   // 마지막으로 받아온 목록
    bool HasFetched { get; }

    // 0개는 실패가 아니다. 성공 + 빈 배열로 온다.
    event Action<bool, CharacterDto[], string> OnMyCharactersResult;
    event Action<bool, CharacterDto, string> OnCreateResult;
}
```

교체 지점은 **`AccountServiceBootstrap` 파일 한 곳**이다. 씬을 고치지 않는다.

```csharp
// AccountServiceBootstrap.CreateIfMissing()
host.AddComponent<FakeAuthService>();       //  →  HttpAuthService       ← PRD 06 에서 이 두 줄만
host.AddComponent<FakeCharacterService>();  //  →  HttpCharacterService
```

`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` 로 첫 씬보다 먼저 만든다.
씬 파일을 건드리지 않아도 되고, `Boot` 을 거치지 않고 `Login` 씬만 단독 실행해도 서비스가 준비된다.
(기존 `FakeNetworkService` 는 `Boot` 씬의 컴포넌트 그대로다. 손대지 않았다)

### 4-2. 씬 전환의 변화 — 분기 근거만 교체

```text
[ 현재 ]
Login [로그인] ──▶ Submit() ──▶ SceneFlow.FromLogin()
                                     └─ PlayerPrefs "PlayerNickname" 존재? ─▶ ChannelSelect
                                                                       없음 ─▶ CharacterCreate

[ 목표 ]
Login [로그인] ──▶ IAuthService.Login()
                        │ 실패 ─▶ 화면에 message 표시, 씬 이동 없음
                        └ 성공 ─▶ ICharacterService.RequestMyCharacters()
                                        └─ 개수 판단 ─▶ SceneFlow.FromLogin(count)
                                                            0개 ─▶ CharacterCreate
                                                            1개 ─▶ ChannelSelect
                                                           2개+ ─▶ (미래) CharacterSelect
                                                                   지금은 ChannelSelect + 경고 로그
```

`SceneFlow` 에 **오버로드를 추가**하고 기존 시그니처는 남긴다.

```csharp
public static void FromLogin(int characterCount);   // 신규. 서버 응답 기반
public static void FromLogin();                     // 기존 유지. 단독 씬 실행 / 서버 미가동 대비
```

기존 메서드를 지우면 `Login` 씬만 단독 실행하는 개발 흐름과 에디터 메뉴(`SceneFlowDevMenu`)가 깨진다.

### 4-3. 캐릭터 정보 보관 — `SceneFlow` 를 파사드로 남긴다

```text
CharacterSession (신설, static)          SceneFlow (기존 API 유지)
├── CurrentCharacterId                   ├── Nickname          ──┐
├── Nickname                             ├── NicknameOrDefault ──┼─▶ CharacterSession 에 위임
└── Appearance (CharacterAppearance)     └── HasCharacter      ──┘
```

이렇게 하면 **`ChannelSelectController` 를 한 줄도 고치지 않는다.**
그 파일은 계속 `SceneFlow.NicknameOrDefault` 만 읽고, 값의 출처가 `PlayerPrefs` 에서 서버로 조용히 바뀐다.
`NicknameKey = "PlayerNickname"` 상수도 **문자열 그대로 유지**한다 (오프라인 캐시 겸용).

### 4-4. 외형 스냅샷 — 인덱스에서 이름으로

```csharp
[Serializable] public struct CharacterPartSnapshot { public string slot; public string prefabName; }

[Serializable]
public class CharacterAppearance
{
    public string skinColorHex;              // "#F2C9A0"
    public CharacterPartSnapshot[] parts;       // Dictionary 아님 — JsonUtility 제약
}
```

- 저장: `equippedParts` (WearSlot → CatalogPart.prefab) 를 순회해 `prefab.name` 을 적는다.
  카탈로그를 타지 않은 파츠는 `selectedOptions[category]` → `partPrefabs[index].name` 으로 보완한다.
- 복원: `partPrefabs` 를 `Dictionary<string, GameObject>` 로 한 번 색인해 두고 이름으로 찾아 `ApplyPart` 를 호출한다.
- 이름을 못 찾으면 **그 슬롯만 건너뛰고 경고**한다. 캐릭터 생성 화면 전체가 죽지 않게 한다.
- 스킨색은 `skinColors` 배열에서 같은 hex 를 찾아 **기존 `ApplySkinColor(int)` 경로를 그대로 태운다.**
  (텍스처 재색칠 파이프라인 `CreateRecoloredSkinTexture` 를 건드리지 않기 위함) 없으면 `defaultSkinColorIndex`.

---

## 5. PRD 01~07 — 작은 구현 단위

| PRD | 제목 | 범위 | 서버 필요 | 브랜치(권장) |
| --- | --- | --- | --- | --- |
| **01** | 외형 스냅샷 로컬 저장 · 복원 | Unity | ✗ | `feature/gh-character-appearance-snapshot` |
| **02** | 계정 / 캐릭터 서비스 경계 + Fake | Unity | ✗ | `feature/gh-account-service-boundary` |
| **03** | 서버 스켈레톤 + MySQL 스키마 | Server | — | `feature/gh-server-skeleton` |
| **04** | 회원가입 / 로그인 + JWT | Server | — | `feature/gh-auth-api` |
| **05** | 캐릭터 조회 / 생성 API | Server | — | `feature/gh-character-api` |
| **06** | Unity HTTP 구현체 교체 | Unity | ✓ | `feature/gh-http-account-service` |
| **07** | 외형 서버 복원 + 다중 캐릭터 대비 | Unity | ✓ | `feature/gh-character-restore` |

**의존 관계**

```text
01 ──▶ 02 ──────────────▶ 06 ──▶ 07
        03 ──▶ 04 ──▶ 05 ──┘
```

01 · 02 와 03~05 는 **서로를 기다리지 않는다.** (`GAME_STRUCTURE.md` 12장 원칙)
Unity 쪽은 Fake 로 흐름을 완성하고, 서버 쪽은 Swagger 로 단독 검증한다.

> 브랜치 이름 주의: `GIT_CONVENTION.md` 2장은 `<타입>/<이니셜>-<작업내용>` 을 요구한다.
> 현재 브랜치 `feature/auth-api` 에는 이니셜이 빠져 있다. **다음 브랜치부터 `gh-` 를 붙인다.**

---

## 6. 단계별 완료 조건 · 테스트 방법 · 커밋 메시지 · 변경 금지 범위

커밋 메시지는 `GIT_CONVENTION.md` 3장 형식(`[type] subject`, 50자 이하, 명사형, 마침표 없음)을 따른다.

---

### PRD 01 — 외형 스냅샷 로컬 저장 · 복원

**하는 일** 인덱스 기반 런타임 선택 상태를 **이름 기반 스냅샷**으로 바꾸고 `PlayerPrefs` 에 JSON 으로 저장 · 복원한다.

**완료 조건**

- [ ] `CharacterAppearance` / `CharacterPartSnapshot` DTO 가 있고 `JsonUtility` 로 왕복 직렬화된다.
- [ ] `[생성 완료]` 시 닉네임과 **함께** 외형 JSON 이 저장된다. (`PlayerNickname` 키 동작은 그대로)
- [ ] `CharacterCreate` 씬 재진입 시 **저장된 외형이 복원**된다. 저장값이 없을 때만 기존 기본 조합이 뜬다.
- [ ] 저장된 JSON 안에 **정수 인덱스가 하나도 없다.** 슬롯 · 파츠 · 스킨색 모두 문자열.
- [ ] 존재하지 않는 `prefabName` 가 들어와도 그 슬롯만 건너뛰고 경고 로그를 남긴 뒤 화면이 정상 동작한다.

**테스트 방법**

1. `CharacterCreate` 씬 Play → 얼굴 / 머리 / 상의 / 하의 / 신발 / 액세서리와 피부색을 기본값과 다르게 고르고 이름 입력 → `[생성 완료]`.
2. Play 중지 후 다시 Play → **고른 조합 그대로** 나오는지 눈으로 확인.
3. `Tools > 아라아띠` 의 이름 지우기(또는 외형 키 삭제) 실행 후 Play → 기본 조합 복귀 확인.
4. 저장된 JSON 을 손으로 편집해 `prefabName` 를 `"NoSuchPart_99"` 로 바꾸고 Play → Console 경고 1건, 나머지 슬롯 정상.
5. Console 에 **새로운 Error 가 0건**임을 확인. (MR 체크리스트 항목)

**예상 커밋 메시지**

```text
[feat] 캐릭터 외형 스냅샷 모델 추가
[feat] 캐릭터 외형 로컬 저장·복원 구현
```

**변경 금지 범위**

- `CharacterCustomization.prefab` 의 **UI 레이아웃 · 버튼 배치 · 계층 구조.** (로직만 건드린다)
- `CharacterCreate.unity` 씬 파일. **저장하지 않는다.**
- `CharacterCustomizationController` 의 UI 동작: 카테고리 전환, 페이징, 스크롤, 회전 버튼, 닉네임 검증 문구.
- `Completed` 이벤트의 시그니처 `Action<string>` 와 발생 시점. (`CharacterCreateFlow` 가 의존)
- `PlayerPrefs` 키 `"PlayerNickname"` 문자열.
- `SceneFlow` 전체. `Login`/`ChannelSelect`/`Title`/`Boot`/`Lobby` 씬.
- `INetworkService`, `FakeNetworkService`, `NetworkServiceLocator`.
- `Assets/ithappy/`, `Assets/Photon/`, `Assets/Synty/` 등 외부 에셋 원본.
- `Assets/Game/Scenes/Develop/` 의 타인 씬 (`SeoYeon/CharacterCustomizationTest` 포함).

---

### PRD 02 — 계정 / 캐릭터 서비스 경계 + Fake 구현

**하는 일** `IAuthService` · `ICharacterService` · DTO · `AccountServiceLocator` · `FakeAccountService` 를 만들고,
`LoginScreenController` 와 `SceneFlow` 를 **서비스 기반 흐름**으로 바꾼다. HTTP 는 아직 없다.

**완료 조건**

- [ ] 위 파일들이 `Account/` 에 있고, DTO 는 `JsonUtility` 로 직렬화 가능한 배열 필드만 쓴다.
- [ ] `FakeAuthService` · `FakeCharacterService` 가 `AccountServiceLocator` 에 자기를 등록한다
      (`FakeNetworkService` 와 동일 패턴). 만드는 것은 `AccountServiceBootstrap` 이며 **씬을 고치지 않는다.**
- [ ] `LoginScreenController.Submit()` 이 탭 상태에 따라 `Login`/`Register` 를 호출하고, **성공 콜백에서만** 씬을 넘긴다.
- [ ] 요청 중 `submitButton` 이 잠기고, 실패 시 사유가 화면에 표시된다.
- [ ] `SceneFlow.FromLogin(int characterCount)` 오버로드가 0 → `CharacterCreate`, 1 → `ChannelSelect` 로 보낸다.
      2개 이상은 경고 로그 + `ChannelSelect` (`CharacterSelect` 씬은 **만들지 않는다**).
- [ ] `AccountServiceLocator.IsReady == false` 여도 `Login` 씬 단독 실행이 되고, 기존 `FromLogin()` 경로로 통과한다.
- [ ] Fake 는 `Fake.Account.*` 키만 쓴다. PRD 01 의 `PlayerNickname` · `CharacterAppearanceSnapshotV1` 을 건드리지 않는다.
- [ ] `CharacterCreate` 의 [생성 완료] 가 `ICharacterService.CreateCharacter` 를 부르고,
      **성공했을 때만** 로컬 저장 + `ChannelSelect` 이동을 한다. 실패하면 씬을 넘기지 않고 다시 시도할 수 있다.
- [ ] `GAME_STRUCTURE.md` 4장(새 경계 추가) · 9장(폴더) · 11장(제외 항목 정정)이 갱신되었다.

**테스트 방법**

1. `Boot` 부터 Play → `Login` 에서 빈 입력 `[로그인]` → **씬이 넘어가지 않고** 안내 문구가 뜬다.
2. `FakeAccountService` 의 캐릭터 보유 개수를 `0` 으로 두고 로그인 → `CharacterCreate` 진입.
3. 같은 값을 `1` 로 바꾸고 로그인 → `ChannelSelect` 진입.
4. `2` 로 바꾸고 로그인 → Console 경고 + `ChannelSelect` 진입 (크래시 없음).
5. Fake 의 실패 스위치를 켜고 로그인 → 실패 문구 표시, 버튼이 다시 눌리는 상태로 복귀.
6. `Login` 씬만 단독 Play → 예외 없이 기존 흐름대로 통과.

**예상 커밋 메시지**

```text
[feat] 계정·캐릭터 서비스 인터페이스 추가
[feat] 로그인 화면 서비스 연동 구현
[refactor] 씬 분기를 캐릭터 개수 기준으로 변경
[docs] 계정·캐릭터 경계 규격 반영
```

**변경 금지 범위**

- `INetworkService` **시그니처와 `FakeNetworkService` 동작.** 계정 기능을 여기에 넣지 않는다.
- `LoginScreenController` 의 **탭 전환 시각 처리** (`ApplyTabVisual`, 스프라이트 · 색 필드) 와 Inspector 참조 이름.
- `Login.unity` 씬의 기존 오브젝트 배치. (안내 라벨 추가가 필요하면 **비어도 동작하도록** `[SerializeField]` 선택 필드로 만든다)
- `ChannelSelectController`, `ChannelRowView` — **한 줄도 고치지 않는다.** `SceneFlow` 파사드로 흡수한다.
- `SceneFlow` 의 기존 `public` 멤버 삭제 · 이름 변경. (`NicknameKey`, `Nickname`, `HasCharacter`, `FromLogin()` 등)
- `StartMenuController`, `MenuButtonView`, `SettingsPanelView`, `Title.unity`.
- PRD 01 에서 만든 저장 포맷. (이 단계에서 스키마를 다시 바꾸지 않는다)

---

### PRD 03 — 서버 스켈레톤 + MySQL 스키마

**하는 일** `server/AraAtti.Api/` 에 ASP.NET Core Web API 를 만들고 `users`/`characters`/`character_parts` 를
EF Core 마이그레이션으로 생성한다. **Unity 변경 0.**

**완료 조건**

- [ ] `server/AraAtti.Api/` 프로젝트가 있고 `dotnet run` 으로 기동된다.
- [ ] `GET /api/health` 가 `200` 과 DB 연결 상태를 돌려준다.
- [ ] 엔티티 3개 + 마이그레이션 1개. `dotnet ef database update` 로 스키마가 생성된다.
- [ ] 2장의 인덱스가 실제로 생성된다: `idx_characters_user_id`, `uk_characters_name`, `uk_characters_user_slot`, `uk_parts_character_slot`.
- [ ] **`user_id` 에 UNIQUE 제약이 없다.** (요구사항 3)
- [ ] 개발 환경에서 Swagger UI 가 열린다.
- [ ] 접속 문자열 · JWT 키가 **소스에 하드코딩되지 않았고** `.gitignore` 로 실제 시크릿이 제외된다.
      `appsettings.Development.json` 은 예시 값만 담는다.
- [ ] `server/README.md` 에 실행 순서(MySQL 준비 → 마이그레이션 → 실행)가 적혀 있다.

**테스트 방법**

1. 로컬 MySQL 에 빈 DB 생성 → `dotnet ef database update`.
2. `SHOW CREATE TABLE characters;` 로 FK · 인덱스 확인, **`user_id` UNIQUE 부재** 확인.
3. `dotnet run` → `GET /api/health` `200`.
4. MySQL 을 내리고 `/api/health` 재호출 → DB 실패가 **명확한 메시지**로 나오고 프로세스가 죽지 않는다.
5. `git status` 로 `bin/`, `obj/`, 실제 시크릿이 스테이징되지 않았는지 확인.

**예상 커밋 메시지**

```text
[feat] ASP.NET Core 서버 프로젝트 추가
[feat] 계정·캐릭터 DB 스키마 마이그레이션 추가
[chore] 서버 실행 방법 문서 추가
```

**변경 금지 범위**

- `unity/` **디렉터리 전체.** 이 단계는 Unity 파일을 하나도 건드리지 않는다.
- `Packages/manifest.json` (서버 작업으로 Unity 패키지를 추가할 이유가 없다).
- 루트 문서 `GIT_CONVENTION.md`, `README.md`.

---

### PRD 04 — 회원가입 / 로그인 + JWT

**완료 조건**

- [ ] `POST /api/auth/signup` `201`, `POST /api/auth/login` `200`, 응답은 3-2 형식.
- [ ] 비밀번호가 **BCrypt 해시**로 저장된다. 응답 · 로그에 평문이나 해시가 노출되지 않는다.
- [ ] JWT 가 `sub`/`email`/`exp` 를 담고 `HS256` 으로 서명된다. 만료 2시간.
- [ ] 오류 코드가 3-5 표대로 나온다: `VALIDATION_FAILED`, `EMAIL_ALREADY_USED`, `INVALID_CREDENTIALS`.
- [ ] `[Authorize]` 가 붙은 시험용 엔드포인트가 **토큰 없이 401**, 유효 토큰으로 200.
- [ ] `message` 가 **한국어**다. Unity 가 그대로 화면에 띄운다.
- [ ] 존재하지 않는 이메일과 틀린 비밀번호가 **같은 401 `INVALID_CREDENTIALS`** 를 돌려준다 (계정 존재 여부 노출 방지).

**테스트 방법**

1. Swagger 로 회원가입 → `users` 행 1개, `password_hash` 가 `$2` 로 시작함을 SQL 로 확인.
2. 같은 이메일 재가입 → `409 EMAIL_ALREADY_USED`.
3. 올바른 비밀번호 로그인 → 토큰 수령. jwt.io 로 `sub` 확인.
4. 틀린 비밀번호 / 없는 이메일 → **둘 다** `401 INVALID_CREDENTIALS`.
5. `password` 7자 → `400 VALIDATION_FAILED`.
6. 토큰 없이 보호된 엔드포인트 → `401`.

**예상 커밋 메시지**

```text
[feat] 회원가입 API 구현
[feat] 로그인 및 JWT 발급 구현
[test] 인증 API 테스트 추가
```

**변경 금지 범위**

- `unity/` 전체.
- PRD 03 의 **스키마와 마이그레이션.** 컬럼 추가가 필요하면 **새 마이그레이션**을 만든다. 기존 것을 편집하지 않는다.
- `/api/health` 의 응답 형식.

---

### PRD 05 — 캐릭터 조회 / 생성 API

**완료 조건**

- [ ] `GET /api/characters` 가 **항상 배열**을 돌려준다. 없으면 `{"characters":[]}` (404 아님).
- [ ] `POST /api/characters` 가 캐릭터 + 파츠를 **한 트랜잭션**으로 저장하고 `201` 을 돌려준다.
- [ ] 두 번째 생성 시도 → `409 CHARACTER_LIMIT_REACHED`.
- [ ] 제한이 **상수 하나**(`MaxCharactersPerUser = 1`)로 표현되어 있고, 스키마 제약이 아니다.
- [ ] 닉네임 중복 → `409 NICKNAME_ALREADY_USED`. 정규식 · 길이 위반 → `400`.
- [ ] `slot` 중복이나 미지의 `slot` → `400`.
- [ ] **다른 계정의 캐릭터가 절대 조회되지 않는다.** 필터가 항상 JWT 의 `sub` 기준이다.
- [ ] 목록 조회에 언제나 `user_id` 조건이 붙어 남의 캐릭터가 섞이지 않는다.
- [ ] `parts` 는 저장 순서와 무관하게 **슬롯 기준으로 안정 정렬**되어 반환된다.

**테스트 방법**

1. 계정 A 로 로그인 → `GET /api/characters` → `{"characters":[]}`.
2. `POST` 로 6슬롯 캐릭터 생성 → `201`. `character_parts` 행 6개 SQL 확인.
3. 다시 `GET` → 배열 길이 1, `parts` 6개, `prefabName` 가 프리팹 이름 그대로.
4. `POST` 재시도 → `409 CHARACTER_LIMIT_REACHED`.
5. 계정 B 생성 → `GET` → **빈 배열** (A 의 캐릭터가 보이지 않음). B 로 A 의 캐릭터 `id` 직접 조회 → `404`.
6. 계정 B 가 A 와 **같은 닉네임**으로 생성 → `409 NICKNAME_ALREADY_USED`.
7. `slot` 을 `"Face"` 두 번 → `400`.
8. `MaxCharactersPerUser` 를 임시로 `3` 으로 바꿔 2개 생성 성공 확인 → **다시 1로 되돌리고 커밋.**
   (스키마 변경 없이 다중 캐릭터가 가능함을 증명하는 단계다)

**예상 커밋 메시지**

```text
[feat] 내 캐릭터 조회 API 구현
[feat] 캐릭터 생성 API 구현
[test] 캐릭터 API 테스트 추가
```

**변경 금지 범위**

- `unity/` 전체.
- PRD 04 의 **인증 응답 형식과 JWT 클레임.**
- 마이그레이션 이력. (수정 대신 추가)
- `GET` / `DELETE /api/characters/{id}` 구현. **이 단계 범위 밖이다.**

---

### PRD 06 — Unity HTTP 구현체 교체

**하는 일** `HttpAccountService` (UnityWebRequest) 를 만들고 `Boot` 씬에서 Fake 와 교체한다.
**화면 코드는 고치지 않는다.** 이 단계가 요구사항 7의 검증 지점이다.

**완료 조건**

- [ ] `HttpAccountService` 가 `IAuthService` + `ICharacterService` 를 구현하고 실서버와 통신한다.
- [ ] 서버 주소가 `[SerializeField]` 로 Inspector 에서 바뀐다. **하드코딩 금지.**
- [ ] `Boot` 씬 컴포넌트를 Fake ↔ Http 로 바꾸는 것만으로 전환되고,
      **`LoginScreenController` · `SceneFlow` · `ChannelSelectController` diff 가 0 줄이다.**
- [ ] 토큰이 메모리에 보관되고 `Authorization: Bearer` 헤더에 실린다. **비밀번호는 어디에도 저장하지 않는다.**
- [ ] 서버 오류 본문의 `message` 가 로그인 화면에 그대로 표시된다.
- [ ] **서버가 꺼져 있어도** 타임아웃 후 사용자에게 안내가 뜨고, Unity 가 멈추지 않는다.
- [ ] 요청 / 응답 로그에 **비밀번호와 토큰 전문이 찍히지 않는다.**

**테스트 방법**

1. 서버 기동 후 Unity Play(`Boot` 부터) → 회원가입 → `CharacterCreate` 진입. MySQL `users` 에 행 확인.
2. Play 중지 → 같은 계정 로그인 → 캐릭터 0개이므로 다시 `CharacterCreate`.
3. 캐릭터 생성 후 재로그인 → **`ChannelSelect` 로 직행**. (요구사항 4 최종 확인)
4. 틀린 비밀번호 → 서버가 준 한국어 문구가 화면에 표시되고 씬 이동 없음.
5. **서버를 끄고** 로그인 → 타임아웃 안내, Console Error 없음, 재시도 가능.
6. `Boot` 의 컴포넌트를 `FakeAccountService` 로 되돌려도 예전처럼 동작.
7. `git diff` 로 `LoginScreenController.cs` · `SceneFlow.cs` · `ChannelSelectController.cs` 무변경 확인.

**예상 커밋 메시지**

```text
[feat] 계정 API HTTP 구현체 추가
[feat] Boot 씬 실제 서버 연동 전환
```

**변경 금지 범위**

- `LoginScreenController`, `SceneFlow`, `ChannelSelectController`, `CharacterCreateFlow`
  — **수정이 필요하면 PRD 02 설계가 틀린 것이다.** 여기서 땜질하지 않고 경계를 고친다.
- `IAuthService` / `ICharacterService` 시그니처. (PRD 02 에서 확정)
- `INetworkService` 와 Photon Fusion 관련 파일 전체.
- `Boot.unity` 이외의 씬 파일.
- `Packages/manifest.json` — `UnityWebRequest` 로 충분하므로 HTTP/JSON 패키지를 추가하지 않는다.

---

### PRD 07 — 외형 서버 복원 + 다중 캐릭터 대비

**하는 일** 서버에서 받은 파츠로 실제 외형을 복원하고, `PlayerPrefs` 를 **캐시로 격하**한다.
`CharacterSelect` 의 자리를 만들되 **씬은 만들지 않는다.**

**완료 조건**

- [ ] `CharacterCreate` 의 `[생성 완료]` 가 `POST /api/characters` 를 호출하고, **성공했을 때만** 씬을 넘긴다.
- [ ] 서버 실패(이름 중복 등) 시 화면에 남아 사유를 보여준다. 로컬에만 저장하고 넘어가지 않는다.
- [ ] 로그인 후 캐릭터 1개면 `ICharacterService.CurrentCharacter` 에 서버의 이름 · 파츠 · 스킨색이 채워지고,
      같은 값으로 로컬 캐시(`PlayerNickname` · `CharacterAppearanceSnapshotV1`)가 갱신된다.
      갱신 지점은 `Account/CharacterSessionCache.cs` 한 곳이다.
- [ ] 캐릭터가 **0개면 로컬 캐시를 비운다.** 다른 계정으로 로그인했을 때
      이전 계정의 외형 · 이름이 `CharacterCreate` 에 남아 보이지 않는다.
- [ ] 통신 실패 · 파싱 실패에는 **로컬 캐시를 덮어쓰거나 지우지 않는다.**
- [ ] 캐릭터가 2개 이상이면 **자동으로 고르지 않고** 경고만 남긴다. (`CurrentCharacter` 는 null)
      `SetCurrentCharacter()` 가 선택 화면의 연결 지점이다.
- [ ] `ChannelSelect` 의 `Connect(nickname, serverId)` 에 **서버가 준 이름**이 실린다. (파일은 무변경)
- [ ] **다른 PC 에서 같은 계정으로 로그인하면 같은 캐릭터가 나온다.** (요구사항의 최종 목적)
- [ ] `PlayerPrefs` 는 오프라인 캐시로만 쓰이고, 서버 응답이 오면 **서버 값이 이긴다.**
- [ ] 다중 캐릭터를 켜는 방법이 문서화되어 있다: `MaxCharactersPerUser` 상수 + `SceneFlow.FromLogin(count)` 의
      `2개+` 분기 + `CharacterSelect` 상수 추가. **API · 스키마 변경이 필요 없음을 명시한다.**
- [ ] `GAME_STRUCTURE.md` 1장 흐름도와 6장이 최종 상태로 갱신되었다.

**테스트 방법**

1. 계정 A 로 신규 가입 → 특징적인 외형(예: 눈에 띄는 모자 · 의상)으로 캐릭터 생성 → `ChannelSelect` 도달.
2. `PlayerPrefs` 를 **전부 지우고**(에디터 메뉴) 재로그인 → **같은 외형**이 복원되는지 확인. ★핵심 검증
3. 다른 PC(또는 다른 Unity 프로젝트 사본)에서 계정 A 로그인 → 같은 캐릭터 확인.
4. 계정 A 로 `CharacterCreate` 씬을 강제 진입해 이미 있는 이름으로 생성 → 서버 실패 문구, 씬 유지.
5. 서버를 끄고 재로그인 → 캐시된 이름으로 안내가 뜨거나 로그인 실패 안내. **무한 로딩이 없다.**
6. `MaxCharactersPerUser` 를 `3` 으로 올려 2개 생성 → 로그인 시 경고 로그 + `ChannelSelect` 진입 (크래시 없음)
   → **1로 되돌리고 커밋.**

**예상 커밋 메시지**

```text
[feat] 캐릭터 생성 서버 저장 연동
[feat] 서버 외형 데이터 복원 구현
[docs] 다중 캐릭터 확장 방법 정리
```

**변경 금지 범위**

- **`CharacterSelect.unity` 씬을 만들지 않는다.** 상수와 분기 자리까지만 준비한다.
- `CharacterCustomization.prefab` 의 UI 레이아웃과 조작 방식.
- `ChannelSelectController`, `ChannelRowView`, `Lobby.unity`, `PlayerSpawner`, `FusionLauncher`.
- `INetworkService` 시그니처 — **`Connect` 에 캐릭터 ID 를 추가하지 않는다.** 필요해지면 4장 합의를 거친다.
- 서버 API 형식. (PRD 05 에서 확정. 이 단계는 클라이언트만)
- 미니게임 폴더(`MiniGames/`) 와 IoT 폴더(`IoT/`) 전체.

---

## 7. 전 단계 공통 규칙

1. **한 커밋에 한 작업.** (`GIT_CONVENTION.md` 3장)
2. MR 대상은 항상 `develop`. `master` 로 직접 올리지 않는다. 리뷰어 1명 이상.
3. Unity 작업 MR 전 체크: Console 새 Error 0건 / `.meta` 누락 없음 / `Library` · `Temp` · `Logs` 미포함 /
   **타인 Develop 씬 무변경.**
4. **경계 파일(`INetworkService`, `IAuthService`, `ICharacterService`)을 혼자 바꾸지 않는다.**
   바꿨으면 같은 MR 에서 `GAME_STRUCTURE.md` 4장도 고친다.
5. Main 씬(`Boot`/`Title`/`Login`/`CharacterCreate`/`ChannelSelect`/`Lobby`)은 **동시에 만지지 않는다.**
   씬 저장이 필요한 단계는 PRD 02(`Login`, 선택적) 와 PRD 06(`Boot`) **뿐이다.**
6. 각 PRD 는 시작 시 `docs/prd/prd-0N-*.md` 로 세부 문서를 하나 만든다. 이 문서는 색인 역할을 유지한다.

---

## 8. 다음 단계 — PRD 01 의 구체적 구현 범위

> **목표 한 문장** — 서버를 전혀 건드리지 않고, 캐릭터 외형을 **인덱스가 아닌 프리팹 이름**으로
> 저장 · 복원할 수 있게 만든다. 이후 모든 단계가 이 스냅샷 구조 위에 올라간다.

### 8-1. 왜 이것이 1번인가

- 서버 · 네트워크 **의존이 0** 이다. 오늘 바로 시작해서 오늘 검증할 수 있다.
- 1-3 에서 확인한 **가장 큰 실제 공백**(외형이 아예 저장되지 않음)을 메운다.
- PRD 05 의 `character_parts` 스키마와 PRD 06 의 요청 본문이 **이 단계에서 정하는 스냅샷 모양과 같다.**
  여기서 포맷을 확정하면 서버 담당 작업(03~05)을 병렬로 시작할 수 있다.

### 8-2. 새로 만드는 파일

```text
Assets/Game/Scripts/Character/
├── CharacterAppearance.cs                  신규
│     [Serializable] CharacterPartSnapshot { string slot; string prefabName; }
│     [Serializable] CharacterAppearance  { string skinColorHex; CharacterPartSnapshot[] parts; }
│     + ToJson() / FromJson()  — JsonUtility
│
├── CharacterAppearanceStore.cs             신규
│     PlayerPrefs 키 "Char.Appearance" 하나에 JSON 문자열로 저장
│     Save(CharacterAppearance) / TryLoad(out ...) / Clear()
│     ※ 키 이름은 GAME_STRUCTURE 6장의 "Char.*" 규약을 따른다
│
└── CharacterCustomizationSnapshot.cs       신규
      partial class CharacterCustomizationController 의 새 조각
      BuildAppearance()            현재 착용 상태 → CharacterAppearance
      ApplyAppearance(appearance)  스냅샷 → 실제 파츠 적용
      BuildPartNameIndex()         partPrefabs 를 이름→프리팹 Dictionary 로 색인 (+중복 이름 경고)
```

> `CharacterCustomizationController` 는 이미 `public sealed partial class` 다.
> **새 `partial` 파일에 로직을 모으면 서연의 877줄 파일에는 훅 두 줄만 남는다.**
> 씬 / 프리팹 충돌과 코드 리뷰 부담을 동시에 줄이는 선택이다. (`GAME_STRUCTURE.md` 9장 폴더 소유권 배려)

### 8-3. 기존 파일에서 건드리는 곳 — 딱 2군데

| 파일 | 위치 | 변경 |
| --- | --- | --- |
| `CharacterCustomizationController.cs` | `CompleteCustomization()` 내 `PlayerPrefs.Save()` 직후 | `CharacterAppearanceStore.Save(BuildAppearance());` **1줄 추가** |
| `CharacterCustomizationController.cs` | `Awake()` 의 `ApplyCuteDefaultCharacter()` 호출부 | 저장값이 있으면 `ApplyAppearance(saved)`, 없으면 기존 기본 조합. **분기 1개** |

그 밖의 기존 코드는 **읽기만** 한다.

### 8-4. 스냅샷 생성 규칙 (구현 시 반드시 이대로)

1. **1차 출처는 `equippedParts`** (`Dictionary<WearSlot, CatalogPart>`).
   `WearSlot` enum 이름 → `slot`, `CatalogPart.prefab.name` → `prefabName`.
2. `equippedParts` 에 없는 카테고리는 **`selectedOptions[category]` → `partPrefabs[index].name`** 으로 보완한다.
   (카탈로그를 타지 않고 `targetRenderer` 경로로 적용되는 파츠가 있다)
3. 스킨색은 `currentSkinColor` → `"#" + ColorUtility.ToHtmlStringRGB(...)`.
4. **저장 문자열에 정수 인덱스를 넣지 않는다.** `slot` 도 숫자 flag 값이 아니라 enum **이름**으로 적는다.

### 8-5. 복원 규칙

1. 카테고리별 `partPrefabs` 를 `Dictionary<string, GameObject>` 로 한 번만 색인한다. 이름 중복이 있으면 `LogWarning`.
2. 슬롯별로 이름을 찾아 **기존 `ApplyPart(collection, prefab)` 를 그대로 호출**한다.
   본 적용 · 본 매핑 · 재질 처리 로직을 새로 쓰지 않는다.
3. 적용 순서는 `ApplyCuteDefaultCharacter()` 와 같게 유지한다:
   **face → hair → shoes → top → bottom → accessory.**
   (주석에 명시된 대로 의상 일체형 부츠가 신발을 가리려면 신발이 먼저여야 한다)
4. 스킨색 hex 를 `skinColors` 배열에서 찾아 **인덱스로 변환해 `ApplySkinColor(int)`** 를 호출한다.
   못 찾으면 `defaultSkinColorIndex` + 경고. (`CreateRecoloredSkinTexture` 파이프라인 무변경)
5. 이름을 못 찾은 슬롯은 **건너뛰고 경고**한다. 예외를 던지지 않는다.

### 8-6. PRD 01 완료 판정

`docs/prd/` 에 PRD 01 세부 문서를 만들고, 6장 "PRD 01" 의 체크리스트 5개 + 테스트 5개를 모두 통과시킨 뒤
`feature/gh-character-appearance-snapshot` → `develop` MR 을 올린다.

**이 단계에서 절대 하지 않는 것**

- 서버 호출, HTTP 코드, DTO 의 네트워크 전송
- `SceneFlow` 수정 (분기 근거는 PRD 02 에서 바꾼다)
- 씬 파일 저장, 프리팹 UI 레이아웃 수정
- `PlayerNickname` 키 이름 변경
