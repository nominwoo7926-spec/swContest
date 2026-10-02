@echo off
set ADB="C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe"
set DEST=%~dp0Reports
set DEVICE_PATH=/storage/emulated/0/Android/data/com.nova.smartfactory.questtask/files/Reports/

if not exist "%DEST%" mkdir "%DEST%"

echo ================================================
echo  NOVA Smart Factory - Auto Report Sync
echo  Connect Quest2 via USB to sync reports
echo  Press Ctrl+C to stop
echo ================================================
echo.

:wait_connect
echo [%TIME%] Waiting for Quest2...
%ADB% wait-for-device >nul 2>&1

echo [%TIME%] Quest2 connected. Waiting 3 sec...
timeout /t 3 /nobreak >nul

echo [%TIME%] Pulling reports...
%ADB% pull %DEVICE_PATH% "%DEST%" 2>&1

set LATEST=
for /f "delims=" %%f in ('dir /b /o-d "%DEST%\*.html" 2^>nul') do (
    if not defined LATEST set LATEST=%%f
)
if defined LATEST (
    echo [%TIME%] Opening: %LATEST%
    start "" "%DEST%\%LATEST%"
) else (
    echo [%TIME%] No HTML files found.
)

echo.
echo [%TIME%] Waiting for Quest2 disconnect...
:wait_disconnect
%ADB% get-state >nul 2>&1
if %errorlevel% equ 0 (
    timeout /t 2 /nobreak >nul
    goto wait_disconnect
)

echo [%TIME%] Quest2 disconnected. Restarting...
echo.
goto wait_connect
