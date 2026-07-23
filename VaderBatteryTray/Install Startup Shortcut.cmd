@echo off
setlocal
cd /d "%~dp0"

if not exist "VaderBatteryTray.exe" call build.cmd
if errorlevel 1 (
    echo.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$shell=New-Object -ComObject WScript.Shell;" ^
  "$startup=[Environment]::GetFolderPath('Startup');" ^
  "$shortcut=$shell.CreateShortcut((Join-Path $startup 'Vader Battery Tray.lnk'));" ^
  "$shortcut.TargetPath='%CD%\VaderBatteryTray.exe';" ^
  "$shortcut.WorkingDirectory='%CD%';" ^
  "$shortcut.IconLocation='%CD%\VaderBatteryTray.exe,0';" ^
  "$shortcut.Save();"

if errorlevel 1 (
    echo ERROR: The Startup shortcut could not be created.
    pause
    exit /b 1
)

echo Startup shortcut installed for the current Windows user.
pause
