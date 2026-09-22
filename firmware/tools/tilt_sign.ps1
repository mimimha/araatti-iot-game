# TILT_SIGN 과 mirroredGrip 을 한 번에 정한다  (IotPlayerController.md 7-5)
#
#   동글 없이 완드 2대를 USB 로 직접 꽂고 #TILT 로그만 읽는다.
#   포트가 모자랄 때 쓰는 길이다. 동글이 있으면 steer_verify.ps1 쪽이 50Hz 라 더 낫다.
#
#   순서 — 화면이 시키는 대로 자세를 잡고 **그대로 유지**한다
#     1) 기준     6초   양손에 하나씩 들고 편하게 (중립값을 잰다)
#     2) 오른쪽   6초   휠 잡듯 쥐고 오른쪽 끝까지 돌려 유지
#     3) 왼쪽     6초   왼쪽 끝까지 돌려 유지
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

# ------------------------------------------------------------
# 판정 — 잰 값 세 벌을 받아 TILT_SIGN 과 mirroredGrip 을 정한다
#
# 포트를 안 보므로 -SelfTest 로 합성 값을 넣어 그대로 검사할 수 있다.
# 되돌려주는 값은 종료 코드다. 0 이면 통과.
# ------------------------------------------------------------

function Show-Verdict {
    param($Neutral, $Right, $Left)

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

    # 기준 자세를 뺀 변화량으로 본다. 완드마다, 잡는 방식마다 기준 각도가 다르다.
    #
    # ⚠ 뺀 뒤 반드시 ±180 안으로 되감는다. 기준이 -175도이고 지금이 +170도면
    #    뺀 값은 345도지만 실제로 움직인 것은 -15도다. 이 되감기가 없으면
    #    경계에 걸친 자세에서 부호가 통째로 뒤집힌다. (실측에서 터진 자리)
    $deltaRight = @{}
    $deltaLeft  = @{}
    foreach ($key in $keys) {
        $deltaRight[$key] = Get-WrappedDelta $Right[$key] $Neutral[$key]
        $deltaLeft[$key]  = Get-WrappedDelta $Left[$key]  $Neutral[$key]
    }

    Write-Host ""
    Write-Host "=== 기준 대비 변화량 ===" -ForegroundColor Yellow
    foreach ($key in $keys) {
        # 부호를 늘 보이게 한다. 판정이 부호로 갈리므로 눈으로 따라갈 수 있어야 한다.
        Write-Host ("  {0,-8} 오른쪽 {1,6:+0;-0;0}   왼쪽 {2,6:+0;-0;0}" -f `
                $key, $deltaRight[$key], $deltaLeft[$key])
    }

    $dead = @($keys | Where-Object {
        [Math]::Abs($deltaRight[$_]) -lt $MinimumDelta -and [Math]::Abs($deltaLeft[$_]) -lt $MinimumDelta
    })

    if ($dead.Count -gt 0) {
        Write-Host ""
        Write-Host "  $($dead -join ' · ') 이 거의 안 움직였습니다 (|변화| < $MinimumDelta)." -ForegroundColor Red
        Write-Host "  그 손을 실제로 돌렸는지, IMU 가 붙어 있는지 보고 다시 재세요." -ForegroundColor Red
        return 1
    }

    # 오른쪽과 왼쪽이 같은 방향으로 갔으면 휠을 돌린 것이 아니다.
    foreach ($key in $keys) {
        if ([Math]::Sign($deltaRight[$key]) -eq [Math]::Sign($deltaLeft[$key])) {
            Write-Host ""
            Write-Host "  $key 이 오른쪽·왼쪽에서 **같은 방향**으로 움직였습니다." -ForegroundColor Red
            Write-Host "  좌우를 반대로 잡았거나 자세가 섞인 기록입니다. 다시 재세요." -ForegroundColor Red
            return 1
        }
    }

    # 1. mirroredGrip — 두 손이 반대로 움직이면 왼손 부호를 뒤집어야 평균이 산다
    $mirrored = [Math]::Sign($deltaRight['왼손']) -ne [Math]::Sign($deltaRight['오른손'])
    $leftSign = if ($mirrored) { -1 } else { 1 }

    # 2. TILT_SIGN — 보정을 넣은 뒤 "오른쪽으로 돌리면 양수" 가 되는가
    #    ShipCoopInput.Steer 와 같은 계산이다.
    $steerRight = ($deltaRight['왼손'] * $leftSign + $deltaRight['오른손']) / 2.0
    $steerLeft  = ($deltaLeft['왼손']  * $leftSign + $deltaLeft['오른손'])  / 2.0

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

        # ★ 실측에서 터진 자리. 기준이 ±180 경계라 그냥 빼면 부호가 뒤집힌다.
        #   왼손 -175 -> 오른쪽 -145 는 +30도, 왼쪽 +155 는 -30도여야 한다.
        @{ Name = '기준이 ±180 경계 (되감기 필요)'
           N = @{ '왼손' = -175; '오른손' = 178 }
           R = @{ '왼손' = -145; '오른손' = -152 }
           L = @{ '왼손' = 155;  '오른손' = 148 }
           Expect = 0 }

        @{ Name = '오른손이 안 움직임'
           N = @{ '왼손' = 15; '오른손' = 5 }
           R = @{ '왼손' = 75; '오른손' = 6 }
           L = @{ '왼손' = -45; '오른손' = 5 }
           Expect = 1 }

        @{ Name = '좌우가 같은 방향 (자세 섞임)'
           N = @{ '왼손' = 15; '오른손' = 5 }
           R = @{ '왼손' = 75; '오른손' = 65 }
           L = @{ '왼손' = 85; '오른손' = 75 }
           Expect = 1 }

        @{ Name = '#TILT 가 안 옴 (HAS_IMU 0)'
           N = @{ '왼손' = 15; '오른손' = $null }
           R = @{ '왼손' = 75; '오른손' = $null }
           L = @{ '왼손' = -45; '오른손' = $null }
           Expect = 1 }
    )

    $failed = 0

    foreach ($c in $cases) {
        Write-Host ""
        Write-Host "######## $($c.Name) ########" -ForegroundColor Magenta

        $code = Show-Verdict -Neutral $c.N -Right $c.R -Left $c.L

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
# 한 구간 동안 양쪽 포트를 같이 읽고 #TILT 값만 모은다
# ------------------------------------------------------------

function Measure-Phase {
    param([string] $Title, [string] $Instruction, [int] $Beeps)

    Write-Host ""
    Write-Host "  [$Title]  $Instruction" -ForegroundColor Cyan

    # 자세를 잡을 시간을 준다. 앞 구간 자세가 섞이면 안 된다.
    #
    # 양손에 완드를 들면 화면을 못 본다. 낮은 소리로 초읽기를 하고,
    # 측정이 시작될 때 구간마다 다른 횟수로 높은 소리를 내 귀로 따라갈 수 있게 한다.
    for ($i = 5; $i -ge 1; $i--) {
        Write-Host "`r    준비 $i ..." -NoNewline -ForegroundColor DarkGray
        Send-Beep 440 80
        Start-Sleep -Milliseconds 920
    }

    Write-Host "`r    측정 중       " -NoNewline -ForegroundColor Yellow
    for ($i = 1; $i -le $Beeps; $i++) { Send-Beep 1200 120; Start-Sleep -Milliseconds 80 }

    foreach ($h in $hands.Values) {
        [void]$h.Port.ReadExisting()   # 앞 구간에 밀린 것을 버린다
        $h.Buf = ''
    }

    $watch = [Diagnostics.Stopwatch]::StartNew()
    while ($watch.Elapsed.TotalSeconds -lt $PhaseSeconds) {
        foreach ($h in $hands.Values) { $h.Buf += $h.Port.ReadExisting() }
        Start-Sleep -Milliseconds 50
    }

    $result = @{}

    foreach ($key in $hands.Keys) {
        $values = @()

        foreach ($line in ($hands[$key].Buf -split "`n")) {
            # "#TILT   15  grav -0.02 -0.18 -0.98"
            #
            # ⚠ 앞의 정수(tilt)는 쓰지 않는다. 펌웨어가 ±90도를 ±127 로 잘라서 내보내는데,
            #    휠을 잡는 자세는 그 바깥이라 레일에 박힌다. 실측에서 오른손이 기준·오른쪽
            #    두 구간 모두 -127 로 나와 변화량이 0 이었다.
            #
            #    grav 는 자르지 않은 중력 원값이라 여기서 각도를 다시 낸다.
            #    펌웨어와 같은 식이다:  roll = atan2(-ay, -az)
            if ($line -match '^#TILT\s+-?\d+\s+grav\s+([-+]?[\d.]+)\s+([-+]?[\d.]+)\s+([-+]?[\d.]+)') {
                $ay = [double]$matches[2]
                $az = [double]$matches[3]
                $values += [Math]::Atan2(-$ay, -$az) * 57.2958
            }
        }

        if ($values.Count -eq 0) {
            $result[$key] = $null
        }
        else {
            # ⚠ 각도는 그냥 평균내면 안 된다. +179 와 -179 는 2도 차이인데 평균은 0 이 된다.
            #    휠 잡는 자세가 하필 그 경계라 실제로 터진 자리다.
            #    사인·코사인을 평균내고 다시 각도로 돌린다.
            $sumSin = 0.0; $sumCos = 0.0
            foreach ($deg in $values) {
                $rad = $deg / 57.2958
                $sumSin += [Math]::Sin($rad)
                $sumCos += [Math]::Cos($rad)
            }
            $result[$key] = [Math]::Round([Math]::Atan2($sumSin, $sumCos) * 57.2958, 1)
        }
    }

    $shown = (@('왼손', '오른손') | ForEach-Object { "$_ $($result[$_])도" }) -join '   '
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
    $neutral = Measure-Phase '기준'   '양손에 하나씩 들고 편하게 — 움직이지 마세요'    1
    $right   = Measure-Phase '오른쪽' '휠 잡듯 쥐고 오른쪽 끝까지 돌려서 유지'          2
    $left    = Measure-Phase '왼쪽'   '왼쪽 끝까지 돌려서 유지'                          3
}
finally {
    foreach ($h in $hands.Values) { if ($h.Port.IsOpen) { $h.Port.Close() } }
}

Send-Beep 600 700   # 끝. 이제 손을 내려도 된다

exit (Show-Verdict -Neutral $neutral -Right $right -Left $left)
