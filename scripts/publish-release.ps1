param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Repository,
    [Parameter(Mandatory = $true)][string]$Commit,
    [string]$PublishDirectory = (Join-Path $PSScriptRoot '../artifacts/publish'),
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '../artifacts')
)
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+(?:\.\d+)?(?:-[A-Za-z0-9.-]+)?$') { throw 'Invalid release version.' }
if ($Repository -notmatch '^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$') { throw 'Invalid repository.' }
if ($Commit -notmatch '^[a-fA-F0-9]{40}$') { throw 'Release requires a complete commit SHA.' }
if (-not $env:GH_TOKEN -and -not $env:GITHUB_TOKEN) { throw 'GitHub release requires authenticated GH_TOKEN.' }
Get-Command gh -ErrorAction Stop | Out-Null
$tag = "v$Version"
$package = Join-Path $ArtifactDirectory "B01Timer-$Version-windows-x64.zip"
$assets = @((Join-Path $PublishDirectory 'B01Timer.exe'), $package, (Join-Path $ArtifactDirectory 'SHA256SUMS.txt'), (Join-Path $PublishDirectory 'LICENSE'), (Join-Path $PublishDirectory 'THIRD-PARTY-NOTICES.txt'))
foreach ($asset in $assets) { if (-not (Test-Path $asset -PathType Leaf)) { throw "Missing release asset: $asset" } }
$notesFile = Join-Path $ArtifactDirectory 'release-notes.md'
$notes = @"
Windows 64비트용 포터블 타이머예요. ZIP을 풀고 B01Timer.exe를 실행하세요.

- hh:mm:ss 입력, 실행·일시정지·리셋
- 즐겨찾기 시간 추가·수정·삭제
- Dark/Light 테마 · EXE에 내장된 Pretendard 폰트
- 중심 정렬된 Reset, 채워진 Pause 아이콘, Idle/Hover 버튼 상태
- 완료 시 삐비빅 알림 두 번
- EXE와 같은 폴더의 B01Timer.ini에 즐겨찾기·테마·마지막 설정 시간 저장
- 기존 AppData JSON 설정이 있으면 처음 한 번 INI로 이전
- Record: 실행 중인 프로그램 최대 5개 등록, 포커스된 시간 자동 누적
- Record 누적 시간과 프로그램 목록을 INI에 보관 · 1d / 1w 1d 표시
- 재생·일시정지·리셋 아이콘 크기 통일, 테마 토글 확대

소스: [$Commit](https://github.com/$Repository/commit/$Commit)
검증: [Windows 빌드 및 QA](https://github.com/$Repository/actions/runs/$env:GITHUB_RUN_ID)
파일 해시는 SHA256SUMS.txt에서 확인할 수 있어요.
"@
$notes | Set-Content -Encoding utf8 $notesFile
function Invoke-ReleaseGh([string[]]$GhArguments) {
    & gh @GhArguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: $($GhArguments[0]) $($GhArguments[1])" }
}
& gh release view $tag --repo $Repository --json tagName 2>$null | Out-Null
$exists = $LASTEXITCODE -eq 0
if ($exists) {
    # The version represents its latest successfully verified build.
    Invoke-ReleaseGh @('api', "repos/$Repository/git/refs/tags/$tag", '--method', 'PATCH', '-f', "sha=$Commit", '-F', 'force=true')
    Invoke-ReleaseGh (@('release', 'upload', $tag, '--repo', $Repository, '--clobber') + $assets)
    Invoke-ReleaseGh @('release', 'edit', $tag, '--repo', $Repository, '--title', "Version $Version", '--notes-file', $notesFile, '--latest')
} else {
    Invoke-ReleaseGh (@('release', 'create', $tag, '--repo', $Repository, '--target', $Commit, '--title', "Version $Version", '--notes-file', $notesFile, '--latest') + $assets)
}
Invoke-ReleaseGh @('release', 'view', $tag, '--repo', $Repository, '--json', 'url', '--jq', '.url')
