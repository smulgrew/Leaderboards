@echo off
rem Double-click me to start LeadersBoards (database, API and Expo).
rem The real work happens in scripts\windows\start.ps1. See README "Quick start on Windows".
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\windows\start.ps1"
echo.
pause
