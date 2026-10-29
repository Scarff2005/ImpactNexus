@echo off
title Impact Nexus
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Setup-and-Run.ps1"
echo.
echo Press any key to close this window.
pause >nul
