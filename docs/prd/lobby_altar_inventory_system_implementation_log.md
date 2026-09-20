# 로비 심장 제단 봉헌 시스템 구현 기록

이 문서는 실제 구현 과정에서 확인된 결과와 트러블슈팅을 기록합니다.

기준 설계: `docs/prd/lobby_altar_inventory_system_design.md`
브랜치: `feature/hj-lobby-altar`
작성일: 2026-09-20

## 두 문서의 구분

| 문서 | 담는 것 |
|---|---|
| `lobby_altar_inventory_system_design.md` | 시스템 설계 · 확정 정책 · API 계약 · 동시성 규칙 · 테스트 기준 |
| `lobby_altar_inventory_system_implementation_log.md` (이 문서) | 실제 구현 결과 · 설계와 실제 라이브러리의 차이 · Migration/DB 적용 결과 · 트러블슈팅 · STEP 별 완료 기록 |

설계 문서는 "무엇을 만들기로 했는가" 를 안정적으로 유지합니다.
이 문서는 "실제로 만들었더니 어땠는가" 를 STEP 이 끝날 때마다 아래로 쌓아 갑니다.
정책을 다시 정의하지 않습니다. 정책이 바뀌어야 한다면 설계 문서를 고칩니다.

---

## STEP 1. 서버 스키마와 Migration

### 구현 파일

수정

```text
server/AraAtti.Api/Data/AraAttiDbContext.cs
server/AraAtti.Api/Migrations/AraAttiDbContextModelSnapshot.cs   ← EF 자동 생성
```

신규

```text
server/AraAtti.Api/Entities/PlayerInventoryItem.cs
server/AraAtti.Api/Entities/AltarState.cs
server/AraAtti.Api/Entities/AltarContribution.cs
server/AraAtti.Api/Entities/RewardClaim.cs

server/AraAtti.Api/Migrations/20260920051333_AddInventoryAndAltar.cs
server/AraAtti.Api/Migrations/20260920051333_AddInventoryAndAltar.Designer.cs
```

⚠ 기존 `20260908075724_InitialCreate` Migration 은 **수정하지 않았습니다.**
DB drop · recreate · Migration history 삭제도 하지 않았습니다.

### Entity 구현 결과

기존 프로젝트 관례를 그대로 따랐습니다. 엔티티는 속성(Attribute) 없는 POCO 이고,
컬럼 이름 · 타입 · 인덱스는 전부 `AraAttiDbContext.OnModelCreating` 한 곳에 모았습니다.

| Entity | 필드 | 비고 |
|---|---|---|
| `PlayerInventoryItem` | `ulong Id` · `ulong UserId` · `string ItemId` · `uint Quantity` · `DateTime UpdatedAt` | `UserId` 는 `users.id` 와 같은 `ulong` |
| `AltarState` | `int Id` · `ulong TotalOffered` · `uint TargetOffering` · `DateTime? ActivatedAt` · `DateTime UpdatedAt` | `Id` 는 `ValueGeneratedNever`. 행 하나, 값은 1 |
| `AltarContribution` | `ulong Id` · **`Guid RequestId`** · `ulong UserId` · `uint Amount` · `DateTime CreatedAt` | `RequestId` 의 CLR 타입은 아래 별도 절 참고 |
| `RewardClaim` | `ulong Id` · `ulong UserId` · `string GameId` · `string MatchKey` · `DateTime ClaimedAt` | STEP 11 의 보상 멱등성이 쓴다 |

FK 3개(`fk_inventory_user` · `fk_contributions_user` · `fk_claims_user`)는 모두

```text
DeleteBehavior.Cascade
```

입니다. 기존 프로젝트의 FK 두 개(`fk_characters_user` · `fk_parts_character`)와 같은 정책이고,
EF Core 의 필수 관계 기본값이기도 해서 별도 설정 없이 일관됩니다.

`altar_contributions.user_id` 의 삭제 동작은 설계 문서에 명시되어 있지 않아
구현 전에 확인을 받고 Cascade 로 정했습니다.

### DB 스키마 생성 결과

테이블 4개 생성 성공.

```text
player_inventories
altar_state
altar_contributions
reward_claims
```

UNIQUE 3개 — 셋 다 복합 키입니다. `item_id` · `request_id` · `match_key` **단독 UNIQUE 는 없습니다.**

```text
uk_inventory_user_item          (user_id, item_id)
uk_contributions_user_request   (user_id, request_id)
uk_claims_user_match            (user_id, match_key)
```

INDEX 2개 — `idx_claims_user_time` 의 컬럼 순서가 `user_id` → `claimed_at` 인 것까지 확인했습니다.

```text
idx_contributions_user   (user_id)
idx_claims_user_time     (user_id, claimed_at)
```

CHECK 3개.

```text
ck_altar_target_positive   target_offering > 0
ck_altar_total_nonneg      total_offered >= 0
ck_altar_total_le_target   total_offered <= target_offering
```

확인 방법은 `SHOW CREATE TABLE` 4회입니다.

### CHECK 실제 동작 검증

스키마에 이름이 있는 것만으로 끝내지 않고, **실제로 거부되는지**까지 확인했습니다.

```text
UPDATE altar_state SET target_offering = 0    WHERE id = 1;
  → ERROR 3819 (HY000): Check constraint 'ck_altar_target_positive' is violated.

UPDATE altar_state SET total_offered = 2000   WHERE id = 1;
  → ERROR 3819 (HY000): Check constraint 'ck_altar_total_le_target' is violated.
```

두 검증 모두 `START TRANSACTION` 안에서 수행하고 `ROLLBACK` 했습니다.
DB 값은 변하지 않았습니다 — 검증 직후 다시 조회해 `0 / 1000` 그대로임을 확인했습니다.

`ck_altar_total_nonneg` 는 `total_offered` 가 `bigint unsigned` 라 구조적으로 위반할 수 없어
제약 존재만 확인했습니다.

### altar_state seed

```text
id                1
total_offered     0
target_offering   1000
activated_at      NULL
updated_at        2026-09-20 00:00:00.000000
```

Migration 의 `InsertData` 는 컬럼을 `id` · `activated_at` · `target_offering` · `updated_at` 만 적고
`total_offered` 를 생략합니다. EF 가 "CLR 기본값(0)이고 컬럼에 DB DEFAULT 가 있다" 고 판단해
빼는 정상 동작이고, MySQL 이 `DEFAULT '0'` 을 적용하므로 실제 행에는 0 이 들어갑니다.
위 조회 결과가 그것입니다.

`updated_at` 을 `DateTime.UtcNow` 가 아니라 고정값으로 둔 이유는, 시드 값이 매번 달라지면
`migrations add` 를 할 때마다 모델이 바뀐 것으로 잡혀 빈 Migration 이 계속 생기기 때문입니다.

### Migration 결과

```text
Migration:
20260920051333_AddInventoryAndAltar

생성:
성공

DB 적용:
성공

적용된 Migration:
- 20260908075724_InitialCreate
- 20260920051333_AddInventoryAndAltar
```

적용 직전 `araatti` 데이터베이스는 **테이블 0개**였습니다(`__EFMigrationsHistory` 조차 없음).
그래서 `migrations list` 에 둘 다 `Pending` 으로 나온 것이 정상이며, Migration history 불일치가
아니었습니다. 이 점을 먼저 확인하고 적용했습니다.

DB drop · recreate · 기존 데이터 삭제는 하지 않았습니다.

생성된 Migration 의 `Up()` 은 `CreateTable` 4개 · FK 3개 · `CreateIndex` 5개 · `CheckConstraint` 3개 ·
`InsertData` 1개뿐이고, 기존 테이블에 대한 `AlterColumn` · `DropColumn` · `DropTable` 은 없습니다.
`Down()` 은 신규 4개 테이블만 떨어뜨립니다. 적용 전에 이 내용을 직접 읽고 확인했습니다.

### 빌드 결과

```text
dotnet build
Error   0
Warning 0

dotnet ef migrations has-pending-model-changes
→ No changes have been made to the model since the last migration.
```

`Warning 0` 은 이번 변경 전과 같습니다. 새로 생긴 경고가 없습니다.

---

### 설계와 실제 구현의 차이: `altar_contributions.request_id`

이번 STEP 에서 설계와 저장소가 부딪힌 지점은 여기 하나입니다.
**DB 스키마는 설계 그대로이고, 달라진 것은 C# 쪽 타입뿐입니다.**

```text
설계 DB 타입   char(36)
최종 DB 타입   char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL
CLR 타입       Guid
```

#### 초기 시도

```csharp
public string RequestId { get; set; } = string.Empty;   // Entity

entity.Property(contribution => contribution.RequestId)
    .HasColumnName("request_id")
    .HasColumnType("char(36)")                          // DbContext
    .IsRequired();
```

#### 문제

Pomelo 9.0.0 은 `char(36)` 을 **Guid 전용 store type** 으로 취급합니다.
그래서 `string` 속성에 `char(36)` 을 직접 매핑하면 EF 가 정상적인 string scalar mapping 을
찾지 못하고, `string` 을 `IEnumerable<char>` 형태의 primitive collection 으로 처리하려다
relational model 생성 과정에서 `NullReferenceException` 이 납니다.

```text
System.NullReferenceException
  at RelationalTypeMappingSource.FindCollectionMapping(...)
  at ElementMappingConvention.ProcessModelFinalizing(...)
  at Model.FinalizeModel()
```

⚠ `char(7)` 등 **다른 길이의 char 는 정상 동작합니다.** 실제로 기존 `characters.skin_color` 가
`char(7)` 이고 아무 문제가 없습니다. 문제는 길이나 char 타입 자체가 아니라
**`char(36)` 이 Guid 용으로 예약된 매핑**이라는 점입니다.

`HasColumnType("char(36)")` 대신 `HasMaxLength(36).IsFixedLength()` 로 우회해도 결과는 같습니다.
그렇게 적으면 모델 생성은 통과하지만, EF 가 ModelSnapshot 과 Designer 를 생성할 때
`.HasColumnType("char(36)")` 로 역직렬화해 적어 두기 때문에, 그 snapshot 을 다시 읽는
`database update` 시점에 같은 자리에서 다시 깨집니다.

### 문제 해결 과정

```text
1.  string + char(36) 으로 Entity 작성
2.  Migration 생성 과정에서 relational model NRE 발생
3.  신규 Entity 설정 문제인지 확인
4.  pristine checkout 에서는 정상임을 확인 (기존 모델 문제가 아님)
5.  Entity/DbSet 을 하나씩 bisect — 개별·조합 모두 통과해 DbContext 가 원인이 아님을 확인
6.  generated snapshot/Designer round-trip 에서 문제가 나는 것을 확인
7.  char(36) + string 조합이 원인임을 확정
8.  DB 타입을 varchar(36) 으로 바꾸는 방법은 설계를 변경하므로 사용하지 않음
9.  DB char(36) 을 유지하고 CLR 타입을 Guid 로 변경
10. snapshot round-trip 정상 (has-pending-model-changes 통과)
11. Migration 재생성 (실패한 Migration 은 적용 전이라 폐기, InitialCreate 는 그대로)
12. DB 적용 성공
```

### 왜 Guid CLR 타입이 설계 위반이 아닌가

```text
설계의 핵심 요구사항은

  - request_id 가 UUID 형식
  - DB 컬럼은 char(36)
  - (user_id, request_id) 로 멱등성 보장

이다.

CLR 타입을 string 에서 Guid 로 변경해도
DB 스키마와 requestId 의 의미는 그대로 유지된다.

따라서 DB 설계를 변경한 것이 아니라
Pomelo 9 의 실제 타입 매핑에 맞춘 구현 선택이다.
```

설계 문서 8.6절 검증 6번이 이미 `requestId` 를 "Guid 문자열" 로 규정하고
형식이 틀리면 `400 REQUEST_ID_INVALID` 로 돌려주게 되어 있습니다.
CLR 타입 `Guid` 는 그 규정과 어긋나지 않습니다.

### STEP 3 에 전달할 구현 메모

```text
STEP 3 의 POST /api/altar/offer 에서 외부 요청의 requestId 는 문자열로 들어온다.

서버에서는

  Guid.TryParse(requestId, out Guid parsedRequestId)

형태로 검증한다.

파싱 실패
  → HTTP 400
  → code = REQUEST_ID_INVALID

파싱 성공
  → Guid 값으로 AltarContribution.RequestId 에 저장
```

⚠ **`Guid.Parse` 를 바로 쓰지 않습니다.** 형식이 틀린 요청은 클라이언트 잘못이라 400 이어야 하는데,
`Guid.Parse` 는 예외를 던져 500 으로 올라갑니다. 반드시 `TryParse` 기반 validation 을 씁니다.

이것은 설계 문서 8.6절 검증 6번(`REQUEST_ID_INVALID`)을 구현하는 방법일 뿐,
새 검증을 추가하는 것이 아닙니다.

---

### 환경 트러블슈팅: Docker Desktop

Migration 생성과 코드 리뷰 이후 DB 적용 단계에서 MySQL 에 연결할 수 없었습니다.
STEP 1 의 Entity · Migration 설계 문제가 아니라 로컬 실행 환경 문제입니다.

아래는 **누가 직접 확인한 사실인지** 를 나눠 적습니다.

#### 에이전트 세션에서 직접 확인한 내용

- `docker compose ps` 가 Docker API(`npipe:////./pipe/dockerDesktopLinuxEngine`)에 연결하지 못했고,
  `localhost:3306` 도 닫혀 있는 것을 확인했습니다.
- Docker Desktop 이 실행되지 않은 상태였습니다.
- Docker Desktop 을 실행한 뒤, 기존 `araatti-mysql` 컨테이너가 다시 올라오는 것을 관측했습니다.
  새로 만든 컨테이너가 아니라 4일 전에 만들어진 그 컨테이너입니다.
- 컨테이너 상태를 다음과 같이 확인했습니다.

```text
docker compose ps

NAME            IMAGE       STATUS                  PORTS
araatti-mysql   mysql:8.4   Up (healthy)            0.0.0.0:3306->3306/tcp
```

```text
image      mysql:8.4
container  araatti-mysql
status     healthy
port       3306
```

- 이 과정에서 Docker volume 을 삭제하거나 DB 를 초기화하지 않았습니다.

#### 사용자 환경에서 별도로 확인된 복구 과정

아래는 에이전트가 관측한 것이 아니라, **사용자가 자신의 Windows 환경에서 직접 확인해 전달한 내용**입니다.

사용자 화면에서는 Docker Desktop 시작 과정에서 다음 오류가 발생했습니다.

```text
sailor-ingest.sock
The file cannot be accessed by the system.
```

사용자가 Windows PowerShell 에서

```powershell
wsl --shutdown
```

을 실행한 뒤 Docker Desktop 을 다시 시작했고, 이후 `docker ps` 에서 기존 `araatti-mysql` 컨테이너가
`healthy` 상태로 복구된 것을 확인했습니다.

이 과정에서도 아래 작업은 수행되지 않았습니다.

```text
Docker volume 삭제 없음
컨테이너 재생성 없음
DB 초기화 없음
Factory Reset 없음
```

#### 결론

두 경로 모두 결과는 같습니다 — 기존 컨테이너와 볼륨이 그대로 살아난 뒤 Migration 이 적용되었습니다.
따라서 이 문제는 STEP 1 의 Entity/Migration 설계 문제가 아니라
로컬 Docker Desktop / WSL 실행 환경에서 발생한 문제로 기록합니다.

---

### STEP 1 완료 상태

- [x] Entity 4개 생성
- [x] DbContext 등록
- [x] Migration 생성
- [x] Migration 코드 리뷰
- [x] 기존 InitialCreate 수정 없음
- [x] DB 적용 성공
- [x] 신규 테이블 4개 확인
- [x] UNIQUE 3개 확인
- [x] INDEX 2개 확인
- [x] CHECK 3개 확인
- [x] CHECK 실제 동작 확인
- [x] altar_state seed 확인
- [x] dotnet build Error 0
- [x] dotnet build Warning 0
- [x] pending model changes 없음

STEP 1 완료.

---

## STEP 2. 서버 조회 엔드포인트

구현일: 2026-09-20

이 절의 모든 명령 실행 결과와 HTTP 응답은 **에이전트 세션에서 직접 실행하고 관측한 것**입니다.
사용자가 전달한 사실이 섞여 있지 않습니다. (STEP 1 Docker 절에서 정한 출처 구분 원칙)

### 구현 파일

수정

```text
server/AraAtti.Api/Program.cs        ← 엔드포인트 등록 8줄 추가
```

신규

```text
server/AraAtti.Api/Contracts/InventoryContracts.cs
server/AraAtti.Api/Contracts/AltarContracts.cs
server/AraAtti.Api/Endpoints/InventoryEndpoints.cs
server/AraAtti.Api/Endpoints/AltarEndpoints.cs
```

Service · Repository · ItemCatalog · 새 DI 추상화는 만들지 않았습니다.
조회 두 개는 엔드포인트에서 `AraAttiDbContext` 를 직접 쓰는 것으로 충분하고,
그것이 `CharacterEndpoints` 가 이미 하고 있는 방식입니다.

STEP 1 산출물(Entity 4개 · DbContext 매핑 · Migration · ModelSnapshot · seed)은
한 줄도 건드리지 않았습니다.

### 기존 서버 패턴을 따른 부분

| 항목 | 따른 방식 | 근거 |
|---|---|---|
| Endpoint 등록 | `public static void MapXxxEndpoints(this IEndpointRouteBuilder)` | `CharacterEndpoints` |
| 그룹 | `MapGroup(...).WithTags(...).RequireAuthorization()` | `CharacterEndpoints` |
| DTO | `public sealed record` (파일당 여러 개) | `CharacterContracts` |
| 사용자 식별 | `principal.TryGetUserId(out ulong userId)` | `Auth/ClaimsPrincipalExtensions` |
| 실패 응답 | `ErrorResponse(code, message)`, message 는 한국어 | 프로젝트 전역 |
| 조회 | `AsNoTracking()` + `CancellationToken` 전달 | `GetMyCharactersAsync` |
| 아이템 id 상수 | 엔드포인트 클래스의 `public const` | `CharacterEndpoints.MaxCharactersPerUser` |

JSON 은 `Program.cs` 에 별도 설정이 없어 minimal API 기본값(camelCase)을 그대로 씁니다.

### `GET /api/inventory`

```text
인증   RequireAuthorization (그룹 단위)
주인   JWT sub → ClaimsPrincipalExtensions.TryGetUserId → ulong userId
조회   player_inventories WHERE user_id = <sub>  ORDER BY item_id
```

요청 본문 · 쿼리 · 경로 어디에서도 userId 를 받지 않습니다. 남의 인벤토리는 조회할 수 없습니다.

가진 것이 없으면 **빈 배열**입니다. 404 가 아니고, `quantity = 0` 짜리 가짜 항목을 만들지도
않습니다. `CharacterListResponse` 가 같은 이유로 같은 모양입니다.

```json
{ "items": [] }
```

`displayName` 은 DB 에 컬럼이 없어 서버가 붙입니다. 아는 아이템이 `sea_heart_fragment`
하나뿐이라 분기도 하나이고, 모르는 id 는 id 를 그대로 돌려줍니다.
아이템 카탈로그나 ScriptableObject 구조는 만들지 않았습니다 (설계 문서 4장).

### `GET /api/altar/state`

각 필드의 출처입니다.

| 필드 | 어디서 왔나 |
|---|---|
| `totalOffered` | `altar_state(id=1).total_offered` |
| `targetOffering` | `altar_state(id=1).target_offering` — **1000 을 코드에 박지 않음** |
| `remainingToTarget` | `total >= target ? 0 : target - total` (서버 계산) |
| `myFragments` | `player_inventories` 의 `(user_id = sub, item_id = 'sea_heart_fragment')` 수량. 행이 없으면 0 |
| `maxOfferAmount` | `min(myFragments, remainingToTarget)` (서버 계산) |
| `altarActivated` | `total >= target` (서버 계산) |
| `recoveryPercent` | `min(total / target, 1) * 100` (서버 계산) |
| `myOfferedTotal` | `altar_contributions` 의 `user_id = sub` 인 행들의 `amount` **합계** |
| `updatedAt` | `altar_state(id=1).updated_at` |

놓치기 쉬운 세 가지를 따로 적어 둡니다.

```text
altarActivated 는 activated_at 의 null 여부로 판단하지 않는다.
  activated_at 은 감사·기록용이다 (설계 11.4.16).
  부등호는 == 이 아니라 >= 다. 시연 중 target_offering 을 낮추면
  total > target 이 되는데 그때도 섬은 켜져 있어야 한다 (설계 10.7).

myOfferedTotal 은 totalOffered 와 다르다.
  전체 봉헌량이 아니라 "내가" 봉헌한 합계다. 기여가 없으면 0.

myFragments 는 인벤토리 행이 없으면 0 이다.
  조회하면서 행을 새로 만들지 않는다.
```

`remainingToTarget` 은 빼기 전에 대소를 먼저 봅니다. 두 값 모두 부호 없는 정수라
`total > target` 인 비정상 데이터에서 그냥 빼면 음수가 아니라 거대한 양수가 됩니다.

`recoveryPercent` 는 `(float)` 로 올려 나눠 정수 나눗셈을 피하고, `Math.Min(..., 1f)` 로
100% 를 넘기지 않습니다. `target == 0` 이면 0 으로 떨어뜨립니다 —
`ck_altar_target_positive` 가 막고 있지만 0 으로 나누면 `Infinity`/`NaN` 이 JSON 으로
나가기 때문입니다 (설계 11.2).

#### `altar_state` 싱글턴 처리

`id = 1` 행이 없으면 **만들지 않고** `InvalidOperationException` 으로 멈춥니다.
런타임에 "없으면 생성" 을 넣으면 두 요청이 동시에 만들려다 부딪힙니다.
설계에 없는 새 public error code 를 만들지 않았습니다 — 이것은 클라이언트 잘못이 아니라
마이그레이션이 적용되지 않은 서버 상태이므로 500 이 맞습니다.

### 실제 테스트 결과

에이전트가 `dotnet run` 으로 서버를 띄우고 직접 호출한 결과입니다.
로그인 절차는 `server/README.md` 6장을 그대로 따랐습니다(회원가입 → 로그인 → Bearer 토큰).

인증 없음 — 둘 다 401. 본문도 프로젝트 공통 모양입니다.

```text
GET /api/inventory     (Authorization 없음)  → 401
GET /api/altar/state   (Authorization 없음)  → 401

{"code":"TOKEN_INVALID","message":"로그인이 필요합니다. 토큰이 없거나 만료되었습니다."}
```

정상 JWT — 둘 다 200.

```text
GET /api/inventory     → 200   {"items":[]}
GET /api/altar/state   → 200
{"totalOffered":0,"targetOffering":1000,"remainingToTarget":1000,"myFragments":0,
 "maxOfferAmount":0,"altarActivated":false,"recoveryPercent":0,"myOfferedTotal":0,
 "updatedAt":"2026-09-20T00:00:00Z"}
```

빈 인벤토리가 200 + `items: []` 입니다. 404 가 아닙니다.

Swagger(`/swagger/v1/swagger.json`)에 두 경로가 올라온 것도 확인했습니다.
Swagger 설정은 STEP 2 때문에 손대지 않았습니다.

```text
/api/inventory      get   tags=["Inventory"]
/api/altar/state    get   tags=["Altar"]
```

#### 계산 경로 검증 (임시 데이터 → 원복)

기본값 상태(0 / 1000, 인벤토리 0행, 기여 0행)에서는 모든 파생값이 0이라
계산식이 실제로 검증되지 않습니다. 그래서 **현재값을 먼저 기록하고** 임시 데이터를 넣어
확인한 뒤 **정확히 되돌렸습니다.**

기록한 기준값

```text
altar_state          id=1, total_offered=0, target_offering=1000,
                     activated_at=NULL, updated_at=2026-09-20 00:00:00.000000
player_inventories   0행
altar_contributions  0행
```

① 730 / 1000, 보유 77, 기여 20 + 30 — 설계 9.2절의 예시와 같은 값이 나왔습니다.

```json
{"totalOffered":730,"targetOffering":1000,"remainingToTarget":270,"myFragments":77,
 "maxOfferAmount":77,"altarActivated":false,"recoveryPercent":73,"myOfferedTotal":50,
 "updatedAt":"2026-09-20T00:00:00Z"}
```

`myOfferedTotal` 이 기여 건수 2 가 아니라 합계 50 이고, 전체 730 과도 다릅니다.

② 사용자 격리 — 조각도 기여도 없는 두 번째 계정으로 같은 시점에 호출했습니다.
전역 값은 같고 내 값만 0 입니다. 남의 데이터가 섞이지 않습니다.

```json
{"totalOffered":730,...,"myFragments":0,"maxOfferAmount":0,"myOfferedTotal":0}
{"items":[]}
```

③ 회복 완료 1000 / 1000

```json
{"totalOffered":1000,"targetOffering":1000,"remainingToTarget":0,"myFragments":77,
 "maxOfferAmount":0,"altarActivated":true,"recoveryPercent":100,...}
```

`altarActivated` 가 `true` 로 바뀌고 `remainingToTarget` 과 `maxOfferAmount` 가 0 이 됩니다.

⚠ `recoveryPercent` 의 **100% 상한(`Math.Min(..., 1f)`)은 여기까지만 검증됩니다.**
상한이 실제로 깎아내는 경우는 `total > target` 일 때뿐인데, 그 상태는
`ck_altar_total_le_target` 이 막고 있어 정상 DB 에서 만들 수 없습니다.
설계 10.7절이 말하는 대로 이 분기는 **방어 코드**입니다.

원복 확인 — 기준값과 정확히 같습니다. `updated_at` 은 UPDATE 대상에 넣지 않아 그대로입니다.

```text
id  total_offered  target_offering  activated_at  updated_at
1   0              1000             NULL          2026-09-20 00:00:00.000000

player_inventories   0행
altar_contributions  0행
```

테스트 계정 두 개(`test@araatti.test`, `test2@araatti.test`)도 **지웠습니다.**
로그인 없이는 두 엔드포인트를 검증할 수 없어 README 6장의 회원가입 절차를 그대로 따라
만든 것이고, STEP 2 테스트 외에는 쓰지 않았습니다. 지우기 전에 확인한 것은 다음 두 가지입니다.

```text
STEP 2 시작 시점 users 0행  → 두 계정 모두 이번 테스트에서 생긴 것
characters · character_parts · player_inventories ·
altar_contributions · reward_claims 전부 0행  → 참조하는 자식 행 없음
```

삭제 후 `users` 는 다시 0행이고, 나머지 여섯 테이블도 모두 0행,
`altar_state` 는 `1 / 0 / 1000 / NULL / 2026-09-20 00:00:00.000000` 입니다.
STEP 2 시작 전 상태와 같습니다. 남은 흔적은 `AUTO_INCREMENT` 카운터뿐입니다.

DB drop · recreate · 스키마 변경 · 마이그레이션 생성은 하지 않았습니다.

### 빌드 · 모델 상태

```text
dotnet build
Error   0
Warning 0                     ← STEP 1 종료 시점과 같음. 새 경고 없음

dotnet ef migrations has-pending-model-changes
→ No changes have been made to the model since the last migration.
```

`Migrations/` · `Entities/` · `AraAttiDbContextModelSnapshot.cs` 는 변경되지 않았습니다.

### 설계와 실제 저장소가 달랐던 부분

#### ① 응답 필드 개수 — 작업 지시서 7개 vs 설계 문서 9개

작업 지시서의 `GET /api/altar/state` 예시는 7개 필드였지만,
설계 문서 9.2절의 계약에는 `remainingToTarget` 과 `maxOfferAmount` 가 더 있습니다.

```text
지시서 예시   totalOffered, targetOffering, altarActivated, recoveryPercent,
              myOfferedTotal, myFragments, updatedAt
설계 9.2절    위 7개 + remainingToTarget + maxOfferAmount
```

**설계 문서를 따라 9개로 구현했습니다.** 지시서 자체가 "API 계약은 설계 문서를 우선한다" 고
정해 두었고, 설계 9.2 · 11.1절이 이 두 값을 서버가 계산해야 하는 이유를 명시합니다 —
UI 가 `min(보유량, 남은 칸)` 을 스스로 계산하면 같은 공식이 두 군데에 생깁니다.
STEP 4 이후 Unity 쪽이 이 두 값을 읽습니다.

#### ② `updatedAt` 의 UTC 표기

`altar_state.updated_at` 은 UTC 로 저장되지만, Pomelo 는 `DateTimeKind.Unspecified` 로
돌려줍니다. 그대로 직렬화하면 끝의 `Z` 가 빠져(`"2026-09-20T00:00:00"`) 클라이언트가
현지 시각으로 읽습니다. 설계 9.2절의 응답 예시는 `Z` 가 붙은 UTC 표기입니다.

그래서 이 엔드포인트에서만 `DateTime.SpecifyKind(state.UpdatedAt, DateTimeKind.Utc)` 로
**잃어버린 Kind 만 다시 붙입니다.** 시각 값 자체는 바꾸지 않고, 새 시각을 만들지도 않습니다.

```text
DB          2026-09-20 00:00:00.000000
응답        "2026-09-20T00:00:00Z"
```

⚠ 기존 엔드포인트(`/api/auth/me` · `/api/characters` 의 `createdAt` · `updatedAt`)는
여전히 `Z` 없이 나갑니다. **이번 STEP 에서 고치지 않았습니다.** 전역 JSON 설정이나 기존
응답을 STEP 2 때문에 바꾸는 것은 범위 밖이기 때문입니다. 나중에 정리한다면 그때는
`AraAttiDbContext` 에서 `DateTime` 을 UTC 로 읽어 오는 변환을 한 번에 거는 편이 낫습니다.

설계 예시의 소수점 여섯 자리(`.000000Z`)는 나오지 않습니다. `System.Text.Json` 이 0인
소수부를 생략하기 때문이고, 값은 같습니다. ISO-8601 파서는 둘 다 동일하게 읽습니다.

`SpecifyKind` 가 값을 왜곡하지 않는 근거를 커밋 전에 세 가지로 확인했습니다.

```text
① 서버 코드 전체에 DateTime.Now(로컬)가 없다. 시각은 전부 DateTime.UtcNow 이거나
   명시적 DateTimeKind.Utc 리터럴이다.
② altar_state.updated_at 의 쓰기 경로는 지금 마이그레이션 seed 하나뿐이고
   그 값이 new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc) 이다.
③ 컬럼이 timestamp 가 아니라 datetime 이다. MySQL 의 datetime 은 시간대 변환을 하지 않고
   wall-clock 을 그대로 저장·반환한다. 접속 문자열에도 시간대 옵션이 없다.
   실측: DB 2026-09-20 00:00:00.000000 → 응답 "2026-09-20T00:00:00Z" (같은 wall-clock)
```

즉 잃는 것은 `Kind` 뿐이고 값은 UTC wall-clock 그대로입니다.

⚠ **STEP 3 에서 `updated_at` 을 UPDATE 할 때도 반드시 `DateTime.UtcNow` 를 써야 합니다.**
한 곳이라도 `DateTime.Now` 를 쓰면 위 ①이 깨지고, 그때부터 `SpecifyKind(Utc)` 는
현지 시각에 Z 를 붙이는 잘못된 코드가 됩니다.

#### ③ `myOfferedTotal` 의 CLR 타입

`altar_contributions.amount` 는 `int unsigned`(`uint`)인데 LINQ 에 `Sum(uint)` 오버로드가
없습니다. `(long)` 으로 올려 합산한 뒤 응답에서 `ulong` 으로 내보냅니다.
합계는 언제나 0 이상이고, DB 타입을 축소하지 않았습니다.

### STEP 2 완료 상태

- [x] InventoryContracts.cs · AltarContracts.cs 생성
- [x] InventoryEndpoints.cs · AltarEndpoints.cs 생성
- [x] `GET /api/inventory` 구현
- [x] `GET /api/altar/state` 구현
- [x] 두 엔드포인트 모두 `RequireAuthorization`
- [x] 사용자 id 는 JWT `sub` 에서만 가져옴
- [x] 빈 인벤토리 → 200 + `items: []`
- [x] 다른 사용자의 데이터가 섞이지 않음 (두 계정으로 확인)
- [x] `altar_state(id=1)` 조회, 런타임 생성 없음
- [x] `targetOffering` 은 DB 값 사용
- [x] `altarActivated` · `recoveryPercent` 서버 계산
- [x] `myOfferedTotal` 은 사용자별 `amount` 합계
- [x] `myFragments` 는 행이 없으면 0
- [x] 토큰 없음 → 401 (두 엔드포인트)
- [x] 정상 JWT → 200 (두 엔드포인트)
- [x] dotnet build Error 0 / Warning 0
- [x] pending model changes 없음
- [x] Migration · DB 스키마 · Entity · Unity 변경 없음
- [x] STEP 3 코드 없음 (봉헌 · clear-reward 미구현)

STEP 2 완료.
