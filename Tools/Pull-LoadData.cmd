@echo off
rem Double-click: copy the Quest's per-run load data to Desktop\NOVA_부하데이터
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Pull-LoadData.ps1"
pause
