# 아라아띠 서버 (AraAtti.Api)

계정과 캐릭터를 저장하는 ASP.NET Core Web API 입니다.

| 항목 | 값 |
| --- | --- |
| 프레임워크 | ASP.NET Core (net8.0) |
| DB | MySQL 8.4 (Docker) |
| ORM | EF Core 9 + Pomelo MySQL 프로바이더 |
| 실행 주소 | `http://localhost:5080` |
| Swagger | `http://localhost:5080/swagger` |

관련 문서

- `../docs/prd/auth-character-roadmap.md` — 전체 로드맵과 데이터 모델 근거
- `../GIT_CONVENTION.md` — 브랜치 · 커밋 · MR 규칙

> **왜 net8.0 인가**
> MySQL 프로바이더(Pomelo)의 최신 안정판이 EF Core 9 기반이고, EF Core 9 는 net8.0 을 대상으로 만들어져 있습니다.
> 검증된 조합으로 맞췄습니다. net8.0 은 LTS 입니다.

> **왜 API 는 Docker 로 안 만드나**
> 코드를 고칠 때마다 이미지를 다시 만들 필요가 없고, 디버거를 붙이기 쉽습니다.
> Docker 로 띄우는 것은 **MySQL 하나뿐**입니다.

---

## 0. 준비물

| 필요한 것 | 확인 명령 |
| --- | --- |
| .NET 8 SDK | `dotnet --list-sdks` (8.0.x 가 보여야 합니다) |
| Docker | `docker compose version` |

Docker Desktop 을 쓰는 경우 **Docker Desktop 을 먼저 실행**해 두어야 합니다.
엔진이 꺼져 있으면 `docker compose up` 이 `failed to connect to the docker API` 로 실패합니다.

아래 명령은 모두 **`server/` 폴더**에서 실행합니다.

```bash
cd server
```

---

## 1. MySQL 실행

접속 정보를 담을 `.env` 를 예시 파일에서 만듭니다. **`.env` 는 커밋되지 않습니다.**

```bash
cp .env.example .env
```

```powershell
# PowerShell
Copy-Item .env.example .env
```

`.env` 를 열어 비밀번호 두 개(`MYSQL_ROOT_PASSWORD`, `MYSQL_PASSWORD`)를 직접 바꿉니다.
`change-me` 를 그대로 두지 마세요.

컨테이너를 띄웁니다.

```bash
docker compose up -d
```

상태를 확인합니다. `STATUS` 가 **`healthy`** 가 될 때까지 20~30초 걸립니다.

```bash
docker compose ps
```

```text
NAME             IMAGE        STATUS                   PORTS
araatti-mysql    mysql:8.4    Up 30 seconds (healthy)  0.0.0.0:3306->3306/tcp
```

`healthy` 가 되기 전에 다음 단계로 가면 마이그레이션이 접속 실패로 끝납니다.

---

## 2. 접속 정보 설정

접속 문자열은 **소스에 넣지 않습니다.** 아래 둘 중 하나로 넣습니다.

### 방법 A — 개발용 파일 (권장)

```bash
cp AraAtti.Api/appsettings.Development.json.example AraAtti.Api/appsettings.Development.json
```

```powershell
# PowerShell
Copy-Item AraAtti.Api/appsettings.Development.json.example AraAtti.Api/appsettings.Development.json
```

복사한 파일에서 **두 곳**을 고칩니다. 이 파일은 `.gitignore` 대상이라 커밋되지 않습니다.

| 항목 | 넣을 값 |
| --- | --- |
| `ConnectionStrings.Default` 의 `password=` | `.env` 의 `MYSQL_PASSWORD` 와 **같은 값** |
| `Jwt.Key` | **본인만의 임의 문자열. 32자 이상.** 예시 값을 그대로 쓰지 마세요 |

`Jwt.Key` 는 로그인 토큰에 서명하는 열쇠입니다. 이 값을 아는 사람은 아무 계정의 토큰이나
위조할 수 있으므로 **커밋되는 파일에 넣지 않습니다.** 32자보다 짧으면 서버가 켜질 때 멈춥니다.

만드는 법 (PowerShell):

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Max 256 }))
```

### 방법 B — 환경 변수

방법 A 를 덮어씁니다. CI 나 배포 환경에서 씁니다.

```bash
export ConnectionStrings__Default="server=localhost;port=3306;database=araatti;user=araatti;password=본인비밀번호"
```

```powershell
# PowerShell
$env:ConnectionStrings__Default = "server=localhost;port=3306;database=araatti;user=araatti;password=본인비밀번호"
$env:Jwt__Key = "본인만의-32자-이상-임의-문자열"
```

`__`(밑줄 두 개)가 설정의 `:` 를 대신합니다. `Jwt__Key` → `Jwt:Key`.

> 접속 문자열이나 JWT 키가 없으면 서버가 켜질 때 이 문서를 가리키는 오류 메시지와 함께 멈춥니다.

---

## 3. 마이그레이션 적용

마이그레이션 도구(`dotnet-ef`)는 저장소에 버전이 고정되어 있습니다. (`.config/dotnet-tools.json`)
전역 설치가 필요 없고, 팀원 모두 같은 버전을 씁니다.

```bash
dotnet tool restore
```

테이블을 만듭니다.

```bash
dotnet ef database update --project AraAtti.Api
```

성공하면 `Done.` 이 나옵니다. 테이블이 만들어졌는지 확인합니다.

```bash
docker compose exec mysql mysql -u araatti -p -D araatti -e "SHOW TABLES;"
```

```text
+-----------------------+
| Tables_in_araatti     |
+-----------------------+
| __EFMigrationsHistory |
| character_parts       |
| characters            |
| users                 |
+-----------------------+
```

`characters` 의 구조를 확인합니다. **`user_id` 에 UNIQUE 가 없어야** 합니다. (계정당 여러 캐릭터가 가능한 구조)

```bash
docker compose exec mysql mysql -u araatti -p -D araatti -e "SHOW CREATE TABLE characters;"
```

```text
KEY `idx_characters_user_id` (`user_id`)              ← UNIQUE 아님. 이게 맞습니다
UNIQUE KEY `uk_characters_name` (`name`)
UNIQUE KEY `uk_characters_user_slot` (`user_id`,`slot_index`)
CONSTRAINT `fk_characters_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
```

---

## 4. API 실행

```bash
dotnet run --project AraAtti.Api
```

```text
Now listening on: http://localhost:5080
Application started. Press Ctrl+C to shut down.
```

멈추려면 `Ctrl+C` 입니다.

---

## 5. health 확인

서버를 켠 **다른 터미널**에서 실행합니다.

```bash
curl http://localhost:5080/api/health
```

```powershell
# PowerShell
Invoke-RestMethod http://localhost:5080/api/health
```

DB 까지 정상이면 **200** 과 함께 이렇게 나옵니다.

```json
{"status":"ok","database":"connected","message":null,"serverTimeUtc":"2026-09-08T08:00:00.0000000Z"}
```

MySQL 이 꺼져 있으면 **503** 과 이유를 돌려줍니다. **서버는 죽지 않습니다.**

```json
{"status":"degraded","database":"disconnected","message":"MySQL 에 연결할 수 없습니다. docker compose ps 로 컨테이너 상태를 확인해 주세요.","serverTimeUtc":"..."}
```

상태 코드만 보려면:

```bash
curl -o /dev/null -w "HTTP %{http_code}\n" http://localhost:5080/api/health
```

`/health` 로도 같은 내용이 나옵니다. 인프라 도구들이 흔히 기대하는 경로라서 별칭으로 열어 두었습니다.

브라우저에서 `http://localhost:5080/swagger` 를 열면 API 목록을 볼 수 있습니다. (개발 환경에서만 열립니다)

---

## 6. 회원가입 · 로그인 확인

### 엔드포인트

| 메서드 | 경로 | 인증 | 성공 |
| --- | --- | --- | --- |
| `POST` | `/api/auth/signup` | — | `201` + 사용자 정보 (**토큰 없음**) |
| `POST` | `/api/auth/login` | — | `200` + `accessToken` |
| `GET` | `/api/auth/me` | Bearer | `200` + 사용자 정보 |

회원가입은 계정만 만들고 토큰을 주지 않습니다. **가입 직후 로그인을 한 번 더 호출**해야 토큰을 받습니다.

### Swagger 로 확인

1. `http://localhost:5080/swagger` 를 엽니다.
2. `POST /api/auth/signup` → **Try it out** → 본문을 넣고 실행 → `201` 확인.
3. `POST /api/auth/login` → 같은 본문으로 실행 → 응답의 `accessToken` 값을 복사.
4. 오른쪽 위 **Authorize** 버튼 → 복사한 토큰을 붙여 넣습니다. (`Bearer ` 는 빼고 토큰만)
5. `GET /api/auth/me` 실행 → `200` 과 내 정보가 나오면 JWT 설정이 정상입니다.

### PowerShell 로 확인

```powershell
$base = "http://localhost:5080"
$body = @{ email = "test@araatti.test"; password = "secret123" } | ConvertTo-Json

# 1) 회원가입 — 201, 비밀번호 없는 사용자 정보
Invoke-RestMethod -Method Post "$base/api/auth/signup" -ContentType "application/json" -Body $body

# 2) 로그인 — accessToken 수령
$login = Invoke-RestMethod -Method Post "$base/api/auth/login" -ContentType "application/json" -Body $body
$login.accessToken

# 3) 토큰으로 보호된 엔드포인트 호출 — 200
Invoke-RestMethod "$base/api/auth/me" -Headers @{ Authorization = "Bearer $($login.accessToken)" }

# 4) 토큰 없이 호출 — 401 이 나야 정상
Invoke-RestMethod "$base/api/auth/me"
```

> `Invoke-RestMethod` 는 401 · 409 같은 실패 응답에서 예외를 던집니다.
> 실패 본문까지 보려면 `try { ... } catch { $_.ErrorDetails.Message }` 로 감싸세요.

### 실패 응답

모든 실패는 같은 모양입니다. `code` 로 분기하고 `message` 를 화면에 그대로 띄웁니다.

```json
{ "code": "EMAIL_ALREADY_USED", "message": "이미 가입된 이메일입니다." }
```

| 상태 | `code` | 언제 |
| --- | --- | --- |
| 400 | `VALIDATION_FAILED` | 이메일 형식 오류, 비밀번호 8자 미만, 빈 값 |
| 401 | `INVALID_CREDENTIALS` | 로그인 실패. **없는 이메일과 틀린 비밀번호가 똑같은 응답** |
| 401 | `TOKEN_INVALID` | 토큰이 없거나 만료·위조됨 |
| 409 | `EMAIL_ALREADY_USED` | 이미 가입된 이메일 |

없는 이메일과 틀린 비밀번호를 구분해서 답하면 **어떤 이메일이 가입되어 있는지 알아낼 수 있습니다.**
그래서 두 경우의 응답을 완전히 같게 만들었습니다.

### 비밀번호가 평문으로 저장되지 않는지 확인

```bash
docker compose exec mysql mysql -u araatti -p -D araatti -e "SELECT id, email, password_hash FROM users;"
```

`password_hash` 가 `$2a$12$` 로 시작하는 60자 문자열이면 정상입니다. (BCrypt, 비용 12)
평문은 어디에도 저장되지 않으며, 해시는 되돌릴 수 없습니다.
같은 비밀번호로 두 번 가입해도 salt 가 달라 해시 결과가 서로 다릅니다.

---

## 7. 자주 쓰는 명령

| 하고 싶은 것 | 명령 |
| --- | --- |
| MySQL 켜기 | `docker compose up -d` |
| MySQL 상태 보기 | `docker compose ps` |
| MySQL 로그 보기 | `docker compose logs -f mysql` |
| MySQL 끄기 (데이터 유지) | `docker compose stop` |
| MySQL 지우기 (데이터 유지) | `docker compose down` |
| **데이터까지 완전 초기화** | `docker compose down -v` |
| SQL 직접 실행 | `docker compose exec mysql mysql -u araatti -p -D araatti` |
| 적용된 마이그레이션 목록 | `dotnet ef migrations list --project AraAtti.Api` |

### 새 마이그레이션 만들기

엔티티나 `AraAttiDbContext` 를 고친 뒤에 실행합니다.

```bash
dotnet ef migrations add 변경내용이름 --project AraAtti.Api
dotnet ef database update --project AraAtti.Api
```

**이미 만들어진 마이그레이션 파일을 손으로 고치지 않습니다.** 새 마이그레이션을 하나 더 만듭니다.
다른 팀원이 이미 적용한 마이그레이션을 고치면 그쪽 DB 와 어긋납니다.

마지막 마이그레이션을 **아직 적용하지 않았을 때만** 되돌릴 수 있습니다.

```bash
dotnet ef migrations remove --project AraAtti.Api
```

---

## 8. 막혔을 때

| 증상 | 원인과 해결 |
| --- | --- |
| `failed to connect to the docker API` | Docker 엔진이 꺼져 있습니다. Docker Desktop 을 실행하세요. |
| `set MYSQL_PASSWORD in server/.env` | `.env` 가 없습니다. 1번을 다시 하세요. |
| `DB 접속 문자열이 없습니다` | 2번을 하지 않았습니다. |
| `JWT 서명 키(Jwt:Key)가 없거나 너무 짧습니다` | 2번에서 `Jwt.Key` 를 안 넣었거나 32자 미만입니다. |
| 로그인은 되는데 `/api/auth/me` 가 401 | `Jwt.Key` 를 바꾼 뒤 예전 토큰을 쓰고 있습니다. 다시 로그인하세요. |
| health 가 `disconnected` | `docker compose ps` 로 `healthy` 인지 확인하고, `.env` 의 `MYSQL_PASSWORD` 와 `appsettings.Development.json` 의 `password=` 가 같은지 확인하세요. |
| `Access denied for user 'araatti'` | 위 두 비밀번호가 다릅니다. `.env` 를 바꿨다면 `docker compose down -v` 로 DB 를 초기화해야 반영됩니다. (비밀번호는 처음 생성될 때만 적용됩니다) |
| 포트 3306 이 이미 사용 중 | `.env` 의 `MYSQL_PORT` 를 3307 로 바꾸고 `docker compose up -d`, 접속 문자열의 `port=` 도 같이 바꾸세요. |
| 5080 포트가 이미 사용 중 | `AraAtti.Api/Properties/launchSettings.json` 의 `applicationUrl` 을 바꾸세요. |

---

## 9. 폴더 구조

```text
server/
├── docker-compose.yml                MySQL 하나만 띄운다
├── .env.example                      → 복사해서 .env 로 쓴다 (커밋 안 됨)
├── .config/dotnet-tools.json         dotnet-ef 버전 고정
└── AraAtti.Api/
    ├── Program.cs                    서버 설정 + /api/health
    ├── appsettings.json              공통 설정 (비밀 정보 없음)
    ├── appsettings.Development.json.example  → 복사해서 쓴다 (복사본은 커밋 안 됨)
    ├── Auth/                         인증 재료
    │   ├── JwtOptions.cs             Jwt 설정 (Key 는 여기 없음)
    │   ├── JwtTokenGenerator.cs      토큰 발급
    │   └── PasswordHasher.cs         BCrypt 해싱 · 대조
    ├── Contracts/AuthContracts.cs    요청 · 응답 모양 (비밀번호 절대 미포함)
    ├── Endpoints/AuthEndpoints.cs    signup · login · me
    ├── Entities/                     테이블에 대응하는 클래스 3개
    │   ├── User.cs
    │   ├── Character.cs
    │   └── CharacterPart.cs
    ├── Data/
    │   ├── AraAttiDbContext.cs       컬럼 · 타입 · 인덱스를 정하는 곳
    │   └── AraAttiDbContextFactory.cs  dotnet ef 전용
    └── Migrations/                   dotnet ef 가 만든 파일. 손으로 고치지 않는다
```

---

## 10. 테이블 구조

```text
users                        characters                    character_parts
─────────────────            ─────────────────             ─────────────────
id            PK    1 ──── N  id            PK    1 ──── N  id            PK
email    UNIQUE               user_id       FK             character_id  FK
password_hash                 name     UNIQUE              slot          ┐ 함께
created_at                    skin_color                   prefab_name   ┘ UNIQUE
last_login_at                 slot_index    ┐ user_id 와
                              created_at    ┘ 함께 UNIQUE
                              updated_at
```

### 계정당 캐릭터 1개는 DB 로 막지 않습니다

지금 정책은 계정당 캐릭터 1개지만, `characters.user_id` 에 **UNIQUE 를 걸지 않았습니다.**

걸어 버리면 나중에 다중 캐릭터를 붙일 때 **인덱스를 떼는 마이그레이션**이 필요해집니다.
개수 제한은 이후 단계에서 서비스 계층의 상수(`MaxCharactersPerUser = 1`)로 겁니다.
그래야 다중 캐릭터를 켤 때 **스키마를 손대지 않습니다.**

### 파츠를 JSON 컬럼이 아니라 별도 테이블로 둔 이유

- 슬롯이 늘어나도(모자 · 안경 · 장갑 …) 서버 코드를 고칠 필요가 없다
- `(character_id, slot)` UNIQUE 로 한 슬롯에 두 개가 들어가는 것을 DB 가 막아준다
- 슬롯별 조회 · 집계가 가능하다

`prefab_name` 에는 **프리팹 이름 문자열**이 들어갑니다. 예: `Costume_14_01`
배열 인덱스를 저장하지 않습니다. 인덱스는 Unity Inspector 의 배열 순서라서,
에셋을 정렬하거나 파츠를 중간에 추가하면 저장된 모든 캐릭터의 외형이 밀립니다.

Unity 쪽 저장 형식과 필드 이름이 같습니다.
(`unity/UnderTheSea/Assets/Game/Scripts/Character/CharacterAppearanceSnapshot.cs`)

---

## 11. 아직 없는 것

이 단계는 **개발 기반만** 만듭니다. 아래는 다음 단계에서 붙입니다.

| 없는 것 | 언제 |
| --- | --- |
| 캐릭터 조회 · 생성 (`/api/characters`) | 다음 단계 (로드맵 PRD 05) |
| 계정당 캐릭터 1개 제한 검사 | 다음 단계 (로드맵 PRD 05) |
| Unity 연동 (로그인 화면 → 실제 API) | 로드맵 PRD 02 · 06 · 07 |
| Refresh Token (토큰 자동 갱신) | **범위 밖.** 2시간 만료 뒤에는 다시 로그인 |
| 이메일 인증, 비밀번호 재설정 | **범위 밖** |
| 비밀번호 변경 · 회원 탈퇴 | **범위 밖** |

`characters` 와 `character_parts` 테이블은 만들어져 있지만 **읽고 쓰는 API 가 아직 없습니다.**
Unity 는 여전히 캐릭터를 `PlayerPrefs` 로 그 PC 에만 저장합니다.
