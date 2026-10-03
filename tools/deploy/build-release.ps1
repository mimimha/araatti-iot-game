#
# 아라아띠 — 배포용 한 벌 굽기
#
#   리눅스 Dedicated Server 4대 + 윈도우 Release 클라이언트 1개를 만든다.
#
#   쓰는 법:
#     powershell -ExecutionPolicy Bypass -File tools\deploy\build-release.ps1
#
#   결과물:
#     unity\UnderTheSea\Builds\Linux\{Server,MineServer,WarriorsServer,ShipCoopServer}
#     unity\UnderTheSea\Builds\Showcase
#
#   다음 단계는 tools\deploy\pack-client.ps1 과 tools\deploy\deploy-servers.sh 다.
#   전체 흐름은 tools\deploy\README.md 를 보라.
#
# ─────────────────────────────────────────────────────────────────────────
# ⚠ 한 프로젝트를 두 Unity 가 동시에 못 연다. 그래서 순차로 돈다.
#
# ⚠ `& Unity.exe` 는 블로킹하지 않는다. Unity 가 GUI 서브시스템이라 셸이
#    곧바로 다음 줄로 넘어간다. 그래서 Unity 두 개가 같은 프로젝트를 열고
#    서로 망가뜨린 적이 있다. 반드시 `Start-Process -Wait` 를 쓴다.
#
# ⚠ `-buildTarget` 을 빼면 안 된다. 출력 경로가 플랫폼을 따라가므로,
#    안 주면 리눅스 서버가 윈도우 빌드 자리에 덮인다.
#
# ⚠ 이 파일은 UTF-8 BOM 으로 저장해야 한다. PowerShell 5.1 은 BOM 이 없으면
#    시스템 ANSI(CP949)로 읽어서 한글 주석이 깨지고 한글 경로가 실패한다.
# ─────────────────────────────────────────────────────────────────────────

$ErrorActionPreference = 'Stop'

$Repo    = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$Project = Join-Path $Repo 'unity\UnderTheSea'
$Unity   = 'C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Unity.exe'
$LogDir  = Join-Path $env:TEMP 'araatti-build'
$NS      = 'UnderTheSea.Network.Editor.FusionTestBuilds'

if (-not (Test-Path $Unity)) {
    throw "Unity 를 못 찾았습니다: $Unity`n허브에서 설치한 버전이 다르면 이 경로를 고쳐 주세요."
}
if (-not (Test-Path $LogDir)) { New-Item -ItemType Directory -Path $LogDir | Out-Null }

function Bake($name, $target, $subtarget, $method) {
    $logFile = Join-Path $LogDir "$name.log"
    Write-Output "[$name] 시작 $(Get-Date -Format 'HH:mm:ss')  ($target / $subtarget)"

    $unityArgs = @(
        '-batchmode', '-quit', '-nographics',
        '-projectPath', $Project,
        '-buildTarget', $target,
        '-standaloneBuildSubtarget', $subtarget,
        '-executeMethod', "$NS.$method",
        '-logFile', $logFile
    )

    $p = Start-Process -FilePath $Unity -ArgumentList $unityArgs -Wait -PassThru -NoNewWindow
    Write-Output "[$name] 끝   $(Get-Date -Format 'HH:mm:ss')  종료코드 $($p.ExitCode)"

    if ($p.ExitCode -ne 0) {
        Write-Output "[$name] 실패했습니다. 로그 끝부분:"
        Get-Content $logFile -Tail 40 | ForEach-Object { "    $_" }
        throw "$name 빌드 실패 — 전체 로그: $logFile"
    }

    Select-String -Path $logFile -Pattern '\[FusionTestBuilds\] 빌드 성공' |
        ForEach-Object { "    " + $_.Line.Trim() }
}

Bake 'linux-servers' 'Linux64' 'Server' 'BuildDeployServerSetFromCommandLine'
Bake 'win-client'    'Win64'   'Player' 'BuildShowcaseClientFromCommandLine'

Write-Output ''
Write-Output '=== 빌드 완료 ==='
Write-Output '  다음: tools\deploy\pack-client.ps1  (클라이언트 zip 만들기)'
Write-Output '        tools\deploy\README.md        (EC2 에 올리는 법)'
