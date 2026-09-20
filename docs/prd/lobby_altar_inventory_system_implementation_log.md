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

---

## STEP 3. 제단 봉헌 API

구현일: 2026-09-20

이 절의 명령 실행 결과와 HTTP 응답은 모두 **에이전트 세션에서 직접 실행하고 관측한 것**입니다.
설계에서 읽어 해석한 부분은 "설계상", 제가 고른 부분은 "구현 선택" 으로 적었습니다.

### 구현 파일

수정

```text
server/AraAtti.Api/Contracts/AltarContracts.cs     ← 요청·응답 DTO 3개 추가
server/AraAtti.Api/Endpoints/AltarEndpoints.cs     ← POST /offer 추가 + 상태 계산 helper 추출
```

`Program.cs` 는 손대지 않았습니다. STEP 2 에서 `MapAltarEndpoints()` 가 이미 등록되어 있고
새 엔드포인트는 같은 그룹 안에 붙기 때문입니다.
Service · Repository 계층도 만들지 않았습니다.

### `POST /api/altar/offer` 계약

```text
요청   { "amount": 50, "requestId": "0f2c9b1e-..." }
성공   200  success · offeredAmount · remainingFragments · totalOffered · targetOffering ·
            remainingToTarget · maxOfferAmount · altarActivated · recoveryPercent · duplicate
실패   409  success · code · message + 위 상태값 전부
입력   400  ErrorResponse(code, message)   ← 프로젝트 공통 모양
```

`acceptedAmount` · `refundedAmount` 는 없습니다. 부분 수락을 하지 않으므로 성공했다면
`offeredAmount` 는 언제나 요청한 수량입니다. (설계 10.3.2)

409 응답에도 최신 상태를 전부 싣습니다. 클라이언트가 실패 직후 `GET /api/altar/state` 를
다시 부르지 않아도 UI 를 맞출 수 있어야 하기 때문입니다. (설계 9.2)

### 입력 검증

순서는 설계 8.6절 그대로입니다. 2번(캐릭터 보유)은 결정 #6 으로 검사하지 않습니다.

| # | 검증 | 실패 |
|---|---|---|
| 1 | 로그인 | 401 `TOKEN_INVALID` |
| 3 | amount 가 정수 | 역직렬화 400 (아래 참고) |
| 4 | amount > 0 | 400 `AMOUNT_INVALID` |
| 5 | amount ≤ int.MaxValue | 400 `AMOUNT_TOO_LARGE` |
| 6 | requestId 가 Guid | 400 `REQUEST_ID_INVALID` |
| 7 | 이미 처리된 요청 | 200 `duplicate: true` |
| 8 | 남은 칸 ≥ amount | 409 `OFFERING_CLOSED` / `OFFERING_AMOUNT_CHANGED` |
| 9 | 보유량 ≥ amount | 409 `NOT_ENOUGH_FRAGMENTS` |

#### amount 를 `long?` 으로 받은 이유 — 구현 선택

DTO 를 `int` 로 두면 `int.MaxValue` 보다 큰 JSON 숫자가 **핸들러에 닿기도 전에**
역직렬화 400 이 되어, 설계 8.6 의 5번 `AMOUNT_TOO_LARGE` 를 우리가 돌려줄 수 없습니다.
`uint` 로 받으면 반대로 음수를 잃어 `AMOUNT_INVALID` 를 구분할 수 없습니다.
그래서 `long?` 으로 받고 핸들러에서 두 경계를 직접 봅니다. `JsonElement` 수동 파서나
전역 JSON 설정 변경은 하지 않았습니다.

`?`(nullable)인 이유는 기존 `SignupRequest` · `CreateCharacterRequest` 와 같습니다 —
JSON 에서 통째로 빠질 수 있고, 그때 우리 문구로 답하기 위해서입니다.

실측한 경계입니다.

```text
amount = 0                      → 400 AMOUNT_INVALID
amount = -5                     → 400 AMOUNT_INVALID
amount 누락                      → 400 AMOUNT_INVALID
amount = 2147483648             → 400 AMOUNT_TOO_LARGE      ← int.MaxValue + 1
amount = 9223372036854775807    → 400 AMOUNT_TOO_LARGE      ← long.MaxValue
amount = 99999999999999999999   → 400 (역직렬화 단계)        ← long 범위 초과
amount = 1.5                    → 400 (역직렬화 단계)        ← 소수
amount = "abc"                  → 400 (역직렬화 단계)        ← 문자열
requestId = "abc"               → 400 REQUEST_ID_INVALID
```

⚠ 마지막 세 줄은 `BadHttpRequestException` 이라 **우리 `ErrorResponse` 모양이 아닙니다.**
설계 15.1 이 이 경우를 "JSON 파싱 실패 → 400" 으로 적어 둔 그대로입니다.
Development 환경에서는 예외 본문이 그대로 나오고, 운영 환경에서는 본문 없는 400 입니다.

#### requestId

문자열로 받아 `Guid.TryParse` 로 검증합니다. `Guid` 로 직접 받으면 형식 오류가
역직렬화 400 이 되어 `REQUEST_ID_INVALID` 를 돌려줄 수 없고, `Guid.Parse` 는 예외가
500 으로 올라갑니다. 저장은 STEP 1 의 Entity 타입대로 `Guid` 입니다.

### 트랜잭션

```text
duplicate precheck (트랜잭션 밖)
  → BEGIN
  → ① altar_state 조건부 원자적 UPDATE
  → ② player_inventories 조건부 UPDATE
  → ③ altar_contributions INSERT
  → COMMIT
  → 최신 상태를 읽어 응답
```

제단이 먼저, 인벤토리가 나중입니다. "봉헌 가능량이 줄었다" 가 "보유량이 모자라다" 보다
먼저 판정되어야 정확한 이유를 줄 수 있고, 8번에서 걸리면 9번에 닿지도 않아
인벤토리가 건드려지지 않습니다. (설계 10.3.1)

#### ① 제단 — 판단을 WHERE 안에 넣습니다

```csharp
int accepted = await database.AltarStates
    .Where(altar => altar.Id == AltarStateId
        && altar.TotalOffered + offerAmount <= altar.TargetOffering)
    .ExecuteUpdateAsync(setters => setters
        .SetProperty(altar => altar.TotalOffered, altar => altar.TotalOffered + offerAmount)
        .SetProperty(altar => altar.UpdatedAt, _ => DateTime.UtcNow), cancellationToken);
```

영향 행 0 이 곧 실패입니다. 먼저 `SELECT` 해서 판단한 뒤 `UPDATE` 하면 그 사이에 다른
요청이 끼어들어 1001/1000 이 만들어집니다. (설계 10.2 의 두 번째 금지 사례)

⚠ **조건을 설계 코드의 뺄셈(`TargetOffering - TotalOffered >= amount`)이 아니라 덧셈으로
썼습니다 — 구현 선택입니다.** 두 컬럼이 모두 UNSIGNED 라, `total > target` 인 비정상
데이터에서 뺄셈은 음수가 아니라 MySQL 오류(`BIGINT UNSIGNED value is out of range`)가
됩니다. 덧셈은 같은 판정을 하면서 그 경로가 없습니다. 불변식이 지켜지는 정상 데이터에서
두 식은 완전히 같습니다.

`UpdatedAt` 은 `DateTime.UtcNow` 입니다. STEP 2 의 UTC 규칙을 유지합니다 —
서버 코드 어디에도 `DateTime.Now` 가 없습니다.

#### ② 인벤토리 — 같은 모양

```csharp
.Where(item => item.UserId == userId
    && item.ItemId == InventoryEndpoints.SeaHeartFragmentItemId
    && item.Quantity >= offerAmount)
.ExecuteUpdateAsync(... Quantity - offerAmount ..., UpdatedAt = UtcNow)
```

행이 아예 없어도 0행이므로 `NOT_ENOUGH_FRAGMENTS` 가 맞습니다.
봉헌은 UPSERT 가 아니라서 조각이 없는 사용자를 위해 행을 만들지 않습니다.
`updated_at` 을 함께 갱신하는 것은 그 컬럼의 뜻 그대로입니다(설계에 명시는 없음 — 구현 선택).

#### ③ contribution INSERT

`UserId` / `RequestId`(parsed Guid) / `Amount` / `CreatedAt = DateTime.UtcNow`.
같은 트랜잭션 안입니다.

### 멱등성

#### 사전 확인 + UNIQUE 최후 방어선

트랜잭션 **밖에서** `(user_id, request_id)` 를 먼저 조회합니다. 있으면 봉헌을 건너뛰고
현재 상태를 `duplicate: true` 로 돌려줍니다.

사전 확인만으로는 부족합니다. 정말 동시에 온 두 요청은 둘 다 "없음" 을 봅니다.
그래서 `uk_contributions_user_request` 가 최후의 방어선이고, 그 예외를 잡습니다.

```csharp
catch (DbUpdateException exception) when (IsDuplicateKeyViolation(exception))
{
    await transaction.RollbackAsync(cancellationToken);
    database.Entry(contribution).State = EntityState.Detached;   // 실패한 INSERT 를 떼어낸다
    AltarContribution? winner = await FindContributionAsync(...);
    if (winner is null) throw;                                   // 우리 키가 아닌 중복은 숨기지 않는다
    return await DuplicateAsync(database, userId, winner.Amount, cancellationToken);
}
```

⚠ **모든 `DbUpdateException` 을 duplicate 로 보지 않습니다.** 연결이 끊긴 것도, 다른 제약을
위반한 것도 전부 "성공" 으로 둔갑합니다. MySQL 의 1062(`ER_DUP_ENTRY`)일 때만 참입니다.

```csharp
exception.InnerException is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry }
```

`MySqlConnector` 는 Pomelo 의 전이 의존성이라 패키지를 새로 추가하지 않았습니다.

⚠ rollback 후 `Added` 로 남은 엔티티를 떼어내지 않으면 다음 `SaveChanges` 가 같은 INSERT 를
또 시도합니다. 그래서 `Detached` 로 바꿉니다.

#### 실패 경로에서도 duplicate 를 다시 확인합니다 — 구현 선택

같은 `(user_id, requestId)` 두 요청이 동시에 오면, 먼저 온 쪽이 마지막 칸을 채운 뒤
나중 쪽이 **contribution INSERT 에 닿기도 전에** 제단 조건부 UPDATE 0행으로 떨어질 수
있습니다. 그때 `OFFERING_CLOSED` 로 답하면 "같은 requestId 는 다시 실행하지 않고
duplicate 로 돌려준다" 는 설계 10.4 의 계약이 실제 동시 상황에서 깨집니다.

그래서 세 경로 모두에서 **실패를 확정하기 전에** `(user_id, requestId)` 를 다시 조회합니다.

```text
① altar 조건부 UPDATE 0행   → 재확인 → 있으면 duplicate:true
② inventory 조건부 UPDATE 0행 → 재확인 → 있으면 duplicate:true
③ contribution UNIQUE 충돌   → 재확인 → 있으면 duplicate:true
```

새 오류 코드는 만들지 않았습니다. 설계 계약을 동시 요청에서도 지키기 위한 보강입니다.

#### 같은 requestId + 다른 amount — 구현 선택

`duplicate` 응답의 `offeredAmount` 는 **이미 기록된 `altar_contributions.amount`** 입니다.
이번 요청의 `amount` 가 달라도 새 봉헌을 하지 않습니다.
설계에 `IDEMPOTENCY_AMOUNT_MISMATCH` 같은 코드가 없고, 실제로 반영된 수량은 처음 것
하나뿐이므로 기존 기록을 돌려주는 편이 일관됩니다.

상태값은 "그때" 가 아니라 **"지금"** 값입니다. (설계 10.4)

### 상태 계산 공식 공유

`GET /state` 와 `POST /offer` 가 서로 다른 공식을 갖지 않도록 `AltarSnapshot` 이라는
`private readonly record struct` 와 `ReadSnapshotAsync` 한 곳에 모았습니다.
STEP 2 에서 `GetAltarStateAsync` 안에 있던 계산을 그대로 옮긴 것이고, 동작은 같습니다.
Service 계층으로 올리지 않았습니다.

```text
remainingToTarget = total >= target ? 0 : target - total
maxOfferAmount    = min(myFragments, remainingToTarget)
altarActivated    = total >= target
recoveryPercent   = target == 0 ? 0 : min(total / (float)target, 1) * 100
```

### 실제 테스트 결과

서버를 `dotnet run` 으로 띄우고 실제 MySQL · 실제 HTTP 로 확인했습니다.
동시 요청은 Python `ThreadPoolExecutor` 로 보냈습니다
(Windows PowerShell 5.1 에는 `ForEach-Object -Parallel` 이 없습니다).

#### 단일 · 순차 (49개 항목 전부 PASS)

```text
26. 단일 성공      보유 100, 제단 0 → 30 봉헌
    → 200 duplicate=false offeredAmount=30
    → DB 보유 70, 제단 30, contribution 1행

27. 보유량 부족    보유 20, 제단 500 → 30 요청
    → 409 NOT_ENOUGH_FRAGMENTS
    → "보유한 조각이 모자랍니다. (보유 20 / 요청 30)"
    → DB 보유 20 그대로, 제단 500 그대로   ← ① 의 +30 이 rollback 된 것을 확인
    → contribution 0행

28. 남은 칸 초과   제단 998/1000, 보유 10 → 5 요청
    → 409 OFFERING_AMOUNT_CHANGED
    → DB 보유 10, 제단 998, contribution 0행
    → remainingToTarget=2, maxOfferAmount=2
    → acceptedAmount · refundedAmount 필드 없음 (부분 수락 없음)

29. 완료 이후      제단 1000/1000, 보유 10 → 1 요청
    → 409 OFFERING_CLOSED
    → "섬 회복이 완료되어 더 이상 봉헌할 수 없습니다."
    → DB 보유 10, 제단 1000, contribution 0행
    → altarActivated=true, recoveryPercent=100

30. 순차 멱등성    같은 requestId 2회 (amount 3)
    → 1차 200 duplicate=false, 보유 10→7, 제단 500→503
    → 2차 200 duplicate=true,  보유 7 그대로, 제단 503 그대로, contribution 1행

31. 같은 requestId + 다른 amount (3 성공 후 8 재요청)
    → 200 duplicate=true, offeredAmount=3 (기존 기록)
    → 보유·제단·contribution 전부 변화 없음

33. 다른 사용자 + 같은 requestId
    → A 2개 성공, B 3개 성공 (둘 다 duplicate=false)
    → 제단 505, contribution 2행
    → UNIQUE 가 (user_id, request_id) 라서 서로 막지 않는다
```

#### 동시성 (23 + 7 항목 전부 PASS)

```text
32-1. 동시 동일 requestId   보유 10, 제단 500, amount 3, 2건 동시
      → 두 응답 모두 200, 하나 duplicate=false / 하나 duplicate=true
      → 보유 정확히 -3(7), 제단 정확히 +3(503), contribution 정확히 1행

32-2. 완료 경계 + 동시 동일 requestId   제단 999/1000, 보유 5, amount 1, 2건 동시
      → 두 응답 모두 200 (code 필드 없음)
      → 하나 duplicate=false / 하나 duplicate=true
      → 제단 1000, 보유 4, contribution 1행
      ⚠ 두 번째가 OFFERING_CLOSED 로 끝나지 않았다. 위의 "실패 경로 duplicate 재확인" 이
        실제로 동작한 것이다. 이 보강이 없으면 여기서 계약이 깨진다.

34. 동시 20건 합계 정확성   제단 500, 보유 30, amount 1 × 20 (requestId 전부 다름)
    → 20건 모두 200
    → 제단 정확히 520, 보유 정확히 10(-20), contribution 20행
    → lost update 없음

35. 하드캡 부하   계정 20개, 제단 990/1000, 각 보유 5, 각 amount 5, 20건 동시
    → 200 이 2건, 409 가 18건 (전부 OFFERING_CLOSED)
    → 성공 amount 총합 정확히 10
    → 제단 정확히 1000
    → 성공 계정 보유 0, 실패 계정 18개 전부 보유 5 그대로
    → contribution 정확히 2행
    → SELECT * FROM altar_state WHERE total_offered > target_offering;  →  0행

36. 마지막 한 칸 경쟁   제단 999/1000, A·B 각 보유 5, 각 amount 1, requestId 다름
    → 정확히 1명 200 / 1명 409
    → 제단 정확히 1000, contribution 1행
    → 패자 보유 5 그대로, 승자 4
    → 불변식 위반 0행
```

#### 트랜잭션 · 잠금

실패 경로를 모두 돌린 뒤 확인했습니다.

```text
information_schema.innodb_trx           0행   ← 남아 있는 트랜잭션 없음
performance_schema.data_lock_waits      0행
performance_schema.data_locks           0행

다른 연결에서 SELECT ... FOR UPDATE on altar_state  → 즉시 성공 (lock wait timeout 3초 설정)
```

### 설계 문구 충돌 — 마지막 칸 경쟁의 실패 코드 (해소 완료)

구현 중 **설계 문서 안에서 두 곳이 서로 다르게 적혀 있는 것**을 발견했습니다.

```text
8.6.2절 (판정 규칙)     0행 이후 최신 상태를 읽어
                        remainingToTarget == 0  → OFFERING_CLOSED
                        remainingToTarget  > 0  → OFFERING_AMOUNT_CHANGED
15.1절 표               남은 칸 0(회복 완료) → OFFERING_CLOSED

10.6절 (서술)           999/1000 경쟁에서 진 쪽 → "응답 OFFERING_AMOUNT_CHANGED + 최신 상태"
19.4 테스트 C           진 쪽: 409 OFFERING_AMOUNT_CHANGED
```

999/1000 에서 한 명이 이겨 1000/1000 이 되면, 진 쪽이 읽는 `remainingToTarget` 은 0 입니다.
따라서 8.6.2 의 규칙으로는 `OFFERING_CLOSED` 이고, 10.6 · 테스트 C 의 문구로는
`OFFERING_AMOUNT_CHANGED` 입니다. 같은 상황에 두 답이 적혀 있습니다.

**구현은 8.6.2 의 판정 규칙을 따랐습니다.** 그것이 조건을 명시한 규범 조항이고,
15.1 표와도 일치하며, 이번 작업 지시서도 같은 규칙을 지정했기 때문입니다.
실측 결과 테스트 36(=설계 테스트 C)의 패자 코드는 `OFFERING_CLOSED` 입니다.

```text
테스트 36 실측   승자 200 / 패자 409 OFFERING_CLOSED
                 제단 1000, contribution 1행, 패자 보유 5 그대로
```

사용자 경험상으로도 이쪽이 맞습니다 — 그 시점에 제단은 실제로 닫혔고, 수량을 낮춰 다시
눌러도 되는 상황이 아닙니다.

#### 설계 충돌 해소 (2026-09-20)

**§8.6.2 를 기준으로 통일하기로 확정되어 설계 문서를 고쳤습니다.** 코드는 그대로입니다 —
구현이 이미 8.6.2 를 따르고 있었고, 문서 쪽 두 곳이 그것과 어긋나 있던 것입니다.

```text
§10.6         진 쪽 응답  OFFERING_AMOUNT_CHANGED → OFFERING_CLOSED
§19.4 테스트 C  진 쪽 기대  409 OFFERING_AMOUNT_CHANGED → 409 OFFERING_CLOSED
                           + remainingToTarget 0 · maxOfferAmount 0 ·
                             altarActivated true · recoveryPercent 100.0 명시
```

확정된 규칙은 하나입니다.

```text
조건부 UPDATE 0행 → 최신 상태를 읽어
  remainingToTarget == 0  → 409 OFFERING_CLOSED
  remainingToTarget  > 0  → 409 OFFERING_AMOUNT_CHANGED
```

`OFFERING_AMOUNT_CHANGED` 는 아직 칸이 남아 있어 **수량을 낮춰 다시 시도할 수 있는**
상태의 코드입니다. 마지막 칸 경쟁의 패자는 그 시점에 남은 칸이 0 이라 다시 봉헌할 수
없으므로 `OFFERING_CLOSED` 가 맞습니다.

⚠ **멱등성 규칙은 건드리지 않았습니다.** 같은 `(user_id, requestId)` 가 동시에 두 번 오는
것은 칸 경쟁이 아니라 재전송이므로, 여전히 `200` + `duplicate: true` 입니다 (설계 10.4).
위 테스트 32-2 의 실측이 그것이고, 이번 문서 수정 대상이 아닙니다.
두 경우를 구분해 두도록 §10.6 과 테스트 C 에 각각 한 줄씩 덧붙였습니다.

### 관측된 부수 사항

`recoveryPercent` 가 `float` 라서 값에 따라 소수 꼬리가 보입니다.

```text
503 / 1000  →  50.300003
998 / 1000  →  99.8
1000 / 1000 →  100
```

설계 11.3 의 코드가 `float` 로 적혀 있어 그대로 두었습니다. 화면 표기는 클라이언트가
포맷하는 값이므로 서버에서 반올림을 덧붙이지 않았습니다(설계에 없는 동작이라서).
STEP 4 이후 UI 에서 자리수를 정하면 됩니다.

### DB 원복

테스트 전 기준값

```text
altar_state          id=1, total_offered=0, target_offering=1000,
                     activated_at=NULL, updated_at=2026-09-20 00:00:00.000000
users                0행
player_inventories   0행
altar_contributions  0행
characters           0행
reward_claims        0행
```

테스트 중 바꾼 것

```text
계정 22개 생성        offer-a · offer-b · cap00 ~ cap19
player_inventories    테스트용 행 생성·수정
altar_contributions   테스트용 행 생성
altar_state           total_offered 를 0 · 500 · 990 · 998 · 999 · 1000 으로 반복 변경
                      (target_offering 은 1000 그대로, activated_at 은 건드리지 않음)
                      updated_at 은 봉헌 성공 시 서버가 UtcNow 로 갱신
```

원복 후 실제 조회 결과 — 기준값과 같습니다.

```text
id  total_offered  target_offering  activated_at  updated_at
1   0              1000             NULL          2026-09-20 00:00:00.000000

users 0행 · player_inventories 0행 · altar_contributions 0행
characters 0행 · reward_claims 0행
```

`updated_at` 도 기준값 문자열로 되돌렸습니다. 남은 흔적은 `AUTO_INCREMENT` 카운터뿐입니다.
DB drop · recreate · 스키마 변경은 하지 않았습니다.

### 빌드 · 모델 상태

```text
dotnet build
Error   0
Warning 0                     ← STEP 2 종료 시점과 같음. 새 경고 없음

dotnet ef migrations has-pending-model-changes
→ No changes have been made to the model since the last migration.
```

`Migrations/` · `Entities/` · `AraAttiDbContext.cs` · Unity · `appsettings*` · `.env` ·
`README.md` · `design.md` 모두 변경 없습니다.

### STEP 3 완료 상태

- [x] `POST /api/altar/offer` 구현
- [x] 사용자 id 는 JWT `sub` 에서만
- [x] amount 입력 검증 (`AMOUNT_INVALID` · `AMOUNT_TOO_LARGE`)
- [x] requestId `Guid.TryParse`
- [x] duplicate precheck
- [x] altar 조건부 원자적 UPDATE
- [x] inventory 조건부 UPDATE
- [x] contribution INSERT
- [x] 세 작업이 하나의 트랜잭션
- [x] altar 먼저 / inventory 나중
- [x] 부분 수락 없음
- [x] `total_offered` 하드캡 유지 (불변식 위반 0행)
- [x] 200 성공 / 200 duplicate / 400 3종 / 409 3종
- [x] 실패 시 DB 부분 변경 없음
- [x] 실패 응답에 최신 상태 포함
- [x] 같은 requestId 순차 중복 지급 없음
- [x] 같은 requestId 동시 중복 지급 없음
- [x] 같은 requestId 동시 요청 완료 race 도 `duplicate: true`
- [x] 다른 user + 같은 requestId 각각 정상
- [x] 병렬 20건 합계 정확
- [x] 마지막 칸 경쟁 정상
- [x] 하드캡 부하 테스트 정상
- [x] 트랜잭션 · 잠금 잔존 없음
- [x] 테스트 DB 원복
- [x] dotnet build Error 0 / Warning 0
- [x] pending model changes 없음
- [x] Migration · Entity · DbContext · Unity · design.md 무변경

STEP 3 완료.
