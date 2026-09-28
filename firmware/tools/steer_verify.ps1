# 조타(양손 휠) 측정 검증
#
#   완드 2대를 양손에 들고 휠을 돌리는 동안 동글 시리얼을 녹화하고,
#   유니티가 쓰는 것과 **같은 식**으로 조타값을 계산해 본다.
#
#   판정하는 것
#     1. 두 완드가 다 들어오는가            (HasTwoDevices 가 설 수 있는가)
#     2. 각 손의 roll 이 실제로 움직이는가  (IMU 가 붙어 있는가)
#     3. 두 손의 부호가 같은가 반대인가     ← mirroredGrip 을 정하는 값
#     4. 왼쪽/오른쪽이 구분되는가           (최종 판정)
#
#   쓰는 법
#     powershell -ExecutionPolicy Bypass -File steer_verify.ps1 -Port COM8
#
#   녹화만 해두고 나중에 다시 보고 싶으면 -Csv 로 파일을 준다.
#     steer_verify.ps1 -Csv wheel.csv          이미 녹화한 파일을 재생만 한다

[CmdletBinding()]
param(
    [string] $Port,
    [int]    $Baud     = 115200,
    [int]    $Seconds  = 20,
    [int]    $LeftId   = 0,
    [int]    $RightId  = 1,
    [string] $Csv
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------
# 1. 줄 모으기 — 시리얼에서 녹화하거나, 이미 있는 파일을 읽는다
# ------------------------------------------------------------

function Read-FromSerial {
    param([string] $PortName, [int] $BaudRate, [int] $Duration)

    $sp = New-Object System.IO.Ports.SerialPort $PortName, $BaudRate, 'None', 8, 'One'

    # ⚠ RTS 는 건드리지 않는다. DTR 과 함께 토글하면 ESP32 가 다운로드 모드로 들어가
    #    아무것도 안 보내게 된다.
    $sp.DtrEnable = $false
    $sp.RtsEnable = $false

    $sp.Open()
    try {
        Write-Host ""
        Write-Host "  녹화 시작 — $Duration 초" -ForegroundColor Cyan
        Write-Host "  완드 2대를 양손에 들고, 휠을 잡듯 쥐고" -ForegroundColor Cyan
        Write-Host "  왼쪽 끝까지 --> 가운데 --> 오른쪽 끝까지 를 두세 번 반복하세요." -ForegroundColor Cyan
        Write-Host ""

        $buffer = ''
        $watch  = [Diagnostics.Stopwatch]::StartNew()
        $tick   = 0

        while ($watch.Elapsed.TotalSeconds -lt $Duration) {
            $buffer += $sp.ReadExisting()
            Start-Sleep -Milliseconds 50

            $elapsed = [int]$watch.Elapsed.TotalSeconds
            if ($elapsed -ne $tick) {
                $tick = $elapsed
                Write-Host "`r  $($Duration - $tick) 초 남음   " -NoNewline
            }
        }

        Write-Host "`r  녹화 끝            "
        return $buffer -split "`n"
    }
    finally {
        if ($sp.IsOpen) { $sp.Close() }
    }
}

if ($Csv -and (Test-Path $Csv) -and -not $Port) {
    $lines = Get-Content $Csv
    Write-Host "  기록 재생: $Csv"
}
else {
    if (-not $Port) { throw "-Port 또는 이미 녹화된 -Csv 가 필요합니다." }

    $lines = Read-FromSerial -PortName $Port -BaudRate $Baud -Duration $Seconds

    if ($Csv) {
        $lines | Set-Content -Path $Csv -Encoding utf8
        Write-Host "  기록 저장: $Csv"
    }
}

# ------------------------------------------------------------
# 2. 파싱 — 유니티의 TryParse 와 같은 규칙
#    '#' 로 시작하는 줄은 동글 로그다. 10필드보다 많으면 초과분은 버린다.
# ------------------------------------------------------------

$samples = @()

foreach ($line in $lines) {
    $text = $line.Trim()
    if ($text -eq '' -or $text.StartsWith('#')) { continue }

    $f = $text -split ','
    if ($f.Count -lt 10) { continue }

    $id = 0; $tilt = 0; $ms = 0
    if (-not [int]::TryParse($f[0], [ref]$id))   { continue }
    if (-not [int]::TryParse($f[4], [ref]$tilt)) { continue }
    if (-not [int]::TryParse($f[9], [ref]$ms))   { continue }

    $samples += [pscustomobject]@{
        Id   = $id
        # 유니티와 같은 정규화: packet.Tilt / 127 을 -1~+1 로 자른다
        Tilt = [Math]::Max(-1.0, [Math]::Min(1.0, $tilt / 127.0))
        Ms   = $ms
    }
}

Write-Host ""
Write-Host "=== 1. 받은 줄 ===" -ForegroundColor Yellow
Write-Host "  해석된 CSV 줄: $($samples.Count)"

if ($samples.Count -eq 0) {
    Write-Host "  줄이 하나도 없습니다. 동글이 꽂혀 있는지, 포트 번호가 맞는지 보세요." -ForegroundColor Red
    exit 1
}

$byId = $samples | Group-Object Id
foreach ($g in $byId) {
    Write-Host "  완드 id=$($g.Name) : $($g.Count) 줄"
}

$leftSamples  = @($samples | Where-Object { $_.Id -eq $LeftId })
$rightSamples = @($samples | Where-Object { $_.Id -eq $RightId })

if ($leftSamples.Count -eq 0 -or $rightSamples.Count -eq 0) {
    Write-Host ""
    Write-Host "  왼손(id=$LeftId) 또는 오른손(id=$RightId) 완드가 안 들어옵니다." -ForegroundColor Red
    Write-Host "  양손 조타는 2대가 다 있어야 성립합니다. 여기서 멈춥니다." -ForegroundColor Red
    exit 1
}

# ------------------------------------------------------------
# 3. 각 손의 roll 이 실제로 움직였는가
#    IMU 가 없는 보드(HAS_IMU 0)로 구우면 tilt 가 늘 0 으로 나온다.
# ------------------------------------------------------------

function Get-Span {
    param($Set)
    $stat = $Set | ForEach-Object { $_.Tilt } | Measure-Object -Minimum -Maximum
    [pscustomobject]@{
        Min  = $stat.Minimum
        Max  = $stat.Maximum
        Span = $stat.Maximum - $stat.Minimum
    }
}

$leftSpan  = Get-Span $leftSamples
$rightSpan = Get-Span $rightSamples

Write-Host ""
Write-Host "=== 2. 손별 roll 움직임 ===" -ForegroundColor Yellow
Write-Host ("  왼손  min {0,6:F2}  max {1,6:F2}  폭 {2,5:F2}" -f $leftSpan.Min, $leftSpan.Max, $leftSpan.Span)
Write-Host ("  오른손 min {0,6:F2}  max {1,6:F2}  폭 {2,5:F2}" -f $rightSpan.Min, $rightSpan.Max, $rightSpan.Span)

# 폭이 이만큼도 안 움직이면 신호로 쓸 수 없다. ±0.2 는 roll 로 약 ±14도다.
$MinimumSpan = 0.4

$deadHands = @()
if ($leftSpan.Span  -lt $MinimumSpan) { $deadHands += "왼손" }
if ($rightSpan.Span -lt $MinimumSpan) { $deadHands += "오른손" }

if ($deadHands.Count -gt 0) {
    Write-Host ""
    Write-Host "  $($deadHands -join ' · ') 의 roll 이 거의 안 움직였습니다 (폭 < $MinimumSpan)." -ForegroundColor Red
    Write-Host "  그 완드에 IMU 가 없거나(HAS_IMU 0), 녹화 중에 그 손을 안 돌린 것입니다." -ForegroundColor Red
    exit 1
}

# ------------------------------------------------------------
# 4. 두 손의 부호 관계 — mirroredGrip 을 정하는 값
#
#    같은 시각의 두 손을 짝지어야 한다. 완드마다 ms 원점이 달라서 시각으로는 못 맞춘다.
#    두 줄이 번갈아 들어오므로 **들어온 순서**로 가장 가까운 짝을 만든다.
# ------------------------------------------------------------

$pairCount = [Math]::Min($leftSamples.Count, $rightSamples.Count)
$pairs = for ($i = 0; $i -lt $pairCount; $i++) {
    [pscustomobject]@{
        Left  = $leftSamples[$i].Tilt
        Right = $rightSamples[$i].Tilt
    }
}

# 피어슨 상관계수. +1 이면 두 손이 같이 움직이고, -1 이면 정반대로 움직인다.
$leftMean  = ($pairs | Measure-Object Left  -Average).Average
$rightMean = ($pairs | Measure-Object Right -Average).Average

$cov = 0.0; $varL = 0.0; $varR = 0.0
foreach ($p in $pairs) {
    $dl = $p.Left  - $leftMean
    $dr = $p.Right - $rightMean
    $cov  += $dl * $dr
    $varL += $dl * $dl
    $varR += $dr * $dr
}

$correlation = if ($varL -gt 0 -and $varR -gt 0) { $cov / [Math]::Sqrt($varL * $varR) } else { 0.0 }

Write-Host ""
Write-Host "=== 3. 두 손의 부호 관계 ===" -ForegroundColor Yellow
Write-Host ("  상관계수: {0,6:F3}   (짝 {1} 개)" -f $correlation, $pairCount)

# 0.5 미만이면 두 손이 같은 동작을 하고 있다고 보기 어렵다.
if ([Math]::Abs($correlation) -lt 0.5) {
    Write-Host ""
    Write-Host "  두 손이 함께 움직이지 않았습니다." -ForegroundColor Red
    Write-Host "  한 손만 돌렸거나, 휠 동작이 아니라 제각각 흔든 기록입니다. 다시 녹화하세요." -ForegroundColor Red
    exit 1
}

$mirrored = $correlation -lt 0

if ($mirrored) {
    Write-Host "  두 손의 roll 이 **반대 방향**입니다 (마주 잡기)." -ForegroundColor Green
    Write-Host "  => IotPlayerController 의 Mirrored Grip 을 [켜세요]" -ForegroundColor Green
}
else {
    Write-Host "  두 손의 roll 이 **같은 방향**입니다." -ForegroundColor Green
    Write-Host "  => IotPlayerController 의 Mirrored Grip 을 [끄세요]" -ForegroundColor Green
}

# ------------------------------------------------------------
# 5. 최종 판정 — 그 설정으로 조타가 좌우 구분되는가
#    유니티 경로와 같다:  Wand.Tilt (마주 잡기면 왼손 부호 반전)
#                        -> ShipCoopInput.Steer = (Left + Right) / 2
# ------------------------------------------------------------

$leftSign = if ($mirrored) { -1.0 } else { 1.0 }

$steer = $pairs | ForEach-Object { ($_.Left * $leftSign + $_.Right) * 0.5 }

$steerStat = $steer | Measure-Object -Minimum -Maximum

# 상쇄되지 않고 살아남았는지 보려면, 부호를 잘못 잡았을 때와 견줘야 한다.
$wrongSteer = $pairs | ForEach-Object { ($_.Left * -$leftSign + $_.Right) * 0.5 }
$wrongStat  = $wrongSteer | Measure-Object -Minimum -Maximum

Write-Host ""
Write-Host "=== 4. 조타값 (ShipCoopInput.Steer 와 같은 계산) ===" -ForegroundColor Yellow
Write-Host ("  맞는 설정 : {0,6:F2} ~ {1,6:F2}   폭 {2,5:F2}" -f `
        $steerStat.Minimum, $steerStat.Maximum, ($steerStat.Maximum - $steerStat.Minimum))
Write-Host ("  틀린 설정 : {0,6:F2} ~ {1,6:F2}   폭 {2,5:F2}   <- 상쇄되어 죽는 쪽" -f `
        $wrongStat.Minimum, $wrongStat.Maximum, ($wrongStat.Maximum - $wrongStat.Minimum))

$goodSpan = $steerStat.Maximum - $steerStat.Minimum
$leftEnd  = $steerStat.Minimum
$rightEnd = $steerStat.Maximum

Write-Host ""
Write-Host "=== 판정 ===" -ForegroundColor Yellow

$ok = $true

if ($goodSpan -lt $MinimumSpan) {
    Write-Host "  [실패] 조타값이 거의 안 움직입니다." -ForegroundColor Red
    $ok = $false
}

# 좌우가 구분되려면 한쪽은 음수, 한쪽은 양수로 가야 한다.
if ($leftEnd -gt -0.15 -or $rightEnd -lt 0.15) {
    Write-Host "  [실패] 좌우 한쪽으로만 값이 나옵니다. 중립이 치우쳐 있습니다." -ForegroundColor Red
    Write-Host ("         왼쪽 끝 {0,5:F2} · 오른쪽 끝 {1,5:F2} (둘 다 0 을 넘어가야 함)" -f $leftEnd, $rightEnd) -ForegroundColor Red
    $ok = $false
}

if ($ok) {
    Write-Host "  [통과] 좌우가 구분됩니다." -ForegroundColor Green
    Write-Host ("         왼쪽으로 돌리면 {0,5:F2} · 오른쪽으로 돌리면 {1,5:F2}" -f $leftEnd, $rightEnd) -ForegroundColor Green
    Write-Host ""
    Write-Host "  유니티에 넣을 값" -ForegroundColor Green
    Write-Host "    Left Hand Id  = $LeftId"
    Write-Host "    Right Hand Id = $RightId"
    Write-Host "    Mirrored Grip = $(if ($mirrored) { '켬' } else { '끔' })"
}

exit $(if ($ok) { 0 } else { 1 })
