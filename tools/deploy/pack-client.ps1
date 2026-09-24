#
# 아라아띠 — 배포용 클라이언트 꾸리기
#
#   build-release.ps1 이 만든 Showcase(Release) 빌드에 실행 배치와 안내문을 넣고
#   zip 으로 묶는다. 이 zip 이 araatti.site 에서 받아지는 파일이다.
#
#   쓰는 법:
#     powershell -ExecutionPolicy Bypass -File tools\deploy\pack-client.ps1
#
#   결과물: unity\UnderTheSea\Builds\AraAtti-EC2.zip
#
# ─────────────────────────────────────────────────────────────────────────
# ⚠ .bat 과 .txt 는 반드시 CRLF + CP949 로 써야 한다.
#    LF 로만 쓰면 cmd.exe 가 `set HOST=...` 을 못 읽어 %HOST% 가 빈 값이 되고,
#    클라이언트가 `-api http://:5080` 을 받아 UriFormatException 으로 죽는다.
#    실제로 한 번 그렇게 됐다.
#
# ⚠ 이 파일은 UTF-8 BOM 으로 저장해야 한다. PowerShell 5.1 은 BOM 이 없으면
#    CP949 로 읽어서 한글 파일명(게임시작.bat) 만들기가 "Illegal characters in path"
#    로 실패한다. 이것도 실제로 겪었다.
# ─────────────────────────────────────────────────────────────────────────

$ErrorActionPreference = 'Stop'

$Repo   = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Builds = Join-Path $Repo 'unity\UnderTheSea\Builds'
$Src    = Join-Path $Builds 'Showcase'
$Dst    = Join-Path $Builds 'AraAtti-EC2'
$Zip    = Join-Path $Builds 'AraAtti-EC2.zip'
$Server = '43.202.67.137'

if (-not (Test-Path $Src)) {
    throw "Showcase 빌드가 없습니다: $Src`n먼저 tools\deploy\build-release.ps1 을 돌려 주세요."
}

Write-Output "[1/4] 이전 배포본 정리"
if (Test-Path $Dst) { Remove-Item $Dst -Recurse -Force }
if (Test-Path $Zip) { Remove-Item $Zip -Force }

Write-Output "[2/4] Release 빌드 복사"
Copy-Item $Src $Dst -Recurse
# 디버그 심볼은 플레이어에게 필요 없다. 용량만 차지한다.
Get-ChildItem $Dst -Recurse -Directory |
    Where-Object { $_.Name -like '*BurstDebugInformation_DoNotShip*' } |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Write-Output "[3/4] 실행 배치와 안내문 작성"
$cp949 = [System.Text.Encoding]::GetEncoding(949)

$bat = @"
@echo off
cd /d "%~dp0"

rem ===========================================================
rem  아라아띠 - EC2 접속용
rem
rem  로그인 서버와 게임 서버가 모두 EC2 에 있습니다.
rem  이 PC 에는 아무것도 설치할 것이 없습니다.
rem
rem  EC2 주소가 바뀌면 아래 HOST 만 고치면 됩니다.
rem ===========================================================

set HOST=$Server
set APPVER=prod
set REGION=kr

echo.
echo   아라아띠를 시작합니다.
echo     서버      %HOST%
echo     방 열쇠   %APPVER%  /  지역 %REGION%
echo.
echo   처음이면 [회원가입] 을 먼저 누르세요.
echo.

start "" "AraAtti-Flow.exe" -api http://%HOST%:5080 -appver %APPVER% -region %REGION% -screen-width 1600 -screen-height 900 -screen-fullscreen 0
"@

$readme = @"
아라아띠 - EC2 접속용 클라이언트

[실행]
  게임시작.bat 을 더블클릭하세요.
  AraAtti-Flow.exe 를 직접 실행하면 서버 주소가 안 들어가서 로그인이 안 됩니다.

[처음 실행하면]
  1. 시작   2. 회원가입   3. 캐릭터 생성   4. 채널 선택   5. 로비
  계정은 EC2 서버에 저장됩니다. PC 마다 다른 계정을 만드세요.

[파란 창이 뜨면서 "Windows의 PC 보호" 라고 나오면]
  인터넷에서 받은 프로그램이라 뜨는 경고입니다.
  [추가 정보] 를 누르고 [실행] 을 누르면 됩니다.

[게임이 무거우면]
  게임시작.bat 을 메모장으로 열어 맨 아랫줄 끝에 -fps 30 을 붙이면
  프레임을 30 으로 낮춰 가볍게 돌릴 수 있습니다.

[안 되면]
  방화벽에서 AraAtti-Flow.exe 를 허용해 주세요.
  그래도 안 되면 회사나 학교 망에서 UDP 가 막힌 경우일 수 있습니다.
"@

[System.IO.File]::WriteAllText(
    (Join-Path $Dst '게임시작.bat'), ($bat -replace "`r?`n", "`r`n"), $cp949)
[System.IO.File]::WriteAllText(
    (Join-Path $Dst '읽어주세요.txt'), ($readme -replace "`r?`n", "`r`n"), $cp949)

Write-Output "[4/4] 압축"
Compress-Archive -Path "$Dst\*" -DestinationPath $Zip -CompressionLevel Optimal

Write-Output ''
Write-Output ("  {0}" -f $Zip)
Write-Output ("  zip   {0:N0} MB" -f ((Get-Item $Zip).Length / 1MB))
Write-Output ("  푼 것 {0:N0} MB" -f ((Get-ChildItem $Dst -Recurse | Measure-Object Length -Sum).Sum / 1MB))
Write-Output ''
Write-Output '  다음: tools\deploy\README.md 의 "3. 올리기" 로'
