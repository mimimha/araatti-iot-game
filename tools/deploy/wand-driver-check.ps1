#
# 아라아띠 — 완드 동글 드라이버(CP210x) 확인 · 설치
#
#   게임시작.bat 이 게임을 켜기 전에 부른다. 배포 zip 의 WandDriver\ 폴더에 들어간다.
#   게임시작.bat 은 이 PC 에 동글이 꽂힌 적이 있을 때(레지스트리 Enum\USB\VID_10C4&PID_EA6x)만 부른다 —
#   키보드만 쓰는 사람은 PowerShell 조차 켜지 않아서 게임이 늦게 뜨지 않는다.
#
#   동글이 꽂혀 있고 드라이버가 없을 때만   관리자 확인 창 → Silicon Labs CP210x 드라이버 설치
#   그 밖(동글 없음 · 이미 잡혀 있음)        아무것도 묻지 않고 바로 끝난다 → 게임이 켜진다
#
#   확인 창에서 [아니요] 를 눌러도 게임은 켜진다(키보드로 한다). 다음 실행 때 다시 묻는다.
#
# ─────────────────────────────────────────────────────────────────────────
# ⚠ 드라이버 설치는 Windows 에서 늘 관리자 권한이 필요하다. 확인 창(UAC)은 없앨 수 없다.
#    그래서 "필요한 사람에게 · 처음 한 번만" 뜨게 한다.
# ⚠ 동글 판별은 USB 칩 번호로 한다. Silicon Labs = VID_10C4,
#    CP2102/CP2104 = PID_EA60 · CP2105 = EA70 · CP2108 = EA71.
# ⚠ 이 파일은 UTF-8 BOM 으로 저장한다. PowerShell 5.1 은 BOM 이 없으면 한글이 깨진다.
# ⚠ 드라이버는 Silicon Labs 라이선스상 제품과 함께만 배포할 수 있다(단독 배포 금지).
#    CP210x\SLAB_License_Agreement_VCP_Windows.txt 를 같이 넣는다.
# ─────────────────────────────────────────────────────────────────────────

$ErrorActionPreference = 'SilentlyContinue'

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$inf  = Join-Path $here 'CP210x\silabser.inf'

# 지금 꽂혀 있는 동글. Get-PnpDevice 는 처음 불리면 모듈을 불러오느라 8초가 걸려서(실측) WMI 로 묻는다(1초 안팎).
# ⚠ WQL 의 LIKE 에서 백슬래시는 이스케이프라 쓰지 않고, _ 는 한 글자 와일드카드라 [_] 로 적는다.
$dongles = @(Get-CimInstance -ClassName Win32_PnPEntity -Filter "DeviceID LIKE '%VID[_]10C4%'" |
    Where-Object { $_.DeviceID -match 'VID_10C4&PID_EA(60|70|71)' })

if ($dongles.Count -eq 0) {
    # 동글이 없다 — 키보드로 한다. 아무것도 묻지 않는다.
    exit 0
}

# ConfigManagerErrorCode 0 = 정상. 드라이버가 없으면 28 이다.
if (@($dongles | Where-Object { $_.ConfigManagerErrorCode -ne 0 }).Count -eq 0) {
    # 이미 드라이버가 잡혀 COM 포트로 보인다.
    exit 0
}

if (-not (Test-Path $inf)) {
    Write-Host '  완드 드라이버 파일이 없습니다. 키보드로 시작합니다.'
    exit 0
}

Write-Host ''
Write-Host '  완드 동글을 찾았지만 드라이버가 없습니다.'
Write-Host '  드라이버를 설치합니다 — 관리자 확인 창이 뜨면 [예] 를 눌러 주세요. (처음 한 번만)'
Write-Host ''

try {
    $p = Start-Process -FilePath 'pnputil.exe' `
        -ArgumentList @('/add-driver', "`"$inf`"", '/install') `
        -Verb RunAs -Wait -PassThru -WindowStyle Hidden -ErrorAction Stop

    # 0 = 설치됨 · 3010 = 설치됨(재부팅 권장) · 259 = 설치됐지만 바로 붙은 장치 없음
    if ($p.ExitCode -in 0, 3010, 259) {
        Write-Host '  완드 드라이버를 설치했습니다.'
        # 장치가 새 드라이버로 다시 잡히는 동안 잠깐 기다린다 — 그래야 게임이 COM 포트를 찾는다.
        Start-Sleep -Seconds 3
    }
    else {
        Write-Host ("  드라이버 설치가 끝나지 않았습니다 (코드 {0}). 키보드로 시작합니다." -f $p.ExitCode)
    }
}
catch {
    # [아니요] 를 눌렀거나 관리자 권한이 없는 PC.
    Write-Host '  드라이버 설치를 건너뛰었습니다. 키보드로 시작합니다. 다음 실행 때 다시 묻습니다.'
}

exit 0
