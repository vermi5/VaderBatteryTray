@echo off
setlocal
cd /d "%~dp0"

if not exist "VaderBatteryTray.exe" call build.cmd
if errorlevel 1 (
    echo.
    pause
    exit /b 1
)

start "" "%~dp0VaderBatteryTray.exe"
exit /b 0
