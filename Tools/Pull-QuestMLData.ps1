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
$destination = Join-Path $projectRoot 'Recordings/Quest/FactoryMLSnapshots'
$snapshot = Join-Path $destination (Get-Date -Format 'yyyyMMdd_HHmmss_fff')
New-Item -ItemType Directory -Force -Path $snapshot | Out-Null
& $AdbPath -s $serial pull '/sdcard/Android/data/com.nova.smartfactory.questtask/files/FactoryML' $snapshot
if ($LASTEXITCODE -ne 0) { throw 'No ML dataset copied. Complete a trial and press feedback 1-5 first.' }
& (Join-Path $PSScriptRoot 'Merge-QuestMLData.ps1')
Write-Output "Quest snapshot: $snapshot"
Write-Output "Combined Excel-compatible CSV: $(Join-Path $projectRoot 'Recordings/Quest/FactoryMLCombined')"
