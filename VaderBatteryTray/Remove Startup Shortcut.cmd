@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$path=Join-Path ([Environment]::GetFolderPath('Startup')) 'Vader Battery Tray.lnk';" ^
  "if(Test-Path -LiteralPath $path){Remove-Item -LiteralPath $path -Force}"

echo Startup shortcut removed for the current Windows user.
pause
