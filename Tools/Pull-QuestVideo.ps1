param([string]$AdbPath, [switch]$All)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $AdbPath) {
    $versionText = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')
    $unityVersion = (($versionText | Where-Object { $_ -match '^m_EditorVersion:' }) -split ': ')[1]
    $AdbPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$unityVersion/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
}
if (-not (Test-Path -LiteralPath $AdbPath)) { throw "ADB not found: $AdbPath" }
$devices = @(& $AdbPath devices | Where-Object { $_ -match '^\S+\s+device$' })
if ($devices.Count -ne 1) { throw 'Connect one Quest and allow USB debugging in the headset.' }
$serial = ($devices[0] -split '\s+')[0]
$remoteDirectory = '/sdcard/Oculus/VideoShots'
$videos = @(& $AdbPath -s $serial shell ls -1 $remoteDirectory | ForEach-Object { $_.Trim() } |
    Where-Object { $_ -match '^com\.nova\.smartfactory\.questtask-[0-9-]+\.mp4$' } | Sort-Object -Descending)
if ($videos.Count -eq 0) { throw 'No Adaptive Workbench VR first-person MP4 was found on the Quest.' }
if (-not $All) { $videos = @($videos[0]) }
$destination = Join-Path $projectRoot 'Recordings/Quest/VideoShots'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach ($video in $videos) {
    & $AdbPath -s $serial pull "$remoteDirectory/$video" (Join-Path $destination $video)
    if ($LASTEXITCODE -ne 0) { throw "Could not copy $video" }
}
Write-Output "Copied $($videos.Count) video(s) to $destination"
