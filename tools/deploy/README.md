# EC2 배포

`develop` 을 EC2(`43.202.67.137`)에 올려 실제로 사람들이 접속하는 상태로 만드는 절차다.
손으로 하면 20~30분 걸리고 중간에 틀리기 쉬워서 스크립트로 묶었다.

```
빌드        build-release.ps1     리눅스 서버 4 + 윈도우 Release 클라 1
포장        pack-client.ps1       클라 + 실행 배치 + 안내문 → zip
올리기      (scp)                 서버·API·클라를 EC2 staging 으로
교체        deploy-servers.sh     EC2 에서 멈추고 · 바꾸고 · 다시 띄우고 · 확인
```

## 올라가는 것

| | 무엇 | 어디 |
|---|---|---|
| 게임 서버 | Dedicated Server 7대 (로비 1 · 광산 2 · 검 2 · 배 2) | `~/araatti/{Server,MineServer,WarriorsServer,ShipCoopServer}` |
| 계정 서버 | ASP.NET Core 8 API, 포트 5080 | `~/araatti/api` |
| 데이터베이스 | MySQL 8 (Docker, **127.0.0.1 에만 바인딩**) | `araatti-mysql` 컨테이너 |
| 다운로드 | 클라이언트 zip | `/var/www/araatti/AraAtti.zip` |

세션 이름과 포트는 **클라이언트가 아는 값**이다. 한쪽만 바꾸면 `GameNotFound` 가 난다.

```
lobby-ch1   27015      warriors-1  27018      shipcoop-1  27020
mine-1      27016      warriors-2  27019      shipcoop-2  27021
mine-2      27017
```

## 하는 법

### 1. 빌드

```powershell
powershell -ExecutionPolicy Bypass -File tools\deploy\build-release.ps1
powershell -ExecutionPolicy Bypass -File tools\deploy\pack-client.ps1
```

Unity 를 두 번 띄운다(리눅스 · 윈도우). 20~30분쯤 걸린다.

### 2. API 만들기

```bash
dotnet publish server/AraAtti.Api/AraAtti.Api.csproj \
  -c Release -r linux-x64 --self-contained false -o <임시폴더>
```

`--self-contained false` 가 중요하다. SDK 10 으로 `net8.0` 을 self-contained 로 publish 하면
`System.Text.Json.dll` 같은 프레임워크 어셈블리가 **빠진 채로** 나와서 EC2 에서 안 뜬다.
EC2 에는 `aspnetcore-runtime-8.0` 이 깔려 있으므로 framework-dependent 로 충분하다.

### 3. 올리기

```bash
cd unity/UnderTheSea/Builds/Linux
tar -czf servers.tar.gz --exclude="*_BurstDebugInformation_DoNotShip" \
    Server MineServer WarriorsServer ShipCoopServer
tar -czf api.tar.gz -C <임시폴더> .

scp -i ~/.ssh/J15C101T.pem servers.tar.gz api.tar.gz \
    ubuntu@43.202.67.137:~/araatti/staging/
scp -i ~/.ssh/J15C101T.pem ../AraAtti-EC2.zip \
    ubuntu@43.202.67.137:~/araatti/staging/AraAtti.zip

# deploy-servers.sh 를 고쳤으면 이것도. 서버에 있는 것은 따로 복사해 둔 사본이다.
scp -i ~/.ssh/J15C101T.pem ../../../../tools/deploy/deploy-servers.sh \
    ubuntu@43.202.67.137:~/araatti/deploy-servers.sh
```

⚠ **`deploy-servers.sh` 는 서버에 사본이 따로 있다.** 저장소에서 고쳐도 올리지 않으면 서버는 옛 것으로
돈다. 서버 실행 인자(`-devmode` 등)를 바꿨다면 반드시 같이 올린다.

### 4. 교체

```bash
ssh -i ~/.ssh/J15C101T.pem ubuntu@43.202.67.137 'cd ~/araatti && ./deploy-servers.sh'
sudo mv ~/araatti/staging/AraAtti.zip /var/www/araatti/AraAtti.zip
sudo chown www-data:www-data /var/www/araatti/AraAtti.zip
```

`deploy-servers.sh` 는 옛 빌드를 `~/araatti/backup-<날짜>/` 로 옮겨 둔다.
잘못됐으면 그걸 되돌리면 된다. 되돌리는 명령은 스크립트가 실행 중에 찍어 준다.

## 개발자 모드 (`-devmode`)

시연을 Release 빌드로 하면서 개발자 패널(배·검 게임에서 **P**)로 판을 빨리 넘긴다.
**클라이언트와 서버가 둘 다** `-devmode` 로 떠야 한다. 클라이언트만 켜면 패널은 열리지만
서버가 명령을 듣지 않는다.

| 어디 | 무엇 |
|---|---|
| 클라이언트 | `pack-client.ps1` 이 만드는 `게임시작.bat` 의 `set DEVMODE=-devmode` |
| 서버 7대 | `deploy-servers.sh` 의 `DEVMODE=-devmode` |

지금은 우리끼리만 플레이하므로 **늘 켜 둔다.** 끄려면 두 곳의 값을 비우고 다시 배포한다
(다시 빌드할 필요는 없다). 코드 쪽 설명은 `Assets/Game/Scripts/Core/DevMode.cs`.

## 걸려 넘어졌던 것들

**돌고 있는 실행 파일은 못 덮는다.** 리눅스에서 `Text file busy` 가 난다.
그래서 `deploy-servers.sh` 는 반드시 멈추고 → 풀고 → 다시 띄우는 순서로 간다.

**`pgrep -f` 는 자기 자신을 잡는다.** `pgrep -f "AraAtti"` 가 그 명령을 담은
ssh 명령줄까지 잡아서, 죽이려던 것 말고 엉뚱한 것이 남은 적이 있다.
`[A]raAtti` 처럼 대괄호를 쓰면 패턴이 자기 명령줄과 안 맞는다.

**`-publicip` 이 없으면 아무도 못 붙는다.** Fusion 이 자기 주소를 사설 IP 로 알리기 때문이다.
포트는 열려 있고 로그도 멀쩡해서 원인을 찾기 어렵다.

**`.bat` 은 CRLF + CP949 로 써야 한다.** LF 로만 쓰면 `cmd.exe` 가 `set HOST=...` 을
못 읽어 `%HOST%` 가 빈 값이 되고, 클라이언트가 `-api http://:5080` 을 받아
`UriFormatException` 으로 죽는다.

**`.ps1` 은 UTF-8 BOM 으로 써야 한다.** PowerShell 5.1 은 BOM 이 없으면 CP949 로 읽어서
한글 주석이 깨지고 한글 파일명(`게임시작.bat`) 만들기가 실패한다.

**서버만 새로 올리면 안 된다.** 클라이언트와 버전이 어긋나면
`Behaviour count mismatch` 가 난다. 항상 한 벌로 같이 올린다.

**`appsettings.Development.json` 이 publish 에 따라온다.** 그 안에 각자 PC 의 DB 비밀번호와
JWT 서명 키가 들어 있다. 실제로 EC2 에 한 번 올라갔다.
지금은 `AraAtti.Api.csproj` 의 `CopyToPublishDirectory="Never"` 로 막아 두었고,
`deploy-servers.sh` 도 혹시 섞여 오면 지운다.

## 아직 안 한 것

**`systemd` 유닛이 없다.** 지금은 `nohup` 으로 띄워서, EC2 가 재부팅되면 전부 내려간다.
다시 띄우려면 사람이 `deploy-servers.sh` 를 실행해야 한다.

**완전 자동화(push 하면 배포)는 안 했다.** Unity 빌드에는 라이선스가 활성화된 Unity 가
깔린 기계가 필요해서, GitLab Runner 를 개발 PC 에 따로 등록해야 한다.
클라이언트가 386MB 라 매 커밋마다 굽고 올리는 것도 현실적이지 않다.

## 절대 하지 말 것

- **`sudo shutdown` / `poweroff` / `halt`** — AWS 콘솔 접근 권한이 없어서 다시 못 켠다.
- MySQL 컨테이너·볼륨 삭제, 다른 팀원 프로세스 종료
- SSH 포트(22) 차단, 공개키 삭제, `/home` 퍼미션 변경 — 접속 불가 시 초기화 요청만 가능하다
