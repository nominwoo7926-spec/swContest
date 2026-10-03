param([string]$AdbPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $AdbPath) {
    $versionText = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')
    $unityVersion = (($versionText | Where-Object { $_ -match '^m_EditorVersion:' }) -split ': ')[1]
    $AdbPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$unityVersion/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
}
$devices = @((& $AdbPath devices) | Where-Object { $_ -match '^\S+\s+device$' })
if ($devices.Count -ne 1) { throw 'Connect and authorize exactly one Quest.' }
$serial = ($devices[0] -split '\s+')[0]
$package = 'com.nova.smartfactory.questtask'
$folder = "/sdcard/Android/data/$package/files/FactoryML"
$profile = "$folder/worker_profile_v1.json"
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
$archive = "$folder/worker_profile_archived_$stamp.json"

# Stop the old participant's session, then preserve every available CSV and profile locally.
& $AdbPath -s $serial shell am force-stop $package
if ($LASTEXITCODE -ne 0) { throw 'Could not stop the Quest app.' }
& (Join-Path $PSScriptRoot 'Pull-QuestMLData.ps1') -AdbPath $AdbPath
$present = (& $AdbPath -s $serial shell ls $profile 2>&1) -join "`n"
if ($LASTEXITCODE -ne 0 -or $present -notmatch 'worker_profile_v1.json') {
    throw 'No saved profile exists yet; nothing was changed. Enter height and reach for the current participant first.'
}
& $AdbPath -s $serial shell mv $profile $archive
if ($LASTEXITCODE -ne 0) { throw 'Could not archive the old profile. No new participant started.' }
Write-Output "Archived prior participant profile on Quest: $archive"
Write-Output 'Open the app, press Y, and enter the next participant height and arm reach before recording data.'
