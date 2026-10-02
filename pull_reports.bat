@echo off
set ADB="C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
set DEST=%~dp0Reports

if not exist "%DEST%" mkdir "%DEST%"
%ADB% pull /storage/emulated/0/Android/data/com.nova.smartfactory.questtask/files/Reports/ "%DEST%"
echo.
echo Done: %DEST%
start "" "%DEST%"
pause
