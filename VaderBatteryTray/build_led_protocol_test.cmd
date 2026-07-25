@echo off
setlocal EnableExtensions
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo ERROR: The built-in 64-bit C# compiler was not found:
    exit /b 1
)

echo Building Vader LED protocol self-test...
"%CSC%" /nologo /target:exe /platform:x64 /optimize+ /warn:4 ^
  /out:"%CD%\VaderLedProtocolSelfTest.exe" ^
  /reference:System.dll ^
  "%CD%\DockBatteryPolicy.cs" ^
  "%CD%\VaderLedProtocol.cs" ^
  "%CD%\VaderLedPolicy.cs" ^
  "%CD%\VaderLedSettings.cs" ^
  "%CD%\VaderLedProtocolSelfTest.cs"

if errorlevel 1 (
    echo ERROR: Compilation failed.
    exit /b 1
)

echo Build complete:
echo   %CD%\VaderLedProtocolSelfTest.exe
exit /b 0
