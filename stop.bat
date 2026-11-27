@echo off
rem Double-click me to stop LeadersBoards. Your data is kept.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\windows\stop.ps1"
echo.
pause
