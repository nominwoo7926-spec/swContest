param([string]$AdbPath)
# Copies the per-run load data (CSV) from the Quest to Desktop\NOVA_부하데이터.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $AdbPath) {
    $versionText = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')
    $unityVersion = (($versionText | Where-Object { $_ -match '^m_EditorVersion:' }) -split ': ')[1]
    $AdbPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$unityVersion/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
    if (-not (Test-Path -LiteralPath $AdbPath)) {
        $command = Get-Command adb -ErrorAction SilentlyContinue
        if (-not $command) { throw 'ADB not found. Pass -AdbPath with the path to adb.exe.' }
        $AdbPath = $command.Source
    }
}
$devices = @(& $AdbPath devices | Where-Object { $_ -match '^\S+\s+device$' })
if ($devices.Count -ne 1) { throw 'Connect one Quest with USB debugging authorized, then run this script again.' }
$serial = ($devices[0] -split '\s+')[0]
$source = '/sdcard/Android/data/com.nova.smartfactory.questtask/files/LoadData'
$target = Join-Path ([Environment]::GetFolderPath('Desktop')) 'NOVA_부하데이터'
New-Item -ItemType Directory -Force -Path $target | Out-Null
$files = @(& $AdbPath -s $serial shell "ls $source 2>/dev/null" | ForEach-Object { $_.Trim() } | Where-Object { $_ -like '*.csv' })
if ($files.Count -eq 0) { Write-Output 'No finished runs on the headset yet (a run is saved after all 100 parts).'; return }
foreach ($file in $files) { & $AdbPath -s $serial pull "$source/$file" (Join-Path $target $file) | Out-Null }
Write-Output "Copied $($files.Count) file(s) to $target"
