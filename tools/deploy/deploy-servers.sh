#!/usr/bin/env bash
#
# 아라아띠 — EC2 서버 한 벌 교체
#
#   ~/araatti/staging/servers.tar.gz  를 풀어 돌고 있는 서버 7대를 새 빌드로 바꾼다.
#   api.tar.gz 가 같이 있으면 API 도 바꾼다.
#
#   쓰는 법:  ./deploy-servers.sh
#
# ─────────────────────────────────────────────────────────────────────────
# ⚠ 세션 이름과 포트는 클라이언트가 아는 값이다.
#   여기를 고치면 클라이언트도 같이 고쳐야 한다. 안 맞으면 GameNotFound 가 난다.
#   (실제로 lobby-ch1 을 lobby 로 잘못 띄워서 한 번 겪었다)
#
# ⚠ 돌고 있는 실행 파일을 그냥 덮으면 "Text file busy" 가 난다.
#   그래서 반드시 먼저 멈추고 → 풀고 → 다시 띄운다.
#
# ⚠ pgrep -f 는 자기 자신도 잡는다.
#   `[A]raAtti` 처럼 대괄호를 쓰면 패턴이 자기 명령줄과는 안 맞는다.
# ─────────────────────────────────────────────────────────────────────────

set -euo pipefail

ROOT=/home/ubuntu/araatti
STAGE=$ROOT/staging
BACKUP=$ROOT/backup-$(date +%Y%m%d-%H%M%S)

PUBLIC_IP=43.202.67.137    # -publicip. 이게 없으면 바깥에서 아무도 못 붙는다.
APPVER=prod                # -appver / -region 은 세션을 나누는 열쇠다.
REGION=kr                  # 클라이언트와 같은 값이어야 서로 보인다.

# 폴더 | 실행파일 | 세션이름 | 포트 | 로그이름
SERVERS="
Server|AraAtti-Server.x86_64|lobby-ch1|27015|lobby
MineServer|AraAtti-MineServer.x86_64|mine-1|27016|mine-1
MineServer|AraAtti-MineServer.x86_64|mine-2|27017|mine-2
WarriorsServer|AraAtti-WarriorsServer.x86_64|warriors-1|27018|warriors-1
WarriorsServer|AraAtti-WarriorsServer.x86_64|warriors-2|27019|warriors-2
ShipCoopServer|AraAtti-ShipCoopServer.x86_64|shipcoop-1|27020|shipcoop-1
ShipCoopServer|AraAtti-ShipCoopServer.x86_64|shipcoop-2|27021|shipcoop-2
"

say() { echo "  $*"; }
step() { echo; echo "── $* ─────────────────────────────"; }

# ── 0. 올릴 것이 있는지 ────────────────────────────────────────────────
step "0. 확인"
if [ ! -f "$STAGE/servers.tar.gz" ]; then
    echo "  ❌ $STAGE/servers.tar.gz 가 없습니다. 먼저 올려 주세요."
    exit 1
fi
say "새 빌드   $(du -h "$STAGE/servers.tar.gz" | cut -f1)"

PLAYING=$(ss -un 2>/dev/null | grep -cE ':2701[5-9]|:2702[01]' || true)
say "접속 중인 흐름  $PLAYING"
if [ "$PLAYING" -gt 0 ]; then
    say "⚠ 지금 사람이 붙어 있습니다. 재시작하면 끊깁니다."
fi

# ── 1. 멈추기 ──────────────────────────────────────────────────────────
step "1. 서버 멈추기"
PIDS=$(pgrep -f '[A]raAtti.*\.x86_64' || true)
if [ -n "$PIDS" ]; then
    say "멈출 프로세스: $(echo "$PIDS" | tr '\n' ' ')"
    # 먼저 곱게 부탁하고(TERM), 5초 기다렸다가 안 죽으면 강제로 죽인다(KILL).
    # Fusion 은 TERM 을 받으면 세션을 정리하고 나간다.
    kill $PIDS 2>/dev/null || true
    for i in $(seq 1 10); do
        sleep 0.5
        pgrep -f '[A]raAtti.*\.x86_64' >/dev/null || break
    done
    LEFT=$(pgrep -f '[A]raAtti.*\.x86_64' || true)
    [ -n "$LEFT" ] && { say "안 죽어서 강제 종료: $LEFT"; kill -9 $LEFT 2>/dev/null || true; sleep 1; }
    say "멈췄습니다."
else
    say "돌고 있는 서버가 없습니다."
fi

# ── 2. 옛 빌드 보관 ────────────────────────────────────────────────────
step "2. 옛 빌드 보관"
mkdir -p "$BACKUP"
for d in Server MineServer WarriorsServer ShipCoopServer; do
    [ -d "$ROOT/$d" ] && mv "$ROOT/$d" "$BACKUP/"
done
say "보관 위치  $BACKUP"
say "되돌리려면  rm -rf $ROOT/{Server,MineServer,WarriorsServer,ShipCoopServer} && mv $BACKUP/* $ROOT/"

# ── 3. 새 빌드 풀기 ────────────────────────────────────────────────────
step "3. 새 빌드 풀기"
tar -xzf "$STAGE/servers.tar.gz" -C "$ROOT"
for d in Server MineServer WarriorsServer ShipCoopServer; do
    chmod +x "$ROOT/$d"/*.x86_64
    say "$(printf '%-16s' "$d") $(stat -c %y "$ROOT/$d"/*_Data/globalgamemanagers | cut -c1-19)"
done

# ── 4. API 바꾸기 (있을 때만) ──────────────────────────────────────────
if [ -f "$STAGE/api.tar.gz" ]; then
    step "4. API 바꾸기"
    API_PID=$(pgrep -f '[A]raAtti\.Api' || true)
    [ -n "$API_PID" ] && { say "멈춤 pid $API_PID"; kill $API_PID; sleep 2; }

    mv "$ROOT/api" "$BACKUP/api"
    mkdir -p "$ROOT/api"
    tar -xzf "$STAGE/api.tar.gz" -C "$ROOT/api"
    chmod +x "$ROOT/api/AraAtti.Api"

    # api.env 는 서버에만 있는 비밀이다. 배포본에 섞여 오면 안 된다.
    if [ -f "$ROOT/api/appsettings.Development.json" ]; then
        say "⚠ 개발용 설정이 섞여 왔습니다. 지웁니다."
        rm -f "$ROOT/api/appsettings.Development.json"
    fi

    set -a; . "$ROOT/api.env"; set +a
    export ASPNETCORE_ENVIRONMENT=Production
    export ASPNETCORE_URLS=http://0.0.0.0:5080
    cd "$ROOT/api"
    nohup ./AraAtti.Api >> "$ROOT/api.log" 2>&1 &
    say "다시 띄웠습니다. pid $!"
    sleep 4
fi

# ── 5. 서버 다시 띄우기 ────────────────────────────────────────────────
step "5. 서버 다시 띄우기"
echo "$SERVERS" | grep -v '^$' | while IFS='|' read -r dir exe session port log; do
    cd "$ROOT/$dir"
    nohup "./$exe" -batchmode -nographics \
        -logFile "$ROOT/$log.log" \
        -session "$session" -port "$port" \
        -publicip "$PUBLIC_IP" -appver "$APPVER" -region "$REGION" \
        >/dev/null 2>&1 &
    printf "  %-12s 포트 %s  pid %s\n" "$session" "$port" "$!"
done

# ── 6. 확인 ────────────────────────────────────────────────────────────
step "6. 확인 (20초 기다림)"
sleep 20

OK=0
FAIL=0
echo "$SERVERS" | grep -v '^$' | while IFS='|' read -r dir exe session port log; do
    if ss -uln | grep -q ":$port "; then
        printf "  ✅ %-12s 포트 %s 열림\n" "$session" "$port"
    else
        printf "  ❌ %-12s 포트 %s 안 열림 — %s\n" "$session" "$port" "$ROOT/$log.log"
        tail -5 "$ROOT/$log.log" | sed 's/^/       /'
    fi
done

echo
say "돌고 있는 서버 $(pgrep -cf '[A]raAtti.*\.x86_64') 대"
say "API $(curl -s -o /dev/null -w '%{http_code}' --max-time 5 http://127.0.0.1:5080/ 2>/dev/null || echo '응답 없음')  (404 면 정상 — 루트 경로가 없을 뿐)"
echo
echo "=== 끝 ==="
