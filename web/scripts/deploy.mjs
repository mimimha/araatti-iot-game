// araatti.site 에 빌드 결과(dist/)를 올린다.
//
//   npm run deploy:dry   빌드 + 검사 + 서버 접속 확인만. 아무것도 안 바꾼다.
//   npm run deploy       빌드 + 올리기 + 확인
//
// 하는 일
//   1. dist/ 를 검사한다       index.html 이 있는가, 지키는 파일을 덮어쓰려 하지 않는가
//   2. 묶는다                  .deploy/site.tar.gz
//   3. EC2 로 보낸다           ~/web-staging/
//   4. 서버에서 바꾼다          지금 사이트를 백업 → 지키는 파일만 남기고 비움 → 새 파일 → 권한
//   5. 실제로 떠 있는지 본다    https://araatti.site/ 와 다운로드 파일
//
// ⚠ AraAtti.zip 은 게임 다운로드 파일이다(400MB 남짓). 사이트 폴더에 같이 들어 있지만
//    이 저장소에서 만드는 것이 아니다. 지우면 게임을 다시 빌드해야 되살릴 수 있다.
//    그래서 PROTECTED 에 적힌 이름은 비우지도, 덮어쓰지도 않는다.
//
// 키는 ARAATTI_PEM 환경 변수로 준다. 없으면 ~/.ssh/J15C101T.pem 을 찾는다.

import { spawnSync } from 'node:child_process'
import { createHash } from 'node:crypto'
import { existsSync, mkdirSync, readFileSync, readdirSync, statSync } from 'node:fs'
import { homedir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const HOST = 'ubuntu@43.202.67.137'
const SITE_URL = 'https://araatti.site'
const LIVE_SITE = '/var/www/araatti'

/**
 * 올릴 서버 폴더. 평소에는 실제 사이트다.
 *
 * ARAATTI_SITE_DIR 로 다른 폴더를 주면 그쪽에 올린다. **이 스크립트 자체를 고쳤을 때**
 * 진짜 사이트를 건드리기 전에 가짜 폴더에서 먼저 돌려 보려고 둔 것이다. 그때는 사이트가
 * 바뀌지 않으므로 5단계(웹 확인)를 건너뛴다.
 */
const REMOTE_SITE = process.env.ARAATTI_SITE_DIR || LIVE_SITE
const SANDBOX = REMOTE_SITE !== LIVE_SITE

// ⚠ Git Bash 는 `/home/...` 같은 값을 Windows 경로(`C:/Program Files/Git/home/...`)로 바꿔서
//    넘긴다. 그대로 쓰면 서버에서 엉뚱한 경로를 찾는다. 실제로 그렇게 됐다.
if (!/^\/[^:\\]*$/.test(REMOTE_SITE)) {
  console.error(`\n❌ 서버 폴더 경로가 이상합니다: ${REMOTE_SITE}`)
  console.error('   /home/ubuntu/... 처럼 / 로 시작하는 서버 경로여야 합니다.')
  console.error('   Git Bash 라면 명령 앞에 MSYS_NO_PATHCONV=1 을 붙여 주세요.')
  process.exit(1)
}

/** 사이트 폴더에 있지만 이 저장소가 만들지 않는 것. 비우지도 덮어쓰지도 않는다. */
const PROTECTED = ['AraAtti.zip']

/** 다운로드 파일이 이보다 작으면 뭔가 잘못된 것이다. */
const ZIP_MIN_BYTES = 100 * 1024 * 1024

const DRY_RUN = process.argv.includes('--dry-run')
// import.meta.dirname 은 Node 20.11 부터라 쓰지 않는다. 팀원 PC 의 Node 가 그보다 낮을 수 있다.
const WEB_ROOT = fileURLToPath(new URL('..', import.meta.url))
const DIST = join(WEB_ROOT, 'dist')

// ─────────────────────────────── 도우미 ───────────────────────────────

function step(title) {
  console.log(`\n── ${title} ${'─'.repeat(Math.max(0, 50 - title.length))}`)
}

function fail(message, hint) {
  console.error(`\n❌ ${message}`)
  if (hint) console.error(`   ${hint.split('\n').join('\n   ')}`)
  process.exit(1)
}

/**
 * 명령을 돌린다. 실패하면 멈춘다.
 *
 * ⚠ tar 에는 상대 경로만 넘긴다. Git Bash 의 GNU tar 는 `C:\...` 의 콜론을
 *    "원격 호스트:" 로 읽어서 엉뚱한 곳에 접속하려 한다. 그래서 cwd 를 web/ 로 두고
 *    `.deploy/site.tar.gz`, `dist` 처럼 상대 경로를 쓴다.
 */
function run(cmd, args, { input, capture = false } = {}) {
  // 출력을 붙잡아 두면 키 권한 오류 같은 것을 읽어서 알아듣게 설명할 수 있다.
  const r = spawnSync(cmd, args, {
    cwd: WEB_ROOT,
    input,
    encoding: 'utf8',
    stdio: ['pipe', 'pipe', 'pipe'],
  })

  if (r.error) {
    fail(`${cmd} 를 실행하지 못했습니다: ${r.error.message}`,
      `${cmd} 가 설치되어 있고 PATH 에 있는지 확인해 주세요.`)
  }

  // capture 면 부른 쪽이 돌려받아 쓴다. 아니면 그대로 보여 준다.
  if (!capture && r.stdout) process.stdout.write(r.stdout)

  if (r.status !== 0) {
    if (capture && r.stdout) process.stdout.write(r.stdout)
    if (r.stderr) process.stderr.write(r.stderr)

    if (/UNPROTECTED PRIVATE KEY FILE|bad permissions/i.test(r.stderr ?? '')) {
      fail('키 파일 권한이 너무 넓어서 ssh 가 거부했습니다.',
        'Windows: 키 파일 우클릭 → 속성 → 보안 → 고급 → 상속 사용 안 함 →\n' +
        '         본인 계정만 "읽기" 로 남기고 나머지 사용자를 지웁니다.\n' +
        'macOS/Linux: chmod 400 <키 파일>')
    }
    fail(`${cmd} 가 실패했습니다 (종료 코드 ${r.status}).`)
  }

  return r.stdout ?? ''
}

function findKey() {
  const fromEnv = process.env.ARAATTI_PEM
  if (fromEnv) {
    if (!existsSync(fromEnv)) fail(`ARAATTI_PEM 이 가리키는 파일이 없습니다: ${fromEnv}`)
    return fromEnv
  }

  const fallback = join(homedir(), '.ssh', 'J15C101T.pem')
  if (existsSync(fallback)) return fallback

  fail('서버 접속 키(.pem)를 찾지 못했습니다.',
    'SSAFY 에서 받은 J15C101T.pem 을 ~/.ssh/ 에 두거나,\n' +
    '환경 변수 ARAATTI_PEM 에 키 파일 경로를 넣어 주세요.\n' +
    '⚠ 키 파일을 이 저장소 폴더 안에 두지 마세요. 실수로 커밋될 수 있습니다.')
}

function listFiles(dir, base = dir) {
  const out = []
  for (const name of readdirSync(dir)) {
    const full = join(dir, name)
    if (statSync(full).isDirectory()) out.push(...listFiles(full, base))
    else out.push(full.slice(base.length + 1).replaceAll('\\', '/'))
  }
  return out
}

function gitInfo() {
  const g = (...a) => spawnSync('git', a, { cwd: WEB_ROOT, encoding: 'utf8' }).stdout?.trim() ?? ''
  return {
    commit: g('rev-parse', '--short', 'HEAD') || 'unknown',
    branch: g('rev-parse', '--abbrev-ref', 'HEAD') || 'unknown',
    dirty: g('status', '--porcelain', '--', '.') !== '',
    who: g('config', 'user.name') || 'unknown',
  }
}

const sha256 = (buf) => createHash('sha256').update(buf).digest('hex')

// ─────────────────────────────── 1. 검사 ───────────────────────────────

step('1. 빌드 결과 검사')

if (!existsSync(join(DIST, 'index.html'))) {
  fail('dist/index.html 이 없습니다.', '빌드가 실패했거나 출력 폴더가 dist/ 가 아닙니다. vite.config.js 의 outDir 을 확인해 주세요.')
}

const files = listFiles(DIST)
const clash = files.filter((f) => PROTECTED.includes(f))
if (clash.length > 0) {
  fail(`빌드 결과에 ${clash.join(', ')} 가 들어 있습니다. 서버의 게임 다운로드 파일을 덮어쓰게 됩니다.`,
    'public/ 이나 빌드 설정에서 그 파일을 빼 주세요.')
}

const totalBytes = files.reduce((n, f) => n + statSync(join(DIST, f)).size, 0)
console.log(`  파일 ${files.length}개, ${(totalBytes / 1024).toFixed(1)} KB`)
if (totalBytes > 50 * 1024 * 1024) {
  console.warn('  ⚠ 50MB 가 넘습니다. 큰 동영상이나 이미지가 들어갔는지 확인해 주세요.')
}

const git = gitInfo()
console.log(`  커밋 ${git.commit} (${git.branch})`)
if (git.dirty) {
  console.warn('  ⚠ web/ 에 커밋하지 않은 변경이 있습니다. 올라간 사이트가 git 어디에도 없는 상태가 됩니다.')
  console.warn('    확인이 끝나면 꼭 커밋하고 MR 을 올려 주세요.')
}

const key = findKey()
console.log(`  키 ${key}`)

// ─────────────────────────────── 2. 묶기 ───────────────────────────────

step('2. 묶기')

mkdirSync(join(WEB_ROOT, '.deploy'), { recursive: true })
run('tar', ['-czf', '.deploy/site.tar.gz', '-C', 'dist', '.'])
console.log(`  .deploy/site.tar.gz  ${(statSync(join(WEB_ROOT, '.deploy', 'site.tar.gz')).size / 1024).toFixed(1)} KB`)

const ssh = ['-i', key, '-o', 'StrictHostKeyChecking=accept-new', '-o', 'ConnectTimeout=15']

// ─────────────────────────────── 여기서 멈추기 ───────────────────────────────

if (DRY_RUN) {
  step('서버 접속 확인 (읽기만)')
  const now = run('ssh', [...ssh, HOST, `ls -la ${REMOTE_SITE}`], { capture: true })
  console.log(now.replace(/^/gm, '  '))

  step('끝 (--dry-run)')
  console.log('  아무것도 바꾸지 않았습니다. 실제로 올리려면 npm run deploy')
  process.exit(0)
}

// ─────────────────────────────── 3. 보내기 ───────────────────────────────

step('3. 서버로 보내기')

run('ssh', [...ssh, HOST, 'mkdir -p ~/web-staging'])
run('scp', [...ssh, '.deploy/site.tar.gz', `${HOST}:~/web-staging/site.tar.gz`])
console.log('  보냈습니다.')

// ─────────────────────────────── 4. 바꾸기 ───────────────────────────────

step('4. 서버에서 바꾸기')

// find 로 "지키는 파일이 아닌 것" 을 고른다. ! -name A ! -name B ...
const keepArgs = PROTECTED.map((p) => `! -name '${p}'`).join(' ')
const note = `${new Date().toISOString()}  ${git.who}  ${git.commit} (${git.branch})${git.dirty ? '  +커밋안한변경' : ''}  → ${REMOTE_SITE}`

const remote = `
set -euo pipefail
SITE='${REMOTE_SITE}'
STAGE="$HOME/web-staging"
STAMP=$(date +%Y%m%d-%H%M%S)
BACKUP="$HOME/araatti/web-backup-$STAMP"

# 사이트 폴더가 있어야 한다. 없는데 진행하면 엉뚱한 곳에 파일을 쏟는다.
test -d "$SITE" || { echo "서버에 $SITE 가 없습니다. 멈춥니다."; exit 1; }

# 먼저 풀어서 검사한다. 사이트는 아직 그대로다.
rm -rf "$STAGE/new" && mkdir -p "$STAGE/new"
tar -xzf "$STAGE/site.tar.gz" -C "$STAGE/new"
test -f "$STAGE/new/index.html" || { echo "받은 묶음에 index.html 이 없습니다. 멈춥니다."; exit 1; }
for p in ${PROTECTED.map((p) => `'${p}'`).join(' ')}; do
  if [ -e "$STAGE/new/$p" ]; then echo "받은 묶음이 $p 를 덮어쓰려 합니다. 멈춥니다."; exit 1; fi
  test -e "$SITE/$p" || echo "⚠ 서버에 $p 가 없습니다. (이번 배포와는 무관하지만 확인이 필요합니다)"
done

# 지금 사이트를 백업한다. 지키는 파일은 크고 바뀌지 않으므로 빼고.
mkdir -p "$BACKUP"
sudo find "$SITE" -mindepth 1 -maxdepth 1 ${keepArgs} -exec cp -a {} "$BACKUP/" \\;

# 지키는 파일만 남기고 비운 뒤 새 파일을 넣는다.
sudo find "$SITE" -mindepth 1 -maxdepth 1 ${keepArgs} -exec rm -rf {} +
sudo cp -a "$STAGE/new/." "$SITE/"
sudo chown -R www-data:www-data "$SITE"

test -f "$SITE/index.html"

# 누가 언제 무엇을 올렸는지 남긴다.
echo '${note.replaceAll("'", '')}' >> "$HOME/araatti/web-deploys.log"

# 백업은 최근 5개만 남긴다.
ls -1dt "$HOME"/araatti/web-backup-* 2>/dev/null | tail -n +6 | xargs -r rm -rf

echo "  백업   $BACKUP"
echo "  되돌리려면 서버에서:"
echo "    sudo find $SITE -mindepth 1 -maxdepth 1 ${keepArgs.replaceAll("'", '')} -exec rm -rf {} + && sudo cp -a $BACKUP/. $SITE/ && sudo chown -R www-data:www-data $SITE"
`

run('ssh', [...ssh, HOST, 'bash -s'], { input: remote })

// ─────────────────────────────── 5. 확인 ───────────────────────────────

if (SANDBOX) {
  step('끝 (가짜 폴더)')
  console.log(`  ${REMOTE_SITE} 에 올렸습니다. 실제 사이트는 그대로라 웹 확인은 건너뜁니다.`)
  process.exit(0)
}

step('5. 실제로 떠 있는지 확인')

const local = readFileSync(join(DIST, 'index.html'))
const page = await fetch(`${SITE_URL}/`, { cache: 'no-store' })
const served = Buffer.from(await page.arrayBuffer())

if (page.status !== 200) fail(`${SITE_URL}/ 가 ${page.status} 를 돌려줍니다.`)
if (sha256(served) !== sha256(local)) {
  fail('사이트의 index.html 이 방금 올린 것과 다릅니다.', '브라우저·CDN 캐시일 수도 있습니다. 잠시 뒤 다시 확인해 주세요.')
}
console.log(`  ✅ ${SITE_URL}/  방금 올린 index.html 과 같음`)

for (const p of PROTECTED) {
  const head = await fetch(`${SITE_URL}/${p}`, { method: 'HEAD' })
  const size = Number(head.headers.get('content-length') ?? 0)
  if (head.status !== 200) fail(`${SITE_URL}/${p} 가 ${head.status} 를 돌려줍니다. 게임 다운로드가 끊겼습니다.`)
  if (p.endsWith('.zip') && size < ZIP_MIN_BYTES) fail(`${p} 가 ${size} bytes 입니다. 너무 작습니다.`)
  console.log(`  ✅ ${SITE_URL}/${p}  ${(size / 1024 / 1024).toFixed(0)} MB`)
}

step('끝')
console.log(`  ${SITE_URL}/ 에 올라갔습니다. 브라우저에서 Ctrl+F5 로 새로고침해 보세요.`)
