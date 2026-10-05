@echo off
rem Double-click: draw the baseline-vs-AI comparison charts into Desktop\NOVA_부하데이터\Charts
chcp 65001 >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Make-Charts.ps1"
pause
