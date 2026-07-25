@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo ERROR: The built-in 64-bit C# compiler was not found:
    echo %CSC%
    echo.
    echo Install or enable .NET Framework 4.x, then run this file again.
    exit /b 1
)

echo Building VaderBatteryTray.exe...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warn:4 ^
  /out:"%CD%\VaderBatteryTray.exe" ^
  /win32icon:"%CD%\VaderBatteryTray.ico" ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  "%CD%\VaderBatteryTray.cs" ^
  "%CD%\VaderLedProtocol.cs" ^
  "%CD%\VaderLedPolicy.cs" ^
  "%CD%\VaderLedController.cs" ^
  "%CD%\VaderLedSettings.cs" ^
  "%CD%\VaderLedSettingsForm.cs" ^
  "%CD%\DiagnosticLogger.cs" ^
  "%CD%\RainmeterBridge.cs"

if errorlevel 1 (
    echo.
    echo ERROR: Compilation failed.
    exit /b 1
)

echo.
echo Build complete:
echo   %CD%\VaderBatteryTray.exe
exit /b 0
