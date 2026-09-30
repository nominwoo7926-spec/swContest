param([string]$AdbPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$apkPath = Join-Path $projectRoot 'Builds/QuestTask/NOVA_FactoryTask.apk'
if (-not (Test-Path -LiteralPath $apkPath)) { throw "APK not found: $apkPath" }
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
$deviceLines = & $AdbPath devices
$devices = @($deviceLines | Where-Object { $_ -match '^\S+\s+device$' })
if ($devices.Count -ne 1) {
    $deviceLines | Write-Output
    throw 'Connect one Quest with USB debugging authorized, then run this script again.'
}
$serial = ($devices[0] -split '\s+')[0]
& $AdbPath -s $serial install -r $apkPath
if ($LASTEXITCODE -ne 0) { throw 'APK installation failed.' }
& $AdbPath -s $serial shell monkey -p com.nova.smartfactory.questtask -c android.intent.category.LAUNCHER 1
if ($LASTEXITCODE -ne 0) { throw 'Installed, but application launch failed. Open NOVA Factory Task on the headset.' }
Write-Output 'Installed and launched NOVA Factory Task. Put on the headset and stand comfortably for calibration.'
