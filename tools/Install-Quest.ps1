param(
    [string]$Apk = "$PSScriptRoot/../Builds/Quest/ErgoTwin.apk",
    [string]$Adb = 'C:/Program Files/Unity/Hub/Editor/6000.3.25f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $Adb)) { throw "adb not found. Pass -Adb with your SDK platform-tools/adb.exe path." }
if (!(Test-Path -LiteralPath $Apk)) { throw "APK not found. Run Tools > Ergo Twin > Build Quest APK in Unity first." }
$devices = & $Adb devices
$connected = @($devices | Where-Object { $_ -match '^\S+\s+device$' })
if ($connected.Count -ne 1) { throw "Connect exactly one Quest and authorize USB debugging in the headset. adb reports: $devices" }
& $Adb install -r $Apk
if ($LASTEXITCODE -ne 0) { throw 'APK installation failed.' }
& $Adb shell monkey -p com.ergotwin.adaptiveassembly -c android.intent.category.LAUNCHER 1
if ($LASTEXITCODE -ne 0) { throw 'App launch failed.' }
