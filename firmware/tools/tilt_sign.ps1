# TILT_SIGN 과 mirroredGrip 을 한 번에 정한다  (IotPlayerController.md 7-5)
#
#   동글 없이 완드 2대를 USB 로 직접 꽂고 #TILT 로그만 읽는다.
#   포트가 모자랄 때 쓰는 길이다. 동글이 있으면 steer_verify.ps1 쪽이 50Hz 라 더 낫다.
#
#   순서 — **다 돌린 끝 자세**를 잰다. 돌리는 동작이 아니다.
#     초읽기 5초 동안 자세를 만들고, "측정" 뒤 6초는 굳어 있는다.
#     1) 기준    조타 자세 그대로
#     2) 오른쪽  오른쪽 끝까지 돌린 끝 자세
#     3) 왼쪽    왼쪽 끝까지 돌린 끝 자세
#
#   쓰는 법
#     powershell -ExecutionPolicy Bypass -File tilt_sign.ps1
#     powershell -ExecutionPolicy Bypass -File tilt_sign.ps1 -LeftPort COM7 -RightPort COM13
#     powershell -ExecutionPolicy Bypass -File tilt_sign.ps1 -SelfTest      보드 없이 판정부만 검사

[CmdletBinding()]
param(
    [string] $LeftPort  = 'COM7',    # 왼손으로 쓸 완드 (leftHandId)
    [string] $RightPort = 'COM13',   # 오른손으로 쓸 완드 (rightHandId)
    [int]    $Baud      = 115200,
    [int]    $PhaseSeconds = 6,
    [switch] $SelfTest
)

$ErrorActionPreference = 'Stop'

# 이만큼(도)도 안 움직였으면 신호로 쓸 수 없다.
$MinimumDelta = 20

# 측정 중 화면에 흔들림을 눈에 띄게 표시할 기준(도). 판정은 이 값이 아니라
# 움직인 폭 대비로 한다. (Show-Verdict)
$MaxSpread = 15

# 구간별 · 손별 흔들림. Measure-Phase 가 채우고 마지막에 한꺼번에 본다.
$script:PhaseSpread = @{}

# 두 각도의 차이를 ±180 안으로 되감는다. 원 위에서 가까운 쪽으로 간 거리다.
function Get-WrappedDelta {
    param([double] $Angle, [double] $Reference)

    $d = $Angle - $Reference
    while ($d -gt  180) { $d -= 360 }
    while ($d -le -180) { $d += 360 }
    return [Math]::Round($d, 1)
}

# 소리는 안내일 뿐이다. 콘솔이 없는 환경에서 Beep 이 막혀도 측정은 그대로 진행한다.
function Send-Beep {
    param([int] $Hz, [int] $Ms)
    try { [Console]::Beep($Hz, $Ms) } catch { Start-Sleep -Milliseconds $Ms }
}

# 말로 알린다.
#
# 양손에 완드를 들면 화면을 못 본다. [Console]::Beep 은 메인보드 비프로 나가서
# 요즘 노트북에서는 아예 안 들리는 경우가 있다 — 실측에서 구간을 놓쳤다.
# SAPI 는 기본 오디오 장치로 나가므로 이어폰·스피커로 들린다.
$script:Voice = $null
try { $script:Voice = New-Object -ComObject SAPI.SpVoice } catch { }

function Say {
    param([string] $Text)

    # 1 = SVSFlagsAsync. 말하는 동안 측정이 멈추면 안 된다.
    if ($null -ne $script:Voice) {
        try { $script:Voice.Speak($Text, 1) | Out-Null; return } catch { }
    }

    Write-Host "    ($Text)" -ForegroundColor DarkCyan
}

# ------------------------------------------------------------
# 판정 — 잰 값 세 벌을 받아 TILT_SIGN 과 mirroredGrip 을 정한다
#
# 포트를 안 보므로 -SelfTest 로 합성 값을 넣어 그대로 검사할 수 있다.
# 되돌려주는 값은 종료 코드다. 0 이면 통과.
# ------------------------------------------------------------

function Show-Verdict {
    param($Neutral, $Right, $Left, $Spread)

    $keys = @('왼손', '오른손')

    Write-Host ""
    Write-Host "=== 잰 각도 (grav 로 다시 계산, 자르지 않음) ===" -ForegroundColor Yellow
    Write-Host ("  {0,-8} {1,8} {2,8} {3,8}" -f '', '기준', '오른쪽', '왼쪽')

    foreach ($key in $keys) {
        Write-Host ("  {0,-8} {1,8} {2,8} {3,8}" -f $key, $Neutral[$key], $Right[$key], $Left[$key])
    }

    $missing = @($keys | Where-Object { $null -eq $Neutral[$_] -or $null -eq $Right[$_] -or $null -eq $Left[$_] })
    if ($missing.Count -gt 0) {
        Write-Host ""
        Write-Host "  $($missing -join ' · ') 에서 #TILT 가 하나도 안 왔습니다." -ForegroundColor Red
        Write-Host "  그 완드가 HAS_IMU 0 으로 구워졌거나 IMU 초기화에 실패한 것입니다." -ForegroundColor Red
        return 1
    }

    # 판정은 **오른쪽 끝과 왼쪽 끝만** 비교한다. 기준 구간은 쓰지 않는다.
    #
    # ⚠ 기준을 빼는 방식은 실측에서 두 번 깨졌다. 조타 자세로 가만히 있으라고 해도
    #    그 자세가 스윙 범위의 가운데라는 보장이 없다. 실제로 오른손 기준이 -80도로
    #    나왔는데 스윙 범위는 -62 ~ +87 이었다. 기준이 왼쪽 끝 바깥에 있어서
    #    "왼쪽으로 돌렸는데 기준보다 오른쪽" 이 됐다.
    #
    #    좌우 극값끼리 빼면 기준이 어디에 있든 상관없다. 우리가 알고 싶은 것은
    #    "오른쪽으로 돌릴 때 각도가 어느 쪽으로 가는가" 뿐이다.
    #
    # ⚠ 여기서는 되감지 **않는다.** 들어오는 값이 이미 이어 붙인(펼친) 각도라
    #    209도를 돌리면 209도 그대로 들어온다. 여기서 되감으면 -151도로 둔갑해
    #    부호가 뒤집힌다. 되감기는 표본과 표본 사이에서만 한다. (Update-Wands)
    $swing = @{}
    foreach ($key in $keys) {
        $swing[$key] = [Math]::Round($Right[$key] - $Left[$key], 1)
    }

    Write-Host ""
    Write-Host "=== 왼쪽 끝 → 오른쪽 끝 ===" -ForegroundColor Yellow
    foreach ($key in $keys) {
        # 부호를 늘 보이게 한다. 판정이 부호로 갈리므로 눈으로 따라갈 수 있어야 한다.
        Write-Host ("  {0,-8} {1,8:+0.0;-0.0;0.0} 도" -f $key, $swing[$key])
    }

    $dead = @($keys | Where-Object { [Math]::Abs($swing[$_]) -lt $MinimumDelta })

    if ($dead.Count -gt 0) {
        Write-Host ""
        Write-Host "  $($dead -join ' · ') 이 거의 안 움직였습니다 (|좌우 폭| < $MinimumDelta 도)." -ForegroundColor Red
        Write-Host "  그 손을 실제로 돌렸는지, IMU 가 붙어 있는지 보고 다시 재세요." -ForegroundColor Red
        return 1
    }

    # 구간 안 흔들림이 폭에 비해 큰지 본다.
    #
    # ⚠ 고정 한계(±15도)로 막았더니 멀쩡한 기록을 세 번 연속 버렸다. 좌우 폭이
    #    190도인데 ±25도 흔들려도 **부호는 안 바뀐다.** 우리가 알고 싶은 것은
    #    부호뿐이므로, 흔들림은 폭 대비로 본다.
    #
    #    기준 구간은 보지 않는다. 판정에 안 쓰는 값이다.
    if ($null -ne $Spread) {
        $shaky = @()

        foreach ($key in $keys) {
            $worst = 0.0
            foreach ($phase in @('오른쪽', '왼쪽')) {
                $s = $Spread["$phase|$key"]
                if ($null -ne $s -and $s -gt $worst) { $worst = $s }
            }

            if ($worst -gt ([Math]::Abs($swing[$key]) / 3)) {
                $shaky += "$key (흔들림 ±$worst 도, 폭 $([Math]::Abs($swing[$key])) 도)"
            }
        }

        if ($shaky.Count -gt 0) {
            Write-Host ""
            Write-Host "  움직인 폭에 비해 자세가 너무 흔들렸습니다." -ForegroundColor Red
            foreach ($s in $shaky) { Write-Host "    $s" -ForegroundColor Red }
            Write-Host "  IMU 가 손에 대해 움직였을 수 있습니다. 고정을 보고 다시 재세요." -ForegroundColor Red
            return 1
        }
    }

    # 1. mirroredGrip — 두 손이 반대로 움직이면 왼손 부호를 뒤집어야 평균이 산다
    $mirrored = [Math]::Sign($swing['왼손']) -ne [Math]::Sign($swing['오른손'])
    $leftSign = if ($mirrored) { -1 } else { 1 }

    # 2. TILT_SIGN — 보정을 넣은 뒤 "오른쪽으로 돌리면 양수" 가 되는가
    #    ShipCoopInput.Steer 와 같은 평균이다. 좌우 폭의 절반이 한쪽 진폭이다.
    $steerRight = ($swing['왼손'] * $leftSign + $swing['오른손']) / 4.0
    $steerLeft  = -$steerRight

    $tiltSign = if ($steerRight -gt 0) { '+1' } else { '-1' }

    Write-Host ""
    Write-Host "=== 판정 ===" -ForegroundColor Yellow
    Write-Host ("  조타값  오른쪽 {0,7:+0.0;-0.0;0.0}   왼쪽 {1,7:+0.0;-0.0;0.0}   (도)" -f `
            $steerRight, $steerLeft)
    Write-Host ""

    if ($mirrored) {
        Write-Host "  두 손의 roll 이 반대 방향 → 마주 잡기" -ForegroundColor Green
        Write-Host "  [유니티]  IotPlayerController 의 Mirrored Grip 을 **켜세요**" -ForegroundColor Green
    }
    else {
        Write-Host "  두 손의 roll 이 같은 방향" -ForegroundColor Green
        Write-Host "  [유니티]  IotPlayerController 의 Mirrored Grip 을 **끄세요**" -ForegroundColor Green
    }

    Write-Host ""

    if ($steerRight -gt 0) {
        Write-Host "  오른쪽으로 돌릴 때 조타값이 양수입니다." -ForegroundColor Green
        Write-Host "  [펌웨어]  TILT_SIGN = +1 **그대로 두세요**. 다시 구울 필요 없습니다." -ForegroundColor Green
    }
    else {
        Write-Host "  오른쪽으로 돌릴 때 조타값이 음수입니다. 좌우가 뒤집혀 있습니다." -ForegroundColor Red
        Write-Host "  [펌웨어]  wand_tinys3.ino 의 TILT_SIGN 을 **-1 로 바꿔 두 보드 다 다시 구우세요**." -ForegroundColor Red
        Write-Host "            (mirroredGrip 은 위에 나온 대로 두면 됩니다. 둘은 별개입니다.)" -ForegroundColor Red
    }

    Write-Host ""
    Write-Host "  요약:  TILT_SIGN = $tiltSign   ·   Mirrored Grip = $(if ($mirrored) { '켬' } else { '끔' })"

    return 0
}

# ------------------------------------------------------------
# 자체 검사 — 보드 없이 판정부만 돌린다
# ------------------------------------------------------------

if ($SelfTest) {
    # 값은 전부 **도(degree)** 다. 기준 각도가 손마다 다른 것까지 넣어 둔다.
    $cases = @(
        @{ Name = '마주 잡기 · TILT_SIGN 맞음'
           N = @{ '왼손' = 15; '오른손' = 5 }
           R = @{ '왼손' = -45; '오른손' = 65 }
           L = @{ '왼손' = 75; '오른손' = -55 }
           Expect = 0 }

        @{ Name = '같은 방향 · TILT_SIGN 맞음'
           N = @{ '왼손' = 15; '오른손' = 5 }
           R = @{ '왼손' = 75; '오른손' = 65 }
           L = @{ '왼손' = -45; '오른손' = -55 }
           Expect = 0 }

        @{ Name = '같은 방향 · TILT_SIGN 뒤집힘'
           N = @{ '왼손' = 15; '오른손' = 5 }
           R = @{ '왼손' = -45; '오른손' = -55 }
           L = @{ '왼손' = 75; '오른손' = 65 }
           Expect = 0 }

        # ★ 2026-09-23 실측. 왼손이 +103 -> -106 으로 **209도**를 돌았다.
        #   되감으면 -151도가 되어 부호가 뒤집힌다. 펼친 각도는 209 그대로여야 한다.
        @{ Name = '실측 회귀 — 좌우 폭이 180도를 넘음'
           N = @{ '왼손' = 71.6; '오른손' = -50.6 }
           R = @{ '왼손' = 103.0; '오른손' = 101.2 }
           L = @{ '왼손' = -106.3; '오른손' = -103.0 }
           Expect = 0 }

        # ★ 2026-09-23 실측. 기준 구간이 스윙 범위 바깥(-80도)이라 예전 판정은 실패했다.
        #   좌우 극값만 보면 두 손 다 +130~150도로 같은 방향이라 답이 나온다.
        @{ Name = '실측 회귀 — 기준이 스윙 범위 바깥'
           N = @{ '왼손' = -38.1; '오른손' = -80.0 }
           R = @{ '왼손' = 61.3;  '오른손' = 87.1 }
           L = @{ '왼손' = -71.2; '오른손' = -62.1 }
           Expect = 0 }

        @{ Name = '오른손이 안 움직임'
           N = @{ '왼손' = 15; '오른손' = 5 }
           R = @{ '왼손' = 75; '오른손' = 6 }
           L = @{ '왼손' = -45; '오른손' = 5 }
           Expect = 1 }

        @{ Name = '좌우를 같은 자세로 쟀음'
           N = @{ '왼손' = 15; '오른손' = 5 }
           R = @{ '왼손' = 75; '오른손' = 65 }
           L = @{ '왼손' = 78; '오른손' = 69 }
           Expect = 1 }

        @{ Name = '#TILT 가 안 옴 (HAS_IMU 0)'
           N = @{ '왼손' = 15; '오른손' = $null }
           R = @{ '왼손' = 75; '오른손' = $null }
           L = @{ '왼손' = -45; '오른손' = $null }
           Expect = 1 }

        # ★ 2026-09-23 실측. 폭 190/160도에 흔들림 ±25도면 부호는 안 바뀐다.
        #   고정 한계 ±15도로 막던 시절에 멀쩡한 기록을 버렸다.
        @{ Name = '흔들렸지만 폭이 충분함 — 통과해야 함'
           N = @{ '왼손' = -0.8; '오른손' = -44.1 }
           R = @{ '왼손' = 112.7; '오른손' = 76.9 }
           L = @{ '왼손' = -77.5; '오른손' = -83.5 }
           S = @{ '오른쪽|오른손' = 25.1; '기준|오른손' = 33.9; '기준|왼손' = 20.6 }
           Expect = 0 }

        # 폭(60도)에 비해 흔들림(40도)이 커서 부호를 믿을 수 없다.
        @{ Name = '폭에 비해 흔들림이 큼 — 막아야 함'
           N = @{ '왼손' = 0; '오른손' = 0 }
           R = @{ '왼손' = 30; '오른손' = 30 }
           L = @{ '왼손' = -30; '오른손' = -30 }
           S = @{ '오른쪽|오른손' = 40.0 }
           Expect = 1 }
    )

    $failed = 0

    foreach ($c in $cases) {
        Write-Host ""
        Write-Host "######## $($c.Name) ########" -ForegroundColor Magenta

        $code = Show-Verdict -Neutral $c.N -Right $c.R -Left $c.L -Spread $c.S

        if ($code -eq $c.Expect) {
            Write-Host "  -> 기대대로 (종료 코드 $code)" -ForegroundColor DarkGreen
        }
        else {
            Write-Host "  -> 어긋남. 기대 $($c.Expect), 실제 $code" -ForegroundColor Red
            $failed++
        }
    }

    Write-Host ""
    if ($failed -eq 0) { Write-Host "자체 검사 $($cases.Count) 건 모두 통과" -ForegroundColor Green }
    else               { Write-Host "자체 검사 $failed 건 실패" -ForegroundColor Red }

    exit $(if ($failed -eq 0) { 0 } else { 1 })
}

# ------------------------------------------------------------
# 포트 열기
#
# ⚠ RTS 는 건드리지 않는다. DTR 과 함께 토글하면 ESP32 가 다운로드 모드로 들어가
#    아무것도 안 보내게 된다.
# ------------------------------------------------------------

function Open-Wand {
    param([string] $Name)

    $s = New-Object System.IO.Ports.SerialPort $Name, $Baud, 'None', 8, 'One'
    $s.DtrEnable = $false
    $s.RtsEnable = $false
    $s.Open()
    return $s
}

$hands = [ordered]@{}

try {
    $hands['왼손']   = @{ Port = Open-Wand $LeftPort;  Buf = '' }
    $hands['오른손'] = @{ Port = Open-Wand $RightPort; Buf = '' }
}
catch {
    Write-Host "포트를 열지 못했습니다 — $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "아두이노 시리얼 모니터나 유니티 Play 가 포트를 잡고 있지 않은지 보세요." -ForegroundColor Red
    foreach ($h in $hands.Values) { if ($h.Port.IsOpen) { $h.Port.Close() } }
    exit 1
}

# ------------------------------------------------------------
# 양쪽 포트를 읽어 각도를 **이어 붙인다**
#
# 처음부터 끝까지 쉬지 않고 불러야 한다. 표본 사이가 벌어지면 이어 붙일 수 없다.
# ------------------------------------------------------------

$script:LastRaw   = @{}   # 직전 원각도 (-180 ~ 180)
$script:Unwrapped = @{}   # 펼친 누적 각도. ±180 을 넘어서도 이어진다
$script:Collected = @{}   # 지금 구간에서 모은 펼친 각도

function Update-Wands {
    param([bool] $Collect)

    foreach ($key in $hands.Keys) {
        $hands[$key].Buf += $hands[$key].Port.ReadExisting()

        # 완성된 줄만 떼어낸다. 마지막 조각은 다음 회차로 넘긴다.
        $text = $hands[$key].Buf
        $cut  = $text.LastIndexOf("`n")
        if ($cut -lt 0) { continue }

        $ready = $text.Substring(0, $cut)
        $hands[$key].Buf = $text.Substring($cut + 1)

        foreach ($line in ($ready -split "`n")) {
            # "#TILT   15  grav -0.02 -0.18 -0.98"
            #
            # ⚠ 앞의 정수(tilt)는 쓰지 않는다. 펌웨어가 ±90도를 ±127 로 잘라서 내보내는데,
            #    휠을 잡는 자세는 그 바깥이라 레일에 박힌다. 실측에서 오른손이 기준·오른쪽
            #    두 구간 모두 -127 로 나와 변화량이 0 이었다.
            #
            #    grav 는 자르지 않은 중력 원값이라 여기서 각도를 다시 낸다.
            #    펌웨어와 같은 식이다:  roll = atan2(-ay, -az)
            if ($line -notmatch '^#TILT\s+-?\d+\s+grav\s+([-+]?[\d.]+)\s+([-+]?[\d.]+)\s+([-+]?[\d.]+)') {
                continue
            }

            $ay  = [double]$matches[2]
            $az  = [double]$matches[3]
            $raw = [Math]::Atan2(-$ay, -$az) * 57.2958

            if (-not $script:LastRaw.ContainsKey($key)) {
                $script:Unwrapped[$key] = $raw
            }
            else {
                # 직전 표본에서 얼마나 움직였는지만 더한다. 한 번에 180도를 넘지
                # 않는 한(1Hz 라도 손동작은 그 안이다) 몇 바퀴를 돌려도 이어진다.
                $script:Unwrapped[$key] += Get-WrappedDelta $raw $script:LastRaw[$key]
            }

            $script:LastRaw[$key] = $raw

            if ($Collect) { $script:Collected[$key] += $script:Unwrapped[$key] }
        }
    }
}

# ------------------------------------------------------------
# 한 구간 동안 양쪽 포트를 같이 읽고 #TILT 값만 모은다
# ------------------------------------------------------------

function Measure-Phase {
    param([string] $Title, [string] $Instruction, [string] $Spoken)

    Write-Host ""
    Write-Host "  [$Title]  $Instruction" -ForegroundColor Cyan

    # 자세를 잡을 시간을 준다. 앞 구간 자세가 섞이면 안 된다.
    #
    # 양손에 완드를 들면 화면을 못 본다. 낮은 소리로 초읽기를 하고,
    # 측정이 시작될 때 구간마다 다른 횟수로 높은 소리를 내 귀로 따라갈 수 있게 한다.
    Say $Spoken

    # ⚠ 초읽기 동안에도 계속 읽는다. **각도를 이어 붙이기 위해서다.**
    #
    #    끝점만 보면 +103도와 -106도 사이가 209도인지 -151도인지 알 수 없다.
    #    ±180 으로 되감으면 209도가 -151도로 둔갑해 부호가 통째로 뒤집힌다.
    #    실측에서 실제로 209도를 돌렸다.
    #
    #    쉬지 않고 읽으면서 매 표본의 변화량을 더해 나가면(펼치기) 한 바퀴를
    #    넘겨도 값이 이어진다. 그래서 구간 사이 이동 구간도 버리지 않는다.
    for ($i = 5; $i -ge 1; $i--) {
        Write-Host "`r    준비 $i ..." -NoNewline -ForegroundColor DarkGray
        if ($i -le 3) { Say "$i" }

        $tick = [Diagnostics.Stopwatch]::StartNew()
        while ($tick.Elapsed.TotalMilliseconds -lt 1000) {
            Update-Wands $false
            Start-Sleep -Milliseconds 50
        }
    }

    Write-Host "`r    측정 중 — 그대로 멈춰 계세요       " -NoNewline -ForegroundColor Yellow
    Say '그대로 멈춰 계세요'
    Send-Beep 1200 120

    foreach ($key in $hands.Keys) { $script:Collected[$key] = @() }

    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $PhaseSeconds) {
        Update-Wands $true
        Start-Sleep -Milliseconds 50
    }

    $result = @{}

    foreach ($key in $hands.Keys) {
        $values = @($script:Collected[$key])

        if ($values.Count -eq 0) {
            $result[$key] = $null
        }
        else {
            # 펼친 각도라 원형 평균이 필요 없다. 이어져 있으므로 그냥 평균낸다.
            $sum = 0.0
            foreach ($deg in $values) { $sum += $deg }
            $mean = $sum / $values.Count
            $result[$key] = [Math]::Round($mean, 1)

            # 구간 안에서 얼마나 흔들렸는지 따로 잰다.
            #
            # ⚠ 자세를 유지하라고 해도 값이 흔들릴 수 있다. 실측에서 IMU 보드를 붙인
            #    테이프가 떨어져 센서가 손에 대해 돌아간 적이 있다. 그러면 평균은
            #    떨어지기 전과 후의 중간값이 되어 **그럴듯하지만 틀린 각도**가 나온다.
            #    구간 안 편차를 같이 보고, 크면 그 구간을 버린다.
            $spread = 0.0
            foreach ($deg in $values) {
                $d = [Math]::Abs($deg - $mean)
                if ($d -gt $spread) { $spread = $d }
            }
            $script:PhaseSpread["$Title|$key"] = [Math]::Round($spread, 1)
        }
    }

    $shown = (@('왼손', '오른손') | ForEach-Object {
        $s = $script:PhaseSpread["$Title|$_"]
        "$_ $($result[$_])도" + $(if ($null -ne $s -and $s -ge $MaxSpread) { " (흔들림 ±$s!)" } else { "" })
    }) -join '   '
    Write-Host "`r    $shown                    " -ForegroundColor Yellow

    return $result
}

Write-Host ""
Write-Host "  왼손  = $LeftPort" -ForegroundColor Gray
Write-Host "  오른손 = $RightPort" -ForegroundColor Gray
Write-Host "  #TILT 은 1초에 한 번만 나옵니다. 자세를 잡으면 $PhaseSeconds 초 동안 그대로 유지하세요."

Write-Host ""
Write-Host "  소리로 따라가세요 — 낮은 소리 5번은 준비, 높은 소리가 측정 시작입니다." -ForegroundColor Gray
Write-Host "    삐 1번 = 기준(가만히)   삐 2번 = 오른쪽 끝   삐 3번 = 왼쪽 끝" -ForegroundColor Gray
Write-Host "    긴 소리 = 끝. 그때까지 자세를 유지하세요." -ForegroundColor Gray

try {
    # 재는 것은 **다 돌린 끝 자세**이지 돌리는 동작이 아니다.
    # 초읽기 동안 돌려서 자세를 만들고, "측정" 뒤에는 굳어 있어야 한다.
    # 안내 문구가 애매하면 측정 중에도 계속 돌려서 구간이 통째로 흔들린다.
    $neutral = Measure-Phase '기준'   '조타 자세 그대로 가만히'    '조타 자세를 잡고 멈추세요'
    $right   = Measure-Phase '오른쪽' '오른쪽 끝 자세로 멈춤'      '오른쪽 끝까지 돌린 다음 멈추세요'
    $left    = Measure-Phase '왼쪽'   '왼쪽 끝 자세로 멈춤'        '왼쪽 끝까지 돌린 다음 멈추세요'
}
finally {
    foreach ($h in $hands.Values) { if ($h.Port.IsOpen) { $h.Port.Close() } }
}

Say '끝났습니다'
Send-Beep 600 700   # 끝. 이제 손을 내려도 된다
Start-Sleep -Seconds 2   # 비동기로 말하므로 끝말이 잘리지 않게 기다린다

exit (Show-Verdict -Neutral $neutral -Right $right -Left $left -Spread $script:PhaseSpread)
