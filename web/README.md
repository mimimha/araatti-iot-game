# araatti.site 홈페이지

게임 아라아띠의 소개·다운로드 페이지 `https://araatti.site` 의 소스입니다.

AI 로 작업하신다면 AI 가 [AGENTS.md](AGENTS.md) 를 먼저 읽게 해 주세요. 하면 안 되는 것이 거기 모여 있습니다.
(Claude Code 는 `CLAUDE.md` 를 통해 자동으로 읽습니다.)

## 준비 (처음 한 번)

1. **Node.js 20.19 이상** — `node -v` 로 확인합니다.
2. **서버 접속 키** — SSAFY 에서 받은 `J15C101T.pem` 을 `~/.ssh/` 에 둡니다.
   (Windows: `C:\Users\<이름>\.ssh\J15C101T.pem`)
   다른 곳에 두셨다면 환경 변수 `ARAATTI_PEM` 에 경로를 넣어 주세요.
   **이 저장소 폴더 안에는 두지 마세요.**
3. 설치:
   ```
   cd web
   npm install
   ```

## 쓰는 법

| 명령 | 하는 일 |
|---|---|
| `npm run dev` | 내 PC 에서 미리보기 — http://localhost:5173 |
| `npm run deploy:dry` | 빌드하고 검사하고 서버에 접속만 해 봅니다. **아무것도 안 바꿉니다.** |
| `npm run deploy` | 빌드해서 araatti.site 에 올리고, 실제로 떴는지 확인합니다. |

`npm run deploy` 끝에 이 두 줄이 나오면 성공입니다:

```
✅ https://araatti.site/  방금 올린 index.html 과 같음
✅ https://araatti.site/AraAtti.zip  408 MB
```

브라우저에서 **Ctrl+F5** 로 새로고침하면 바뀐 페이지가 보입니다.

## 꼭 지켜 주세요

- **배포는 `npm run deploy` 로만** 해 주세요. 서버 사이트 폴더에는 게임 다운로드 파일(`AraAtti.zip`)이
  같이 들어 있어서, 직접 지우거나 덮어쓰면 다운로드가 끊깁니다. 스크립트가 이 파일을 지켜 줍니다.
- 다운로드 버튼 두 개(`/AraAtti.zip`, 구글 드라이브)는 디자인을 바꿔도 **남겨 주세요.**
- 같은 서버에서 게임 서버와 DB 가 돌고 있습니다. 서버에 접속해 **`/var/www/araatti/` 밖은 건드리지 마세요.**
  SSH 설정·권한을 잘못 바꾸면 서버를 복구할 수 없습니다.
- 올려 보고 괜찮으면 **커밋 → MR** 까지 해 주세요. 그래야 다음 사람이 이어서 고칠 수 있습니다.

## React 등으로 바꾸고 싶다면

됩니다. `npm run build` 가 `dist/index.html` 을 만들기만 하면 배포 스크립트가 그대로 동작합니다.
자세한 조건(정적 사이트여야 함, 페이지를 여러 개 만들 때 서버 설정 변경 필요)은 [AGENTS.md](AGENTS.md) 에 있습니다.
페이지 주소를 여러 개(`/about` 등) 만드실 거면 건희에게 먼저 말해 주세요. 서버 설정을 한 줄 바꿔야 합니다.

## 잘못 올렸을 때

배포할 때마다 직전 사이트가 서버 `~/araatti/web-backup-<시각>/` 에 백업됩니다(최근 5개).
되돌리는 명령은 `npm run deploy` 가 끝에 찍어 줍니다. 아니면 이전 커밋으로 돌아가서 `npm run deploy` 를
다시 하면 됩니다.

## 자주 나는 문제

**`UNPROTECTED PRIVATE KEY FILE`** — 키 파일 권한이 너무 넓어서 ssh 가 거부한 것입니다.
- Windows: 키 파일 우클릭 → 속성 → 보안 → 고급 → **상속 사용 안 함** → 본인 계정만 "읽기" 로 남기고 나머지를 지웁니다.
- macOS/Linux: `chmod 400 ~/.ssh/J15C101T.pem`

**`ssh 를 실행하지 못했습니다`** — Windows 10 이상에는 기본으로 들어 있습니다.
설정 → 앱 → 선택적 기능 → **OpenSSH 클라이언트** 가 켜져 있는지 확인해 주세요.

**서버에 올라간 `index.html` 이 소스와 다르게 생겼어요** — 빌드할 때 CSS 가 압축됩니다. 화면은 같습니다. 정상입니다.
