# araatti.site 홈페이지 — AI 작업 규칙

이 폴더(`web/`)는 게임 **아라아띠**의 소개·다운로드 페이지 `https://araatti.site` 의 소스다.
이 문서는 이 폴더에서 일하는 AI(Claude, Cursor, Copilot 등)가 **작업 전에 반드시 읽는** 규칙이다.
사람을 위한 설명은 [README.md](README.md) 에 있다.

---

## 한눈에

```
web/                     ← 여기서만 일한다
  index.html             지금 홈페이지 전부 (HTML + 인라인 CSS 한 장)
  public/                (만들면) 그대로 복사되는 파일 — 이미지, 파비콘 등
  vite.config.js         빌드 설정. 출력은 dist/
  scripts/deploy.mjs     서버에 올리는 스크립트. 사고를 막는 장치가 들어 있다
  dist/                  빌드 결과. git 에 안 올라간다. 서버에 올라가는 것은 이것이다
```

```
npm install          처음 한 번
npm run dev          로컬 미리보기 (http://localhost:5173)
npm run build        dist/ 생성
npm run deploy:dry   빌드 + 검사 + 서버 접속 확인. 아무것도 안 바꾼다
npm run deploy       빌드 + 서버에 올리기 + 실제로 떴는지 확인
```

서버 구조:

```
araatti.site ─▶ nginx (EC2 43.202.67.137) ─▶ /var/www/araatti/
                                              ├─ index.html, assets/ ...  ← dist/ 가 여기로
                                              └─ AraAtti.zip              ← 게임 다운로드. 게임 배포가 바꾼다
```

`AraAtti.zip` 은 **게임 클라이언트**다. develop 의 게임을 새로 빌드해 배포할 때마다
`tools/deploy/`(`build-release.ps1` → `pack-client.ps1` → 서버로 복사)가 이 파일을 새것으로 바꾼다.
**홈페이지 배포(`web/`)와는 따로 움직인다.** 홈페이지를 올릴 때는 이 파일을 건드리지 않고,
게임을 올릴 때는 홈페이지를 건드리지 않는다.

---

## 절대 하지 말 것

1. **`/var/www/araatti/AraAtti.zip` 을 지우거나 덮어쓰지 않는다.**
   게임 다운로드 파일(400MB 남짓)이다. 사이트 폴더에 같이 있지만 `web/` 이 만드는 파일이 아니라
   게임 배포가 올린다(위 설명). 지우면 게임을 다시 빌드해야 되살릴 수 있다. `deploy.mjs` 가 막고
   있으니 **배포는 반드시 `npm run deploy` 로만** 한다. 서버에서 `rm`, `scp` 로 직접 사이트 폴더를
   만지지 않는다.

2. **다운로드 버튼의 주소 `/AraAtti.zip` 을 바꾸지 않는다.**
   nginx 가 이 주소에 다운로드 헤더를 붙인다(`Content-Disposition`). 주소가 바뀌면 다운로드가 안
   되거나 파일 이름이 달라진다. **게임은 이 사이트에서만 받는다.** 구글 드라이브 등 다른 다운로드
   경로를 넣지 않는다.

3. **`web/` 밖을 건드리지 않는다.** 저장소의 나머지(`unity/`, `server/`, `tools/` …)는 게임이다.

4. **서버에서 `/var/www/araatti/` 밖을 건드리지 않는다.** 같은 EC2 에서 게임 서버 7대·계정 API·MySQL 이
   돈다. 특히 **SSH 설정·방화벽·권한·키(`~/.ssh/authorized_keys`)** 는 절대 바꾸지 않는다.
   SSAFY EC2 는 접속이 막히면 복구가 안 되고 초기화만 된다. **서버 재시작(shutdown, reboot)도 금지.**
   nginx 설정(`/etc/nginx/`)을 바꿔야 하면 멈추고 사람에게 알린다(아래 "라우팅" 참고).

5. **키 파일(`.pem`)을 이 폴더에 두거나 커밋하지 않는다.** 키는 서버 전체 열쇠다.
   `~/.ssh/` 에 두거나 환경 변수 `ARAATTI_PEM` 으로 넘긴다.

6. **`develop`, `master` 에 직접 push 하지 않는다.** 작업 브랜치 → Merge Request 로만 들어간다.
   force push 도 하지 않는다. 규칙은 저장소 최상위 `GIT_CONVENTION.md`.

---

## 프레임워크를 바꿀 때 (React 등)

지금은 HTML 한 장이지만 React·Vue·Svelte 등으로 바꿔도 된다. **규칙은 하나다: `npm run build` 가
`dist/index.html` 을 만들어야 한다.** 그러면 배포 스크립트가 그대로 동작한다.

- **Vite 위에 얹는다.** 이미 Vite 가 깔려 있다. React 면 `@vitejs/plugin-react` 를 추가하고
  `vite.config.js` 에 `plugins: [react()]` 를 넣는다. 출력 폴더 `dist/` 는 바꾸지 않는다.
- **정적 사이트여야 한다.** 서버는 파일을 내줄 뿐 Node 를 돌리지 않는다. Next.js 를 쓰면
  `output: 'export'` 로 정적 내보내기만 가능하고, 출력 폴더를 `dist/` 로 맞춘다. SSR·API 라우트는 안 된다.
- **라우팅(페이지 여러 개)** — `/about` 같은 주소를 클라이언트 라우터로 만들면 지금 nginx 설정
  (`try_files $uri $uri/ =404;`)이 **새로고침 때 404** 를 낸다. `try_files $uri $uri/ /index.html;` 로
  바꿔야 한다. **AI 가 직접 바꾸지 말고 사람에게 요청한다.** 해시 라우터(`/#/about`)면 안 바꿔도 된다.
- 이미지·폰트 등은 `public/` 에 두거나 `import` 한다. **`public/` 에 `AraAtti.zip` 을 만들지 않는다**
  (배포 스크립트가 막는다).

## 알아 둘 것

- **빌드하면 인라인 CSS 가 압축된다.** 서버에 올라간 `index.html` 이 소스와 달라 보여도 정상이다.
  화면은 같다.
- `npm run dev` 에서는 `/AraAtti.zip` 링크가 404 다. 로컬에는 그 파일이 없다. 정상이다.
- 배포할 때 커밋 안 한 변경이 있으면 경고가 뜬다. 확인이 끝나면 **커밋하고 MR 을 올린다.**
  그래야 사이트에 올라간 것이 git 에도 남는다.
- 배포 기록은 서버 `~/araatti/web-deploys.log`, 이전 버전 백업은 `~/araatti/web-backup-<시각>/`
  (최근 5개). 되돌리는 명령은 배포 스크립트가 끝에 찍어 준다.
- HTTPS 인증서는 certbot 이 자동 갱신한다. 손대지 않는다.

## `scripts/deploy.mjs` 를 고쳤다면

**진짜 사이트에 돌리기 전에 가짜 폴더에서 먼저 돌려 본다.** 이 스크립트가 틀리면 게임 다운로드
파일이 지워진다.

```bash
# 서버에 가짜 사이트를 만든다 (가짜 AraAtti.zip 과 지워져야 할 옛 파일을 같이 넣어 둔다)
ssh -i <키> ubuntu@43.202.67.137 'mkdir -p ~/web-sandbox && head -c 2048 /dev/urandom > ~/web-sandbox/AraAtti.zip && echo old > ~/web-sandbox/stale.css'

# 그 폴더에 올린다. Git Bash 는 /home/... 을 Windows 경로로 바꾸므로 MSYS_NO_PATHCONV=1 을 붙인다
MSYS_NO_PATHCONV=1 ARAATTI_SITE_DIR=/home/ubuntu/web-sandbox npm run deploy
```

확인할 것: `AraAtti.zip` 이 그대로인가, `stale.css` 가 지워졌는가, 새 `index.html` 이 들어갔는가,
백업에 옛 파일이 있는가. 끝나면 `~/web-sandbox` 와 방금 생긴 `~/araatti/web-backup-*` 를 지운다.

## 작업 순서

1. 최신 `develop` 에서 브랜치를 딴다. 이름은 `feature/<이니셜>-web-<내용>` (예: `feature/sy-web-redesign`).
2. `web/` 에서 고치고 `npm run dev` 로 본다.
3. `npm run deploy:dry` 로 검사한다.
4. `npm run deploy` 로 올린다. 끝에 ✅ 두 줄(페이지, 다운로드 파일)이 떠야 성공이다.
5. 커밋(`[feat] ...`, `[style] ...` 등 `GIT_CONVENTION.md` 형식) → push → develop 으로 MR.
