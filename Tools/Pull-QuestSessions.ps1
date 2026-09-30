param([string]$AdbPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $AdbPath) {
    $versionText = Get-Content -LiteralPath (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt')
    $unityVersion = (($versionText | Where-Object { $_ -match '^m_EditorVersion:' }) -split ': ')[1]
    $AdbPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$unityVersion/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe"
    if (-not (Test-Path -LiteralPath $AdbPath)) {
        $command = Get-Command adb -ErrorAction SilentlyContinue
        if (-not $command) { throw 'Pass -AdbPath with the path to adb.exe.' }
        $AdbPath = $command.Source
    }
}
$deviceLines = & $AdbPath devices
$devices = @($deviceLines | Where-Object { $_ -match '^\S+\s+device$' })
if ($devices.Count -ne 1) {
    $deviceLines | Write-Output
    throw 'Connect exactly one Quest and authorize USB debugging in the headset.'
}
$serial = ($devices[0] -split '\s+')[0]
$destination = Join-Path $projectRoot 'Recordings/Quest'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
& $AdbPath -s $serial pull '/sdcard/Android/data/com.nova.smartfactory.questtask/files/FactorySessions' $destination
if ($LASTEXITCODE -ne 0) { throw 'No recording copied. Finish a calibrated task recording and hold right B for one second before copying.' }
Write-Output "Copied to $destination. In Unity choose Tools > Smart Factory > Avatar Demo > Replay Session File (No Headset)."
